using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Bakery Ingredient module.
/// Route: /managebakeryingredient
/// Migrated from managebakeryingredient.aspx.
/// Module 9 permission check.
/// </summary>
[Route("managebakeryingredient")]
public class BakeryIngredientController : Controller
{
    private readonly BakeryIngredientService _ingredientService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakeryIngredientController(
        BakeryIngredientService ingredientService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _ingredientService = ingredientService;
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

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managebakeryingredient");

        // Module 9 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 9);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var items = await _ingredientService.GetAllAsync(wid);

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
        ViewBag.UserType = userType;

        return View("~/Views/BakeryIngredient/Index.cshtml");
    }

    // ─── Save Single Item (POST) ──────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] long id, [FromForm] string title, [FromForm] string unit, [FromForm] decimal qty, [FromForm] decimal minQty)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var item = new BakeryIngredientItem
        {
            BakeryIngredient_ID = id,
            BakeryIngredient_title = title,
            BakeryIngredient_Unit = unit,
            BakeryIngredient_qty = qty,
            BakeryIngredient_minQty = minQty
        };

        var success = await _ingredientService.UpdateAsync(item, userId);
        return Json(new { success, message = success ? "Bakery Ingredient(s) has been updated successfully!" : "Failed to update." });
    }

    // ─── Save All (POST) ──────────────────────────────────────────────────────

    [HttpPost("saveall")]
    public async Task<IActionResult> SaveAll([FromBody] List<BakeryIngredientItem> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var success = await _ingredientService.BulkSaveAsync(items, userId);
        return Json(new { success, message = success ? "Records saved successfully!" : "Failed to save." });
    }

    // ─── Add New (POST) ───────────────────────────────────────────────────────

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromForm] string title, [FromForm] string unit, [FromForm] decimal qty, [FromForm] decimal minQty)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var item = new BakeryIngredientItem
        {
            BakeryIngredient_title = title,
            BakeryIngredient_Unit = unit,
            BakeryIngredient_qty = qty,
            BakeryIngredient_minQty = minQty
        };

        var success = await _ingredientService.AddAsync(item, wid, userId);
        if (!success)
            return Json(new { success = false, message = "Ingredient already exists!" });

        return Json(new { success = true, message = "Bakery Ingredient has been added successfully!" });
    }

    // ─── Delete (POST) ────────────────────────────────────────────────────────

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var success = await _ingredientService.DeleteAsync(id);
        return Json(new { success, message = success ? "Bakery Ingredient has been deleted successfully!" : "Failed to delete." });
    }

    // ─── Module Access Check ──────────────────────────────────────────────────

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
