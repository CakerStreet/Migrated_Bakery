using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Template Price Formula module.
/// Route: /managetemplateprice (legacy URL preserved exactly)
/// Migrated from managetemplateprice.aspx.
/// Manages baker salary settings and per-size price formula for templates.
/// </summary>
[Route("managetemplateprice")]
public class TemplatePriceController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public TemplatePriceController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long templateID = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managetemplateprice");

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Load baker salary settings
        BakerSalary? salary = null;
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT icingCostPerKG, AdvertisementCostinPer, 
                               CuttingNFillingBakerAvgSalaryPerhour, DecorationBakerAvgSalaryPerhour, 
                               IcingBakerAvgSalaryPerhour
                        FROM tbl_TemplateBakerSalary WHERE BakeryID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                salary = new BakerSalary
                {
                    IcingCostPerKg = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0),
                    AdvertCostPer = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                    CuttingSalary = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                    DecorationSalary = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    IcingSalary = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
                };
            }
        }

        // Load templates
        var templates = new List<TemplateItem>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
                        FROM tbl_specificationTemplate 
                        WHERE specificationTemplate_uid = @uid
                        ORDER BY specificationTemplate_Name";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", webstoreId);
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

        // Load price formula rows if template selected
        var priceRows = new List<PriceFormulaRow>();
        decimal discount = 0;
        int linkedProducts = 0;

        if (templateID > 0 && salary != null)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Get price formula rows
            var sql = @"SELECT f.TemplatePriceFormulaID, f.SizeID, s.SizeTitle, 
                               f.Spongecost, f.FillingCost, f.Icingpowderused, f.IcingPowdercost,
                               f.CuttingNFillingMins, f.IcingMins, f.DecorationMins, f.TotalMins,
                               f.BakerCost, f.BoardNBoxPrice, f.DecorationMaterialCost, f.TopperCost,
                               f.CakeBaseCost, f.CakeCost, f.AdvertisementCost, f.ProfitMargin, f.ProfitMarginPer
                        FROM tbl_TemplatePriceFormula f
                        INNER JOIN tbl_CakeSize s ON f.SizeID = s.SizeID
                        WHERE f.TemplateID = @id
                        ORDER BY s.DisplayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", templateID);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                priceRows.Add(new PriceFormulaRow
                {
                    FormulaId = reader.GetInt64(0),
                    SizeId = reader.GetInt32(1),
                    SizeTitle = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SpongeCost = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    FillingCost = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    IcingPowderUsed = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    IcingPowderCost = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    CuttingMins = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                    IcingMins = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8)),
                    DecorationMins = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9)),
                    TotalMins = reader.IsDBNull(10) ? 0 : Convert.ToInt32(reader.GetValue(10)),
                    BakerCost = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                    BoardBoxPrice = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                    DecorationMaterialCost = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                    TopperCost = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
                    CakeBaseCost = reader.IsDBNull(15) ? 0 : reader.GetDecimal(15),
                    FinalCost = reader.IsDBNull(16) ? 0 : reader.GetDecimal(16),
                    AdvertCost = reader.IsDBNull(17) ? 0 : reader.GetDecimal(17),
                    ProfitMargin = reader.IsDBNull(18) ? 0 : reader.GetDecimal(18),
                    ProfitMarginPer = reader.IsDBNull(19) ? 0 : reader.GetDecimal(19)
                });
            }

            // Get discount
            await using var conn2 = new SqlConnection(connectionString);
            await conn2.OpenAsync();
            var discSql = "SELECT templateSettings_discount FROM tbl_templateSettings WHERE templateSettings_templateID = @id";
            await using var discCmd = new SqlCommand(discSql, conn2);
            discCmd.Parameters.AddWithValue("@id", templateID);
            var discResult = await discCmd.ExecuteScalarAsync();
            if (discResult != null && discResult != DBNull.Value)
                discount = Convert.ToDecimal(discResult);

            // Get linked product count
            await using var conn3 = new SqlConnection(connectionString);
            await conn3.OpenAsync();
            var prdSql = "SELECT COUNT(1) FROM tbl_lnkprdtemplate WHERE lnkprdtemplate_templateID = @id";
            await using var prdCmd = new SqlCommand(prdSql, conn3);
            prdCmd.Parameters.AddWithValue("@id", templateID);
            linkedProducts = Convert.ToInt32(await prdCmd.ExecuteScalarAsync());
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

        ViewBag.Salary = salary;
        ViewBag.Templates = templates;
        ViewBag.SelectedTemplateId = templateID;
        ViewBag.PriceRows = priceRows;
        ViewBag.Discount = discount;
        ViewBag.LinkedProducts = linkedProducts;

        return View("~/Views/TemplatePrice/Index.cshtml");
    }

    [HttpPost("savesalary")]
    public async Task<IActionResult> SaveSalary(
        [FromForm] decimal icingCostPerKg, [FromForm] decimal advertCostPer,
        [FromForm] decimal cuttingSalary, [FromForm] decimal decorationSalary,
        [FromForm] decimal icingSalary)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Check if exists
            var checkSql = "SELECT COUNT(1) FROM tbl_TemplateBakerSalary WHERE BakeryID = @id";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@id", webstoreId);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                var sql = @"UPDATE tbl_TemplateBakerSalary SET
                    icingCostPerKG = @icing, AdvertisementCostinPer = @advert,
                    CuttingNFillingBakerAvgSalaryPerhour = @cutting,
                    DecorationBakerAvgSalaryPerhour = @decoration,
                    IcingBakerAvgSalaryPerhour = @icingSal
                    WHERE BakeryID = @id";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@icing", icingCostPerKg);
                cmd.Parameters.AddWithValue("@advert", advertCostPer);
                cmd.Parameters.AddWithValue("@cutting", cuttingSalary);
                cmd.Parameters.AddWithValue("@decoration", decorationSalary);
                cmd.Parameters.AddWithValue("@icingSal", icingSalary);
                cmd.Parameters.AddWithValue("@id", webstoreId);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                var sql = @"INSERT INTO tbl_TemplateBakerSalary 
                    (BakeryID, icingCostPerKG, AdvertisementCostinPer, CuttingNFillingBakerAvgSalaryPerhour, 
                     DecorationBakerAvgSalaryPerhour, IcingBakerAvgSalaryPerhour)
                    VALUES (@id, @icing, @advert, @cutting, @decoration, @icingSal)";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", webstoreId);
                cmd.Parameters.AddWithValue("@icing", icingCostPerKg);
                cmd.Parameters.AddWithValue("@advert", advertCostPer);
                cmd.Parameters.AddWithValue("@cutting", cuttingSalary);
                cmd.Parameters.AddWithValue("@decoration", decorationSalary);
                cmd.Parameters.AddWithValue("@icingSal", icingSalary);
                await cmd.ExecuteNonQueryAsync();
            }

            return Json(new { success = true, message = "Salary settings saved." });
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

    public class BakerSalary
    {
        public decimal IcingCostPerKg { get; set; }
        public decimal AdvertCostPer { get; set; }
        public decimal CuttingSalary { get; set; }
        public decimal DecorationSalary { get; set; }
        public decimal IcingSalary { get; set; }
    }

    public class PriceFormulaRow
    {
        public long FormulaId { get; set; }
        public int SizeId { get; set; }
        public string SizeTitle { get; set; } = "";
        public decimal SpongeCost { get; set; }
        public decimal FillingCost { get; set; }
        public decimal IcingPowderUsed { get; set; }
        public decimal IcingPowderCost { get; set; }
        public int CuttingMins { get; set; }
        public int IcingMins { get; set; }
        public int DecorationMins { get; set; }
        public int TotalMins { get; set; }
        public decimal BakerCost { get; set; }
        public decimal BoardBoxPrice { get; set; }
        public decimal DecorationMaterialCost { get; set; }
        public decimal TopperCost { get; set; }
        public decimal CakeBaseCost { get; set; }
        public decimal FinalCost { get; set; }
        public decimal AdvertCost { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal ProfitMarginPer { get; set; }
    }
}
