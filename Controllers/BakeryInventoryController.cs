using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Bakery Inventory module.
/// Route: /mywebstore
/// Migrated from manageinventory.aspx.
/// Module 4 (bakery types 0,3,6) / Module 5 (stock types) permission check.
/// </summary>
[Route("mywebstore")]
[Route("manageinventory")]
[Route("manageinventory.aspx")]
[Route("bakeryinventory")]
public class BakeryInventoryController : Controller
{
    private readonly BakeryInventoryService _inventoryService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakeryInventoryController(
        BakeryInventoryService inventoryService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _inventoryService = inventoryService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int prdtype = 1,
        [FromQuery] int pageno = 1,
        [FromQuery] string? search = null,
        [FromQuery] int filterstatus = -1,
        [FromQuery] long catid = 0,
        [FromQuery] long templateid = 0,
        [FromQuery] string? sort = null,
        [FromQuery] bool showcutteritems = false,
        [FromQuery] int typeid = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/mywebstore");

        // Module access check: Module 4 for bakery types (0, 3, 6), Module 5 for stock types
        if (userType != "1" && userType != "2")
        {
            int moduleId = (prdtype == 0 || prdtype == 6 || prdtype == 3) ? 4 : 5;
            var hasAccess = await CheckModuleAccessAsync(userId, moduleId);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var csBakeryId = _config["CsBakeryId"] ?? "82";
        var isCsBakery = webshopId == csBakeryId;

        // Determine page size: 50 for HQ
        int pageSize = isCsBakery ? 50 : 23;

        var filters = new InventoryFilterParams
        {
            WebstoreId = wid,
            Page = pageno,
            PageSize = pageSize,
            Search = search,
            ProductType = prdtype,
            StatusFilter = filterstatus,
            CategoryId = catid,
            TemplateId = templateid,
            Sort = sort,
            HasCutters = showcutteritems,
            IsCsBakery = isCsBakery,
            TypeId = typeid
        };

        var products = await _inventoryService.GetProductsAsync(filters);
        var categories = await _inventoryService.GetCategoriesAsync(wid, prdtype);
        var templates = (prdtype == 1 || prdtype == 6)
            ? await _inventoryService.GetTemplatesAsync(wid, prdtype)
            : new List<TemplateItem>();

        // Determine bakery vs stock tab set
        // Bakery types: 1 (Cakes), 3 (Other Baking), 6 (Cupcakes), 0 (All)
        // Stock types: 2 (Accessories), 4 (Toppers), 5 (Cutters), 7 (Packaging), 8 (Supplies), 9 (Appliances), 10 (Shop Setup)
        var bakeryTypes = new[] { 0, 1, 3, 6 };
        var isBakeryTab = bakeryTypes.Contains(prdtype);

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

        ViewBag.Products = products;
        ViewBag.Categories = categories;
        ViewBag.Templates = templates;
        ViewBag.IsBakeryTab = isBakeryTab;
        ViewBag.IsCsBakery = isCsBakery;
        ViewBag.ProductType = prdtype;
        ViewBag.CurrentPage = pageno;
        ViewBag.TotalPages = products.TotalPages;
        ViewBag.Search = search;
        ViewBag.FilterStatus = filterstatus;
        ViewBag.CategoryId = catid;
        ViewBag.TemplateId = templateid;
        ViewBag.Sort = sort;
        ViewBag.HasCutters = showcutteritems;
        ViewBag.TypeId = typeid;
        ViewBag.UserType = userType;
        ViewBag.WebshopId = webshopId;

        return View("~/Views/BakeryInventory/Index.cshtml");
    }

    // ─── AJAX: Get Stock Locations (GET) ──────────────────────────────────────

    [HttpGet("stocklocations")]
    public async Task<IActionResult> GetStockLocations([FromQuery] long pid)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var locations = await _inventoryService.GetStockLocationsAsync(pid, wid);
        return Json(new { success = true, data = locations });
    }

    // ─── AJAX: Get Qty Log (GET) ─────────────────────────────────────────────

    [HttpGet("qtylog")]
    public async Task<IActionResult> GetQtyLog([FromQuery] long pid)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var log = await _inventoryService.GetQtyLogAsync(pid, wid);
        return Json(new { success = true, data = log });
    }

    // ─── AJAX: Add Qty (POST, HQ-only) ───────────────────────────────────────

    [HttpPost("addqty")]
    public async Task<IActionResult> AddQty([FromForm] long pid, [FromForm] long locationId, [FromForm] int qty)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var csBakeryId = _config["CsBakeryId"] ?? "82";
        if (webshopId != csBakeryId)
            return Json(new { success = false, message = "HQ-only operation" });

        var success = await _inventoryService.AddStockQtyAsync(pid, locationId, qty, userId);
        return Json(new { success, message = success ? "Quantity has been updated successfully" : "Failed to update quantity" });
    }

    // ─── AJAX: Edit Qty (POST, HQ-only) ──────────────────────────────────────

    [HttpPost("editqty")]
    public async Task<IActionResult> EditQty([FromForm] long pid, [FromForm] long locationId, [FromForm] int qty)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var csBakeryId = _config["CsBakeryId"] ?? "82";
        if (webshopId != csBakeryId)
            return Json(new { success = false, message = "HQ-only operation" });

        var success = await _inventoryService.EditStockQtyAsync(pid, locationId, qty, userId);
        return Json(new { success, message = success ? "Quantity has been updated successfully" : "Failed to update quantity" });
    }

    // ─── AJAX: Bulk Active (POST) ────────────────────────────────────────────

    [HttpPost("bulkactive")]
    public async Task<IActionResult> BulkActive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected" });

        var success = await _inventoryService.BulkSetActiveAsync(idList, true);
        return Json(new { success, message = success ? "Products set to active" : "Failed to update" });
    }

    // ─── AJAX: Bulk Inactive (POST) ──────────────────────────────────────────

    [HttpPost("bulkinactive")]
    public async Task<IActionResult> BulkInactive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected" });

        var success = await _inventoryService.BulkSetActiveAsync(idList, false);
        return Json(new { success, message = success ? "Products set to inactive" : "Failed to update" });
    }

    // ─── AJAX: Bulk Delete (POST) ────────────────────────────────────────────

    [HttpPost("bulkdelete")]
    public async Task<IActionResult> BulkDelete([FromForm] string ids)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected" });

        var success = await _inventoryService.BulkDeleteAsync(idList, wid, userId);
        return Json(new { success, message = success ? "Products deleted" : "Failed to delete" });
    }

    // ─── AJAX: Bulk Franchise (POST) ─────────────────────────────────────────

    [HttpPost("bulkfranchise")]
    public async Task<IActionResult> BulkFranchise([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected" });

        var success = await _inventoryService.BulkSetFranchiseAsync(idList, true);
        return Json(new { success, message = success ? "Products set as franchise" : "Failed to update" });
    }

    // ─── AJAX: Bulk Remove Franchise (POST) ──────────────────────────────────

    [HttpPost("bulkremovefranchise")]
    public async Task<IActionResult> BulkRemoveFranchise([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected" });

        var success = await _inventoryService.BulkSetFranchiseAsync(idList, false);
        return Json(new { success, message = success ? "Franchise removed from products" : "Failed to update" });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private List<long> ParseIds(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return new List<long>();
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0L)
                  .Where(id => id > 0)
                  .ToList();
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
