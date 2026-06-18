using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("managereceipe-findandreplace")]
[Route("managereceipeingredient_keywords")]
[Route("managereceipeIngredient_keywords.aspx")]
public class RecipeReplaceController : Controller
{
    private readonly RecipeService _recipeService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public RecipeReplaceController(
        RecipeService recipeService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _recipeService = recipeService;
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
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string search = "",
        [FromQuery] string? msg = null)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        var list = await _recipeService.GetReplaceLogsAsync(search);

        ViewBag.SearchKeyword = search;
        ViewBag.Logs = list;
        ViewBag.SuccessMessage = msg;

        return View("~/Views/RecipeReplace/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] string fromText,
        [FromForm] string toText)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        int count = await _recipeService.GlobalFindAndReplaceIngredientsAsync(fromText, toText, userId.ToString());

        return RedirectToAction(nameof(Index), new { msg = $"{count} Records replaced Successfully" });
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromForm] List<int> selectedIds)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        if (selectedIds != null && selectedIds.Count > 0)
        {
            await _recipeService.DeleteReplaceLogsAsync(selectedIds);
        }

        return RedirectToAction(nameof(Index), new { msg = "Record(s) Deleted Successfully" });
    }
}
