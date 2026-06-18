using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the delivery receipt print page.
/// Route: /printdeliveryreceipt/{id}
/// Migrated from printdeliveryreceipt.aspx - standalone print page (no layout).
/// Read-only: no mutations.
/// </summary>
[Route("printdeliveryreceipt")]
public class PrintDeliveryReceiptController : Controller
{
    private readonly BusinessOrderDetailService _orderDetailService;
    private readonly IConfiguration _config;

    public PrintDeliveryReceiptController(
        BusinessOrderDetailService orderDetailService,
        IConfiguration config)
    {
        _orderDetailService = orderDetailService;
        _config = config;
    }

    /// <summary>
    /// Single order delivery receipt print view.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Index(long id)
    {
        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var result = await _orderDetailService.GetOrderDetailAsync(id, webshopId);

        if (result == null)
            return NotFound("Order not found.");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.CdnBase = cdnBase;

        return View(result);
    }
}
