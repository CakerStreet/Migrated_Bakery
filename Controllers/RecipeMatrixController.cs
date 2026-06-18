using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Recipe Matrix module.
/// Route: /managereceipematrix
/// Migrated from manageReceipeMatrix.aspx.
/// </summary>
[Route("managereceipematrix")]
public class RecipeMatrixController : Controller
{
    private readonly RecipeMatrixService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public RecipeMatrixController(
        RecipeMatrixService service,
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
            return Redirect("/businesslogin?returl=/managereceipematrix");

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

        return View("~/Views/RecipeMatrix/Index.cshtml");
    }

    // ─── Get Categories (GET) ──────────────────────────────────────────────────

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var items = await _service.GetCategoriesAsync();
        return Json(new { success = true, data = items });
    }

    // ─── Get Matrix (GET) ──────────────────────────────────────────────────────

    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix([FromQuery] int bookId = 0, [FromQuery] int catId = 0)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var matrixData = await _service.GetRecipeMatrixAsync(bookId, catId);
        return Json(new { success = true, data = matrixData });
    }

    // ─── Remove Recipes (POST) ─────────────────────────────────────────────────

    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] RemoveRecipesRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.RecipeIds == null || request.RecipeIds.Count == 0)
            return Json(new { success = false, message = "No recipe IDs provided." });

        var success = await _service.RemoveRecipesAsync(request.RecipeIds);
        if (success)
            return Json(new { success = true, message = "Selected recipes removed successfully." });
        
        return Json(new { success = false, message = "Failed to remove selected recipes." });
    }

    // ─── Highlight Recipes by Ingredients (POST) ───────────────────────────────

    [HttpPost("highlight")]
    public async Task<IActionResult> Highlight([FromBody] HighlightRecipesRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.IngredientGroupIds == null || request.IngredientGroupIds.Count == 0)
            return Json(new { success = true, data = new List<long>() });

        var recipeIds = await _service.GetRecipeIdsByIngredientsAsync(request.IngredientGroupIds);
        return Json(new { success = true, data = recipeIds });
    }
}

public class RemoveRecipesRequest
{
    public List<long> RecipeIds { get; set; } = new();
}

public class HighlightRecipesRequest
{
    public List<long> IngredientGroupIds { get; set; } = new();
}
