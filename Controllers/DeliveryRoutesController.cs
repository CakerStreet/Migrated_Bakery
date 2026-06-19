using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;
using System.Data;
using System.Globalization;

namespace CakerStreet.Business.Controllers;

// ─── Request Models ────────────────────────────────────────────────────────────

public class AssignRouteRequest
{
    public long OrderId { get; set; }
    public long RouteId { get; set; }
    public string RouteDate { get; set; } = "";
}

public class CalculateRouteRequest
{
    public long RouteId { get; set; }
    public long TemplateId { get; set; }
    public bool ReturnToUnit { get; set; }
}

/// <summary>
/// Controller for the Delivery Routes list page and route assignment mutations.
/// Route: /managedeliveryroutes
/// Migrated from managedeliveryroutes.aspx + webservices.aspx (get_DeliveryRoute_container, updateDeliveryRoute_toOrder).
/// </summary>
[Route("managedeliveryroutes")]
[Route("deliveryroutes")]
[Route("managedeliveryroutes.aspx")]
public class DeliveryRoutesController : Controller
{
    private readonly DeliveryRoutesService _routesService;
    private readonly BakeryMenuService _menuService;
    private readonly RouteCalculationService _routeCalcService;
    private readonly IConfiguration _config;

    public DeliveryRoutesController(
        DeliveryRoutesService routesService,
        BakeryMenuService menuService,
        RouteCalculationService routeCalcService,
        IConfiguration config)
    {
        _routesService = routesService;
        _menuService = menuService;
        _routeCalcService = routeCalcService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null, int pageno = 1)
    {
        // Auth check from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check: userType 1/2 auto-allowed, else check module 16
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 16);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // If no search param or not a valid date, redirect to today's date
        if (string.IsNullOrEmpty(search) ||
            !DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            var today = DateTime.Today.ToString("dd/MM/yyyy");
            return Redirect($"/managedeliveryroutes?search={today}");
        }

