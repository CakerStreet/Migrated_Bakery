using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class BakeryDriverItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class RouteChargeTemplateItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
}

public class DefaultRouteItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
}

public class DeliveryRouteListResult
{
    public List<DeliveryRouteListItem> Routes { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public string SelectedDate { get; set; } = "";
}

public class DeliveryRouteListItem
{
    public long RouteId { get; set; }
    public string RouteTitle { get; set; } = "";
    public string Remarks { get; set; } = "";
    public int DisplayOrder { get; set; }
    public int OrderCount { get; set; }
    public DateTime RouteDate { get; set; }
    // API data (nullable — only if route has been calculated)
    public string? ApiMiles { get; set; }
    public int? ApiSeconds { get; set; }
    public decimal? ApiCharges { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiId { get; set; }
    // Driver info
    public long DriverId { get; set; }
    public string? DriverName { get; set; }
    public decimal DriverCharges { get; set; }
    // Template info
    public long TemplateId { get; set; }
    public string? TemplateName { get; set; }
    // Flags
    public bool ReturnToUnit { get; set; }
    public bool IsChargePaid { get; set; }
    // Computed totals (from API join)
    public decimal TotalOrderAmt { get; set; }
    public decimal TotalDeliveryAmt { get; set; }
}

public class RouteOrderItem
{
    public int SerialNo { get; set; }
    public long OrderId { get; set; }
    public string ReadyStatus { get; set; } = "Not Ready";
    public string DeliveryAddress { get; set; } = "";
    public string DeliveryMode { get; set; } = "";
    public int DeliveryModeId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string DeliveryTimeWindow { get; set; } = "";
}

public class DeliveryRouteDetailViewModel
{
    public long RouteId { get; set; }
    public string RouteTitle { get; set; } = "";
    public DateTime RouteDate { get; set; }
    public string Remarks { get; set; } = "";
    public string? Distance { get; set; }
    public string? Duration { get; set; }
    public string? RouteMapUrl { get; set; }
    public decimal DriverCharges { get; set; }
    public string? DriverName { get; set; }
    public int OrderCount { get; set; }
    public List<RouteOrderItem> Orders { get; set; } = new();
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Read-only service for Delivery Routes Phase 1 migration.
/// Uses BusinessConnection for route tables (db_cakerstreet_business)
/// and DefaultConnection for order/task/user data (main DB).
/// Migrated from managedeliveryroutes.aspx + deliveryRouteDetail.aspx.
/// </summary>
public class DeliveryRoutesService
{
    private readonly string _businessConnectionString;
    private readonly string _defaultConnectionString;

