using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays the HACCP (Hazard Analysis Critical Control Point) navigation page
/// with links to the Safer Food Better Business guide and Daily Diary.
/// Migrated from legacy haccp.aspx / haccp.aspx.cs.
/// Route: /haccp
/// </summary>
public class HaccpController : Controller
{
    private readonly IConfiguration _config;
    private readonly BakeryMenuService _menuService;

    public HaccpController(IConfiguration config, BakeryMenuService menuService)
    {
        _config = config;
        _menuService = menuService;
    }

    private async Task PopulateLayoutAsync()
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
    }

    [HttpGet("haccp")]
    [HttpGet("haccp.aspx")]
    public async Task<IActionResult> Index([FromQuery] string? msg = null)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path)}");

        await PopulateLayoutAsync();

        ViewBag.Message = msg;
        ViewBag.PageTitle = "Manage Opening and close Checklist";

        return View("~/Views/Haccp/Index.cshtml");
    }
}
