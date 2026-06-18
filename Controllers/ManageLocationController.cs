using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Location module.
/// Route: /managelocation
/// Migrated from managelocation.aspx.
/// Module 7 permission check.
/// Supports hierarchical navigation with breadcrumb, recursive delete, and level-based display.
/// </summary>
[Route("managelocation")]
public class ManageLocationController : Controller
{
    private readonly ManageLocationService _locationService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageLocationController(
        ManageLocationService locationService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _locationService = locationService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] long? id,
        [FromQuery] string? search,
        [FromQuery] int pageno = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managelocation");

        // Module 7 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 7);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var parentId = id ?? 0;
        var pageSize = 10;

        // Get breadcrumb and level
        var breadcrumb = await _locationService.GetBreadcrumbAsync(parentId);
        var level = breadcrumb.Count > 0 ? breadcrumb[0].MaxLevel : 0;

        // Get locations for this parent
        var result = await _locationService.GetLocationsAsync(wid, parentId, search, pageno, pageSize);

        // Get current location title for header
        string currentTitle = "";
        if (parentId > 0 && breadcrumb.Count > 0)
        {
            // Last item in breadcrumb (ordered root→current) is the current location
            currentTitle = breadcrumb[breadcrumb.Count - 1].LocationTitle;
        }

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
        ViewBag.UserType = userType;
        ViewBag.ParentId = parentId;
        ViewBag.Breadcrumb = breadcrumb;
        ViewBag.Level = level;
        ViewBag.CurrentTitle = currentTitle;

        return View("~/Views/ManageLocation/Index.cshtml");
    }

    // ─── Save (POST) — Add or Edit ────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] long id,
        [FromForm] string title,
        [FromForm] int displayOrder,
        [FromForm] long parentId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var item = new LocationItem
        {
            LocationID = id,
            LocationTitle = title,
            DisplayOrder = displayOrder
        };

        var success = await _locationService.SaveAsync(item, wid, parentId);
        if (!success)
            return Json(new { success = false, message = "Location Title already exists." });

        var msg = id == 0
            ? "New Location has been added successfully."
            : "Location details has been updated successfully.";

        return Json(new { success = true, message = msg });
    }

    // ─── Get By ID (POST) — For Edit Modal ────────────────────────────────────

    [HttpPost("getbyid")]
    public async Task<IActionResult> GetById([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var location = await _locationService.GetByIdAsync(id);
        if (location == null)
            return Json(new { success = false, message = "Location not found." });

        return Json(new
        {
            success = true,
            data = new
            {
                location.LocationID,
                location.LocationTitle,
                location.DisplayOrder
            }
        });
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

        var success = await _locationService.BulkSetActiveAsync(idList, wid, true);
        return Json(new { success, message = success ? "Locations set to active." : "Failed to update." });
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

        var success = await _locationService.BulkSetActiveAsync(idList, wid, false);
        return Json(new { success, message = success ? "Locations set to inactive." : "Failed to update." });
    }

    // ─── Bulk Delete (POST) — Recursive soft-delete ───────────────────────────

    [HttpPost("bulkdelete")]
    public async Task<IActionResult> BulkDelete([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        // Recursive delete each selected location and all its descendants
        var allSuccess = true;
        foreach (var locationId in idList)
        {
            var success = await _locationService.RecursiveDeleteAsync(locationId);
            if (!success) allSuccess = false;
        }

        return Json(new { success = allSuccess, message = allSuccess ? "Locations deleted successfully." : "Failed to delete some locations." });
    }

    // ─── Update Display Order (POST) ──────────────────────────────────────────

    [HttpPost("updateorder")]
    public async Task<IActionResult> UpdateOrder([FromForm] string orderData)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (string.IsNullOrEmpty(orderData))
            return Json(new { success = false, message = "No items selected." });

        // orderData format: "id:order,id:order,..."
        var allSuccess = true;
        foreach (var pair in orderData.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length == 2 && long.TryParse(parts[0].Trim(), out var locId) && int.TryParse(parts[1].Trim(), out var order))
            {
                var success = await _locationService.UpdateDisplayOrderAsync(locId, order);
                if (!success) allSuccess = false;
            }
        }

        return Json(new { success = allSuccess, message = allSuccess ? "Display order updated successfully." : "Failed to update some records." });
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
