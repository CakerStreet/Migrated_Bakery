using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("bakeryavailability")]
public class BakeryAvailabilityController : Controller
{
    private readonly BakeryAvailabilityService _availabilityService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakeryAvailabilityController(
        BakeryAvailabilityService availabilityService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _availabilityService = availabilityService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopIdStr) || !long.TryParse(webshopIdStr, out var webshopId))
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        if (userType != "1")
        {
            return Redirect("/mywebstore");
        }

        // Set default month/year if not provided
        var targetMonth = month ?? DateTime.Today.Month;
        var targetYear = year ?? DateTime.Today.Year;

        // Fetch busy days for calendar rendering
        var busyDays = await _availabilityService.GetBusyDaysAsync(webshopId, targetMonth, targetYear);

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopIdStr, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        ViewBag.Month = targetMonth;
        ViewBag.Year = targetYear;
        ViewBag.BusyDays = busyDays;

        return View("~/Views/BakeryAvailability/Index.cshtml");
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleRequest request)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopIdStr) || !long.TryParse(webshopIdStr, out var webshopId))
        {
            return Json(new { success = false, message = "Unauthorized" });
        }

        if (!DateTime.TryParse(request.DateStr, out var date))
        {
            // Try parsing custom format dd/MM/yyyy if standard fails
            if (DateTime.TryParseExact(request.DateStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                date = parsedDate;
            }
            else
            {
                return Json(new { success = false, message = "Invalid date format" });
            }
        }

        bool isBusy = await _availabilityService.ToggleBusyDateAsync(webshopId, date);
        return Json(new { success = true, isBusy = isBusy });
    }

    public class ToggleRequest
    {
        public string DateStr { get; set; } = "";
    }
}
