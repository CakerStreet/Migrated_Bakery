using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Supply Order Item Received page.
/// Route: /managesupplyorderitemreceived
/// Migrated from managesupplyorderitemreceived.aspx.
/// No HQ-only restriction. Module 21 permission check.
/// Uses suppliers.* schema tables.
/// </summary>
[Route("managesupplyorderitemreceived")]
public class SupplyOrderItemReceivedController : Controller
{
    private readonly SupplyOrderService _soService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public SupplyOrderItemReceivedController(
        SupplyOrderService soService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _soService = soService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Main Page ─────────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(long? id)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        // Module 21 permission check (no HQ-only restriction)
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 21);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        if (!id.HasValue || id.Value <= 0)
            return Redirect("/managesupplyorder");

        // Get SO detail (validates status == 3)
        var soDetail = await _soService.GetSOForItemReceivedAsync(id.Value);
        if (soDetail == null)
            return Redirect("/managesupplyorder");

        // Get staff list for Received By dropdown
        var staffList = await _soService.GetStaffListAsync();

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

        ViewBag.SODetail = soDetail;
        ViewBag.StaffList = staffList;

        return View("~/Views/SupplyOrderItemReceived/Index.cshtml");
    }

    // ─── AJAX: Save Item Received ──────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SupplyItemReceivedSaveModel model)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (model == null || model.Items == null || model.Items.Count == 0)
            return Json(new { success = false, message = "No product(s) to save." });

        if (model.PO_ID <= 0)
            return Json(new { success = false, message = "Invalid Supply Order." });

        var resultId = await _soService.SaveItemReceivedAsync(model, userId);
        if (resultId > 0)
            return Json(new { success = true, message = "Supply Order Item(s) have been saved successfully.", id = resultId });

        return Json(new { success = false, message = "Failed to save item received." });
    }

    // ─── Module Access Check ──────────────────────────────────────────────────

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
