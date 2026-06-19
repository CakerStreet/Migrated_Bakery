using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the My Account Balance For Baking module.
/// Route: /myaccountbalanceforbaking
/// Migrated from myaccountbalanceforbaking.aspx.
/// Franchise-oriented account balance page with baking cost tracking,
/// reverse refund handling, and miscellaneous payment transactions.
/// </summary>
[Route("myaccountbalanceforbaking")]
[Route("myaccountbalanceforbaking.aspx")]
public class AccountBalanceBakingController : Controller
{
    private readonly AccountBalanceBakingService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public AccountBalanceBakingController(
        AccountBalanceBakingService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect("/?returl=" + Request.Path);

        if (!long.TryParse(webshopId, out var webstoreId))
            return Redirect("/?returl=" + Request.Path);

        // Check baking status
        var isBakingOff = await _service.GetIsBakingOffAsync(webstoreId);

        // Get account overview
        var overview = await _service.GetAccountOverviewAsync(webstoreId);

        // Check if franchise user
        var isFranchise = await _service.IsFranchiseAsync(webstoreId);

        // Franchise orders
        var franchiseOrders = new List<BakingFranchiseOrderItem>();
        var baseCosts = new List<BakingBaseCostItem>();
        if (isFranchise)
        {
            franchiseOrders = await _service.GetFranchiseOrdersAsync(webstoreId);

            if (franchiseOrders.Count > 0 && !isBakingOff)
            {
                var orderIds = string.Join(",", franchiseOrders.Select(o => o.OrderId.ToString()));
                baseCosts = await _service.GetBaseCostsAsync(orderIds);
            }
        }

        // Miscellaneous payments
        var miscPayments = await _service.GetMiscPaymentsAsync(webstoreId);

        // Set ViewBag for layout
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        var siteUrl = _config["SiteUrl"] ?? "/";
        var paypalClientId = _config["PaypalClientId"] ?? "";

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = _config["SiteUrl"] ?? "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = _config["CustomerSiteUrl"] ?? "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = _config["CrmSiteUrl"] ?? "http://localhost:27201";

        ViewBag.Overview = overview;
        ViewBag.IsFranchise = isFranchise;
        ViewBag.IsBakingOff = isBakingOff;
        ViewBag.FranchiseOrders = franchiseOrders;
        ViewBag.BaseCosts = baseCosts;
        ViewBag.MiscPayments = miscPayments;
        ViewBag.SiteUrl = siteUrl;
        ViewBag.PaypalClientId = paypalClientId;

        return View("~/Views/AccountBalanceBaking/Index.cshtml");
    }

    // ─── Withdraw Order (POST) ─────────────────────────────────────────────────

    [HttpPost("withdraw-order")]
    public async Task<IActionResult> WithdrawOrder([FromBody] BakingWithdrawOrderRequest? request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid request" });

        if (!long.TryParse(webshopId, out var webstoreId))
            return Json(new { success = false, message = "Invalid webshop ID" });

        var (success, message) = await _service.WithdrawOrderAsync(request.OrderId, webstoreId);

        if (message.StartsWith("REDIRECT:"))
        {
            return Json(new { success = false, redirect = "/" + message.Replace("REDIRECT:", "") });
        }

        return Json(new { success, message });
    }

    // ─── Reverse Refund (POST) ─────────────────────────────────────────────────

    [HttpPost("reverse-refund")]
    public async Task<IActionResult> ReverseRefund([FromBody] BakingReverseRefundRequest? request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid request" });

        if (!long.TryParse(webshopId, out var webstoreId))
            return Json(new { success = false, message = "Invalid webshop ID" });

        var (success, message) = await _service.ReverseRefundAsync(request.OrderId, request.Mode, webstoreId, request.ReverseAmount);
        return Json(new { success, message });
    }

    // ─── Withdraw Payout (POST) ────────────────────────────────────────────────

    [HttpPost("withdraw-payout")]
    public async Task<IActionResult> WithdrawPayout([FromBody] BakingWithdrawPayoutRequest? request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.PaymentId <= 0)
            return Json(new { success = false, message = "Invalid request" });

        if (!long.TryParse(webshopId, out var webstoreId))
            return Json(new { success = false, message = "Invalid webshop ID" });

        var (success, message) = await _service.WithdrawPayoutAsync(request.PaymentId, webstoreId);

        if (message.StartsWith("REDIRECT:"))
        {
            return Json(new { success = false, redirect = "/" + message.Replace("REDIRECT:", "") });
        }

        return Json(new { success, message });
    }
}

// ─── Request Models ────────────────────────────────────────────────────────────

public class BakingWithdrawOrderRequest
{
    public long OrderId { get; set; }
}

public class BakingReverseRefundRequest
{
    public long OrderId { get; set; }
    public int Mode { get; set; }
    public decimal ReverseAmount { get; set; }
}

public class BakingWithdrawPayoutRequest
{
    public long PaymentId { get; set; }
}
