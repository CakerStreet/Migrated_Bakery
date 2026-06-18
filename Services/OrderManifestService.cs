using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class ManifestCountItem
{
    public int Count { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
}

public class ManifestOrderItem
{
    public DateTime DispatchDate { get; set; }
    public DateTime ReadyDispatchDate { get; set; }
    public int OrderId { get; set; }
    public int OrderBakeryId { get; set; }
    public int OrderBranchId { get; set; }
    public int OrderStatus { get; set; }
    public string Remarks { get; set; } = "";
    public int ForwardedOrderId { get; set; }
    public int FollowingOrderId { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime CollectionDispatchDate { get; set; }
    public DateTime ReadyToDispatchDate { get; set; }
    public string BranchName { get; set; } = "";
    public string WebstoreCode { get; set; } = "";
    public string ProductImage1 { get; set; } = "";
    public int ProductType { get; set; }
    public int DeliveryMode { get; set; }
    public string ShippingZip { get; set; } = "";
    public string BranchPostcode { get; set; } = "";
    public string BranchNameDetail { get; set; } = "";
    public bool IsRepeat { get; set; }
    public int TotalRecords { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for the Order Manifest page (read-only).
/// Migrated from manageordermenifest.aspx — Business portal version.
/// Uses single date (start = end) unlike CRM which supports date ranges.
/// </summary>
public class OrderManifestService
{
    private readonly string _connectionString;

    public OrderManifestService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets manifest counts per branch for the selected date.
    /// Calls SP: getordermenifestlistbybakeryID_count
    /// </summary>
    public async Task<List<ManifestCountItem>> GetManifestCountsAsync(int bakeryId, string date)
    {
        var items = new List<ManifestCountItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("getordermenifestlistbybakeryID_count", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        cmd.Parameters.AddWithValue("@bakeryID", bakeryId);
        cmd.Parameters.AddWithValue("@dtnow", date);
        cmd.Parameters.AddWithValue("@dt", date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ManifestCountItem
            {
                Count = GetIntSafe(reader, "countprd"),
                BranchId = GetIntSafe(reader, "order_branchID"),
                BranchName = GetStringSafe(reader, "webstore_businessName")
            });
        }
        return items;
    }

    /// <summary>
    /// Gets the full manifest order list for the selected date.
    /// Calls SP: getordermenifestlistbybakeryID_withapcdata
    /// </summary>
    public async Task<List<ManifestOrderItem>> GetManifestListAsync(int bakeryId, string date)
    {
        var items = new List<ManifestOrderItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("getordermenifestlistbybakeryID_withapcdata", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        cmd.Parameters.AddWithValue("@bakeryID", bakeryId);
        cmd.Parameters.AddWithValue("@dtnow", date);
        cmd.Parameters.AddWithValue("@dt", date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ManifestOrderItem
            {
                DispatchDate = GetDateTimeSafe(reader, "dispatchdate"),
                ReadyDispatchDate = GetDateTimeSafe(reader, "readydispatchdate"),
                OrderId = GetIntSafe(reader, "order_ID"),
                OrderBakeryId = GetIntSafe(reader, "order_bakeryID"),
                OrderBranchId = GetIntSafe(reader, "order_branchID"),
                OrderStatus = GetIntSafe(reader, "order_status"),
                Remarks = GetStringSafe(reader, "ordercollection_Remarks"),
                ForwardedOrderId = GetIntSafe(reader, "order_forwardedorderid"),
                FollowingOrderId = GetIntSafe(reader, "order_followingorderid"),
                CollectionDate = GetDateTimeSafe(reader, "ordercollection_Date"),
                CollectionDispatchDate = GetDateTimeSafe(reader, "ordercollection_dispatchDate"),
                ReadyToDispatchDate = GetDateTimeSafe(reader, "ordercollection_readytodispatchDate"),
                BranchName = GetStringSafe(reader, "webstore_businessName"),
                WebstoreCode = GetStringSafe(reader, "webstore_code"),
                ProductImage1 = GetStringSafe(reader, "Product_Image1"),
                ProductType = GetIntSafe(reader, "product_type"),
                DeliveryMode = GetIntSafe(reader, "ordercollection_deliverymode"),
                ShippingZip = GetStringSafe(reader, "shipping_zip"),
                BranchPostcode = GetStringSafe(reader, "branchpostcode"),
                BranchNameDetail = GetStringSafe(reader, "branchName"),
                IsRepeat = GetBoolSafe(reader, "order_isrepeat"),
                TotalRecords = GetIntSafe(reader, "TotalRecords")
            });
        }
        return items;
    }

    // ─── Safe Reader Helpers ───────────────────────────────────────────────────

    private static int GetIntSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }
        catch { return 0; }
    }

    private static string GetStringSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? "" : reader.GetValue(ordinal)?.ToString() ?? "";
        }
        catch { return ""; }
    }

    private static DateTime GetDateTimeSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            if (reader.IsDBNull(ordinal))
                return DateTime.MinValue;

            var value = reader.GetValue(ordinal);
            if (value is DateTime dt)
                return dt;

            return DateTime.TryParse(value?.ToString(), out var parsed) ? parsed : DateTime.MinValue;
        }
        catch { return DateTime.MinValue; }
    }

    private static bool GetBoolSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            if (reader.IsDBNull(ordinal))
                return false;

            var value = reader.GetValue(ordinal);
            if (value is bool b)
                return b;

            // Handle int-as-bool (0 = false, non-zero = true)
            if (int.TryParse(value?.ToString(), out var intVal))
                return intVal != 0;

            return false;
        }
        catch { return false; }
    }
}
