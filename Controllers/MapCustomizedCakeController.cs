using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for mapping expired/customized products to active products.
/// Route: /mapcustomizedcake
/// Migrated from mapcustomizedcake.aspx.
/// </summary>
[Route("mapcustomizedcake")]
[Route("mapcustomizedcake.aspx")]
public class MapCustomizedCakeController : Controller
{
    private readonly MapCustomizedCakeService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public MapCustomizedCakeController(
        MapCustomizedCakeService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    /// <summary>
    /// GET /mapcustomizedcake?id={productId} - page load
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(long id)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (id <= 0)
            return Redirect("/businessorders");

        // Get expired product info
        var expiredProduct = await _service.GetExpiredProductAsync(id, webshopId);
        if (expiredProduct == null)
            return Redirect("/businessorders");

        // Get currently mapped products
        var mappedProducts = await _service.GetMappedProductsAsync(id, webshopId);

        // Layout ViewBag
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        ViewBag.ExpiredProduct = expiredProduct;
        ViewBag.MappedProducts = mappedProducts;

        return View();
    }

    /// <summary>
    /// POST /mapcustomizedcake/search - AJAX autocomplete search
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] MapCakeSearchRequest req)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(req.Keyword) || req.ExpiredProductId <= 0)
            return Json(new List<SearchProductResult>());

        var results = await _service.SearchProductsAsync(req.Keyword, req.ExpiredProductId, webshopId);
        return Json(results);
    }

    /// <summary>
    /// POST /mapcustomizedcake/link - AJAX link item
    /// </summary>
    [HttpPost("link")]
    public async Task<IActionResult> LinkItem([FromBody] MapCakeLinkRequest req)
    {
        if (req.ExpiredProductId <= 0 || req.ActiveProductId <= 0)
            return Json(new { success = false, message = "Invalid product IDs." });

        await _service.LinkProductAsync(req.ExpiredProductId, req.ActiveProductId);
        return Json(new { success = true, message = "Item has been linked successfully." });
    }

    /// <summary>
    /// POST /mapcustomizedcake/unlink - AJAX unlink item
    /// </summary>
    [HttpPost("unlink")]
    public async Task<IActionResult> UnlinkItem([FromBody] MapCakeUnlinkRequest req)
    {
        if (req.ExpiredProductId <= 0)
            return Json(new { success = false, message = "Invalid product ID." });

        await _service.UnlinkProductAsync(req.ExpiredProductId);
        return Json(new { success = true, message = "Item has been unmapped successfully." });
    }

    /// <summary>
    /// POST /mapcustomizedcake/detail - AJAX get item detail
    /// </summary>
    [HttpPost("detail")]
    public async Task<IActionResult> GetItemDetail([FromBody] MapCakeDetailRequest req)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (req.ProductId <= 0)
            return Json(new { success = false });

        var detail = await _service.GetProductDetailAsync(req.ProductId, webshopId);
        if (detail == null)
            return Json(new { success = false });

        return Json(new { success = true, data = detail });
    }
}

// --- Request Models ---

public class MapCakeSearchRequest
{
    public string Keyword { get; set; } = "";
    public long ExpiredProductId { get; set; }
}

public class MapCakeLinkRequest
{
    public long ExpiredProductId { get; set; }
    public long ActiveProductId { get; set; }
}

public class MapCakeUnlinkRequest
{
    public long ExpiredProductId { get; set; }
}

public class MapCakeDetailRequest
{
    public long ProductId { get; set; }
}
