using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class DriverDropdownItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class RoutePaymentItem
{
    public long RouteId { get; set; }
    public string RouteTitle { get; set; } = "";
    public DateTime RouteDate { get; set; }
    public int DisplayOrder { get; set; }
    public long DriverId { get; set; }
    public string DriverName { get; set; } = "";
    public decimal DriverCharges { get; set; }
    public bool IsChargePaid { get; set; }
    public string PaidRemarks { get; set; } = "";
}

public class RoutePaymentListResult
{
    public List<RoutePaymentItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalUnpaidAmount { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Delivery Route Payments module.
/// Migrated from managedeliveryroutesPayments.aspx.
/// Uses BusinessConnection for tbl_deliveryRoute (route data)
/// and DefaultConnection for tbl_bakeryuser (driver names).
/// READ operations always available; payout mutations are FEATURE FLAGGED.
/// </summary>
public class DeliveryRoutePaymentsService
{
    private readonly string _businessConnectionString;
    private readonly string _defaultConnectionString;

    public DeliveryRoutePaymentsService(IConfiguration config)
    {
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    // ─── GetDriversAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets the list of drivers (staff type 5) for the dropdown.
    /// Queries tbl_bakeryuser from DefaultConnection.
    /// </summary>
    public async Task<List<DriverDropdownItem>> GetDriversAsync(long webshopId)
    {
        var drivers = new List<DriverDropdownItem>();

        try
        {
            await using var conn = new SqlConnection(_defaultConnectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT customer_ID, customer_Name 
                FROM tbl_bakeryuser 
                WHERE customer_type IN (3, 4) 
                  AND customer_stafftype = 5 
                  AND customer_webshopID = @webshopId
                ORDER BY customer_Name", conn);
            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@webshopId", webshopId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                drivers.Add(new DriverDropdownItem
                {
                    Id = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0)),
                    Name = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? ""
                });
            }
        }
        catch
        {
            // Safe fallback — return empty list on error
        }

        return drivers;
    }

