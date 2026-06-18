using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class WorkTimeDayGroup
{
    public DateTime TaskDate { get; set; }
    public List<WorkTimeOrderItem> Orders { get; set; } = new();
}

public class WorkTimeOrderItem
{
    public long OrderDetailId { get; set; }
    public long OrderId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public int TaskStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TotalMinutes { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Baker Work Time (Manage Users Timeline) module.
/// Migrated from bakerworktime.aspx.
/// Uses DefaultConnection with tbl_bakeryuser, tbl_ordertaskdet, tbl_orderDetail, tbl_order, tbl_products.
/// READ-ONLY — timing mutations deferred to Phase 2.
/// </summary>
public class BakerWorkTimeService
{
    private readonly string _defaultConnection;

    public BakerWorkTimeService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets delivery staff for the dropdown filter.
    /// Delivery staff: customer_type=3, customer_stafftype=1, webshopid=82, isActive=1.
    /// </summary>
    public async Task<List<StaffDropdownItem>> GetDeliveryStaffAsync()
    {
        var items = new List<StaffDropdownItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT customer_ID, customer_Name 
                    FROM tbl_bakeryuser 
                    WHERE customer_type = 3 
                      AND customer_stafftype = 1 
                      AND customer_webshopid = 82 
                      AND customer_isActive = 1
                    ORDER BY customer_Name";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new StaffDropdownItem
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets work time entries for a staff member within a date range.
    /// Returns data grouped by date with order/task details.
    /// </summary>
    public async Task<List<WorkTimeDayGroup>> GetWorkTimeAsync(int staffId, DateTime fromDate, DateTime toDate)
    {
        var flatItems = new List<WorkTimeOrderItem>();
        var dateMap = new Dictionary<DateTime, List<WorkTimeOrderItem>>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT 
                        CAST(t.ordertaskdet_staDate AS date) AS TaskDate,
                        od.orderDetail_ID AS OrderDetailId,
                        od.orderDetail_orderID AS OrderId,
                        p.product_Name AS ProductName,
                        p.product_image1 AS ProductImage,
                        t.ordertaskdet_taskID AS TaskStatus,
                        t.ordertaskdet_staDate AS StartDate,
                        t.ordertaskdet_endDate AS EndDate,
                        DATEDIFF(minute, t.ordertaskdet_staDate, t.ordertaskdet_endDate) AS TotalMinutes
                    FROM tbl_ordertaskdet t
                    INNER JOIN tbl_orderDetail od ON t.ordertaskdet_orderdetailID = od.orderDetail_ID
                    INNER JOIN tbl_order o ON od.orderDetail_orderID = o.order_ID
                    INNER JOIN tbl_products p ON od.orderDetail_productID = p.product_ID
                    WHERE t.ordertaskdet_userID = @staffId
                      AND CAST(t.ordertaskdet_staDate AS date) >= @fromDate
                      AND CAST(t.ordertaskdet_staDate AS date) <= @toDate
                      AND t.ordertaskdet_staDate IS NOT NULL
                    ORDER BY t.ordertaskdet_staDate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
        cmd.Parameters.AddWithValue("@toDate", toDate.Date);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var taskDate = reader.GetDateTime(0);
            var item = new WorkTimeOrderItem
            {
                OrderDetailId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                OrderId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                ProductName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ProductImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                TaskStatus = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                StartDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                EndDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                TotalMinutes = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
            };

            if (!dateMap.ContainsKey(taskDate))
                dateMap[taskDate] = new List<WorkTimeOrderItem>();

            dateMap[taskDate].Add(item);
        }

        // Build grouped result ordered by date descending
        var result = dateMap
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new WorkTimeDayGroup
            {
                TaskDate = kv.Key,
                Orders = kv.Value
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Maps task status ID to display name.
    /// </summary>
    public static string GetTaskStatusName(int taskStatus)
    {
        return taskStatus switch
        {
            1 => "Topper",
            11 => "Filling",
            12 => "Icing",
            22 => "Decoration",
            33 => "Finishing",
            44 => "Under Delivery",
            _ => "Unknown"
        };
    }
}
