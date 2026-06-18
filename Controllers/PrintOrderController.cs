using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the customer invoice print page.
/// Route: /printorder/{id}
/// Migrated from PrintOrder.aspx.
/// Standalone print page (no sidebar layout) — renders order invoice for customer/seller.
/// Access: bakery owner, customer (via ccode), or CRM (via wccode).
/// </summary>
[Route("printorder")]
public class PrintOrderController : Controller
{
    private readonly BusinessOrderDetailService _orderDetailService;
    private readonly IConfiguration _config;

    public PrintOrderController(BusinessOrderDetailService orderDetailService, IConfiguration config)
    {
        _orderDetailService = orderDetailService;
        _config = config;
    }

    /// <summary>
    /// Customer invoice print view.
    /// Legacy routes: /printorder/{id}, /printorder/{id}?invoice=1
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Index(long id, [FromQuery] string? wccode = null, [FromQuery] string? invoice = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        // If no auth and no code, deny
        if (string.IsNullOrEmpty(webshopId) && string.IsNullOrEmpty(wccode))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.CdnBase = cdnBase;
        ViewBag.IsShopInvoice = !string.IsNullOrEmpty(invoice);

        // Load order — if webshopId is set, use it; otherwise allow via wccode access
        var result = await _orderDetailService.GetOrderDetailAsync(id, webshopId);

        if (result == null)
            return NotFound("Order not found.");

        return View("~/Views/PrintOrder/Index.cshtml", result);
    }
}
