using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Cake Templates module.
/// Route: /editCaketemplate (legacy URL preserved exactly)
/// Migrated from addnewcaketemplate.aspx.
/// Module 3 auth check.
/// </summary>
[Route("editCaketemplate")]
[Route("addnewcaketemplate")]
[Route("addnewcaketemplate.aspx")]
public class CakeTemplateController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public CakeTemplateController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] int cakefor = 0, [FromQuery] long templateID = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/editCaketemplate");

        // Module 3 auth check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 3);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Load saved templates for this webstore filtered by cakefor (product type)
        var templates = new List<TemplateItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
                        FROM tbl_specificationTemplate 
                        WHERE specificationTemplate_uid = @webstoreId 
                          AND specificationTemplate_prdtype = @cakefor
                        ORDER BY specificationTemplate_Name";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            cmd.Parameters.AddWithValue("@cakefor", cakefor);
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

        // Load cake shapes
        var shapes = new List<LookupItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = "SELECT CakeShapeID, CakeShapeTitle FROM tbl_CakeShape WHERE IsActive = 1 ORDER BY DisplayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shapes.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load cake types (default + bakery-specific)
        var cakeTypes = new List<LookupItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT CakeTypeID, CakeTypeTitle FROM tbl_CakeType 
                        WHERE IsActive = 1 AND (custid = @custId OR Isdefault = 1) 
                        ORDER BY DisplayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@custId", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                cakeTypes.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load sizes (bakery-specific or defaults)
        var sizes = new List<SizeItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            // Check if bakery has custom sizes
            var countSql = "SELECT COUNT(1) FROM tbl_CakeSize WHERE custid = @custId AND IsActive = 1";
            await using var countCmd = new SqlCommand(countSql, conn);
            countCmd.Parameters.AddWithValue("@custId", webstoreId);
            var hasCustom = Convert.ToInt32(await countCmd.ExecuteScalarAsync()) > 0;

            string sql;
            if (hasCustom)
                sql = "SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE IsActive = 1 AND custid = @custId ORDER BY DisplayOrder";
            else
                sql = "SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE IsActive = 1 AND Isdefault = 1 ORDER BY DisplayOrder";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@custId", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sizes.Add(new SizeItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Load selected template detail if templateID provided
        TemplateDetail? selectedTemplate = null;
        if (templateID > 0)
        {
            selectedTemplate = await LoadTemplateDetailAsync(connectionString, templateID, webstoreId);
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
        ViewBag.Shapes = shapes;
        ViewBag.CakeTypes = cakeTypes;
        ViewBag.Sizes = sizes;
        ViewBag.CakeFor = cakefor;
        ViewBag.SelectedTemplateId = templateID;
        ViewBag.SelectedTemplate = selectedTemplate;
        ViewBag.IsAdmin = (userType == "1" || userType == "2");

        return View("~/Views/CakeTemplate/Index.cshtml");
    }

    // ─── Save Template (POST) ─────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] CakeTemplateSaveModel model)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 3);
            if (!hasAccess)
                return Json(new { success = false, message = "Access denied" });
        }

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            long templateId = model.TemplateId;

            if (templateId == 0)
            {
                // Insert new template
                var insertSql = @"INSERT INTO tbl_specificationTemplate 
                    (specificationTemplate_Name, specificationTemplate_uid, specificationTemplate_prdtype, 
                     specificationTemplate_shapeID, specificationTemplate_typeID, specificationTemplate_preparationDay,
                     specificationTemplate_collection, specificationTemplate_delivery, specificationTemplate_deliverymiles,
                     specificationTemplate_postaldelivery, specificationTemplate_createdOn)
                    VALUES (@name, @uid, @prdtype, @shapeId, @typeId, @prepDay, @collection, @delivery, @miles, @postal, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                await using var cmd = new SqlCommand(insertSql, conn, tx);
                cmd.Parameters.AddWithValue("@name", model.TemplateName ?? "");
                cmd.Parameters.AddWithValue("@uid", webstoreId);
                cmd.Parameters.AddWithValue("@prdtype", model.CakeFor);
                cmd.Parameters.AddWithValue("@shapeId", model.ShapeId);
                cmd.Parameters.AddWithValue("@typeId", model.TypeId);
                cmd.Parameters.AddWithValue("@prepDay", model.PreparationDay);
                cmd.Parameters.AddWithValue("@collection", model.Collection);
                cmd.Parameters.AddWithValue("@delivery", model.Delivery);
                cmd.Parameters.AddWithValue("@miles", model.DeliveryMiles ?? "");
                cmd.Parameters.AddWithValue("@postal", model.PostalDelivery);

                var result = await cmd.ExecuteScalarAsync();
                templateId = Convert.ToInt64(result);
            }
            else
            {
                // Update existing template
                var updateSql = @"UPDATE tbl_specificationTemplate SET
                    specificationTemplate_Name = @name,
                    specificationTemplate_shapeID = @shapeId,
                    specificationTemplate_typeID = @typeId,
                    specificationTemplate_preparationDay = @prepDay,
                    specificationTemplate_collection = @collection,
                    specificationTemplate_delivery = @delivery,
                    specificationTemplate_deliverymiles = @miles,
                    specificationTemplate_postaldelivery = @postal
                    WHERE specificationTemplate_ID = @id AND specificationTemplate_uid = @uid";

                await using var cmd = new SqlCommand(updateSql, conn, tx);
                cmd.Parameters.AddWithValue("@name", model.TemplateName ?? "");
                cmd.Parameters.AddWithValue("@shapeId", model.ShapeId);
                cmd.Parameters.AddWithValue("@typeId", model.TypeId);
                cmd.Parameters.AddWithValue("@prepDay", model.PreparationDay);
                cmd.Parameters.AddWithValue("@collection", model.Collection);
                cmd.Parameters.AddWithValue("@delivery", model.Delivery);
                cmd.Parameters.AddWithValue("@miles", model.DeliveryMiles ?? "");
                cmd.Parameters.AddWithValue("@postal", model.PostalDelivery);
                cmd.Parameters.AddWithValue("@id", templateId);
                cmd.Parameters.AddWithValue("@uid", webstoreId);

                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return Json(new { success = true, message = "Template saved successfully.", templateId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save template: " + ex.Message });
        }
    }

    // ─── Delete Template (POST) ───────────────────────────────────────────────

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromForm] long templateId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        // Only admin can delete
        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Access denied" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            // Check if template has linked products
            var checkSql = "SELECT COUNT(1) FROM tbl_lnkprdtemplate WHERE lnkprdtemplate_templateID = @id";
            await using var checkCmd = new SqlCommand(checkSql, conn, tx);
            checkCmd.Parameters.AddWithValue("@id", templateId);
            var linkedCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (linkedCount > 0)
            {
                tx.Rollback();
                return Json(new { success = false, message = $"Cannot delete: template has {linkedCount} linked product(s). Remove product links first." });
            }

            // Delete related records (same order as legacy)
            var tables = new[]
            {
                ("tbl_specificationTemplate", "specificationTemplate_ID = @id"),
                ("tbl_lnkflvTemplate", "lnkflvTemplate_tempId = @id"),
                ("tbl_CakePrice_template", "templateid = @id"),
                ("tbl_lnkflvTemplateExclude", "flavourExclude_tempID = @id"),
                ("tbl_templateSettings", "templateSettings_templateID = @id"),
                ("tbl_TemplatePriceFormula", "TemplateID = @id")
            };

            foreach (var (table, where) in tables)
            {
                var delSql = $"DELETE FROM {table} WHERE {where}";
                await using var delCmd = new SqlCommand(delSql, conn, tx);
                delCmd.Parameters.AddWithValue("@id", templateId);
                await delCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return Json(new { success = true, message = "Template deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to delete template: " + ex.Message });
        }
    }

    // ─── Load Template Detail ─────────────────────────────────────────────────

    private async Task<TemplateDetail?> LoadTemplateDetailAsync(string connectionString, long templateId, long webstoreId)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name, 
                           specificationTemplate_prdtype, specificationTemplate_shapeID,
                           specificationTemplate_typeID, specificationTemplate_preparationDay,
                           specificationTemplate_collection, specificationTemplate_delivery,
                           specificationTemplate_deliverymiles, specificationTemplate_postaldelivery
                    FROM tbl_specificationTemplate 
                    WHERE specificationTemplate_ID = @id AND specificationTemplate_uid = @uid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@uid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new TemplateDetail
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                PrdType = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                ShapeId = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                TypeId = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                PreparationDay = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                Collection = !reader.IsDBNull(6) && Convert.ToBoolean(reader.GetValue(6)),
                Delivery = !reader.IsDBNull(7) && Convert.ToBoolean(reader.GetValue(7)),
                DeliveryMiles = reader.IsDBNull(8) ? "" : reader.GetValue(8).ToString() ?? "",
                PostalDelivery = !reader.IsDBNull(9) && Convert.ToBoolean(reader.GetValue(9))
            };
        }
        return null;
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

    public class TemplateItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class SizeItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public class TemplateDetail
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int PrdType { get; set; }
        public int ShapeId { get; set; }
        public int TypeId { get; set; }
        public int PreparationDay { get; set; }
        public bool Collection { get; set; }
        public bool Delivery { get; set; }
        public string DeliveryMiles { get; set; } = "";
        public bool PostalDelivery { get; set; }
    }

    public class CakeTemplateSaveModel
    {
        public long TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public int CakeFor { get; set; }
        public int ShapeId { get; set; }
        public int TypeId { get; set; }
        public int PreparationDay { get; set; }
        public bool Collection { get; set; }
        public bool Delivery { get; set; }
        public string? DeliveryMiles { get; set; }
        public bool PostalDelivery { get; set; }
    }
}
