using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for Baker Work Time (Manage Users Timeline) module.
/// Route: /bakerworktime
/// Migrated from bakerworktime.aspx.
/// Module ID: 8 — requires module access check.
/// READ-ONLY — timing mutations deferred to Phase 2.
/// </summary>
[Route("bakerworktime")]
public class BakerWorkTimeController : Controller
{
    private readonly BakerWorkTimeService _workTimeService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakerWorkTimeController(
        BakerWorkTimeService workTimeService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _workTimeService = workTimeService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int? staffid,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect("/businesslogin?returl=/bakerworktime");

        // Module 8 access check (admin types 1,2 bypass)
        if (userType != "1" && userType != "2")
        {
            var moduleAccess = HttpContext.Items["BakeryModuleAccess"] as string ?? "";
            if (!moduleAccess.Contains(",8,") && !moduleAccess.StartsWith("8,") && !moduleAccess.EndsWith(",8") && moduleAccess != "8")
                return Redirect("/businessorders");
        }

        // Parse date range — default: last 7 days
        DateTime fromDate;
        DateTime toDate;

        if (!DateTime.TryParse(from, out fromDate))
            fromDate = DateTime.Today.AddDays(-7);

        if (!DateTime.TryParse(to, out toDate))
            toDate = DateTime.Today;

        // Get staff dropdown
        var staffList = await _workTimeService.GetDeliveryStaffAsync();

        // Get work time data if staff selected
        List<WorkTimeDayGroup> workTimeGroups = new();
        if (staffid.HasValue && staffid.Value > 0)
        {
            workTimeGroups = await _workTimeService.GetWorkTimeAsync(staffid.Value, fromDate, toDate);
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

        ViewBag.StaffList = staffList;
        ViewBag.SelectedStaffId = staffid ?? 0;
        ViewBag.FromDate = fromDate.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.ToString("yyyy-MM-dd");
        ViewBag.WorkTimeGroups = workTimeGroups;

        return View("~/Views/BakerWorkTime/Index.cshtml");
    }
}
