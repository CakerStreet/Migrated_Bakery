using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("linkbakerswithcaketemplate")]
public class LinkBakersController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public LinkBakersController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long bakerId = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect($"/businesslogin?returl=/linkbakerswithcaketemplate");

        if (userType == "3")
            return Redirect("/mywebstore");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Bind Bakers dropdown
        var bakers = new List<BakerItem>();
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT customer_ID, customer_Name 
                        FROM tbl_bakeryuser 
                        WHERE customer_webshopID = @webshopId 
                          AND customer_isActive = 1 
                          AND customer_type = 3 
                        ORDER BY customer_Name";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@webshopId", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                bakers.Add(new BakerItem
                {
                    Id = reader.GetInt64(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        // Get template types if bakerId is selected
        var templateGroups = new List<TemplateTypeGroup>();
        if (bakerId > 0)
        {
            // First get the product types present for this webshop's templates
            var prdTypes = new List<int>();
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"SELECT specificationTemplate_prdtype 
                            FROM tbl_specificationTemplate 
                            WHERE specificationTemplate_uid = @webstoreId 
                            GROUP BY specificationTemplate_prdtype";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    prdTypes.Add(Convert.ToInt32(reader.GetValue(0)));
                }
            }

            foreach (var prdType in prdTypes)
            {
                var groupName = prdType switch
                {
                    0 => "Cake Templates",
                    1 => "Cup Cake Templates",
                    _ => "Party Accessory Templates"
                };

                var templates = new List<TemplateLinkItem>();
                await using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();
                    var sql = @"SELECT s.specificationTemplate_ID, s.specificationTemplate_Name, 
                                       l.lnktemplate2bakers_ID, 
                                       CASE WHEN l.lnktemplate2bakers_ID IS NULL THEN 0 ELSE 1 END AS IsChecked
                                FROM tbl_specificationTemplate s
                                LEFT OUTER JOIN tbl_lnktemplate2bakers l 
                                  ON s.specificationTemplate_ID = l.lnktemplate2bakers_template 
                                 AND l.lnktemplate2bakers_bakerID = @bakerId
                                WHERE s.specificationTemplate_prdtype = @prdType 
                                  AND s.specificationTemplate_uid = @webstoreId
                                  AND s.specificationTemplate_ID IN (SELECT lnkprdtemplate_templateID FROM tbl_lnkprdtemplate)
                                ORDER BY s.specificationTemplate_Name";
                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@bakerId", bakerId);
                    cmd.Parameters.AddWithValue("@prdType", prdType);
                    cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        templates.Add(new TemplateLinkItem
                        {
                            TemplateId = reader.GetInt64(0),
                            TemplateName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LinkBakersId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                            IsChecked = reader.GetInt32(3) == 1
                        });
                    }
                }

                if (templates.Count > 0)
                {
                    templateGroups.Add(new TemplateTypeGroup
                    {
                        PrdType = prdType,
                        TypeName = groupName,
                        Templates = templates
                    });
                }
            }
        }

        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.Bakers = bakers;
        ViewBag.SelectedBakerId = bakerId;
        ViewBag.TemplateGroups = templateGroups;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        if (TempData["SaveMsg"] != null)
        {
            ViewBag.SaveMsg = TempData["SaveMsg"].ToString();
        }

        return View("~/Views/LinkBakers/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] long ddlBusiness, [FromForm] List<long> chkTemplate)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0 || userType == "3")
            return Redirect("/businesslogin");

        if (ddlBusiness == 0)
        {
            TempData["SaveMsg"] = "Please select a baker.";
            return Redirect("/linkbakerswithcaketemplate");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Fetch all templates for this webshop that are active
        var allTemplates = new List<long>();
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT specificationTemplate_ID 
                        FROM tbl_specificationTemplate 
                        WHERE specificationTemplate_uid = @webstoreId
                          AND specificationTemplate_ID IN (SELECT lnkprdtemplate_templateID FROM tbl_lnkprdtemplate)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                allTemplates.Add(reader.GetInt64(0));
            }
        }

        // We will execute the logic in a transaction
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            try
            {
                // Delete unselected/unchecked links for templates belonging to this webstore
                var checkedTemplates = chkTemplate ?? new List<long>();
                var templatesToUnlink = allTemplates.Except(checkedTemplates).ToList();

                if (templatesToUnlink.Count > 0)
                {
                    var deleteSql = $@"DELETE FROM tbl_lnktemplate2bakers 
                                       WHERE lnktemplate2bakers_bakerID = @bakerId 
                                         AND lnktemplate2bakers_template IN ({string.Join(",", templatesToUnlink)})";
                    await using var cmd = new SqlCommand(deleteSql, conn, tx);
                    cmd.Parameters.AddWithValue("@bakerId", ddlBusiness);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Add links for checked templates if they don't already exist
                foreach (var tempId in checkedTemplates)
                {
                    // Check existence
                    var existSql = @"SELECT COUNT(1) FROM tbl_lnktemplate2bakers 
                                     WHERE lnktemplate2bakers_bakerID = @bakerId 
                                       AND lnktemplate2bakers_template = @tempId";
                    await using (var existCmd = new SqlCommand(existSql, conn, tx))
                    {
                        existCmd.Parameters.AddWithValue("@bakerId", ddlBusiness);
                        existCmd.Parameters.AddWithValue("@tempId", tempId);
                        var exists = Convert.ToInt32(await existCmd.ExecuteScalarAsync()) > 0;

                        if (!exists)
                        {
                            var insertSql = @"INSERT INTO tbl_lnktemplate2bakers 
                                              (lnktemplate2bakers_bakerID, lnktemplate2bakers_template, lnktemplate2bakers_modifiedOn) 
                                              VALUES (@bakerId, @tempId, GETDATE())";
                            await using var insCmd = new SqlCommand(insertSql, conn, tx);
                            insCmd.Parameters.AddWithValue("@bakerId", ddlBusiness);
                            insCmd.Parameters.AddWithValue("@tempId", tempId);
                            await insCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                tx.Commit();
                TempData["SaveMsg"] = "Records saved successfully";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                TempData["SaveMsg"] = "Error saving records: " + ex.Message;
            }
        }

        return Redirect($"/linkbakerswithcaketemplate?bakerId={ddlBusiness}");
    }

    public class BakerItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class TemplateLinkItem
    {
        public long TemplateId { get; set; }
        public string TemplateName { get; set; } = "";
        public long LinkBakersId { get; set; }
        public bool IsChecked { get; set; }
    }

    public class TemplateTypeGroup
    {
        public int PrdType { get; set; }
        public string TypeName { get; set; } = "";
        public List<TemplateLinkItem> Templates { get; set; } = new();
    }
}
