using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Ingredient as Tag module (DIY Sandwich Bar).
/// Route: /manageIngredientastag (legacy URL preserved exactly)
/// Migrated from manageIngredientasTag.aspx.
/// Module 3 auth check.
/// No service file — inline SQL (simplified read-only + basic edit implementation).
/// </summary>
[Route("manageIngredientastag")]
public class IngredientTagController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public IngredientTagController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int catid = 0,
        [FromQuery] int gnid = 0,
        [FromQuery] string? search = null,
        [FromQuery] int activestatus = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/manageIngredientastag");

        // Module 3 auth check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 3);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        // Load categories
        var categories = new List<CategoryItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = "SELECT category_ID, category_name FROM tbl_receipeIngredient_category ORDER BY category_displayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryItem
                {
                    Id = reader.GetInt64(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load GN Sizes
        var gnSizes = new List<GnSizeItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = "SELECT GnSize_ID, GnSize_name FROM tbl_receipeIngredient_GnSize";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                gnSizes.Add(new GnSizeItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load ingredient tags with filters
        var ingredients = new List<IngredientTagItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            var sql = @"SELECT g.receipeBookIngredientGrp_ID, g.receipeBookIngredientGrp_ingredient, 
                               g.receipeBookIngredientGrp_marking, g.receipeBookIngredientGrp_active,
                               g.receipeBookIngredientGrp_catID, g.receipeBookIngredientGrp_GNID,
                               c.Category_Name
                        FROM tbl_receipeBookIngredientGrp g
                        LEFT JOIN tbl_receipeIngredient_category c ON g.receipeBookIngredientGrp_catID = c.category_ID
                        WHERE 1=1";

            var parameters = new List<SqlParameter>();

            if (catid > 0)
            {
                sql += " AND g.receipeBookIngredientGrp_catID = @catId";
                parameters.Add(new SqlParameter("@catId", catid));
            }
            if (gnid > 0)
            {
                sql += " AND g.receipeBookIngredientGrp_GNID = @gnId";
                parameters.Add(new SqlParameter("@gnId", gnid));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += " AND g.receipeBookIngredientGrp_ingredient LIKE '%' + @search + '%'";
                parameters.Add(new SqlParameter("@search", search.Trim()));
            }
            if (activestatus == 1)
            {
                sql += " AND g.receipeBookIngredientGrp_active = 1";
            }
            else if (activestatus == 2)
            {
                sql += " AND g.receipeBookIngredientGrp_active = 0";
            }

            sql += " ORDER BY c.category_displayOrder, g.receipeBookIngredientGrp_ingredient";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ingredients.Add(new IngredientTagItem
                {
                    Id = reader.GetInt64(0),
                    Ingredient = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Marking = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Active = !reader.IsDBNull(3) && reader.GetBoolean(3),
                    CatId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    GnId = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                    CategoryName = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }
        }

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

        ViewBag.Categories = categories;
        ViewBag.GnSizes = gnSizes;
        ViewBag.Ingredients = ingredients;
        ViewBag.SelectedCatId = catid;
        ViewBag.SelectedGnId = gnid;
        ViewBag.Search = search ?? "";
        ViewBag.ActiveStatus = activestatus;

        return View("~/Views/IngredientTag/Index.cshtml");
    }

    // ─── Save (POST) ──────────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] long id,
        [FromForm] string? ingredient,
        [FromForm] string? marking,
        [FromForm] long catId,
        [FromForm] int gnId,
        [FromForm] bool active)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        // Module 3 auth check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 3);
            if (!hasAccess)
                return Json(new { success = false, message = "Access denied" });
        }

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_receipeBookIngredientGrp SET
                            receipeBookIngredientGrp_ingredient = @ingredient,
                            receipeBookIngredientGrp_marking = @marking,
                            receipeBookIngredientGrp_catID = @catId,
                            receipeBookIngredientGrp_GNID = @gnId,
                            receipeBookIngredientGrp_active = @active,
                            receipeBookIngredientGrp_createdOn = GETDATE()
                        WHERE receipeBookIngredientGrp_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ingredient", ingredient ?? "");
            cmd.Parameters.AddWithValue("@marking", marking ?? "");
            cmd.Parameters.AddWithValue("@catId", catId);
            cmd.Parameters.AddWithValue("@gnId", gnId);
            cmd.Parameters.AddWithValue("@active", active);
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
                return Json(new { success = true, message = "Ingredient tag saved successfully." });
            else
                return Json(new { success = false, message = "Record not found." });
        }
        catch
        {
            return Json(new { success = false, message = "Failed to save ingredient tag." });
        }
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

    // ─── View Models ──────────────────────────────────────────────────────────

    public class CategoryItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class GnSizeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class IngredientTagItem
    {
        public long Id { get; set; }
        public string Ingredient { get; set; } = "";
        public string Marking { get; set; } = "";
        public bool Active { get; set; }
        public long CatId { get; set; }
        public int GnId { get; set; }
        public string CategoryName { get; set; } = "";
    }
}
