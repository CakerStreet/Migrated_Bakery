using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the My Trade Account module.
/// Route: /mytradeaccount
/// Migrated from mytradeaccount.aspx.
/// No specific module ID — auth check: userId != 0 only.
/// READ-ONLY — no mutations.
/// </summary>
[Route("mytradeaccount")]
public class TradeAccountController : Controller
{
    private readonly TradeAccountService _tradeAccountService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public TradeAccountController(
        TradeAccountService tradeAccountService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _tradeAccountService = tradeAccountService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? startdate,
        [FromQuery] string? enddate)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect("/businesslogin?returl=/mytradeaccount");

        // Parse date range — default: last month to today
        DateTime startDate;
        DateTime endDate;

        if (!DateTime.TryParse(startdate, out startDate))
            startDate = DateTime.Today.AddMonths(-1);

        if (!DateTime.TryParse(enddate, out endDate))
            endDate = DateTime.Today;

        var items = await _tradeAccountService.GetTradeAccountAsync(userId, startDate, endDate);

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

        ViewBag.Items = items;
        ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");

        return View("~/Views/TradeAccount/Index.cshtml");
    }

    // ─── Day Detail (GET — AJAX) ──────────────────────────────────────────────

    [HttpGet("detail")]
    public async Task<IActionResult> Detail([FromQuery] string? date)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (!DateTime.TryParse(date, out var workCostDate))
            return Json(new { success = false, message = "Invalid date" });

        var details = await _tradeAccountService.GetDayDetailAsync(userId, workCostDate);

        var result = details.Select(d => new
        {
            d.BakerWorkCostId,
            d.AmountInOut,
            d.OrderId,
            d.Amount,
            d.ReqId,
            d.ReqType,
            ReqTypeName = GetReqTypeName(d.ReqType),
            d.IsPaid,
            CostDate = d.CostDate.ToString("yyyy-MM-dd"),
            d.TimeTaken,
            d.TotalAmountForDay,
            d.TotalTimeTakenForDay,
            d.AmountLeft
        });

        return Json(new { success = true, data = result });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string GetReqTypeName(int reqType)
    {
        return reqType switch
        {
            33 => "Topper",
            11 => "Filling",
            12 => "Icing",
            22 => "Decoration",
            _ => ""
        };
    }
}
