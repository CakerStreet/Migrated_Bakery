using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("manageingredient")]
public class ManageIngredientController : Controller
{
    private readonly RecipeService _recipeService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageIngredientController(
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

    // ─── List View (GET) ───────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("page-{pageno}")]
    public async Task<IActionResult> Index(
        [FromRoute] int pageno = 1,
        [FromQuery] string search = "",
        [FromQuery] int filterstatus = 1,
        [FromQuery] int activestatus = 1,
        [FromQuery] int cookingstatus = 2,
        [FromQuery] int taggrpstatus = 1,
        [FromQuery] int catid = 0,
        [FromQuery] int grpcatid = 0,
        [FromQuery] string searchtags = "",
        [FromQuery] string? msg = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        long wsId = long.Parse(webshopId);
        await PopulateLayoutMetadataAsync();

        int pageSize = 23;
        var cleanSearch = (search ?? "").Replace("+", " ");
        var cleanTags = (searchtags ?? "").Replace("|", ",").Replace("+", " ").Replace("\"", "");

        var result = await _recipeService.GetManageIngredientsAsync(
            filterstatus, activestatus, cookingstatus, taggrpstatus, wsId,
            cleanSearch, pageno, pageSize, catid, grpcatid, cleanTags);

        var books = await _recipeService.GetRecipeBooksAsync(wsId);
        var categories = await _recipeService.GetRecipeIngredientCategoriesAsync();
        var units = await _recipeService.GetMeasuringUnitsAsync();
        var fuzzySearchList = await _recipeService.GetIngredientGroupsForFuzzySearchAsync();

        ViewBag.RecipeBooks = books;
        ViewBag.Categories = categories;
        ViewBag.MeasuringUnits = units;
        ViewBag.FuzzySearchListJson = System.Text.Json.JsonSerializer.Serialize(fuzzySearchList);

        // Keep values in ViewBag
        ViewBag.CurrentPage = pageno;
        ViewBag.PageSize = pageSize;
        ViewBag.SearchKeyword = search;
        ViewBag.FilterStatus = filterstatus;
        ViewBag.ActiveStatus = activestatus;
        ViewBag.CookingStatus = cookingstatus;
        ViewBag.TagGrpStatus = taggrpstatus;
        ViewBag.CatId = catid;
        ViewBag.GrpCatId = grpcatid;
        ViewBag.SearchTags = searchtags;
        ViewBag.SuccessMessage = msg;
        ViewBag.Result = result;

        return View("~/Views/ManageIngredient/Index.cshtml");
    }

    // ─── Save Single Item (POST) ──────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] long id,
        [FromForm] int typeId,
        [FromForm] string title,
        [FromForm] string grp,
        [FromForm] string cutType,
        [FromForm] decimal ml,
        [FromForm] int unitId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        await _recipeService.SaveIngredientInlineAsync(id, typeId, title, grp, cutType, ml, unitId);
        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    // ─── Bulk Batch Action (POST) ──────────────────────────────────────────────

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAction(
        [FromForm] string action,
        [FromForm] List<long> selectedIds,
        [FromForm] List<int> bulkTypeIds,
        [FromForm] List<string> bulkIngredients,
        [FromForm] List<string> bulkIngredientGrps,
        [FromForm] List<string> bulkIngredientCutTypes,
        [FromForm] List<decimal> bulkUnitMls,
        [FromForm] List<int> bulkUnitIds)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        if (selectedIds == null || selectedIds.Count == 0)
            return RedirectToAction(nameof(Index), GetQueryParameters());

        if (action == "delete")
        {
            await _recipeService.BulkDeleteIngredientsAsync(selectedIds);
        }
        else if (action == "update")
        {
            for (int i = 0; i < selectedIds.Count; i++)
            {
                var id = selectedIds[i];
                var typeId = i < bulkTypeIds.Count ? bulkTypeIds[i] : 1;
                var title = i < bulkIngredients.Count ? bulkIngredients[i] : "";
                var grp = i < bulkIngredientGrps.Count ? bulkIngredientGrps[i] : "";
                var cutType = i < bulkIngredientCutTypes.Count ? bulkIngredientCutTypes[i] : "";
                var ml = i < bulkUnitMls.Count ? bulkUnitMls[i] : 0m;
                var unitId = i < bulkUnitIds.Count ? bulkUnitIds[i] : 0;

                await _recipeService.SaveIngredientInlineAsync(id, typeId, title, grp, cutType, ml, unitId);
            }
        }

        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    // ─── Find and Replace Action (POST) ────────────────────────────────────────

    [HttpPost("findreplace")]
    public async Task<IActionResult> FindReplace(
        [FromForm] List<long> selectedIds,
        [FromForm] string fromText,
        [FromForm] string toText,
        [FromForm] bool replaceGrp,
        [FromForm] bool replaceCut,
        [FromForm] bool replaceIng)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        int replaced = await _recipeService.FindAndReplaceIngredientsAsync(
            selectedIds, fromText, toText, replaceGrp, replaceCut, replaceIng, userId.ToString());

        return RedirectToAction(nameof(Index), GetQueryParameters(replaced + " Records replaced Successfully"));
    }

    // ─── Query Parameter Helpers ───────────────────────────────────────────────

    private object GetQueryParameters(string? msg = null)
    {
        var dict = new Dictionary<string, object>();
        if (Request.Query.ContainsKey("search")) dict["search"] = Request.Query["search"].ToString();
        if (Request.Query.ContainsKey("filterstatus")) dict["filterstatus"] = int.Parse(Request.Query["filterstatus"]);
        if (Request.Query.ContainsKey("activestatus")) dict["activestatus"] = int.Parse(Request.Query["activestatus"]);
        if (Request.Query.ContainsKey("cookingstatus")) dict["cookingstatus"] = int.Parse(Request.Query["cookingstatus"]);
        if (Request.Query.ContainsKey("taggrpstatus")) dict["taggrpstatus"] = int.Parse(Request.Query["taggrpstatus"]);
        if (Request.Query.ContainsKey("catid")) dict["catid"] = int.Parse(Request.Query["catid"]);
        if (Request.Query.ContainsKey("grpcatid")) dict["grpcatid"] = int.Parse(Request.Query["grpcatid"]);
        if (Request.Query.ContainsKey("searchtags")) dict["searchtags"] = Request.Query["searchtags"].ToString();
        if (Request.Query.ContainsKey("pageno")) dict["pageno"] = int.Parse(Request.Query["pageno"]);
        if (msg != null) dict["msg"] = msg;
        return dict;
    }
}