    // ─── GetRoutesAsync ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets paginated delivery routes filtered by date range, driver, and payment status.
    /// Routes come from BusinessConnection (tbl_deliveryRoute).
    /// Driver names are resolved from DefaultConnection (tbl_bakeryuser).
    /// </summary>
    public async Task<RoutePaymentListResult> GetRoutesAsync(
        long webshopId, DateTime fromDate, DateTime toDate,
        int driverId, int statusFilter, int page, int pageSize)
    {
        var result = new RoutePaymentListResult();

        try
        {
            var offset = (page - 1) * pageSize;

            await using var conn = new SqlConnection(_businessConnectionString);
            await conn.OpenAsync();

            // Build WHERE clause dynamically
            var whereClause = "WHERE CAST(route_date AS date) >= @fromDate AND CAST(route_date AS date) <= @toDate";
            if (driverId != -1)
                whereClause += " AND route_driverID = @driverId";
            if (statusFilter != -1)
                whereClause += " AND route_isChargePaid = @isPaid";

            // Get total count
            var countSql = $"SELECT COUNT(*) FROM tbl_deliveryRoute {whereClause}";
            await using (var countCmd = new SqlCommand(countSql, conn))
            {
                countCmd.CommandTimeout = 120;
                countCmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                countCmd.Parameters.AddWithValue("@toDate", toDate.Date);
                if (driverId != -1)
                    countCmd.Parameters.AddWithValue("@driverId", driverId);
                if (statusFilter != -1)
                    countCmd.Parameters.AddWithValue("@isPaid", statusFilter == 1);

                var countResult = await countCmd.ExecuteScalarAsync();
                result.TotalCount = Convert.ToInt32(countResult ?? 0);
            }

            // Calculate total pages
            result.TotalPages = result.TotalCount > 0
                ? (int)Math.Ceiling((double)result.TotalCount / pageSize)
                : 0;

            // Get total unpaid amount (for the current filter, not just current page)
            var unpaidWhereClause = "WHERE CAST(route_date AS date) >= @fromDate AND CAST(route_date AS date) <= @toDate AND route_isChargePaid = 0";
            if (driverId != -1)
                unpaidWhereClause += " AND route_driverID = @driverId";

            var unpaidSql = $"SELECT ISNULL(SUM(route_DriverCharges), 0) FROM tbl_deliveryRoute {unpaidWhereClause}";
            await using (var unpaidCmd = new SqlCommand(unpaidSql, conn))
            {
                unpaidCmd.CommandTimeout = 120;
                unpaidCmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                unpaidCmd.Parameters.AddWithValue("@toDate", toDate.Date);
                if (driverId != -1)
                    unpaidCmd.Parameters.AddWithValue("@driverId", driverId);

                var unpaidResult = await unpaidCmd.ExecuteScalarAsync();
                result.TotalUnpaidAmount = Convert.ToDecimal(unpaidResult ?? 0m);
            }

            // Get paginated routes
            var dataSql = $@"
                SELECT route_ID, route_title, route_date, route_displayOrder, route_driverID, 
                       route_DriverCharges, route_isChargePaid, route_PaidRemarks
                FROM tbl_deliveryRoute
                {whereClause}
                ORDER BY route_date DESC, route_displayOrder
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            await using (var dataCmd = new SqlCommand(dataSql, conn))
            {
                dataCmd.CommandTimeout = 120;
                dataCmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                dataCmd.Parameters.AddWithValue("@toDate", toDate.Date);
                dataCmd.Parameters.AddWithValue("@offset", offset);
                dataCmd.Parameters.AddWithValue("@pageSize", pageSize);
                if (driverId != -1)
                    dataCmd.Parameters.AddWithValue("@driverId", driverId);
                if (statusFilter != -1)
                    dataCmd.Parameters.AddWithValue("@isPaid", statusFilter == 1);

                await using var reader = await dataCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Items.Add(new RoutePaymentItem
                    {
                        RouteId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0)),
                        RouteTitle = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                        RouteDate = reader.IsDBNull(2) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(2)),
                        DisplayOrder = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                        DriverId = reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4)),
                        DriverCharges = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                        IsChargePaid = reader.IsDBNull(6) ? false : Convert.ToBoolean(reader.GetValue(6)),
                        PaidRemarks = reader.IsDBNull(7) ? "" : reader.GetValue(7)?.ToString() ?? ""
                    });
                }
            }

            // Resolve driver names from DefaultConnection
            var driverIds = result.Items
                .Where(r => r.DriverId > 0)
                .Select(r => r.DriverId)
                .Distinct()
                .ToList();

            if (driverIds.Count > 0)
            {
                var driverNames = await GetDriverNamesAsync(driverIds);
                foreach (var item in result.Items)
                {
                    if (item.DriverId > 0 && driverNames.TryGetValue(item.DriverId, out var name))
                    {
                        item.DriverName = name;
                    }
                    else if (item.DriverId == 0)
                    {
                        item.DriverName = "Unlinked";
                    }
                }
            }
        }
        catch
        {
            // Safe fallback — return empty result on error
        }

        return result;
    }

    // ─── Helper: Get Driver Names ──────────────────────────────────────────────

    private async Task<Dictionary<long, string>> GetDriverNamesAsync(List<long> driverIds)
    {
        var names = new Dictionary<long, string>();

        try
        {
            await using var conn = new SqlConnection(_defaultConnectionString);
            await conn.OpenAsync();

            var paramNames = new List<string>();
            for (int i = 0; i < driverIds.Count; i++)
            {
                paramNames.Add($"@did{i}");
            }

            var sql = $@"SELECT customer_ID, customer_Name FROM tbl_bakeryuser 
                         WHERE customer_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 120;

            for (int i = 0; i < driverIds.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@did{i}", driverIds[i]);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0));
                var name = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                if (id > 0) names[id] = name;
            }
        }
        catch
        {
            // Safe fallback — return empty dictionary
        }

        return names;
    }
}