    public DeliveryRoutesService(IConfiguration config)
    {
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    // ─── Method 1: GetRouteListAsync ───────────────────────────────────────────

    /// <summary>
    /// Gets paginated route list for a given date.
    /// Queries tbl_deliveryRoute with joins to tbl_deliveryRouteApi and tbl_deliveryRouteChargeTemplate.
    /// Driver names are fetched separately from DefaultConnection.
    /// </summary>
    public async Task<DeliveryRouteListResult> GetRouteListAsync(DateTime date, int page, int pageSize)
    {
        var result = new DeliveryRouteListResult
        {
            CurrentPage = page,
            PageSize = pageSize,
            SelectedDate = date.ToString("dd/MM/yyyy")
        };

        try
        {
            await using var conn = new SqlConnection(_businessConnectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("USPGetDeliveryRoutes", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;

            cmd.Parameters.AddWithValue("@routeDate", date.Date);
            cmd.Parameters.AddWithValue("@PageNumber", page);
            cmd.Parameters.AddWithValue("@ProductsPerPage", pageSize);

            var totalCountParam = new SqlParameter("@HowManyProducts", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(totalCountParam);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new DeliveryRouteListItem
                {
                    RouteId = GetLongSafe(reader, "route_ID"),
                    RouteTitle = GetStringSafe(reader, "route_title"),
                    Remarks = GetStringSafe(reader, "route_remarks"),
                    DisplayOrder = GetIntSafe(reader, "route_displayOrder"),
                    RouteDate = GetDateTimeSafe(reader, "route_date"),
                    DriverId = GetLongSafe(reader, "route_driverID"),
                    DriverCharges = GetDecimalSafe(reader, "route_DriverCharges"),
                    TemplateId = GetLongSafe(reader, "route_TemplateID"),
                    ReturnToUnit = GetBoolSafe(reader, "route_returnToUnit"),
                    IsChargePaid = GetBoolSafe(reader, "route_isChargePaid"),
                    ApiId = GetStringSafe(reader, "route_ApiID"),
                    ApiMiles = GetStringSafe(reader, "routeApi_miles"),
                    ApiSeconds = GetIntNullableSafe(reader, "routeApi_seconds"),
                    ApiCharges = GetDecimalNullableSafe(reader, "routeApi_charges"),
                    ApiUrl = GetStringSafe(reader, "routeApi_url"),
                    OrderCount = GetIntSafe(reader, "countprd"),
                    TotalOrderAmt = GetDecimalSafe(reader, "TotalOrderAmt"),
                    TotalDeliveryAmt = GetDecimalSafe(reader, "TotaldeleiveryAmt")
                };
                result.Routes.Add(item);
            }

            await reader.CloseAsync();

            if (totalCountParam.Value != null && totalCountParam.Value != DBNull.Value)
            {
                result.TotalCount = Convert.ToInt32(totalCountParam.Value);
            }

            // Fetch driver names from DefaultConnection
            var driverIds = result.Routes
                .Where(r => r.DriverId > 0)
                .Select(r => r.DriverId)
                .Distinct()
                .ToList();

            if (driverIds.Count > 0)
            {
                var driverNames = await GetDriverNamesAsync(driverIds);
                foreach (var route in result.Routes)
                {
                    if (route.DriverId > 0 && driverNames.TryGetValue(route.DriverId, out var name))
                    {
                        route.DriverName = name;
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

    // ─── Method 2: GetRouteOrdersAsync ─────────────────────────────────────────

    /// <summary>
    /// Gets orders assigned to a specific route.
    /// Step 1: Get order IDs from BusinessConnection (tbl_deliveryRouteOrder).
    /// Step 2: Get order details from DefaultConnection (tbl_order, tbl_ordercollection, etc).
    /// </summary>
    public async Task<List<RouteOrderItem>> GetRouteOrdersAsync(long routeId)
    {
        var items = new List<RouteOrderItem>();

        try
        {
            // Step 1: Get order IDs from BusinessConnection
            var orderIds = new List<long>();

            await using (var bizConn = new SqlConnection(_businessConnectionString))
            {
                await bizConn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT routeOrder_orderID FROM tbl_deliveryRouteOrder 
                    WHERE routeOrder_routeID = @routeId", bizConn);
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@routeId", routeId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var orderId = GetLongSafe(reader, "routeOrder_orderID");
                    if (orderId > 0) orderIds.Add(orderId);
                }
            }

            if (orderIds.Count == 0) return items;

            // Step 2: Get order details from DefaultConnection
            await using var defaultConn = new SqlConnection(_defaultConnectionString);
            await defaultConn.OpenAsync();

            // Build parameterized IN clause
            var paramNames = new List<string>();
            for (int i = 0; i < orderIds.Count; i++)
            {
                paramNames.Add($"@oid{i}");
            }

            var sql = $@"
                SELECT o.order_ID,
                       ISNULL(sd.shipping_address1,'') + ' ' + ISNULL(sd.shipping_address2,'') + ' ' + ISNULL(sd.shipping_city,'') + ' ' + ISNULL(sd.shipping_zip,'') AS DestAddress,
                       oc.ordercollection_deliverymode,
                       oc.ordercollection_Date AS ordercollection_OcasionDate,
                       ISNULL(t.ordertask_tasksts, 0) AS ordertask_tasksts,
                       ISNULL(t.ordertask_isCompleted, 0) AS ordertask_isCompleted
                FROM tbl_order o
                INNER JOIN tbl_ordercollection oc ON o.order_ID = oc.ordercollection_OrderID
                LEFT JOIN tbl_shippingDetail sd ON o.order_ID = sd.shipping_orderID
                LEFT JOIN tbl_ordertask t ON t.ordertask_orderID = o.order_ID
                WHERE o.order_ID IN ({string.Join(",", paramNames)})
                ORDER BY o.order_ID";

            await using var orderCmd = new SqlCommand(sql, defaultConn);
            orderCmd.CommandTimeout = 120;

            for (int i = 0; i < orderIds.Count; i++)
            {
                orderCmd.Parameters.AddWithValue($"@oid{i}", orderIds[i]);
            }

            int serialNo = 1;
            await using var orderReader = await orderCmd.ExecuteReaderAsync();
            while (await orderReader.ReadAsync())
            {
                var taskSts = GetIntSafe(orderReader, "ordertask_tasksts");
                var isCompleted = GetBoolSafe(orderReader, "ordertask_isCompleted");
                var deliveryModeId = GetIntSafe(orderReader, "ordercollection_deliverymode");
                var deliveryDate = GetDateTimeSafe(orderReader, "ordercollection_OcasionDate");

                var item = new RouteOrderItem
                {
                    SerialNo = serialNo++,
                    OrderId = GetLongSafe(orderReader, "order_ID"),
                    ReadyStatus = GetReadyStatus(taskSts, isCompleted),
                    DeliveryAddress = GetStringSafe(orderReader, "DestAddress").Trim(),
                    DeliveryModeId = deliveryModeId,
                    DeliveryMode = GetDeliveryModeString(deliveryModeId),
                    DeliveryDate = deliveryDate,
                    DeliveryTimeWindow = GetDeliveryTimeWindow(deliveryModeId, deliveryDate)
                };
                items.Add(item);
            }
        }
        catch
        {
            // Safe fallback — return empty list on error
        }

        return items;
    }

    // ─── Method 3: GetRouteDetailAsync ─────────────────────────────────────────

    /// <summary>
    /// Gets full route detail for the standalone detail page.
    /// Step 1: Route info + API data from BusinessConnection.
    /// Step 2: Driver name from DefaultConnection.
    /// Step 3: Order list via GetRouteOrdersAsync.
    /// </summary>
    public async Task<DeliveryRouteDetailViewModel?> GetRouteDetailAsync(long routeId)
    {
        try
        {
            DeliveryRouteDetailViewModel? model = null;
            long driverId = 0;

            // Step 1: Get route info from BusinessConnection
            await using (var bizConn = new SqlConnection(_businessConnectionString))
            {
                await bizConn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT r.route_ID, r.route_title, r.route_date, r.route_remarks,
                           r.route_driverID, r.route_DriverCharges, r.route_returnToUnit, r.route_TemplateID, r.route_ApiID,
                           a.routeApi_miles, a.routeApi_seconds, a.routeApi_url,
                           (SELECT COUNT(*) FROM tbl_deliveryRouteOrder ro WHERE ro.routeOrder_routeID = r.route_ID) AS CountPrd
                    FROM tbl_deliveryRoute r
                    LEFT JOIN tbl_deliveryRouteApi a ON r.route_ApiID = a.routeApi_ID
                    WHERE r.route_ID = @routeId", bizConn);
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@routeId", routeId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var apiMiles = GetStringSafe(reader, "routeApi_miles");
                    var apiSeconds = GetIntNullableSafe(reader, "routeApi_seconds");

                    model = new DeliveryRouteDetailViewModel
                    {
                        RouteId = GetLongSafe(reader, "route_ID"),
                        RouteTitle = GetStringSafe(reader, "route_title"),
                        RouteDate = GetDateTimeSafe(reader, "route_date"),
                        Remarks = GetStringSafe(reader, "route_remarks"),
                        DriverCharges = GetDecimalSafe(reader, "route_DriverCharges"),
                        OrderCount = GetIntSafe(reader, "CountPrd"),
                        Distance = !string.IsNullOrEmpty(apiMiles) ? $"{apiMiles} mi" : null,
                        Duration = apiSeconds.HasValue ? TimeSpan.FromSeconds(apiSeconds.Value).ToString() : null,
                        RouteMapUrl = GetStringSafe(reader, "routeApi_url")
                    };

                    driverId = GetLongSafe(reader, "route_driverID");

                    // Null out empty strings for URL
                    if (string.IsNullOrEmpty(model.RouteMapUrl)) model.RouteMapUrl = null;
                }
            }

            if (model == null) return null;

            // Step 2: Get driver name from DefaultConnection
            if (driverId > 0)
            {
                await using var defaultConn = new SqlConnection(_defaultConnectionString);
                await defaultConn.OpenAsync();
                await using var driverCmd = new SqlCommand(@"
                    SELECT customer_Name FROM tbl_bakeryuser WHERE customer_ID = @driverId", defaultConn);
                driverCmd.CommandTimeout = 120;
                driverCmd.Parameters.AddWithValue("@driverId", driverId);

                var driverName = await driverCmd.ExecuteScalarAsync();
                model.DriverName = driverName?.ToString() ?? "";
            }

            // Step 3: Get order list
            model.Orders = await GetRouteOrdersAsync(routeId);

            return model;
        }
        catch
        {
            return null;
        }
    }

    // ─── Helper: Get Driver Names ──────────────────────────────────────────────

    /// <summary>
    /// Fetches driver names from DefaultConnection for a list of driver IDs.
    /// </summary>
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
                var id = GetLongSafe(reader, "customer_ID");
                var name = GetStringSafe(reader, "customer_Name");
                if (id > 0) names[id] = name;
            }
        }
        catch
        {
            // Safe fallback — return empty dictionary
        }

        return names;
    }

    // ─── Helper: Ready Status Logic ────────────────────────────────────────────

    /// <summary>
    /// Determines "Ready" or "Not Ready" based on task status fields.
    /// Ready if: tasksts >= 33 OR (tasksts == 22 AND isCompleted == true)
    /// </summary>
    private static string GetReadyStatus(int taskSts, bool isCompleted)
    {
        if (taskSts >= 33) return "Ready";
        if (taskSts == 22 && isCompleted) return "Ready";
        return "Not Ready";
    }

    // ─── Helper: Delivery Mode Mapping ─────────────────────────────────────────

    /// <summary>
    /// Maps delivery mode ID to display string.
    /// 1 → Collection, 2 → Hand Delivery, 4 → Postal Delivery
    /// </summary>
    private static string GetDeliveryModeString(int modeId)
    {
        return modeId switch
        {
            1 => "Collection",
            2 => "Hand Delivery",
            4 => "Postal Delivery",
            _ => ""
        };
    }

    // ─── Helper: Delivery Time Window ──────────────────────────────────────────

    /// <summary>
    /// Formats delivery time window based on mode.
    /// Mode 1 (Collection): {date} ({time} - {time+1hr})
    /// Mode 2 (Hand Delivery): {date} ({time} - {time+2hr})
    /// Mode 4 (Postal): {date} (10:00 AM - 05:30 PM)
    /// </summary>
    private static string GetDeliveryTimeWindow(int modeId, DateTime deliveryDate)
    {
        if (deliveryDate == DateTime.MinValue) return "";

        var dateStr = deliveryDate.ToString("dd/MM/yyyy");
        var timeStr = deliveryDate.ToString("hh:mm tt");

        return modeId switch
        {
            1 => $"{dateStr} ({timeStr} - {deliveryDate.AddHours(1).ToString("hh:mm tt")})",
            2 => $"{dateStr} ({timeStr} - {deliveryDate.AddHours(2).ToString("hh:mm tt")})",
            4 => $"{dateStr} (10:00 AM - 05:30 PM)",
            _ => dateStr
        };
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

    private static int? GetIntNullableSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }
        catch { return null; }
    }

    private static long GetLongSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
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

            if (int.TryParse(value?.ToString(), out var intVal))
                return intVal != 0;

            return false;
        }
        catch { return false; }
    }

    private static decimal GetDecimalSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
        }
        catch { return 0m; }
    }

