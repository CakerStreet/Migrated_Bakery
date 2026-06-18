using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Purchase Order Item Received page.
/// Route: /managepurchaseorderitemreceived
/// Migrated from managepurchaseorderitemreceived.aspx.
/// HQ-only: webshopId must be "82".
/// </summary>
[Route("managepurchaseorderitemreceived")]
public class PurchaseOrderItemReceivedController : Controller
{
    private readonly PurchaseOrderService _poService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public PurchaseOrderItemReceivedController(
        PurchaseOrderService poService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _poService = poService;
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

        // HQ-only check
        if (webshopId != "82")
            return Redirect("/businessorders");

        if (!id.HasValue || id.Value <= 0)
            return Redirect("/managepurchaseorder");

        // Get PO detail (validates status == 3)
        var poDetail = await _poService.GetPOForItemReceivedAsync(id.Value);
        if (poDetail == null)
            return Redirect("/managepurchaseorder");

        // Get staff list for Received By dropdown
        var wid = long.TryParse(webshopId, out var w) ? w : 82L;
        var staffList = await _poService.GetStaffListForReceivedAsync(wid);

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

        ViewBag.PODetail = poDetail;
        ViewBag.StaffList = staffList;
        ViewBag.WebshopId = webshopId;

        return View("~/Views/PurchaseOrderItemReceived/Index.cshtml");
    }

    // ─── AJAX: Stock Locations for a Product ───────────────────────────────────

    [HttpGet("stocklocations")]
    public async Task<IActionResult> StockLocations(long productId, long webshopId)
    {
        var wsId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(wsId) || wsId != "82")
            return Json(new List<StockLocationItem>());

        var locations = await _poService.GetStockLocationsForProductAsync(productId, webshopId);
        return Json(locations);
    }

    // ─── AJAX: Save Item Received ──────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] ItemReceivedSaveModel model)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || webshopId != "82")
            return Json(new { success = false, message = "Unauthorized" });

        if (model == null || model.Items == null || model.Items.Count == 0)
            return Json(new { success = false, message = "No product(s) to save." });

        if (model.PO_ID <= 0)
            return Json(new { success = false, message = "Invalid Purchase Order." });

        var resultId = await _poService.SaveItemReceivedAsync(model, userId);
        if (resultId > 0)
            return Json(new { success = true, message = "Purchase Order Item(s) have been saved successfully.", id = resultId });

        return Json(new { success = false, message = "Failed to save item received." });
    }
}
