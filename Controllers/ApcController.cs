using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("businessapc")]
public class ApcController : Controller
{
    private readonly ApcService _apcService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ApcController(
        ApcService apcService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _apcService = apcService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? orderID)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect($"/businesslogin?returl={Url.Action("Index", "Apc", new { orderID })}");

        // Module permission check (Module 25)
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 25);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // Set layout variables
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopIdStr, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        ApcOrderDetails? orderDetails = null;
        List<ApcBookingLog> history = new();
        ApcBakeryCredentials? apcCreds = null;

        if (orderID.HasValue)
        {
            orderDetails = await _apcService.GetOrderDetailsAsync(orderID.Value);
            history = await _apcService.GetBookingHistoryAsync(orderID.Value);

            long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
            apcCreds = await _apcService.GetBakeryApcDetailsAsync(webshopId);
        }

        ViewBag.OrderId = orderID;
        ViewBag.OrderDetails = orderDetails;
        ViewBag.BookingHistory = history;
        ViewBag.ApcCredentials = apcCreds;
        ViewBag.Channel = -1; // Default select

        return View();
    }

    [HttpPost("view-order")]
    public async Task<IActionResult> ViewOrder(long orderID, int channel)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopIdStr, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        var orderDetails = await _apcService.GetOrderDetailsAsync(orderID);
        var history = await _apcService.GetBookingHistoryAsync(orderID);
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        var apcCreds = await _apcService.GetBakeryApcDetailsAsync(webshopId);

        ViewBag.OrderId = orderID;
        ViewBag.OrderDetails = orderDetails;
        ViewBag.BookingHistory = history;
        ViewBag.ApcCredentials = apcCreds;
        ViewBag.Channel = channel;
        ViewBag.ShowBottomValues = true;

        if (orderDetails == null)
        {
            ViewBag.ErrorMessage = "Order details not found";
            ViewBag.ShowBottomValues = false;
        }

        return View("Index");
    }

    [HttpPost("book-apc")]
    public async Task<IActionResult> BookApc(long orderId, string displayOrderId, string serviceCode, string prdName, double price,
        string fullName, string email, string address1, string address2, string zip, string phone, string city, string county, string instructions)
    {
        var customerId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";

        try
        {
            var result = await _apcService.BookApcStubAsync(orderId, displayOrderId, serviceCode, customerId, fullName, instructions);
            TempData["SuccessMessage"] = result;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "APC Booking Error: " + ex.Message;
        }

        return RedirectToAction("Index", new { orderID = orderId });
    }

    [HttpPost("get-dhl-rates")]
    public async Task<IActionResult> GetDhlRates(long orderId, int channel, string zip, string city, double price)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopIdStr, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        var orderDetails = await _apcService.GetOrderDetailsAsync(orderId);
        var history = await _apcService.GetBookingHistoryAsync(orderId);
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        var apcCreds = await _apcService.GetBakeryApcDetailsAsync(webshopId);

        ViewBag.OrderId = orderId;
        ViewBag.OrderDetails = orderDetails;
        ViewBag.BookingHistory = history;
        ViewBag.ApcCredentials = apcCreds;
        ViewBag.Channel = channel;
        ViewBag.ShowBottomValues = true;

        var rates = _apcService.GetDhlRatesStub(zip, city, price);
        ViewBag.DhlRates = rates;
        ViewBag.ShowDhlBooking = true;

        return View("Index");
    }

    [HttpPost("book-dhl")]
    public async Task<IActionResult> BookDhl(long orderId, string displayOrderId, string serviceTypeCode, string fullName)
    {
        var customerId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";

        try
        {
            var result = await _apcService.BookDhlStubAsync(orderId, displayOrderId, serviceTypeCode, customerId, fullName);
            TempData["SuccessMessage"] = result;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "DHL Booking Error: " + ex.Message;
        }

        return RedirectToAction("Index", new { orderID = orderId });
    }

    [HttpPost("track")]
    public IActionResult Track(long orderId, string recOrderId)
    {
        if (string.IsNullOrEmpty(recOrderId))
        {
            TempData["ErrorMessage"] = "Please enter an order ID to track.";
            return RedirectToAction("Index", new { orderID = orderId });
        }

        var result = _apcService.TrackOrderStub(recOrderId);
        TempData["SuccessMessage"] = result;
        return RedirectToAction("Index", new { orderID = orderId });
    }

    [HttpPost("cancel")]
    public IActionResult Cancel(long orderId, string recOrderId)
    {
        if (string.IsNullOrEmpty(recOrderId))
        {
            TempData["ErrorMessage"] = "Please enter an order ID to cancel.";
            return RedirectToAction("Index", new { orderID = orderId });
        }

        var result = _apcService.CancelOrderStub(recOrderId);
        TempData["SuccessMessage"] = result;
        return RedirectToAction("Index", new { orderID = orderId });
    }

    [HttpPost("label")]
    public async Task<IActionResult> Label(long orderId, string recOrderId)
    {
        if (string.IsNullOrEmpty(recOrderId))
        {
            TempData["ErrorMessage"] = "Please enter an order ID to generate a label.";
            return RedirectToAction("Index", new { orderID = orderId });
        }

        try
        {
            var labelUrl = await _apcService.GenerateLabelStubAsync(recOrderId);
            TempData["SuccessMessage"] = $"Label generated successfully. Link: <a href='{labelUrl}' target='_blank' style='text-decoration: underline; color: #fff;'>View/Download Label PDF</a>";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Label Generation Error: " + ex.Message;
        }

        return RedirectToAction("Index", new { orderID = orderId });
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
