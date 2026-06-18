using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Supplier module.
/// Route: /managesupplier
/// Migrated from managesupplier.aspx.
/// Module 7 permission check + HQ-only (webshopId == 82).
/// </summary>
[Route("managesupplier")]
public class ManageSupplierController : Controller
{
    private readonly ManageSupplierService _supplierService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageSupplierController(
        ManageSupplierService supplierService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _supplierService = supplierService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] int pageno = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managesupplier");

        // HQ-only check: webshopId must equal 82 (csbakeryid)
        var csBakeryId = _config["csbakeryid"] ?? "82";
        if (webshopId != csBakeryId)
            return Redirect("/mywebstore");

        // Module 7 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 7);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var pageSize = 10;
        var result = await _supplierService.GetSuppliersAsync(wid, pageno, pageSize, search);

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

        return View("~/Views/ManageSupplier/Index.cshtml");
    }

    // ─── Save (POST) — Add or Edit ────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] long id,
        [FromForm] string supplierName,
        [FromForm] string? addressDetail,
        [FromForm] string? remarks,
        [FromForm] bool isAccessory,
        [FromForm] bool isTopper)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var item = new SupplierItem
        {
            SupplierId = id,
            SupplierName = supplierName,
            Supplier_AddressDetail = addressDetail,
            Supplier_Remarks = remarks,
            Supplier_IsAccessory = isAccessory,
            Supplier_IsTopper = isTopper
        };

        var success = await _supplierService.SaveAsync(item, wid);
        if (!success)
            return Json(new { success = false, message = "Supplier Name already exists." });

        var msg = id == 0
            ? "New Supplier has been added successfully."
            : "Supplier details has been updated successfully.";

        return Json(new { success = true, message = msg });
    }

    // ─── Get By ID (POST) — For Edit Modal ────────────────────────────────────

    [HttpPost("getbyid")]
    public async Task<IActionResult> GetById([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return Json(new { success = false, message = "Supplier not found." });

        return Json(new
        {
            success = true,
            data = new
            {
                supplier.SupplierId,
                supplier.SupplierName,
                supplier.Supplier_AddressDetail,
                supplier.Supplier_Remarks,
                supplier.Supplier_IsAccessory,
                supplier.Supplier_IsTopper
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

        var success = await _supplierService.BulkSetActiveAsync(idList, wid, true);
        return Json(new { success, message = success ? "Suppliers set to active." : "Failed to update." });
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

        var success = await _supplierService.BulkSetActiveAsync(idList, wid, false);
        return Json(new { success, message = success ? "Suppliers set to inactive." : "Failed to update." });
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

        var success = await _supplierService.BulkDeleteAsync(idList, wid);
        return Json(new { success, message = success ? "Suppliers deleted successfully." : "Failed to delete." });
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
