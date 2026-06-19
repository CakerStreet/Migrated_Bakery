using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Staff Requests page.
/// Route: /managestafftimingrequest
/// Migrated from managestafftimingrequest.aspx.
/// </summary>
[Route("managestafftimingrequest")]
[Route("staffrequests")]
[Route("managestafftimingrequest.aspx")]
[Route("managestafftimirequest.aspx")]
public class StaffRequestsController : Controller
{
    private readonly StaffRequestsService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public StaffRequestsController(
        StaffRequestsService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    /// <summary>
    /// GET /managestafftimingrequest
    /// Displays paginated staff requests with filter options.
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int filterstatus = 0, string? sdate = null, string? edate = null, int pageno = 1)
    {
        // Permission check: redirect staff (type=3, stafftype=1) to /dashboard
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userType == "3")
        {
            var staffType = await GetStaffTypeAsync(userId);
            if (staffType == 1)
            {
                return Redirect("/dashboard");
            }
        }

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

        // Parse dates — default: today to today+1month
        DateTime startDate, endDate;
        if (!string.IsNullOrEmpty(sdate) && DateTime.TryParse(sdate, out var parsedStart))
            startDate = parsedStart.Date;
        else
            startDate = DateTime.Today;

        if (!string.IsNullOrEmpty(edate) && DateTime.TryParse(edate, out var parsedEnd))
            endDate = parsedEnd.Date;
        else
            endDate = DateTime.Today.AddMonths(1);

        // Get data
        var model = await _service.GetRequestsAsync(filterstatus, startDate, endDate, pageno, 50);

        // Check for message from TempData
        if (TempData["Message"] is string msg)
        {
            model.Message = msg;
            model.MessageClass = TempData["MessageClass"] as string ?? "alert-success";
        }

        // If no requests found, show message
        if (model.Requests.Count == 0 && model.Message == null)
        {
            model.Message = "No Request Found";
            model.MessageClass = "alert-danger";
        }

        return View(model);
    }

    /// <summary>
    /// POST /managestafftimingrequest/approve
    /// Approves a pending staff request.
    /// </summary>
    [HttpPost("approve")]
    public async Task<IActionResult> Approve(long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        await _service.ApproveRequestAsync(id, userId);

        TempData["Message"] = "Staff details has been updated successfully.";
        TempData["MessageClass"] = "alert-success";

        // Preserve current filter state
        return Redirect(GetReturnUrl());
    }

    /// <summary>
    /// POST /managestafftimingrequest/decline
    /// Declines a pending staff request with remarks.
    /// </summary>
    [HttpPost("decline")]
    public async Task<IActionResult> Decline(long id, string remarks)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        await _service.DeclineRequestAsync(id, userId, remarks ?? "");

        TempData["Message"] = "Staff details has been updated successfully.";
        TempData["MessageClass"] = "alert-success";

        return Redirect(GetReturnUrl());
    }

    /// <summary>
    /// Builds the return URL preserving current filter/page state from the Referer header.
    /// </summary>
    private string GetReturnUrl()
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && referer.Contains("managestafftimingrequest"))
        {
            var uri = new Uri(referer);
            return uri.PathAndQuery;
        }
        return "/managestafftimingrequest";
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