        // Parse date
        var date = DateTime.ParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture);

        // Call service
        var result = await _routesService.GetRouteListAsync(date, pageno, 24);

        // Fetch dropdown lists
        var bid = long.Parse(webshopId);
        var drivers = await _routesService.GetDriversAsync(bid);
        var templates = await _routesService.GetTemplatesAsync(bid);
        var defaultRoutes = await _routesService.GetDefaultRoutesAsync();

        // Set ViewBag for layout
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        // Pass data to view
        ViewBag.RouteList = result;
        ViewBag.Drivers = drivers;
        ViewBag.Templates = templates;
        ViewBag.DefaultRoutes = defaultRoutes;
        ViewBag.SelectedDate = search;

        return View();
    }

    [HttpGet("orders/{routeId:long}")]
    public async Task<IActionResult> GetRouteOrders(long routeId)
    {
        var orders = await _routesService.GetRouteOrdersAsync(routeId);
        return PartialView("_RouteOrders", orders);
    }

    // ─── Phase 2A: Route Picker + Assignment ───────────────────────────────────

    /// <summary>
    /// Returns routes for a date with selection status for a specific order.
    /// Equivalent of legacy get_DeliveryRoute_container.
    /// </summary>
    [HttpGet("routesfororder")]
    public async Task<IActionResult> GetRoutesForOrder([FromQuery] long orderId, [FromQuery] string date)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Unauthorized();

        if (!DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return BadRequest(new { error = "Invalid date format. Expected dd/MM/yyyy." });

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        var routes = new List<object>();

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SET DATEFORMAT dmy;
                SELECT route_ID AS Id, 
                       route_title + ' - (' + CAST(ISNULL(PrdCount, 0) AS VARCHAR(50)) + ' Orders)' AS Title,
                       (SELECT COUNT(1) FROM tbl_deliveryRouteOrder WHERE routeOrder_routeID = route_ID AND routeOrder_orderID = @orderId) AS IsSelected
                FROM tbl_deliveryRoute gm
                LEFT OUTER JOIN (
                    SELECT routeID = routeOrder_routeID, PrdCount = COUNT(routeOrder_orderID) 
                    FROM tbl_deliveryRouteOrder GROUP BY routeOrder_routeID
                ) gf ON gm.route_ID = gf.routeID 
                WHERE route_date = @date 
                ORDER BY route_displayOrder", conn);

            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@date", parsedDate.Date);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                routes.Add(new
                {
                    id = reader.GetInt64(reader.GetOrdinal("Id")),
                    title = reader.IsDBNull(reader.GetOrdinal("Title")) ? "" : reader.GetString(reader.GetOrdinal("Title")),
                    isSelected = reader.GetInt32(reader.GetOrdinal("IsSelected"))
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load routes.", detail = ex.Message });
        }

        return Json(routes);
    }

    /// <summary>
    /// Assigns/unassigns an order to a route (toggle logic).
    /// Equivalent of legacy updateDeliveryRoute_toOrder.
    /// Uses a transaction for atomicity.
    /// </summary>
    [HttpPost("assignroute")]
    public async Task<IActionResult> AssignRoute([FromBody] AssignRouteRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        if (request.OrderId <= 0 || request.RouteId <= 0)
            return BadRequest(new { error = "OrderId and RouteId must be greater than 0." });

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        string result;

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var transaction = conn.BeginTransaction();

            try
            {
                // Check if order already has a route assignment
                long? existingRouteId = null;
                await using (var checkCmd = new SqlCommand(
                    "SELECT routeOrder_routeID FROM tbl_deliveryRouteOrder WHERE routeOrder_orderID = @orderId",
                    conn, transaction))
                {
                    checkCmd.CommandTimeout = 120;
                    checkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                    var existing = await checkCmd.ExecuteScalarAsync();
                    if (existing != null && existing != DBNull.Value)
                        existingRouteId = Convert.ToInt64(existing);
                }

                if (existingRouteId.HasValue && existingRouteId.Value == request.RouteId)
                {
                    // TOGGLE OFF — same route, remove assignment
                    await using var deleteCmd = new SqlCommand(
                        "DELETE FROM tbl_deliveryRouteOrder WHERE routeOrder_orderID = @orderId",
                        conn, transaction);
                    deleteCmd.CommandTimeout = 120;
                    deleteCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                    await deleteCmd.ExecuteNonQueryAsync();
                    result = "0";
                }
                else if (existingRouteId.HasValue)
                {
                    // MOVE — different route, delete old + insert new
                    await using (var deleteCmd = new SqlCommand(
                        "DELETE FROM tbl_deliveryRouteOrder WHERE routeOrder_orderID = @orderId",
                        conn, transaction))
                    {
                        deleteCmd.CommandTimeout = 120;
                        deleteCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await using (var insertCmd = new SqlCommand(@"
                        INSERT INTO tbl_deliveryRouteOrder 
                            (routeOrder_orderID, routeOrder_routeID, routeOrder_miles, routeOrder_seconds, routeOrder_sortNo, routeOrder_modifiedOn, routeOrder_modifiedBy) 
                        VALUES (@orderId, @routeId, 0, 0, 1, GETDATE(), @userId)",
                        conn, transaction))
                    {
                        insertCmd.CommandTimeout = 120;
                        insertCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                        insertCmd.Parameters.AddWithValue("@routeId", request.RouteId);
                        insertCmd.Parameters.AddWithValue("@userId", userId);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                    result = "1";
                }
                else
                {
                    // ASSIGN — no existing assignment
                    await using var insertCmd = new SqlCommand(@"
                        INSERT INTO tbl_deliveryRouteOrder 
                            (routeOrder_orderID, routeOrder_routeID, routeOrder_miles, routeOrder_seconds, routeOrder_sortNo, routeOrder_modifiedOn, routeOrder_modifiedBy) 
                        VALUES (@orderId, @routeId, 0, 0, 1, GETDATE(), @userId)",
                        conn, transaction);
                    insertCmd.CommandTimeout = 120;
                    insertCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                    insertCmd.Parameters.AddWithValue("@routeId", request.RouteId);
                    insertCmd.Parameters.AddWithValue("@userId", userId);
                    await insertCmd.ExecuteNonQueryAsync();
                    result = "1";
                }

                // ALWAYS invalidate calculated route data after any change
                await using (var deleteApiCmd = new SqlCommand(
                    "DELETE FROM tbl_DeliveryRouteApi WHERE routeApi_routeID = @routeId",
                    conn, transaction))
                {
                    deleteApiCmd.CommandTimeout = 120;
                    deleteApiCmd.Parameters.AddWithValue("@routeId", request.RouteId);
                    await deleteApiCmd.ExecuteNonQueryAsync();
                }

                await using (var resetCmd = new SqlCommand(
                    "UPDATE tbl_deliveryRoute SET route_ApiID = 0, route_DriverCharges = 0 WHERE route_ID = @routeId",
                    conn, transaction))
                {
                    resetCmd.CommandTimeout = 120;
                    resetCmd.Parameters.AddWithValue("@routeId", request.RouteId);
                    await resetCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to assign route.", detail = ex.Message });
        }

        return Json(new { success = true, assigned = result });
    }

    // ─── Phase 2C: Calculate Route (Feature-Flagged, Real Implementation) ─────

    /// <summary>
    /// Calculates optimal route via Google Routes API.
    /// Feature-flagged: requires Mutations:DeliveryRouteCalculation:Enabled = true
    /// and GoogleMaps:RoutesApiKey to be set.
    /// </summary>
    [HttpPost("calculateroute")]
    public async Task<IActionResult> CalculateRoute([FromBody] CalculateRouteRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        // Feature flag check
        var calcEnabled = _config.GetValue<bool>("Mutations:DeliveryRouteCalculation:Enabled", false);
        var routesApiKey = _config["GoogleMaps:RoutesApiKey"] ?? "";

        if (!calcEnabled || string.IsNullOrEmpty(routesApiKey))
            return StatusCode(403, new { success = false, error = "Route calculation is not enabled. Configure GoogleMaps:RoutesApiKey and set Mutations:DeliveryRouteCalculation:Enabled = true." });

        if (request.RouteId <= 0)
            return BadRequest(new { success = false, error = "Invalid RouteId" });

        // Call the real route calculation service
        var result = await _routeCalcService.CalculateRouteAsync(
            request.RouteId,
            request.TemplateId,
            request.ReturnToUnit,
            userId,
            long.Parse(webshopId));

        if (!result.Success)
            return Json(new { success = false, error = result.Error });

        return Json(new
        {
            success = true,
            miles = result.Miles,
            seconds = result.Seconds,
            charges = result.Charges,
            mapUrl = result.MapUrl
        });
    }

    // ─── Phase 2 Mutations ─────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> SaveRoute(
        [FromForm] long routeId,
        [FromForm] string title,
        [FromForm] string remarks,
        [FromForm] int displayOrder,
        [FromForm] string routeDate,
        [FromForm] bool isDefault)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        if (string.IsNullOrEmpty(title))
        {
            TempData["Msg"] = "Route title is required.";
            TempData["MsgClass"] = "alert alert-danger";
            return Redirect($"/managedeliveryroutes?search={routeDate}");
        }

        if (!DateTime.TryParseExact(routeDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            TempData["Msg"] = "Invalid date format. Expected dd/MM/yyyy.";
            TempData["MsgClass"] = "alert alert-danger";
            return Redirect($"/managedeliveryroutes?search={routeDate}");
        }

        await _routesService.SaveRouteAsync(routeId, title, remarks, displayOrder, parsedDate, userId, isDefault);

        TempData["Msg"] = "Route details have been saved successfully.";
        TempData["MsgClass"] = "alert alert-success";

        return Redirect($"/managedeliveryroutes?search={routeDate}");
    }

    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdate([FromBody] List<DeliveryRouteListItem> routes)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        if (routes == null || routes.Count == 0)
            return BadRequest("No routes selected.");

        await _routesService.BulkUpdateRoutesAsync(routes, userId);
        return Json(new { success = true });
    }

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteRoutes([FromBody] List<long> routeIds)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Unauthorized();

        if (routeIds == null || routeIds.Count == 0)
            return BadRequest("No routes selected.");

        await _routesService.DeleteRoutesAsync(routeIds);
        return Json(new { success = true });
    }

    [HttpPost("save-driver")]
    public async Task<IActionResult> SaveDriver([FromForm] long routeId, [FromForm] long driverId, [FromForm] decimal driverCharges, [FromForm] string routeDate)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        await _routesService.SaveDriverChargesAsync(routeId, driverId, driverCharges);

        TempData["Msg"] = "Driver and charges updated successfully.";
        TempData["MsgClass"] = "alert alert-success";

        return Redirect($"/managedeliveryroutes?search={routeDate}");
    }

    [HttpPost("add-driver")]
    public async Task<IActionResult> AddDriver([FromForm] string name, [FromForm] string email, [FromForm] string routeDate)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
        {
            TempData["Msg"] = "Name and email are required.";
            TempData["MsgClass"] = "alert alert-danger";
            return Redirect($"/managedeliveryroutes?search={routeDate}");
        }

        var error = await _routesService.AddDriverAsync(name, email, long.Parse(webshopId));
        if (error != null)
        {
            TempData["Msg"] = error;
            TempData["MsgClass"] = "alert alert-danger";
        }
        else
        {
            TempData["Msg"] = "Driver details have been saved successfully.";
            TempData["MsgClass"] = "alert alert-success";
        }

        return Redirect($"/managedeliveryroutes?search={routeDate}");
    }

    // ─── Map views redirection ──────────────────────────────────────────────────

    [HttpGet("map-all")]
    public async Task<IActionResult> MapAll([FromQuery] string date, [FromQuery] bool postal)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        if (!DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return BadRequest("Invalid date format. Expected dd/MM/yyyy.");

        var orderIds = new List<long>();
        var defaultConnectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        
        await using (var conn = new SqlConnection(defaultConnectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT o.order_ID FROM tbl_order o 
                        INNER JOIN tbl_ordercollection oc ON o.order_ID = oc.ordercollection_OrderID
                        WHERE o.order_isPurchased = 1 
                          AND CAST(oc.ordercollection_dispatchDate AS date) = @date 
                          AND oc.ordercollection_deliverymode IN (2" + (postal ? ", 4" : "") + ")";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@date", parsedDate.Date);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orderIds.Add(reader.GetInt64(0));
            }
        }

        if (orderIds.Count == 0)
        {
            TempData["Msg"] = "No Order found!";
            TempData["MsgClass"] = "alert alert-warning";
            return Redirect($"/managedeliveryroutes?search={date}");
        }

        var idsStr = string.Join(",", orderIds);
        return Redirect($"/map-orderlocation?orderIds={idsStr}&date={date}");
    }

    [HttpGet("map-route")]
    public async Task<IActionResult> MapRoute([FromQuery] long routeId, [FromQuery] string date)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var orderIds = new List<long>();
        var businessConnectionString = _config.GetConnectionString("BusinessConnection") ?? "";

        await using (var conn = new SqlConnection(businessConnectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT routeOrder_orderID FROM tbl_deliveryRouteOrder WHERE routeOrder_routeID = @routeId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@routeId", routeId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orderIds.Add(reader.GetInt64(0));
            }
        }

        if (orderIds.Count == 0)
        {
            TempData["Msg"] = "No Order found!";
            TempData["MsgClass"] = "alert alert-warning";
            return Redirect($"/managedeliveryroutes?search={date}");
        }

        var idsStr = string.Join(",", orderIds);
        return Redirect($"/map-orderlocation?orderIds={idsStr}&date={date}");
    }

    // ─── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the user has access to a specific module via tbl_moduleAssignment.
    /// </summary>
    private async Task<bool> CheckModuleAccessAsync(int userId, int moduleId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT COUNT(1) FROM tbl_moduleAssignment 
            WHERE moduleAssignment_userID = @userId 
              AND moduleAssignment_moduleID = @moduleId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@moduleId", moduleId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
