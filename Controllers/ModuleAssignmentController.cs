using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Module Assignment management page.
/// Route: /managemoduleassignment
/// Migrated from manageModuleAssignment.aspx.
/// Admin-only (userType 1).
/// </summary>
[Route("managemoduleassignment")]
public class ModuleAssignmentController : Controller
{
    private readonly ModuleAssignmentService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ModuleAssignmentController(
        ModuleAssignmentService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int? typeid,
        [FromQuery] int? id)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userTypeStr = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // Auth check
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managemoduleassignment");

        // Admin-only (userType 1)
        var loggedInUserType = int.TryParse(userTypeStr, out var ut) ? ut : 0;
        if (loggedInUserType != 1)
            return Redirect("/businessorders");

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        // Load staff and roles for dropdowns
        var staffList = await _service.GetStaffListAsync(wid);
        var roleList = await _service.GetRolesAsync();

        // Load modules with assignments if a selection is made
        List<ModuleItem> modules = new();
        if (typeid.HasValue && id.HasValue && id.Value > 0)
        {
            int? staffId = typeid == 1 ? id : null;
            int? roleId = typeid == 2 ? id : null;
            modules = await _service.GetModulesWithAssignmentsAsync(wid, staffId, roleId);
        }

        // Menu visibility
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userTypeStr, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        // Page data
        ViewBag.TypeId = typeid ?? 0;
        ViewBag.SelectedId = id ?? 0;
        ViewBag.StaffList = staffList;
        ViewBag.RoleList = roleList;
        ViewBag.Modules = modules;

        return View("~/Views/ModuleAssignment/Index.cshtml");
    }

    // ─── Save (POST) ──────────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveModuleAssignmentRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userTypeStr = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // Auth check
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        // Admin-only
        var loggedInUserType = int.TryParse(userTypeStr, out var ut) ? ut : 0;
        if (loggedInUserType != 1)
            return Json(new { success = false, message = "Access denied. Admin only." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        int? staffId = request.TypeId == 1 ? request.Id : null;
        int? roleId = request.TypeId == 2 ? request.Id : null;

        var success = await _service.SaveAssignmentsAsync(
            wid, staffId, roleId, request.ModuleIds ?? new List<int>(), userId);

        return Json(new
        {
            success,
            message = success ? "Record(s) saved successfully." : "Failed to save assignments."
        });
    }
}

// ─── Request Model ─────────────────────────────────────────────────────────────

public class SaveModuleAssignmentRequest
{
    public int TypeId { get; set; }
    public int Id { get; set; }
    public List<int>? ModuleIds { get; set; }
}
