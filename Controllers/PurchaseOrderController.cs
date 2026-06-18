using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Purchase Order module.
/// Routes: /managepurchaseorder (list), /addpurchaseorder (create/edit)
/// Migrated from managepurchaseorder.aspx / addpurchaseorder.aspx.
/// HQ-only: webshopId must be "82".
/// Module 20 permission check.
/// </summary>
[Route("managepurchaseorder")]
public class PurchaseOrderController : Controller
{
    private readonly PurchaseOrderService _poService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public PurchaseOrderController(
        PurchaseOrderService poService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _poService = poService;
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

        // HQ-only check
        if (webshopId != "82")
            return Redirect("/businessorders");

        // Module 20 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 20);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var page = pageno ?? 1;
        var statusFilter = status ?? -1;
        var searchTerm = search ?? "";

        var result = await _poService.GetPurchaseOrderListAsync(page, 20, searchTerm, statusFilter);

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

        return View("~/Views/PurchaseOrder/Index.cshtml");
    }

    // ─── Remarks (AJAX) ───────────────────────────────────────────────────────

    [HttpGet("remarks/{poId}")]
    public async Task<IActionResult> GetRemarks(long poId)
    {
        var remarks = await _poService.GetRemarksAsync(poId);
        return Json(remarks);
    }

    // ─── Approve (Purchase Dept) ──────────────────────────────────────────────

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _poService.ApprovePOAsync(id, userId, isPurchaseDept: true);
        return Json(new { success, message = success ? "Purchase Order approved successfully." : "Failed to approve." });
    }

    // ─── Approve (Manager/AC) ─────────────────────────────────────────────────

    [HttpPost("managerapprove")]
    public async Task<IActionResult> ManagerApprove([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _poService.ApprovePOAsync(id, userId, isPurchaseDept: false);
        return Json(new { success, message = success ? "Purchase Order approved by manager successfully." : "Failed to approve." });
    }

    // ─── Decline ──────────────────────────────────────────────────────────────

    [HttpPost("decline")]
    public async Task<IActionResult> Decline([FromForm] long id)
    {
        var success = await _poService.DeclinePOAsync(id);
        return Json(new { success, message = success ? "Purchase Order declined successfully." : "Failed to decline." });
    }

    // ─── Send to Supplier ─────────────────────────────────────────────────────

    [HttpPost("sendtosupplier")]
    public async Task<IActionResult> SendToSupplier([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var success = await _poService.SendToSupplierAsync(id, userId);
        return Json(new { success, message = success ? "Purchase order sent to supplier successfully." : "Failed to send." });
    }

    // ─── Save Remark ──────────────────────────────────────────────────────────

    [HttpPost("saveremark")]
    public async Task<IActionResult> SaveRemark([FromForm] long poId, [FromForm] string name, [FromForm] string message)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var remark = await _poService.SaveRemarkAsync(poId, userId, name, message);
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

// ─── Add/Edit PO Controller ────────────────────────────────────────────────────

/// <summary>
/// Controller for Add/Edit Purchase Order page.
/// Route: /addpurchaseorder
/// </summary>
[Route("addpurchaseorder")]
public class AddPurchaseOrderController : Controller
{
    private readonly PurchaseOrderService _poService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public AddPurchaseOrderController(
        PurchaseOrderService poService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _poService = poService;
        _menuService = menuService;
        _config = config;
    }

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

        // HQ-only check
        if (webshopId != "82")
            return Redirect("/businessorders");

        // Only userType 1/2 can create/edit
        if (userType != "1" && userType != "2")
            return Redirect("/managepurchaseorder");

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

        PODetailModel? poDetail = null;
        SupplierDetailModel? supplier = null;

        if (id.HasValue && id.Value > 0)
        {
            poDetail = await _poService.GetPODetailAsync(id.Value);
            if (poDetail != null && poDetail.PO_SupplierID > 0)
                supplier = await _poService.GetSupplierDetailAsync(poDetail.PO_SupplierID);
        }

        var invoiceNo = poDetail?.PO_SysNo ?? await _poService.GenerateInvoiceNumberAsync();

        ViewBag.PODetail = poDetail;
        ViewBag.Supplier = supplier;
        ViewBag.InvoiceNo = invoiceNo;
        ViewBag.IsEdit = (id.HasValue && id.Value > 0);

        return View("~/Views/PurchaseOrder/Add.cshtml");
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Json(new List<SupplierListItem>());

        var suppliers = await _poService.GetSupplierListAsync(keyword);
        return Json(suppliers);
    }

    [HttpGet("supplierdetail/{id}")]
    public async Task<IActionResult> SupplierDetail(long id)
    {
        var supplier = await _poService.GetSupplierDetailAsync(id);
        if (supplier == null)
            return Json(new { success = false });
        return Json(supplier);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] POSaveModel model)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || webshopId != "82")
            return Json(new { success = false, message = "Unauthorized" });

        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Unauthorized" });

        if (model == null || model.lstPOdet == null || model.lstPOdet.Count == 0)
            return Json(new { success = false, message = "No line items provided." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var result = await _poService.SavePurchaseOrderAsync(model, userId, wid);
        return Json(new { success = result == "1", message = result == "1" ? "Purchase Order saved successfully." : "Failed to save." });
    }
}
