using CakerStreet.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// CRM Search Tag Module — migrated from legacy crmsearchtag.aspx.
/// Legacy Module ID: 20, legacy route: /crmsearchtag
/// Migrated route: /managesearchtags
/// 
/// Query string params match legacy pattern:
///   ?searchfor=0&pno=1&status=1&sort=11&filterp=keyword&rdsearchtype=0
/// </summary>
[Route("managesearchtags")]
public class ManageSearchTagsController : Controller
{
    private readonly ManageSearchTagService _service;

    public ManageSearchTagsController(ManageSearchTagService service)
    {
        _service = service;
    }

    // ─── List / Search Tags (matches legacy bindgrid) ────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        int searchfor = 0,      // 0=Cakes, 1=Cupcakes, 2=Party Accessory
        int status = 1,         // 0=All, 1=Active, 2=Inactive
        string filterp = "",    // Keyword search
        int rdsearchtype = 0,   // 0=Anywhere, 1=Starts, 2=Ends, 3=Exact
        int sort = 11,          // Sort option
        int pno = 1)            // Page number
    {
        var result = await _service.GetTagsAsync(
            searchFor: searchfor,
            status: status,
            filterp: filterp,
            searchType: rdsearchtype,
            sort: sort,
            page: pno);

        return View("~/Views/ManageSearchTags/Index.cshtml", result);
    }

    // ─── View Linked Products for a Tag ──────────────────────────────────────────

    [HttpGet("products/{tagId:int}")]
    public async Task<IActionResult> Products(int tagId, int page = 1)
    {
        var result = await _service.GetTagProductsAsync(tagId, page);
        if (string.IsNullOrEmpty(result.TagText))
            return NotFound("Tag not found");
        return View("~/Views/ManageSearchTags/Products.cshtml", result);
    }

    // ─── Search Products (AJAX for link dialog) ─────────────────────────────────

    [HttpGet("searchproducts")]
    public async Task<IActionResult> SearchProducts(string q = "")
    {
        var products = await _service.SearchProductsAsync(q);
        return Json(products);
    }

    // ─── Link Product to Tag ─────────────────────────────────────────────────────

    [HttpPost("link")]
    public async Task<IActionResult> Link([FromForm] int tagId, [FromForm] long productId)
    {
        var linked = await _service.LinkProductToTagAsync(tagId, productId);
        TempData["Message"] = linked
            ? $"Product {productId} linked to tag {tagId}"
            : $"Product {productId} is already linked to tag {tagId}";
        return RedirectToAction("Products", new { tagId });
    }

    // ─── Unlink Product from Tag ─────────────────────────────────────────────────

    [HttpPost("unlink")]
    public async Task<IActionResult> Unlink([FromForm] int tagId, [FromForm] long productId)
    {
        var unlinked = await _service.UnlinkProductFromTagAsync(tagId, productId);
        TempData["Message"] = unlinked
            ? $"Product {productId} unlinked from tag {tagId}"
            : $"Product {productId} was not linked to tag {tagId}";
        return RedirectToAction("Products", new { tagId });
    }

    // ─── Helper: Build URL with current filters (matches legacy GetPageUrl) ─────

    public static string BuildPageUrl(SearchTagListResult model, int pageNo)
    {
        var url = "/managesearchtags";
        var parts = new List<string>();

        if (pageNo > 1) parts.Add($"pno={pageNo}");
        if (model.SearchFor != 0) parts.Add($"searchfor={model.SearchFor}");
        if (!string.IsNullOrEmpty(model.FilterP)) 
        {
            parts.Add($"filterp={Uri.EscapeDataString(model.FilterP)}");
            if (model.SearchType != 0) parts.Add($"rdsearchtype={model.SearchType}");
        }
        if (model.Sort != 11) parts.Add($"sort={model.Sort}");
        if (model.Status != 1) parts.Add($"status={model.Status}");

        if (parts.Count > 0) url += "?" + string.Join("&", parts);
        return url;
    }
}
