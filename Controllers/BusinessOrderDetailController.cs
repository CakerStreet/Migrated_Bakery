using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the business order detail page.
/// Route: /businessorderdetail?ordid={id}
/// Migrated from bakeryorderdetail.aspx — full read + mutation support.
/// </summary>
[Route("businessorderdetail")]
[Route("bakeryorderdetail")]
[Route("bakeryorderdetail.aspx")]
public class BusinessOrderDetailController : Controller
{
    private readonly BusinessOrderDetailService _orderDetailService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BusinessOrderDetailController(
        BusinessOrderDetailService orderDetailService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _orderDetailService = orderDetailService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int ordid = 0)
    {
        if (ordid <= 0)
            return Redirect("/businessorders");

        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

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

        // Get order detail
        var result = await _orderDetailService.GetOrderDetailAsync(ordid, webshopId);

        if (result == null)
            return Redirect("/businessorders");

        return View(result);
    }

    // ─── AJAX MUTATION ENDPOINTS ────────────────────────────────────────────────

    /// <summary>
    /// Confirms a pending order (sets status to Job Assigned=5).
    /// Matches legacy lnkApprove_onclick.
    /// </summary>
    [HttpPost("confirm-order")]
    public async Task<IActionResult> ConfirmOrder([FromBody] OrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            var result = await _orderDetailService.ConfirmOrderAsync(
                request.OrderId, long.Parse(webshopId), userId);
            return Json(new { success = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Updates order status (job assigned=5, processed=2, under delivery=3, completed=4).
    /// Matches legacy OrderJobAssinged_onclick, OrderProcessed_onclick, etc.
    /// </summary>
    [HttpPost("update-status")]
    public async Task<IActionResult> UpdateStatus([FromBody] OrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            var result = await _orderDetailService.UpdateOrderStatusAsync(
                request.OrderId, request.NewStatus, long.Parse(webshopId), userId);
            return Json(new { success = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels an order with reason and remarks.
    /// Matches legacy btnCancelOrder_Click.
    /// </summary>
    [HttpPost("cancel-order")]
    public async Task<IActionResult> CancelOrder([FromBody] CancelOrderRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            var result = await _orderDetailService.CancelOrderAsync(
                request.OrderId, long.Parse(webshopId), userId,
                request.CancelReason, request.CancelRemarks, request.NotifyCustomer);
            return Json(new { success = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Removes (soft-deletes) an order.
    /// Matches legacy Ordedeleted_onclick.
    /// </summary>
    [HttpPost("remove-order")]
    public async Task<IActionResult> RemoveOrder([FromBody] OrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            var result = await _orderDetailService.RemoveOrderAsync(
                request.OrderId, long.Parse(webshopId));
            return Json(new { success = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Forwards an order by cloning it. Returns the new order ID.
    /// </summary>
    [HttpPost("forward-order")]
    public async Task<IActionResult> ForwardOrder([FromBody] OrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            var newOrderId = await _orderDetailService.ForwardOrderAsync(
                request.OrderId, long.Parse(webshopId));
            return Json(new { success = newOrderId > 0, newOrderId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Saves baking cost entries for the order.
    /// Matches legacy btnSaveBakingCost_Click.
    /// </summary>
    [HttpPost("save-baking-cost")]
    public async Task<IActionResult> SaveBakingCost([FromBody] BakingCostRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            foreach (var item in request.Items)
            {
                await _orderDetailService.SaveBakingCostAsync(
                    item.OrderId, item.ProductId, item.SizeId, item.Quantity, item.BakingCost);
            }
            if (request.Items.Any())
            {
                await _orderDetailService.SaveOrderTotalBakingCostAsync(request.Items[0].OrderId);
            }
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

// ─── Request Models ─────────────────────────────────────────────────────────

public class OrderStatusRequest
{
    public long OrderId { get; set; }
    public int NewStatus { get; set; }
}

public class CancelOrderRequest
{
    public long OrderId { get; set; }
    public string CancelReason { get; set; } = "";
    public string CancelRemarks { get; set; } = "";
    public bool NotifyCustomer { get; set; }
}

public class BakingCostRequest
{
    public List<BakingCostSaveItem> Items { get; set; } = new();
}

public class BakingCostSaveItem
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int SizeId { get; set; }
    public int Quantity { get; set; }
    public decimal BakingCost { get; set; }
}
