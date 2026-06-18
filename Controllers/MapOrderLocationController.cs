using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

[Route("map-orderlocation")]
[Route("map_orderlocation")]
[Route("map_orderlocation.aspx")]
public class MapOrderLocationController : Controller
{
    private readonly IConfiguration _config;

    public MapOrderLocationController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Map page — shows orders on Google Maps.
    /// Feature-flagged: requires GoogleMaps:Enabled = true and GoogleMaps:JsApiKey to be set.
    /// If disabled, shows a message instead of the map.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index([FromQuery] string? orderIds = null, [FromQuery] string? date = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var mapsEnabled = _config.GetValue<bool>("GoogleMaps:Enabled", false);
        var jsApiKey = _config["GoogleMaps:JsApiKey"] ?? "";

        ViewBag.MapsEnabled = mapsEnabled && !string.IsNullOrEmpty(jsApiKey);
        ViewBag.JsApiKey = jsApiKey;
        ViewBag.OrderIds = orderIds ?? "";
        ViewBag.Date = date ?? "";

        return View();
    }

    /// <summary>
    /// Returns order location data for the map pins.
    /// Equivalent of legacy map_orderlocation.aspx.cs GetLocations().
    /// </summary>
    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations([FromQuery] string orderIds, [FromQuery] string date)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Unauthorized();

        if (string.IsNullOrEmpty(orderIds))
            return Json(new List<object>());

        var parsedIds = orderIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (parsedIds.Count == 0)
            return Json(new List<object>());

        var locations = new List<object>();
        var defaultConnectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        var businessConnectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        var originAddress = _config["GoogleMaps:OriginAddress"] ?? "UB1 3AF";

        try
        {
            await using var conn = new SqlConnection(defaultConnectionString);
            await conn.OpenAsync();

            // Build parameterized IN clause
            var paramNames = new List<string>();
            for (int i = 0; i < parsedIds.Count; i++)
                paramNames.Add($"@oid{i}");

            var sql = $@"
                SELECT o.order_ID, o.order_branchID,
                       ISNULL(sd.shipping_zip, '') AS shipping_zip,
                       ISNULL(sd.shipping_address1,'') + ' ' + ISNULL(sd.shipping_city,'') + ' ' + ISNULL(sd.shipping_zip,'') AS full_address,
                       oc.ordercollection_deliverymode,
                       oc.ordercollection_Date,
                       ISNULL(t.ordertask_tasksts, 0) AS ordertask_tasksts,
                       ISNULL(t.ordertask_isCompleted, 0) AS ordertask_isCompleted,
                       ISNULL(w.webstore_postcode, '') AS branch_postcode
                FROM tbl_order o
                INNER JOIN tbl_ordercollection oc ON o.order_ID = oc.ordercollection_OrderID
                LEFT JOIN tbl_shippingDetail sd ON o.order_ID = sd.shipping_orderID
                LEFT JOIN tbl_ordertask t ON t.ordertask_orderID = o.order_ID
                LEFT JOIN tbl_webstore w ON o.order_branchID = w.webstore_ID
                WHERE o.order_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            for (int i = 0; i < parsedIds.Count; i++)
                cmd.Parameters.AddWithValue($"@oid{i}", parsedIds[i]);

            // Also get route titles from BusinessConnection
            var routeTitles = new Dictionary<long, string>();
            try
            {
                await using var bizConn = new SqlConnection(businessConnectionString);
                await bizConn.OpenAsync();
                var bizParamNames = new List<string>();
                for (int i = 0; i < parsedIds.Count; i++)
                    bizParamNames.Add($"@bid{i}");

                var bizSql = $@"SELECT ro.routeOrder_orderID, r.route_title 
                    FROM tbl_deliveryRouteOrder ro 
                    INNER JOIN tbl_deliveryRoute r ON ro.routeOrder_routeID = r.route_ID
                    WHERE ro.routeOrder_orderID IN ({string.Join(",", bizParamNames)})";
                await using var bizCmd = new SqlCommand(bizSql, bizConn);
                bizCmd.CommandTimeout = 120;
                for (int i = 0; i < parsedIds.Count; i++)
                    bizCmd.Parameters.AddWithValue($"@bid{i}", parsedIds[i]);
                await using var bizReader = await bizCmd.ExecuteReaderAsync();
                while (await bizReader.ReadAsync())
                {
                    var oid = bizReader.GetInt64(0);
                    var title = bizReader.IsDBNull(1) ? "" : bizReader.GetString(1);
                    routeTitles[oid] = title;
                }
            }
            catch { /* Graceful — route titles are optional */ }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var orderId = reader.GetInt64(reader.GetOrdinal("order_ID"));
                var deliveryMode = reader.GetInt32(reader.GetOrdinal("ordercollection_deliverymode"));
                var shippingZip = reader.IsDBNull(reader.GetOrdinal("shipping_zip")) ? "" : reader.GetString(reader.GetOrdinal("shipping_zip"));
                var branchPostcode = reader.IsDBNull(reader.GetOrdinal("branch_postcode")) ? "" : reader.GetString(reader.GetOrdinal("branch_postcode"));
                var collectionDate = reader.GetDateTime(reader.GetOrdinal("ordercollection_Date"));
                var taskSts = reader.GetInt32(reader.GetOrdinal("ordertask_tasksts"));
                var isCompleted = reader.GetBoolean(reader.GetOrdinal("ordertask_isCompleted"));

                // Map location: for collection (mode 1), use branch postcode; else shipping zip
                var mapLocation = (deliveryMode == 1) ? branchPostcode : shippingZip;
                var modeText = deliveryMode switch { 1 => "Collection", 2 => "Hand Delivery", 4 => "Postal Delivery", _ => "Delivery" };
                var readyStatus = (taskSts >= 33 || (taskSts == 22 && isCompleted)) ? "Ready" : "Not Ready";
                var routeTitle = routeTitles.GetValueOrDefault(orderId, "");
                var hasRoute = !string.IsNullOrEmpty(routeTitle);

                // Build address HTML matching legacy format
                var timeDisplay = deliveryMode != 4
                    ? (deliveryMode == 1
                        ? collectionDate.ToString("dd/MM/yyyy (hh:mm tt") + " - " + collectionDate.AddHours(1).ToString("hh:mm tt)")
                        : collectionDate.ToString("dd/MM/yyyy (hh:mm tt") + " - " + collectionDate.AddHours(2).ToString("hh:mm tt)"))
                    : collectionDate.ToString("dd/MM/yyyy");

                var addressHtml = $"<strong><a target='_blank' href='/businessorders?ordertype=12&dayID={(int)collectionDate.DayOfWeek}&q={orderId}'>{orderId}</a></strong><br>{shippingZip} - {modeText} [{readyStatus}]<br/>{timeDisplay}";

                if (hasRoute)
                    addressHtml += $"<br/><span style='color:#155724;font-weight:bold;'>Route: {routeTitle}</span>";

                locations.Add(new { mapLocation, address = addressHtml });
            }

            // Add origin pin if not already in the list
            var originNormalized = originAddress.Replace(" ", "").ToLower();
            if (!locations.Any(l => ((dynamic)l).mapLocation?.ToString()?.Replace(" ", "").ToLower() == originNormalized))
            {
                locations.Add(new { mapLocation = originAddress, address = $"<strong>CakerStreet HQ</strong><br>{originAddress}" });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }

        return Json(locations);
    }
}