    private static decimal? GetDecimalNullableSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
        }
        catch { return null; }
    }

    // ─── Query & Mutation Extensions ───────────────────────────────────────────

    public async Task<List<BakeryDriverItem>> GetDriversAsync(long webshopId)
    {
        var items = new List<BakeryDriverItem>();
        await using var conn = new SqlConnection(_defaultConnectionString);
        await conn.OpenAsync();
        var sql = @"SELECT customer_ID, customer_Name 
                    FROM tbl_bakeryuser 
                    WHERE customer_type IN (3, 4) AND customer_stafftype = 5 AND customer_webshopID = @webshopId 
                    ORDER BY customer_Name";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BakeryDriverItem
            {
                Id = GetLongSafe(reader, "customer_ID"),
                Name = GetStringSafe(reader, "customer_Name")
            });
        }
        return items;
    }

    public async Task<List<RouteChargeTemplateItem>> GetTemplatesAsync(long webshopId)
    {
        var items = new List<RouteChargeTemplateItem>();
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        var sql = @"SELECT deliveryRouteChargeTemplate_ID, deliveryRouteChargeTemplate_title 
                    FROM tbl_deliveryRouteChargeTemplate 
                    WHERE deliveryRouteChargeTemplate_isActive = 1 AND deliveryRouteChargeTemplate_webstoreID = @webshopId 
                    ORDER BY deliveryRouteChargeTemplate_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new RouteChargeTemplateItem
            {
                Id = GetLongSafe(reader, "deliveryRouteChargeTemplate_ID"),
                Title = GetStringSafe(reader, "deliveryRouteChargeTemplate_title")
            });
        }
        return items;
    }

    public async Task<List<DefaultRouteItem>> GetDefaultRoutesAsync()
    {
        var items = new List<DefaultRouteItem>();
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        var sql = @"SELECT deliveryDefaultRoute_ID, deliveryDefaultRoute_title 
                    FROM tbl_deliveryDefaultRoute 
                    ORDER BY deliveryDefaultRoute_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DefaultRouteItem
            {
                Id = GetLongSafe(reader, "deliveryDefaultRoute_ID"),
                Title = GetStringSafe(reader, "deliveryDefaultRoute_title")
            });
        }
        return items;
    }

    public async Task SaveRouteAsync(long routeId, string title, string remarks, int displayOrder, DateTime date, long userId, bool isDefault)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        if (routeId == 0)
        {
            var sql = @"INSERT INTO tbl_deliveryRoute 
                        (route_title, route_remarks, route_displayOrder, route_date, route_TemplateID, route_isChargePaid, route_PaidRemarks, route_modifiedOn, route_modifiedBy, route_ApiID, route_driverID, route_DriverCharges, route_returnToUnit)
                        VALUES (@title, @remarks, @displayOrder, @date, 0, 0, '', GETDATE(), @userId, 0, 0, 0, 0)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@remarks", remarks);
            cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
            cmd.Parameters.AddWithValue("@date", date.Date);
            cmd.Parameters.AddWithValue("@userId", userId);
            await cmd.ExecuteNonQueryAsync();

            if (isDefault)
            {
                var checkSql = "SELECT COUNT(1) FROM tbl_deliveryDefaultRoute WHERE deliveryDefaultRoute_title = @title";
                await using var checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@title", title);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    var defSql = @"INSERT INTO tbl_deliveryDefaultRoute 
                                  (deliveryDefaultRoute_title, deliveryDefaultRoute_remarks, deliveryDefaultRoute_modifiedOn, deliveryDefaultRoute_modifiedBy, deliveryDefaultRoute_displayOrder)
                                  VALUES (@title, @remarks, GETDATE(), @userId, @displayOrder)";
                    await using var defCmd = new SqlCommand(defSql, conn);
                    defCmd.Parameters.AddWithValue("@title", title);
                    defCmd.Parameters.AddWithValue("@remarks", remarks);
                    defCmd.Parameters.AddWithValue("@userId", userId);
                    defCmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                    await defCmd.ExecuteNonQueryAsync();
                }
            }
        }
        else
        {
            var sql = @"UPDATE tbl_deliveryRoute 
                        SET route_title = @title, route_remarks = @remarks, route_displayOrder = @displayOrder, route_modifiedOn = GETDATE(), route_modifiedBy = @userId
                        WHERE route_ID = @routeId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@remarks", remarks);
            cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@routeId", routeId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task BulkUpdateRoutesAsync(List<DeliveryRouteListItem> routes, long userId)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        foreach (var r in routes)
        {
            var sql = @"UPDATE tbl_deliveryRoute 
                        SET route_title = @title, route_remarks = @remarks, route_displayOrder = @displayOrder, route_modifiedOn = GETDATE(), route_modifiedBy = @userId
                        WHERE route_ID = @routeId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", r.RouteTitle);
            cmd.Parameters.AddWithValue("@remarks", r.Remarks);
            cmd.Parameters.AddWithValue("@displayOrder", r.DisplayOrder);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@routeId", r.RouteId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteRoutesAsync(List<long> routeIds)
    {
        if (routeIds == null || routeIds.Count == 0) return;
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var transaction = conn.BeginTransaction();
        try
        {
            var idsStr = string.Join(",", routeIds);
            var sqlOrders = $"DELETE FROM tbl_deliveryRouteOrder WHERE routeOrder_routeID IN ({idsStr})";
            await using (var cmdOrders = new SqlCommand(sqlOrders, conn, transaction))
            {
                await cmdOrders.ExecuteNonQueryAsync();
            }

            var sqlRoutes = $"DELETE FROM tbl_deliveryRoute WHERE route_ID IN ({idsStr})";
            await using (var cmdRoutes = new SqlCommand(sqlRoutes, conn, transaction))
            {
                await cmdRoutes.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SaveDriverChargesAsync(long routeId, long driverId, decimal driverCharges)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        var sql = @"UPDATE tbl_deliveryRoute 
                    SET route_driverID = @driverId, route_DriverCharges = @driverCharges, route_modifiedOn = GETDATE()
                    WHERE route_ID = @routeId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@driverId", driverId);
        cmd.Parameters.AddWithValue("@driverCharges", driverCharges);
        cmd.Parameters.AddWithValue("@routeId", routeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> AddDriverAsync(string name, string email, long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnectionString);
        await conn.OpenAsync();

        var checkSql = "SELECT COUNT(1) FROM tbl_bakeryuser WHERE customer_EmailID = @email";
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@email", email);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
            if (exists)
            {
                return "Email ID already exists!";
            }
        }

        var ccCode = Guid.NewGuid().ToString();
        var sql = @"INSERT INTO tbl_bakeryuser 
                    (customer_EmailID, customer_isActive, customer_isOpen, customer_stafftype, customer_Name, 
                     customer_password, customer_phone, customer_type, customer_webshopID, customer_ExpiredOn, 
                     customer_createdOn, customer_ccCode, customer_istemporary)
                    VALUES 
                    (@email, 0, 1, 5, @name, '', '', 3, @webshopId, DATEADD(year, 1, GETDATE()), GETDATE(), @ccCode, 1)";
        
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        cmd.Parameters.AddWithValue("@ccCode", ccCode);
        await cmd.ExecuteNonQueryAsync();

        return null;
    }
}
