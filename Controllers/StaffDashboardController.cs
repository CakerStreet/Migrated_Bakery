using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Staff Dashboard page (Phase 1 — read-only).
/// Route: /dashboard
/// Migrated from staffDashboard.aspx.
/// </summary>
[Route("dashboard")]
[Route("staffdashboard")]
[Route("staffDashboard.aspx")]
public class StaffDashboardController : Controller
{
    private readonly StaffDashboardService _dashboardService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public StaffDashboardController(
        StaffDashboardService dashboardService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _dashboardService = dashboardService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long staffID = 0)
    {
        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check: type=1/2 (admin/manager) → full access with dropdown
        // type=3 stafftype=1 → own dashboard only
        bool isAdmin = userType == "1" || userType == "2";
        bool isStaff = false;

        if (!isAdmin && userType == "3")
        {
            var staffType = await GetStaffTypeAsync(userId);
            isStaff = staffType == 1;
        }

        if (!isAdmin && !isStaff)
            return Redirect("/businessorders");

        // Determine which staff to show
        long effectiveStaffId = staffID;
        if (!isAdmin)
        {
            // Staff can only view their own dashboard
            effectiveStaffId = userId;
        }
        else if (effectiveStaffId == 0)
        {
            // Admin with no staffID selected — show prompt
            effectiveStaffId = 0;
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

        // Call service
        var now = DateTime.Now;
        var model = await _dashboardService.GetDashboardAsync(
            long.Parse(webshopId), effectiveStaffId, now.Month, now.Year);

        model.IsAdminView = isAdmin;

        return View(model);
    }

    /// <summary>
    /// Saves working hours for a staff member (7 days).
    /// Source: legacy BtnSave_editstore click → AJAX POST to webservices.aspx/AddUpdate_staffTimings
    /// </summary>
    [HttpPost("save-working-hours")]
    public async Task<IActionResult> SaveWorkingHours([FromBody] SaveWorkingHoursRequest request)
    {
        // Extract auth from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // Unauthenticated check
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check
        bool isAdmin = userType == "1" || userType == "2";

        if (!isAdmin)
        {
            // Staff user (type 3, stafftype 1) can only target their own ID
            if (userType == "3")
            {
                var staffType = await GetStaffTypeAsync(userId);
                if (staffType != 1)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });

                if (request.StaffId != userId)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
            else
            {
                return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
        }

        // Call service
        var result = await _dashboardService.SaveWorkingHoursAsync(request.StaffId, request.Entries);

        if (result.Success)
            return Json(new { success = true });

        // Determine HTTP status based on error type
        var statusCode = result.ErrorMessage == "Save failed. Please try again." ? 500 : 400;
        return StatusCode(statusCode, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Submits a leave request for a staff member.
    /// Source: legacy btnSaveNewRequest_Click with OpenNewRequestModal("3")
    /// </summary>
    [HttpPost("submit-leave-request")]
    public async Task<IActionResult> SubmitLeaveRequest([FromBody] SubmitLeaveRequestPayload request)
    {
        // Extract auth from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // Unauthenticated check
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check
        bool isAdmin = userType == "1" || userType == "2";

        if (!isAdmin)
        {
            // Staff user (type 3, stafftype 1) can only target their own ID
            if (userType == "3")
            {
                var staffType = await GetStaffTypeAsync(userId);
                if (staffType != 1)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });

                if (request.StaffId != userId)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
            else
            {
                return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
        }

        // Parse dates from dd/MM/yyyy format
        DateTime fromDate;
        DateTime toDate;
        try
        {
            fromDate = DateTime.ParseExact(request.FromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            toDate = DateTime.ParseExact(request.ToDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return StatusCode(400, new { success = false, error = "Invalid date format. Use dd/MM/yyyy." });
        }

        // Call service
        var result = await _dashboardService.SubmitLeaveRequestAsync(request.StaffId, fromDate, toDate, request.Remarks);

        if (result.Success)
            return Json(new { success = true });

        // Determine HTTP status based on error type
        var statusCode = result.ErrorMessage == "Save failed. Please try again." ? 500 : 400;
        return StatusCode(statusCode, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Submits a special availability entry for a staff member.
    /// Source: legacy btnSaveNewRequest_Click with OpenNewRequestModal("2")
    /// </summary>
    [HttpPost("submit-special-availability")]
    public async Task<IActionResult> SubmitSpecialAvailability([FromBody] SubmitSpecialAvailabilityPayload request)
    {
        // Extract auth from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // Unauthenticated check
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Permission check
        bool isAdmin = userType == "1" || userType == "2";

        if (!isAdmin)
        {
            // Staff user (type 3, stafftype 1) can only target their own ID
            if (userType == "3")
            {
                var staffType = await GetStaffTypeAsync(userId);
                if (staffType != 1)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });

                if (request.StaffId != userId)
                    return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
            else
            {
                return StatusCode(403, new { success = false, error = "Insufficient permissions" });
            }
        }

        // Parse date from dd/MM/yyyy format
        DateTime date;
        try
        {
            date = DateTime.ParseExact(request.Date, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return StatusCode(400, new { success = false, error = "Invalid date format. Use dd/MM/yyyy." });
        }

        // Call service
        var result = await _dashboardService.SubmitSpecialAvailabilityAsync(request.StaffId, date, request.FromHour, request.ToHour);

        if (result.Success)
            return Json(new { success = true });

        // Determine HTTP status based on error type
        var statusCode = result.ErrorMessage == "Save failed. Please try again." ? 500 : 400;
        return StatusCode(statusCode, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Deletes a special day entry (pending only).
    /// Source: legacy rpSpecialDays_ItemCommand with "DeleteSP".
    /// </summary>
    [HttpPost("delete-special-day")]
    public async Task<IActionResult> DeleteSpecialDay([FromBody] DeleteSpecialDayPayload request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        bool isAdmin = userType == "1" || userType == "2";
        if (!isAdmin && userType == "3")
        {
            var staffType = await GetStaffTypeAsync(userId);
            if (staffType != 1 || request.StaffId != userId)
                return StatusCode(403, new { success = false, error = "Insufficient permissions" });
        }
        else if (!isAdmin)
        {
            return StatusCode(403, new { success = false, error = "Insufficient permissions" });
        }

        var result = await _dashboardService.DeleteSpecialDayAsync(request.Id, request.StaffId);
        if (result.Success)
            return Json(new { success = true });
        return StatusCode(400, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Approves a special day entry (admin only).
    /// Source: legacy lnkApprove_Click.
    /// </summary>
    [HttpPost("approve-special-day")]
    public async Task<IActionResult> ApproveSpecialDay([FromBody] ApproveSpecialDayPayload request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        bool isAdmin = userType == "1" || userType == "2";
        if (!isAdmin)
            return StatusCode(403, new { success = false, error = "Admin access required" });

        var result = await _dashboardService.ApproveSpecialDayAsync(request.Id, request.StaffId);
        if (result.Success)
            return Json(new { success = true });
        return StatusCode(400, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Declines a special day entry with remarks (admin only).
    /// Source: legacy btnSave_Click (decline modal).
    /// </summary>
    [HttpPost("decline-special-day")]
    public async Task<IActionResult> DeclineSpecialDay([FromBody] DeclineSpecialDayPayload request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        bool isAdmin = userType == "1" || userType == "2";
        if (!isAdmin)
            return StatusCode(403, new { success = false, error = "Admin access required" });

        if (string.IsNullOrWhiteSpace(request.Remarks))
            return StatusCode(400, new { success = false, error = "Remarks are required for declining." });

        var result = await _dashboardService.DeclineSpecialDayAsync(request.Id, userId, request.Remarks);
        if (result.Success)
            return Json(new { success = true });
        return StatusCode(400, new { success = false, error = result.ErrorMessage });
    }

    /// <summary>
    /// Gets the staff type (customer_stafftype) for a user from the database.
    /// Source: clsglobaltext.getBakeryUser_StaffType()
    /// </summary>
    private async Task<int> GetStaffTypeAsync(int userId)
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT ISNULL(customer_stafftype, 0) FROM tbl_bakeryuser WHERE customer_ID = @id", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }
}
