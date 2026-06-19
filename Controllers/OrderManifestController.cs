using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Order Manifest page (read-only).
/// Route: /manageordermenifest
/// Migrated from manageordermenifest.aspx.
/// </summary>
[Route("manageordermenifest")]
[Route("ordermanifest")]
[Route("manageordermenifest.aspx")]
public class OrderManifestController : Controller
{
    private readonly OrderManifestService _manifestService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public OrderManifestController(
        OrderManifestService manifestService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _manifestService = manifestService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? from = null)
    {
        // Auth check from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check: userType 1/2 auto-allowed, else check module 24
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 24);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // If no from param, redirect to today's date
        if (string.IsNullOrEmpty(from))
        {
            var today = DateTime.Today.ToString("dd/MM/yyyy");
            return Redirect($"/manageordermenifest?from={today}");
        }

        // Parse bakery ID and date
        var bakeryId = int.TryParse(webshopId, out var bid) ? bid : 0;
        var dateStr = from;

        // Call service
        var counts = await _manifestService.GetManifestCountsAsync(bakeryId, dateStr);
        var orders = await _manifestService.GetManifestListAsync(bakeryId, dateStr);

        // Set ViewBag for layout (same pattern as StaffRotaController)
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
        ViewBag.Counts = counts;
        ViewBag.Orders = orders;
        ViewBag.SelectedDate = dateStr;
        ViewBag.BakeryId = bakeryId;

        return View();
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
