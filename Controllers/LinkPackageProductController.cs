using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("linkpackage2prd")]
public class LinkPackageProductController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public LinkPackageProductController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long templateId = 0, [FromQuery] int sizeId = 0, [FromQuery] long packageId = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/linkpackage2prd");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Bind templates
        var templates = new List<TemplateItem>();
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
                        FROM tbl_specificationTemplate 
                        WHERE specificationTemplate_uid = @webstoreId 
                        ORDER BY specificationTemplate_prdtype, specificationTemplate_Name";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
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

        if (templateId == 0 && templates.Count > 0)
        {
            templateId = templates[0].Id;
        }

        // Bind sizes based on templateId
        var sizes = new List<SizeItem>();
        if (templateId > 0)
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"SELECT SizeID, SizeTitle FROM tbl_CakeSize w
                            WHERE custid = @webstoreId AND IsActive = 1
                              AND EXISTS (SELECT 1 FROM tbl_CakePrice_template c WHERE c.templateid = @templateId AND c.SizeID = w.SizeID)
                            ORDER BY SizeType, DisplayOrder";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                cmd.Parameters.AddWithValue("@templateId", templateId);
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
        }

        if (sizeId == 0 && sizes.Count > 0)
        {
            sizeId = sizes[0].Id;
        }

        var linkedItems = new List<LinkedProductItem>();
        if (templateId > 0 && sizeId > 0)
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"SELECT att.productTopperDefault_ID, att.productTopperDefault_modifiedOn, att.productTopperDefault_Qty, 
                                   lnk.product_Name, lnk.product_type, lnksize.SizeTitle, lnktemp.specificationTemplate_Name,
                                   (SELECT COUNT(1) FROM tbl_Product_Topper w 
                                    WHERE w.sizeID = att.productTopperDefault_sizeID 
                                      AND w.Topper_PrdId = att.productTopperDefault_packageprdID 
                                      AND EXISTS (SELECT 1 FROM tbl_lnkprdtemplate a WHERE a.lnkprdtemplate_templateID = @templateId AND a.lnkprdtemplate_prdId = w.Product_Id)) AS prdcount
                            FROM tbl_productTopperDefault att
                            INNER JOIN tbl_specificationTemplate lnktemp ON att.productTopperDefault_templateID = lnktemp.specificationTemplate_ID
                            INNER JOIN tbl_products lnk ON att.productTopperDefault_packageprdID = lnk.product_ID
                            INNER JOIN tbl_CakeSize lnksize ON att.productTopperDefault_sizeID = lnksize.SizeID
                            WHERE lnktemp.specificationTemplate_uid = @webstoreId 
                              AND att.productTopperDefault_sizeID = @sizeId 
                              AND att.productTopperDefault_templateID = @templateId
                              AND lnk.product_type > 6 
                              AND lnk.product_WebstoreID = @webstoreId
                            ORDER BY lnk.product_type, lnk.product_Name";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                cmd.Parameters.AddWithValue("@sizeId", sizeId);
                cmd.Parameters.AddWithValue("@templateId", templateId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    linkedItems.Add(new LinkedProductItem
                    {
                        Id = reader.GetInt64(0),
                        ModifiedOn = reader.GetDateTime(1),
                        Qty = reader.GetInt32(2),
                        ProductName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ProductType = reader.GetInt32(4),
                        SizeTitle = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        TemplateName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        PrdCount = reader.GetInt32(7)
                    });
                }
            }
        }

        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.Templates = templates;
        ViewBag.Sizes = sizes;
        ViewBag.LinkedItems = linkedItems;
        ViewBag.SelectedTemplateId = templateId;
        ViewBag.SelectedSizeId = sizeId;
        ViewBag.SelectedPackageId = packageId;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        if (TempData["SuccessMsg"] != null) ViewBag.SuccessMsg = TempData["SuccessMsg"].ToString();
        if (TempData["ErrorMsg"] != null) ViewBag.ErrorMsg = TempData["ErrorMsg"].ToString();

        return View("~/Views/LinkPackageProduct/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] long drpTemplate, [FromForm] int drpSize, [FromForm] long hfPackageID, [FromForm] int txtQty)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        if (drpTemplate == 0 || drpSize == 0 || hfPackageID == 0 || txtQty <= 0)
        {
            TempData["ErrorMsg"] = "Invalid selection or qty.";
            return Redirect("/linkpackage2prd");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                
                // Check if linkage already exists
                var checkSql = @"SELECT productTopperDefault_ID FROM tbl_productTopperDefault 
                                 WHERE productTopperDefault_packageprdID = @packageId 
                                   AND productTopperDefault_sizeID = @sizeId 
                                   AND productTopperDefault_templateID = @templateId";
                await using var checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@packageId", hfPackageID);
                checkCmd.Parameters.AddWithValue("@sizeId", drpSize);
                checkCmd.Parameters.AddWithValue("@templateId", drpTemplate);
                var linkIdObj = await checkCmd.ExecuteScalarAsync();

                if (linkIdObj != null)
                {
                    // Update
                    var linkId = Convert.ToInt64(linkIdObj);
                    var updateSql = @"UPDATE tbl_productTopperDefault SET 
                                        productTopperDefault_modifiedOn = GETDATE(), 
                                        productTopperDefault_Qty = @qty 
                                      WHERE productTopperDefault_ID = @linkId";
                    await using var updCmd = new SqlCommand(updateSql, conn);
                    updCmd.Parameters.AddWithValue("@qty", txtQty);
                    updCmd.Parameters.AddWithValue("@linkId", linkId);
                    await updCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Insert
                    var insertSql = @"INSERT INTO tbl_productTopperDefault 
                                        (productTopperDefault_templateID, productTopperDefault_sizeID, 
                                         productTopperDefault_packageprdID, productTopperDefault_Qty, productTopperDefault_modifiedOn) 
                                      VALUES (@templateId, @sizeId, @packageId, @qty, GETDATE())";
                    await using var insCmd = new SqlCommand(insertSql, conn);
                    insCmd.Parameters.AddWithValue("@templateId", drpTemplate);
                    insCmd.Parameters.AddWithValue("@sizeId", drpSize);
                    insCmd.Parameters.AddWithValue("@packageId", hfPackageID);
                    insCmd.Parameters.AddWithValue("@qty", txtQty);
                    await insCmd.ExecuteNonQueryAsync();
                }

                TempData["SuccessMsg"] = "Linking saved successfully.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Failed to save linking: " + ex.Message;
        }

        return Redirect($"/linkpackage2prd?templateId={drpTemplate}&sizeId={drpSize}");
    }

    [HttpPost("action")]
    public async Task<IActionResult> Action([FromForm] long linkId, [FromForm] string commandName)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long templateId = 0;
        int sizeId = 0;

        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                
                // Load the details of this default topper link
                var detailSql = @"SELECT productTopperDefault_templateID, productTopperDefault_sizeID, 
                                         productTopperDefault_packageprdID, productTopperDefault_Qty 
                                  FROM tbl_productTopperDefault 
                                  WHERE productTopperDefault_ID = @linkId";
                await using var detailCmd = new SqlCommand(detailSql, conn);
                detailCmd.Parameters.AddWithValue("@linkId", linkId);
                await using var reader = await detailCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    templateId = reader.GetInt64(0);
                    sizeId = reader.GetInt32(1);
                    var packageId = reader.GetInt64(2);
                    var qty = reader.GetInt32(3);
                    reader.Close();

                    if (commandName == "updatePrd")
                    {
                        // Call SP usp_insertPrdtopper_bysizeID
                        await using var spCmd = new SqlCommand("usp_insertPrdtopper_bysizeID", conn);
                        spCmd.CommandType = CommandType.StoredProcedure;
                        spCmd.Parameters.AddWithValue("@sizeID", sizeId);
                        spCmd.Parameters.AddWithValue("@packageprdID", packageId);
                        spCmd.Parameters.AddWithValue("@qty", qty);
                        spCmd.Parameters.AddWithValue("@templateID", templateId);
                        await spCmd.ExecuteNonQueryAsync();

                        TempData["SuccessMsg"] = "Linking updated successfully via database.";
                    }
                    else if (commandName == "removePrd")
                    {
                        // Remove from default topper links
                        var delDefaultSql = "DELETE FROM tbl_productTopperDefault WHERE productTopperDefault_ID = @linkId";
                        await using var delDefCmd = new SqlCommand(delDefaultSql, conn);
                        delDefCmd.Parameters.AddWithValue("@linkId", linkId);
                        await delDefCmd.ExecuteNonQueryAsync();

                        // Remove from active toppers linked to templates
                        var delTopperSql = @"DELETE FROM tbl_product_topper 
                                             WHERE Topper_PrdId = @packageId AND sizeID = @sizeId 
                                               AND Product_Id IN (SELECT lnkprdtemplate_prdId FROM tbl_lnkprdtemplate WHERE lnkprdtemplate_templateID = @templateId)";
                        await using var delTopCmd = new SqlCommand(delTopperSql, conn);
                        delTopCmd.Parameters.AddWithValue("@packageId", packageId);
                        delTopCmd.Parameters.AddWithValue("@sizeId", sizeId);
                        delTopCmd.Parameters.AddWithValue("@templateId", templateId);
                        await delTopCmd.ExecuteNonQueryAsync();

                        TempData["SuccessMsg"] = "Linking removed successfully.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Action failed: " + ex.Message;
        }

        return Redirect($"/linkpackage2prd?templateId={templateId}&sizeId={sizeId}");
    }

    [HttpPost("GetPackageSupplyList")]
    public async Task<IActionResult> GetPackageSupplyList([FromBody] GetPackageSupplyListRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(new { d = new List<object>() });

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var list = new List<PackageSupplyAutocompleteItem>();

        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT product_id, product_name, product_image1 
                        FROM tbl_products 
                        WHERE product_type > 6 AND product_webstoreid = @wid AND product_isdeleted = 0
                          AND product_isActive = 1 AND product_isexpired = 0 
                          AND product_name LIKE '%' + @keyword + '%' 
                        ORDER BY product_type, product_Name";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", long.Parse(webshopId));
            cmd.Parameters.AddWithValue("@keyword", request.Keyword ?? "");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PackageSupplyAutocompleteItem
                {
                    product_id = reader.GetInt64(0),
                    product_name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    product_image1 = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
        }

        return Json(new { d = list });
    }

    public class GetPackageSupplyListRequest
    {
        public string? Keyword { get; set; }
    }

    public class TemplateItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class SizeItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public class LinkedProductItem
    {
        public long Id { get; set; }
        public DateTime ModifiedOn { get; set; }
        public int Qty { get; set; }
        public string ProductName { get; set; } = "";
        public int ProductType { get; set; }
        public string SizeTitle { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public int PrdCount { get; set; }
    }

    public class PackageSupplyAutocompleteItem
    {
        public long product_id { get; set; }
        public string product_name { get; set; } = "";
        public string product_image1 { get; set; } = "";
    }
}
