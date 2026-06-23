using CakerStreet.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// CRM/Data Management utility — Search Tag Product Association cleanup.
/// Internal/restricted. Not exposed in CRM menu.
/// Route: /managesearchtags
/// </summary>
[Route("managesearchtags")]
public class ManageSearchTagsController : Controller
{
    private readonly ManageSearchTagService _service;

    public ManageSearchTagsController(ManageSearchTagService service)
    {
        _service = service;
    }

    // ─── List / Search Tags ──────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(string search = "", int page = 1)
    {
        var result = await _service.GetTagsAsync(search, page);
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
}
