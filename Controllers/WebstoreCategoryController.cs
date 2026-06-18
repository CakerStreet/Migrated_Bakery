using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Webstore Category module.
/// Route: /managewebstorecategory (legacy URL preserved exactly)
/// Migrated from managewebstorecategory.aspx.
/// Hierarchical category CRUD (3 levels: main → sub → sub-sub) on tbl_webstoreCat.
/// </summary>
[Route("managewebstorecategory")]
public class WebstoreCategoryController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public WebstoreCategoryController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? returl = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managewebstorecategory");

        // Staff type 3 cannot access
        if (userType == "3")
            return Redirect("/mywebstore");

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);

        // Load category tree (3 levels)
        var categories = new List<CategoryNode>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT webstoreCat_ID, webstoreCat_categoryName, webstoreCat_parentID, 
                               webstoreCat_displayOrder, webstoreCat_CatURL
                        FROM tbl_webstoreCat 
                        WHERE webstore_ID = @wid AND webstoreCat_isDeleted = 0 AND webstoreCat_isActive = 1
                        ORDER BY webstoreCat_displayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            await using var reader = await cmd.ExecuteReaderAsync();

            var allItems = new List<CategoryNode>();
            while (await reader.ReadAsync())
            {
                allItems.Add(new CategoryNode
                {
                    Id = Convert.ToInt64(reader.GetValue(0)),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ParentId = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2)),
                    DisplayOrder = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                    CatUrl = reader.IsDBNull(4) ? "" : reader.GetString(4)
                });
            }

            // Build tree
            categories = allItems.Where(c => c.ParentId == 0).OrderBy(c => c.DisplayOrder).ToList();
            foreach (var cat in categories)
            {
                cat.Children = allItems.Where(c => c.ParentId == cat.Id).OrderBy(c => c.DisplayOrder).ToList();
                foreach (var sub in cat.Children)
                {
                    sub.Children = allItems.Where(c => c.ParentId == sub.Id).OrderBy(c => c.DisplayOrder).ToList();
                }
            }
        }

        // ViewBag for layout
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
        ViewBag.ReturnUrl = returl ?? "/mywebstore";

        return View("~/Views/WebstoreCategory/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] List<CategorySaveItem> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (items == null || items.Count == 0)
            return Json(new { success = false, message = "Please enter at least one category name." });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            int displayOrder = 0;
            foreach (var item in items)
            {
                displayOrder++;
                await UpsertCategoryAsync(conn, wid, item, 0, displayOrder);
            }

            return Json(new { success = true, message = "Categories saved successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save: " + ex.Message });
        }
    }

    private async Task UpsertCategoryAsync(SqlConnection conn, long webstoreId, CategorySaveItem item, long parentId, int displayOrder)
    {
        string catName = FormatCategoryName(item.Name ?? "");
        if (string.IsNullOrWhiteSpace(catName)) return;

        string catUrl = FormatCategoryUrl(catName);
        long catId = item.Id;

        if (catId > 0)
        {
            // Update existing
            if (item.IsDeleted)
            {
                var delSql = "UPDATE tbl_webstoreCat SET webstoreCat_isDeleted = 1, webstoreCat_modifiedOn = GETDATE() WHERE webstoreCat_ID = @id AND webstore_ID = @wid";
                await using var delCmd = new SqlCommand(delSql, conn);
                delCmd.Parameters.AddWithValue("@id", catId);
                delCmd.Parameters.AddWithValue("@wid", webstoreId);
                await delCmd.ExecuteNonQueryAsync();
                return; // Don't process children of deleted items
            }
            else
            {
                var updSql = @"UPDATE tbl_webstoreCat SET 
                    webstoreCat_categoryName = @name, webstoreCat_CatURL = @url, 
                    webstoreCat_ShowURL = @showUrl, webstoreCat_displayOrder = @order,
                    webstoreCat_parentID = @parentId, webstoreCat_modifiedOn = GETDATE()
                    WHERE webstoreCat_ID = @id AND webstore_ID = @wid";
                await using var updCmd = new SqlCommand(updSql, conn);
                updCmd.Parameters.AddWithValue("@name", catName);
                updCmd.Parameters.AddWithValue("@url", catUrl);
                updCmd.Parameters.AddWithValue("@showUrl", catUrl);
                updCmd.Parameters.AddWithValue("@order", displayOrder);
                updCmd.Parameters.AddWithValue("@parentId", parentId);
                updCmd.Parameters.AddWithValue("@id", catId);
                updCmd.Parameters.AddWithValue("@wid", webstoreId);
                await updCmd.ExecuteNonQueryAsync();
            }
        }
        else
        {
            // Insert new
            var insSql = @"INSERT INTO tbl_webstoreCat 
                (webstore_ID, webstoreCat_categoryName, webstoreCat_CatURL, webstoreCat_ShowURL,
                 webstoreCat_parentID, webstoreCat_displayOrder, webstoreCat_isActive, webstoreCat_isDeleted,
                 webstoreCat_CreatedOn, webstoreCat_modifiedOn)
                VALUES (@wid, @name, @url, @showUrl, @parentId, @order, 1, 0, GETDATE(), GETDATE());
                SELECT SCOPE_IDENTITY();";
            await using var insCmd = new SqlCommand(insSql, conn);
            insCmd.Parameters.AddWithValue("@wid", webstoreId);
            insCmd.Parameters.AddWithValue("@name", catName);
            insCmd.Parameters.AddWithValue("@url", catUrl);
            insCmd.Parameters.AddWithValue("@showUrl", catUrl);
            insCmd.Parameters.AddWithValue("@parentId", parentId);
            insCmd.Parameters.AddWithValue("@order", displayOrder);
            var newId = await insCmd.ExecuteScalarAsync();
            catId = Convert.ToInt64(newId);
        }

        // Process children recursively
        if (item.Children != null)
        {
            int childOrder = 0;
            foreach (var child in item.Children)
            {
                childOrder++;
                await UpsertCategoryAsync(conn, webstoreId, child, catId, childOrder);
            }
        }
    }

    private static string FormatCategoryName(string name)
    {
        return name.Trim();
    }

    private static string FormatCategoryUrl(string name)
    {
        var url = name.ToLower().Trim();
        url = Regex.Replace(url, @"[^a-z0-9\s-]", "");
        url = Regex.Replace(url, @"\s+", "-");
        url = Regex.Replace(url, @"-+", "-");
        return url.Trim('-');
    }

    // ─── View Models ──────────────────────────────────────────────────────────

    public class CategoryNode
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public long ParentId { get; set; }
        public int DisplayOrder { get; set; }
        public string CatUrl { get; set; } = "";
        public List<CategoryNode> Children { get; set; } = new();
    }

    public class CategorySaveItem
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public bool IsDeleted { get; set; }
        public List<CategorySaveItem>? Children { get; set; }
    }
}
