using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Order Sponge page (Phase 1 — read-only).
/// Route: /orderspongelist
/// Migrated from orderspongelist.aspx.
/// </summary>
[Route("orderspongelist")]
[Route("ordersponge")]
public class OrderSpongeController : Controller
{
    private readonly OrderSpongeService _spongeService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public OrderSpongeController(
        OrderSpongeService spongeService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _spongeService = spongeService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? from = null, string? to = null, int inc = 0)
    {
        // Auth check from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check: userType 1/2 auto-allowed, else check module 15
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 15);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // Set ViewBag for layout (same pattern as BusinessOrdersController)
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        // If no 'from' param, show empty page with just the filter form
        if (string.IsNullOrEmpty(from))
        {
            ViewBag.SubmitEnabled = _config.GetValue<bool>("Mutations:OrderSpongeSubmit:Enabled", false);
            return View(new Models.OrderSpongeViewModel());
        }

        // Call service to get sponge grid data
        var bakeryId = long.Parse(webshopId);
        var includeRequested = inc == 1;
        var toDate = string.IsNullOrEmpty(to) ? from : to;

        var model = await _spongeService.GetSpongeGridAsync(bakeryId, from, toDate, includeRequested);

        ViewBag.SubmitEnabled = _config.GetValue<bool>("Mutations:OrderSpongeSubmit:Enabled", false);
        return View(model);
    }

    [HttpGet("/managespongeorderlist")]
    public async Task<IActionResult> OrderHistory()
    {
        // Auth check
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

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

        // Get sponge order history
        var orders = await _spongeService.GetOrderHistoryAsync(long.Parse(webshopId));
        return View("OrderHistory", orders);
    }

    /// <summary>
    /// Phase 2B-1: Submit sponge order (records only, no shape propagation).
    /// Feature-flagged: Mutations:OrderSpongeSubmit:Enabled must be true.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] Models.SpongeSubmitRequest request)
    {
        // Feature flag check
        var submitEnabled = _config.GetValue<bool>("Mutations:OrderSpongeSubmit:Enabled", false);
        if (!submitEnabled)
            return StatusCode(403, new { success = false, error = "Sponge order submission is not enabled" });

        // Auth check
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var result = await _spongeService.SubmitSpongeOrderAsync(long.Parse(webshopId), userId, request);

        if (result.Success)
            return Json(new { success = true, spongeOrderId = result.SpongeOrderId });

        return StatusCode(400, new { success = false, error = result.Error });
    }

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
