using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Supply Order module.
/// Route: /managesupplyorder (list only — no create/edit page)
/// Migrated from managesupplyorder.aspx.
/// No HQ-only restriction (any bakery can have supply orders).
/// Module 21 permission check.
/// </summary>
[Route("managesupplyorder")]
[Route("supplyorder")]
[Route("managesupplyorder.aspx")]
public class SupplyOrderController : Controller
{
    private readonly SupplyOrderService _soService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public SupplyOrderController(
        SupplyOrderService soService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _soService = soService;
        _menuService = menuService;
        _config = config;
    }

    // ─── List Page ─────────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(int? status, string? search, int? pageno)
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

        var page = pageno ?? 1;
        var statusFilter = status ?? -1;
        var searchTerm = search ?? "";

        var result = await _soService.GetSupplyOrderListAsync(page, 20, searchTerm, statusFilter);

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

        ViewBag.Result = result;
        ViewBag.CurrentPage = page;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.UserType = userType;

        return View("~/Views/SupplyOrder/Index.cshtml");
    }

    // ─── Remarks (AJAX) ───────────────────────────────────────────────────────

    [HttpGet("remarks/{poId}")]
    public async Task<IActionResult> GetRemarks(long poId)
    {
        var remarks = await _soService.GetRemarksAsync(poId);
        return Json(remarks);
    }

    // ─── Approve (Purchase Dept) ──────────────────────────────────────────────

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _soService.ApprovePOAsync(id, userId, isPurchaseDept: true);
        return Json(new { success, message = success ? "Supply Order approved successfully." : "Failed to approve." });
    }

    // ─── Approve (Manager/AC) ─────────────────────────────────────────────────

    [HttpPost("managerapprove")]
    public async Task<IActionResult> ManagerApprove([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _soService.ApprovePOAsync(id, userId, isPurchaseDept: false);
        return Json(new { success, message = success ? "Supply Order approved by manager successfully." : "Failed to approve." });
    }

    // ─── Decline ──────────────────────────────────────────────────────────────

    [HttpPost("decline")]
    public async Task<IActionResult> Decline([FromForm] long id)
    {
        var success = await _soService.DeclinePOAsync(id);
        return Json(new { success, message = success ? "Supply Order declined successfully." : "Failed to decline." });
    }

    // ─── Send to Unit ─────────────────────────────────────────────────────────

    [HttpPost("sendtounit")]
    public async Task<IActionResult> SendToUnit([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _soService.SendToUnitAsync(id, userId);
        return Json(new { success, message = success ? "Supply order sent to unit successfully." : "Failed to send." });
    }

    // ─── Save Remark ──────────────────────────────────────────────────────────

    [HttpPost("saveremark")]
    public async Task<IActionResult> SaveRemark([FromForm] long poId, [FromForm] string name, [FromForm] string message)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var remark = await _soService.SaveRemarkAsync(poId, userId, name, message);
        if (remark != null)
            return Json(new { success = true, remark });
        return Json(new { success = false });
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
