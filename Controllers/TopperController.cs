using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class TopperController : Controller
{
    private readonly TopperService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public TopperController(
        TopperService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    private async Task PopulateLayoutMetadataAsync()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";
    }

    private int GetCurrentUserId()
    {
        return HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
    }

    private long GetWebshopId()
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        long.TryParse(webshopIdStr, out var webshopId);
        return webshopId;
    }

    [HttpGet("managetopper")]
    [HttpGet("managetopper.aspx")]
    public async Task<IActionResult> ManageTopper([FromQuery] long id, [FromQuery] int type = 4, [FromQuery] int sizeId = 0, [FromQuery] string? msg = null, [FromQuery] string? error = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var webstoreId = GetWebshopId();
        var product = await _service.GetProductDetailsAsync(id, webstoreId);
        if (product == null)
        {
            return Redirect("/mywebstore");
        }

        await PopulateLayoutMetadataAsync();

        var assigned = await _service.GetAssignedToppersAsync(id, webstoreId, type, sizeId);
        var sizeDetail = sizeId > 0 ? await _service.GetCakeSizeAsync(sizeId) : null;

        ViewBag.ProductId = id;
        ViewBag.ProductType = type;
        ViewBag.SizeId = sizeId;
        ViewBag.Product = product;
        ViewBag.AssignedToppers = assigned;
        ViewBag.SizeDetail = sizeDetail;
        ViewBag.SuccessMessage = msg;
        ViewBag.ErrorMessage = error;

        return View("~/Views/Topper/ManageTopper.cshtml");
    }

    [HttpGet("managetopper/search")]
    public async Task<IActionResult> SearchToppers([FromQuery] string keyword, [FromQuery] long prdid, [FromQuery] int prdtype)
    {
        var webstoreId = GetWebshopId();
        var list = await _service.GetAvailableToppersAsync(keyword ?? "", prdid, prdtype, webstoreId);
        return Json(list);
    }

    [HttpPost("managetopper/add")]
    public async Task<IActionResult> AddTopper([FromForm] long productId, [FromForm] long topperPrdId, [FromForm] int type, [FromForm] int sizeId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        var added = await _service.AddProductTopperAsync(productId, topperPrdId, sizeId);
        string url = $"/managetopper?id={productId}&type={type}&sizeId={sizeId}";
        if (added)
        {
            return Redirect(url + $"&msg={Uri.EscapeDataString("Topper added successfully.")}");
        }
        else
        {
            return Redirect(url + $"&error={Uri.EscapeDataString("This topper is already assigned to this product.")}");
        }
    }

    [HttpPost("managetopper/delete")]
    public async Task<IActionResult> DeleteTopper([FromForm] long productId, [FromForm] long topperPrdId, [FromForm] int type, [FromForm] int sizeId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        await _service.RemoveProductTopperAsync(topperPrdId, productId, sizeId);
        return Redirect($"/managetopper?id={productId}&type={type}&sizeId={sizeId}&msg={Uri.EscapeDataString("Topper removed successfully.")}");
    }

    [HttpPost("managetopper/updateqty")]
    public async Task<IActionResult> UpdateQty([FromForm] long productTopperId, [FromForm] int qty)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var ok = await _service.UpdateTopperQtyAsync(productTopperId, qty);
        return Json(new { success = ok });
    }

    // ─── LinkTopper Endpoints ──────────────────────────────────────────────────────

    [HttpGet("linktopper")]
    [HttpGet("linktopper.aspx")]
    [HttpGet("linkcutter")]
    [HttpGet("linkcutter.aspx")]
    public async Task<IActionResult> LinkTopper([FromQuery] long id, [FromQuery] int type = 4, [FromQuery] string? msg = null, [FromQuery] string? error = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        // if type is 5, in legacy it redirects to linkcutter.aspx or linkcutter endpoint.
        // We can handle both type 4 and type 5 here or just direct them. Let's support both.
        var webstoreId = GetWebshopId();
        var product = await _service.GetProductDetailsAsync(id, webstoreId);
        if (product == null)
        {
            return Redirect("/mywebstore");
        }

        await PopulateLayoutMetadataAsync();

        var sizes = await _service.GetLinkedSizesAsync(id);
        var sizeToppers = new Dictionary<int, List<SizeTopperItem>>();
        foreach (var size in sizes)
        {
            sizeToppers[size.SizeId] = await _service.GetSizeToppersAsync(id, webstoreId, type, size.SizeId);
        }

        ViewBag.ProductId = id;
        ViewBag.ProductType = type;
        ViewBag.Product = product;
        ViewBag.Sizes = sizes;
        ViewBag.SizeToppers = sizeToppers;
        ViewBag.SuccessMessage = msg;
        ViewBag.ErrorMessage = error;

        return View("~/Views/Topper/LinkTopper.cshtml");
    }

    [HttpGet("linktopper/search")]
    public async Task<IActionResult> SearchSizeToppers([FromQuery] string keyword, [FromQuery] long prdid, [FromQuery] int prdtype)
    {
        var webstoreId = GetWebshopId();
        var list = await _service.GetAvailableSizeToppersAsync(keyword ?? "", prdid, prdtype, webstoreId);
        return Json(list);
    }

    [HttpPost("linktopper/add")]
    public async Task<IActionResult> AddSizeTopper([FromForm] long productId, [FromForm] long topperPrdId, [FromForm] int type)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        var result = await _service.SaveSizeTopperAsync(productId, topperPrdId);
        string url = $"/linktopper?id={productId}&type={type}";
        if (result == 1)
        {
            return Redirect(url + $"&msg={Uri.EscapeDataString("Topper linked successfully.")}");
        }
        else
        {
            return Redirect(url + $"&error={Uri.EscapeDataString("This topper is already linked to all sizes of this product.")}");
        }
    }

    [HttpPost("linktopper/update")]
    public async Task<IActionResult> UpdateSizeTopper(
        [FromForm] long productId, 
        [FromForm] int type,
        [FromForm] List<long> topperIds, 
        [FromForm] List<int> sizeIds,
        IFormCollection form)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        // Parse from form collection for each row checked
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("chk_"))
            {
                var parts = key.Split('_'); // chk_topperId_sizeId
                if (parts.Length == 3 && long.TryParse(parts[1], out var topperId) && int.TryParse(parts[2], out var sizeId))
                {
                    var pricing = form[$"pricing_{topperId}_{sizeId}"].ToString();
                    var mandatory = form[$"mandatory_{topperId}_{sizeId}"].ToString();
                    var stock = form[$"stock_{topperId}_{sizeId}"].ToString();
                    var displayOrderStr = form[$"displayorder_{topperId}_{sizeId}"].ToString();
                    var qtyStr = form[$"qty_{topperId}_{sizeId}"].ToString();
                    var remarks = form[$"remarks_{topperId}_{sizeId}"].ToString();

                    int.TryParse(displayOrderStr, out var displayOrder);
                    int.TryParse(qtyStr, out var qty);

                    await _service.UpdateSizeTopperMappingAsync(productId, topperId, sizeId, pricing, mandatory, stock, displayOrder, qty, remarks);
                }
            }
        }

        return Redirect($"/linktopper?id={productId}&type={type}&msg={Uri.EscapeDataString("Linked toppers updated successfully.")}");
    }

    [HttpPost("linktopper/delete")]
    public async Task<IActionResult> DeleteSizeTopper([FromForm] long productId, [FromForm] long topperPrdId, [FromForm] int sizeId, [FromForm] int type)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        await _service.RemoveSizeTopperAsync(productId, topperPrdId, sizeId);
        return Redirect($"/linktopper?id={productId}&type={type}&msg={Uri.EscapeDataString("Topper unlinked successfully.")}");
    }

    // ─── OrderTopper Endpoints ─────────────────────────────────────────────────────

    [HttpGet("ordertopper")]
    [HttpGet("ordertopper.aspx")]
    public async Task<IActionResult> OrderTopper([FromQuery] long orderID, [FromQuery] long orderdetailid, [FromQuery] int typeId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        try
        {
            var detail = await _service.GetOrderDetailWithProductAndOrderAsync(orderID, orderdetailid);
            if (detail == null)
            {
                return Redirect("/businessorders");
            }

            await PopulateLayoutMetadataAsync();

            long productId = detail.ContainsKey("product_ID") && detail["product_ID"] != null
                ? Convert.ToInt64(detail["product_ID"]) : 0;
            int sizeId = detail.ContainsKey("orderDetail_SizeID") && detail["orderDetail_SizeID"] != null
                ? Convert.ToInt32(detail["orderDetail_SizeID"]) : 0;
            long orderBakeryId = detail.ContainsKey("order_bakeryID") && detail["order_bakeryID"] != null
                ? Convert.ToInt64(detail["order_bakeryID"]) : 0;

            var toppers = await _service.GetOrderToppersAsync(productId, typeId, sizeId);
            var topperLocations = new Dictionary<long, List<TopperStockLocationItem>>();
            foreach (var topper in toppers)
            {
                topperLocations[topper.ProductId] = await _service.GetLocationsWithStockAsync(productId, topper.ProductId, orderBakeryId);
            }

            ViewBag.OrderId = orderID;
            ViewBag.OrderDetailId = orderdetailid;
            ViewBag.TypeId = typeId;
            ViewBag.Detail = detail;
            ViewBag.Toppers = toppers;
            ViewBag.TopperLocations = topperLocations;
            ViewBag.ProductTitle = typeId switch { 4 => "Toppers", 5 => "Cutters", 7 => "Packaging", 8 => "Supplies", _ => "Toppers" };

            return View("~/Views/Topper/OrderTopper.cshtml");
        }
        catch (Exception ex)
        {
            return Content($"Error in OrderTopper: {ex.Message}\n\n{ex.StackTrace}", "text/plain");
        }
    }

    [HttpPost("ordertopper/saveqty")]
    public async Task<IActionResult> SaveOrderTopperQty([FromBody] List<OrderTopperQtyInput> inputs)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (inputs == null || inputs.Count == 0)
        {
            return BadRequest("Inputs cannot be empty.");
        }

        long orderId = inputs[0].orderTopper_orderID;
        long orderDetailId = inputs[0].orderTopper_orderdetailID;

        try
        {
            await _service.SaveOrderToppersQtyAsync(orderId, orderDetailId, inputs, userId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
