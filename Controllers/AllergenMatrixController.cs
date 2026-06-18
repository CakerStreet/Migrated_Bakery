using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Allergen Matrix module.
/// Route: /manageallergenmatrix
/// Migrated from manageallergenmatrix.aspx.
/// Auth check only (no specific module ID).
/// </summary>
[Route("manageallergenmatrix")]
public class AllergenMatrixController : Controller
{
    private readonly AllergenMatrixService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public AllergenMatrixController(
        AllergenMatrixService service,
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
            return Redirect("/businesslogin?returl=/manageallergenmatrix");

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

        return View("~/Views/AllergenMatrix/Index.cshtml");
    }

    // ─── Get Cake Types (GET) ──────────────────────────────────────────────────

    [HttpGet("caketypes")]
    public async Task<IActionResult> GetCakeTypes()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var items = await _service.GetCakeTypesAsync();
        return Json(new { success = true, data = items });
    }

    // ─── Get Flavours (GET) ────────────────────────────────────────────────────

    [HttpGet("flavours")]
    public async Task<IActionResult> GetFlavours([FromQuery] string parentId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (string.IsNullOrEmpty(parentId) || !long.TryParse(parentId, out var parentIdLong))
            return Json(new { success = false, message = "Valid parentId is required" });

        var items = await _service.GetFlavoursAsync(parentIdLong);
        return Json(new { success = true, data = items });
    }

    // ─── Get Matrix (GET) ──────────────────────────────────────────────────────

    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix(
        [FromQuery] long cakeTypeId,
        [FromQuery] long dietaryId = 0,
        [FromQuery] long spongeId = 0,
        [FromQuery] long fillingId = 0)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (cakeTypeId == 0)
            return Json(new { success = false, message = "cakeTypeId is required" });

        var rows = await _service.GetMatrixAsync(cakeTypeId, dietaryId, spongeId, fillingId);
        return Json(new { success = true, data = rows });
    }

    // ─── Save Matrix (POST) ───────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] AllergenMatrixSaveRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (request == null || request.Items == null || request.Items.Count == 0)
            return Json(new { success = false, message = "No items to save." });

        var result = await _service.SaveMatrixAsync(request.Items, request.DeletedIds ?? "");
        if (result)
            return Json(new { success = true, message = "Ingredient(s) detail saved successfully." });
        else
            return Json(new { success = false, message = "Failed to save allergen matrix." });
    }
}
