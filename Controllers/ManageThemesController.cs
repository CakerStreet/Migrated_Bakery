using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Party Themes module.
/// Route: /managethemes
/// Migrated from managethemes.aspx.
/// Module 10 permission check.
/// </summary>
[Route("managethemes")]
public class ManageThemesController : Controller
{
    private readonly ManageThemesService _themesService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageThemesController(
        ManageThemesService themesService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _themesService = themesService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] int filterstatus = 0,
        [FromQuery] int pageno = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managethemes");

        // Module 10 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 10);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var pageSize = 23;
        var result = await _themesService.GetThemesAsync(wid, search, filterstatus, pageno, pageSize);

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

        ViewBag.Items = result.Items;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.TotalCount = result.TotalCount;
        ViewBag.CurrentPage = pageno;
        ViewBag.Search = search ?? "";
        ViewBag.FilterStatus = filterstatus;
        ViewBag.UserType = userType;

        return View("~/Views/ManageThemes/Index.cshtml");
    }

    // ─── Update Single Theme (POST) ───────────────────────────────────────────

    [HttpPost("update")]
    public async Task<IActionResult> Update(
        [FromForm] long id,
        [FromForm] string title,
        [FromForm] bool isPopular)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var success = await _themesService.UpdateThemeAsync(id, title, isPopular, wid);
        return Json(new { success, message = success ? "Theme updated successfully." : "Failed to update theme." });
    }

    // ─── Bulk Update (POST) — Save all checked rows ───────────────────────────

    [HttpPost("bulkupdate")]
    public async Task<IActionResult> BulkUpdate([FromBody] List<BulkThemeUpdateItem> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        if (items == null || items.Count == 0)
            return Json(new { success = false, message = "No items to update." });

        var allSuccess = true;
        foreach (var item in items)
        {
            var ok = await _themesService.UpdateThemeAsync(item.Id, item.Title, item.IsPopular, wid);
            if (!ok) allSuccess = false;
        }

        return Json(new { success = allSuccess, message = allSuccess ? "Records saved successfully." : "Some records failed to update." });
    }

    // ─── Bulk Set Active (POST) ───────────────────────────────────────────────

    [HttpPost("bulkactive")]
    public async Task<IActionResult> BulkActive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _themesService.BulkSetActiveAsync(idList, wid, true);
        return Json(new { success, message = success ? "Themes set to active." : "Failed to update." });
    }

    // ─── Bulk Set Inactive (POST) ─────────────────────────────────────────────

    [HttpPost("bulkinactive")]
    public async Task<IActionResult> BulkInactive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _themesService.BulkSetActiveAsync(idList, wid, false);
        return Json(new { success, message = success ? "Themes set to inactive." : "Failed to update." });
    }

    // ─── Bulk Delete (POST) ───────────────────────────────────────────────────

    [HttpPost("bulkdelete")]
    public async Task<IActionResult> BulkDelete([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _themesService.BulkDeleteAsync(idList, wid);
        return Json(new { success, message = success ? "Themes removed successfully." : "Failed to remove." });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static List<long> ParseIds(string ids)
    {
        var result = new List<long>();
        if (string.IsNullOrEmpty(ids)) return result;

        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part.Trim(), out var id))
                result.Add(id);
        }
        return result;
    }

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

// ─── Bulk Update DTO ──────────────────────────────────────────────────────────

public class BulkThemeUpdateItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsPopular { get; set; }
}
