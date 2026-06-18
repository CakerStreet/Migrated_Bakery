using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Social Links module.
/// Route: /managesociallinks
/// Migrated from managesociallinks.aspx.
/// Module 22 permission check.
/// </summary>
[Route("managesociallinks")]
public class SocialLinksController : Controller
{
    private readonly SocialLinksService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public SocialLinksController(
        SocialLinksService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managesociallinks");

        // Module 22 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 22);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        if (wid == 0)
            return Redirect("/editbusinessinfo");

        var items = await _service.GetSocialLinksAsync(wid);

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
        ViewBag.Items = items;
        ViewBag.UserType = userType;

        return View("~/Views/SocialLinks/Index.cshtml");
    }

    // ─── Save All (POST) ──────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] List<SocialLinkSaveModel> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        if (wid == 0)
            return Json(new { success = false, message = "No webstore found." });

        if (items == null || items.Count == 0)
            return Json(new { success = false, message = "No data provided." });

        var success = await _service.SaveAllAsync(items, wid);
        return Json(new { success, message = success ? "Your changes have been saved successfully." : "Failed to save social links." });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

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
