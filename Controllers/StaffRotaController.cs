using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;
using System.Globalization;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Staff Rota page.
/// Route: /staffrota
/// Migrated from staffRota.aspx.
/// </summary>
[Route("staffrota")]
public class StaffRotaController : Controller
{
    private readonly StaffRotaService _rotaService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public StaffRotaController(
        StaffRotaService rotaService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _rotaService = rotaService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? startDate = null, string? edit = null)
    {
        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var staffType = HttpContext.Items["BakeryUserStaffType"]?.ToString() ?? "";

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

        // Determine start date
        DateTime dt = DateTime.Today;
        if (!string.IsNullOrEmpty(startDate))
        {
            if (DateTime.TryParseExact(startDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                dt = parsed;
            else if (DateTime.TryParse(startDate, out var parsed2))
                dt = parsed2;
        }

        // Determine edit mode and auth
        bool isEditMode = edit != null;
        // Legacy: userType != 3 || staffType != 1 means auth user (admin/manager)
        bool isAuthUser = userType != "3" || staffType != "1";

        // Call service
        var model = await _rotaService.GetRotaAsync(long.Parse(webshopId), dt, userId);
        model.IsEditMode = isEditMode;
        model.IsAuthUser = isAuthUser;

        return View(model);
    }

    [HttpPost("submitrequest")]
    public async Task<IActionResult> SubmitRequest(
        [FromForm] long staffId, [FromForm] string requestDate,
        [FromForm] int availability, [FromForm] int fromTime,
        [FromForm] int toTime, [FromForm] string remarks)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0) return Json(new { success = false, message = "Not authenticated" });

        DateTime dt;
        if (!DateTime.TryParseExact(requestDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            if (!DateTime.TryParse(requestDate, out dt))
                return Json(new { success = false, message = "Invalid date" });
        }

        var result = await _rotaService.SaveStaffAvailabilityRequestAsync(
            staffId, userId, dt, availability, fromTime, toTime, remarks);

        if (result)
            return Json(new { success = true });
        else
            return Json(new { success = false, message = "Request already exists or date is in the past" });
    }
}
