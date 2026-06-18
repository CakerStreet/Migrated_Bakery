using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Template Specifications module.
/// Route: /managespecificationbytemplate (legacy URL preserved exactly)
/// Migrated from managespecificationbytemplate.aspx.
/// Manages ingredient/allergen/advice/delivery/storage specs per template.
/// </summary>
[Route("managespecificationbytemplate")]
public class TemplateSpecController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public TemplateSpecController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] int cakefor = -1, [FromQuery] long templateID = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managespecificationbytemplate");

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // If templateID provided but no cakefor, look up the template's prdtype
        if (templateID > 0 && cakefor == -1)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = "SELECT specificationTemplate_prdtype FROM tbl_specificationTemplate WHERE specificationTemplate_ID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", templateID);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null) cakefor = Convert.ToInt32(result);
        }

        // Load templates for selected product type
        var templates = new List<TemplateItem>();
        if (cakefor >= 0)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
                        FROM tbl_specificationTemplate 
                        WHERE specificationTemplate_uid = @uid AND specificationTemplate_prdtype = @prdtype
                        ORDER BY specificationTemplate_displayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", webstoreId);
            cmd.Parameters.AddWithValue("@prdtype", cakefor);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                templates.Add(new TemplateItem
                {
                    Id = reader.GetInt64(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load specification values if template selected
        var specs = new Dictionary<int, string>();
        if (templateID > 0)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = "SELECT typeID, Value FROM tbl_templateSpecification WHERE template_ID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", templateID);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var typeId = reader.GetInt32(0);
                var value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                specs[typeId] = value;
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

        ViewBag.Templates = templates;
        ViewBag.CakeFor = cakefor;
        ViewBag.SelectedTemplateId = templateID;
        ViewBag.Specs = specs;

        return View("~/Views/TemplateSpec/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] long templateId,
        [FromForm] string? ingredients, [FromForm] string? allergens,
        [FromForm] string? advice, [FromForm] string? deliveryDetails,
        [FromForm] string? storage)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (templateId <= 0)
            return Json(new { success = false, message = "No template selected." });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            // Delete existing specs for this template
            var delSql = "DELETE FROM tbl_templateSpecification WHERE template_ID = @id";
            await using (var delCmd = new SqlCommand(delSql, conn, tx))
            {
                delCmd.Parameters.AddWithValue("@id", templateId);
                await delCmd.ExecuteNonQueryAsync();
            }

            // Insert new specs
            var specValues = new Dictionary<int, string?>
            {
                { 1, ingredients },
                { 2, allergens },
                { 3, advice },
                { 4, deliveryDetails },
                { 5, storage }
            };

            foreach (var kvp in specValues)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    var insertSql = "INSERT INTO tbl_templateSpecification (template_ID, typeID, Value) VALUES (@id, @typeId, @val)";
                    await using var insertCmd = new SqlCommand(insertSql, conn, tx);
                    insertCmd.Parameters.AddWithValue("@id", templateId);
                    insertCmd.Parameters.AddWithValue("@typeId", kvp.Key);
                    insertCmd.Parameters.AddWithValue("@val", kvp.Value.Trim());
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            tx.Commit();
            return Json(new { success = true, message = "Ingredients template details have been saved successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save: " + ex.Message });
        }
    }

    public class TemplateItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }
}
