using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Template Baking Cost module.
/// Route: /managetemplatebakerfee (legacy URL preserved exactly)
/// Migrated from managetemplatebakerfee.aspx.
/// Manages per-size baking time (filling/icing/decoration minutes) per template.
/// </summary>
[Route("managetemplatebakerfee")]
public class TemplateBakerFeeController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public TemplateBakerFeeController(
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
            return Redirect("/businesslogin?returl=/managetemplatebakerfee");

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

        // Load baking fee rows if template selected
        var feeRows = new List<BakingFeeRow>();
        if (templateID > 0)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = @"SELECT f.TemplateBakingfeeID, t.SizeID, s.SizeTitle, t.CakePrice,
                               ISNULL(f.Filling, 0) AS Filling, 
                               ISNULL(f.Icing, 0) AS Icing, 
                               ISNULL(f.Decoration, 0) AS Decoration
                        FROM tbl_CakePrice_template t
                        INNER JOIN tbl_CakeSize s ON t.SizeID = s.SizeID
                        LEFT JOIN tbl_TemplateBakingfee f ON t.templateid = f.TemplateID AND t.SizeID = f.SizeID
                        WHERE t.templateid = @templateId AND s.custid = @custId AND s.IsActive = 1
                        ORDER BY t.cakeprice_displayorder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@templateId", templateID);
            cmd.Parameters.AddWithValue("@custId", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                feeRows.Add(new BakingFeeRow
                {
                    FeeId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0)),
                    SizeId = reader.GetInt32(1),
                    SizeTitle = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CakePrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    Filling = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    Icing = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    Decoration = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6)
                });
            }
        }

        // Set ViewBag
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
        ViewBag.FeeRows = feeRows;

        return View("~/Views/TemplateBakerFee/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] long templateId, [FromForm] string? rows)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        if (templateId <= 0 || string.IsNullOrEmpty(rows))
            return Json(new { success = false, message = "No data to save." });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            // Parse rows: format "sizeId:filling:icing:decoration|sizeId:filling:icing:decoration|..."
            var rowParts = rows.Split('|', StringSplitOptions.RemoveEmptyEntries);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            foreach (var part in rowParts)
            {
                var fields = part.Split(':');
                if (fields.Length < 4) continue;

                int sizeId = int.Parse(fields[0]);
                decimal filling = decimal.Parse(fields[1]);
                decimal icing = decimal.Parse(fields[2]);
                decimal decoration = decimal.Parse(fields[3]);

                // Check if exists
                var checkSql = "SELECT COUNT(1) FROM tbl_TemplateBakingfee WHERE TemplateID = @tid AND SizeID = @sid";
                await using var checkCmd = new SqlCommand(checkSql, conn, tx);
                checkCmd.Parameters.AddWithValue("@tid", templateId);
                checkCmd.Parameters.AddWithValue("@sid", sizeId);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                if (exists)
                {
                    var updateSql = @"UPDATE tbl_TemplateBakingfee SET 
                        Filling = @filling, Icing = @icing, Decoration = @decoration,
                        ModifiedOn = GETDATE(), ModifiedBy = @user
                        WHERE TemplateID = @tid AND SizeID = @sid";
                    await using var updateCmd = new SqlCommand(updateSql, conn, tx);
                    updateCmd.Parameters.AddWithValue("@filling", filling);
                    updateCmd.Parameters.AddWithValue("@icing", icing);
                    updateCmd.Parameters.AddWithValue("@decoration", decoration);
                    updateCmd.Parameters.AddWithValue("@user", userName);
                    updateCmd.Parameters.AddWithValue("@tid", templateId);
                    updateCmd.Parameters.AddWithValue("@sid", sizeId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertSql = @"INSERT INTO tbl_TemplateBakingfee 
                        (TemplateID, SizeID, Filling, Icing, Decoration, ModifiedOn, ModifiedBy)
                        VALUES (@tid, @sid, @filling, @icing, @decoration, GETDATE(), @user)";
                    await using var insertCmd = new SqlCommand(insertSql, conn, tx);
                    insertCmd.Parameters.AddWithValue("@tid", templateId);
                    insertCmd.Parameters.AddWithValue("@sid", sizeId);
                    insertCmd.Parameters.AddWithValue("@filling", filling);
                    insertCmd.Parameters.AddWithValue("@icing", icing);
                    insertCmd.Parameters.AddWithValue("@decoration", decoration);
                    insertCmd.Parameters.AddWithValue("@user", userName);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            tx.Commit();
            return Json(new { success = true, message = "Template Baking Cost have been saved successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── View Models ──────────────────────────────────────────────────────────

    public class TemplateItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class BakingFeeRow
    {
        public long FeeId { get; set; }
        public int SizeId { get; set; }
        public string SizeTitle { get; set; } = "";
        public decimal CakePrice { get; set; }
        public decimal Filling { get; set; }
        public decimal Icing { get; set; }
        public decimal Decoration { get; set; }
    }
}
