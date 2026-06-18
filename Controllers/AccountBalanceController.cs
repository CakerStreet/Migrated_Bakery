using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the My Account Balance module.
/// Route: /myaccountbalance
/// Migrated from myaccountbalance.aspx.
/// No specific module ID — auth check: userId != 0 only.
/// READ-ONLY financial dashboard with feature-flagged withdrawal.
/// </summary>
[Route("myaccountbalance")]
public class AccountBalanceController : Controller
{
    private readonly AccountBalanceService _accountBalanceService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public AccountBalanceController(
        AccountBalanceService accountBalanceService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _accountBalanceService = accountBalanceService;
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
            return Redirect("/businesslogin?returl=/myaccountbalance");

        if (!long.TryParse(webshopId, out var webstoreId))
            return Redirect("/businesslogin?returl=/myaccountbalance");

        var overview = await _accountBalanceService.GetAccountOverviewAsync(webstoreId);
        var orders = await _accountBalanceService.GetOrdersAsync(webstoreId);

        // Feature flag for withdrawals (enabled by default in migration)
        var withdrawalsEnabled = _config.GetValue<bool>("AccountBalance:WithdrawalsEnabled", true);

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

        ViewBag.Overview = overview;
        ViewBag.Orders = orders;
        ViewBag.WithdrawalsEnabled = withdrawalsEnabled;

        return View("~/Views/AccountBalance/Index.cshtml");
    }

    // ─── Withdraw (POST) ───────────────────────────────────────────────────────

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest? request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid request" });

        if (!long.TryParse(webshopId, out var webstoreId))
            return Json(new { success = false, message = "Invalid webshop ID" });

        var success = await _accountBalanceService.RequestWithdrawalAsync(request.OrderId, webstoreId);
        if (success)
        {
            return Json(new { success = true });
        }
        else
        {
            return Json(new { success = false, message = "Withdrawal request failed. Order must be Completed (Status 4), Payout not done, and not already requested." });
        }
    }
}

// ─── Request Model ─────────────────────────────────────────────────────────────

public class WithdrawRequest
{
    public long OrderId { get; set; }
}
