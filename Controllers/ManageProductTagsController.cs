using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("manageproducttags")]
public class ManageProductTagsController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageProductTagsController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int typeId = 0,
        [FromQuery] int packtypeId = 0,
        [FromQuery] string? q = null,
        [FromQuery] long? prdID = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/manageproducttags");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Bind packaging types dropdown if typeId > 0
        var packagingTypes = new List<PackagingTypeItem>();
        if (typeId > 0)
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                if (typeId == 2) // Accessories
                {
                    var sql = "SELECT FlavourID, FlavourTitle FROM tbl_Flavour WHERE flavour_Type = 3 ORDER BY DisplayOrder";
                    await using var cmd = new SqlCommand(sql, conn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        packagingTypes.Add(new PackagingTypeItem
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
                        });
                    }
                }
                else
                {
                    var sql = "SELECT PackagingTypeID, PackagingType FROM tbl_PackagingType WHERE PrdType = @prdType ORDER BY DisplayOrder";
                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@prdType", typeId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        packagingTypes.Add(new PackagingTypeItem
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
                        });
                    }
                }
            }
        }

        // Binds products
        var products = new List<ProductItem>();
        if (typeId > 0 || prdID != null)
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"SELECT p.product_id, p.product_name, p.product_code, p.product_image1, p.product_type,
                                   PackagingTypeID = CASE WHEN @prd_type = 2 THEN a.lnkPrd2Accessory_accessoryID ELSE g.lnkPrd2Packaging_PackagingTypeID END
                            FROM tbl_products p
                            LEFT OUTER JOIN tbl_lnkPrd2Accessory a ON p.product_ID = a.lnkPrd2Accessory_PrdID
                            LEFT OUTER JOIN tbl_lnkPrd2Packaging g ON g.lnkPrd2Packaging_PrdID = p.product_ID
                            WHERE p.product_isdeleted = 0 AND p.product_isexpired = 0 AND p.product_WebstoreID = @wid";

                if (prdID != null)
                {
                    sql += " AND p.product_id = @pid";
                }
                else
                {
                    if (typeId > 0) sql += " AND p.product_type = @prd_type";
                }

                if (!string.IsNullOrWhiteSpace(q))
                {
                    sql += " AND (p.product_code LIKE '%' + @search + '%' OR p.product_name LIKE '%' + @search + '%')";
                }

                if (packtypeId > 0)
                {
                    if (typeId == 2)
                    {
                        sql += " AND p.product_ID IN (SELECT lnkPrd2Accessory_PrdID FROM tbl_lnkPrd2Accessory WHERE lnkPrd2Accessory_accessoryID = @pack_type)";
                    }
                    else if (typeId > 0)
                    {
                        sql += " AND p.product_ID IN (SELECT lnkPrd2Packaging_PrdID FROM tbl_lnkPrd2Packaging WHERE lnkPrd2Packaging_PackagingTypeID = @pack_type)";
                    }
                }

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@wid", webstoreId);
                cmd.Parameters.AddWithValue("@prd_type", typeId);
                cmd.Parameters.AddWithValue("@pid", (object?)prdID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@search", (object?)q?.Trim() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pack_type", packtypeId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(new ProductItem
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Code = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Image1 = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ProductType = Convert.ToInt32(reader.GetValue(4)),
                        PackagingTypeId = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5))
                    });
                }
            }
        }

        // Bind tags for all fetched products
        var productTags = new Dictionary<long, List<ProductTagItem>>();
        if (products.Count > 0)
        {
            var pids = products.Select(p => p.Id).ToList();
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = $@"SELECT t1.StocktypeTag_ID, t1.StocktypeTag_title, t2.lnkStockPrdTag_prdID 
                             FROM tbl_StockPrdTag t1
                             INNER JOIN tbl_lnkStockPrdTag t2 ON t1.StocktypeTag_ID = t2.lnkStockPrdTag_tagID
                             WHERE t2.lnkStockPrdTag_prdID IN ({string.Join(",", pids)})";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tagId = reader.GetInt64(0);
                    var title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var pid = reader.GetInt64(2);

                    if (!productTags.ContainsKey(pid))
                    {
                        productTags[pid] = new List<ProductTagItem>();
                    }
                    productTags[pid].Add(new ProductTagItem { Id = tagId, Title = title });
                }
            }
        }

        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.Products = products;
        ViewBag.ProductTags = productTags;
        ViewBag.PackagingTypes = packagingTypes;
        ViewBag.SelectedTypeId = typeId;
        ViewBag.SelectedPackTypeId = packtypeId;
        ViewBag.Keyword = q ?? "";
        ViewBag.PrdId = prdID;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        if (TempData["SuccessMsg"] != null) ViewBag.SuccessMsg = TempData["SuccessMsg"].ToString();
        if (TempData["ErrorMsg"] != null) ViewBag.ErrorMsg = TempData["ErrorMsg"].ToString();

        return View("~/Views/ManageProductTags/Index.cshtml");
    }

    [HttpPost("UpdatePackagingType")]
    public async Task<IActionResult> UpdatePackagingType([FromForm] long prdId, [FromForm] int packagingTypeId, [FromForm] int prdType)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(new { success = false, message = "Unauthorized" });

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                if (prdType == 2) // Accessory linking
                {
                    var checkSql = "SELECT COUNT(1) FROM tbl_lnkPrd2Accessory WHERE lnkPrd2Accessory_PrdID = @prdId";
                    await using var checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@prdId", prdId);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                    if (exists)
                    {
                        var sql = "UPDATE tbl_lnkPrd2Accessory SET lnkPrd2Accessory_accessoryID = @pkgId WHERE lnkPrd2Accessory_PrdID = @prdId";
                        await using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@pkgId", packagingTypeId);
                        cmd.Parameters.AddWithValue("@prdId", prdId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var sql = "INSERT INTO tbl_lnkPrd2Accessory (lnkPrd2Accessory_accessoryID, lnkPrd2Accessory_PrdID) VALUES (@pkgId, @prdId)";
                        await using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@pkgId", packagingTypeId);
                        cmd.Parameters.AddWithValue("@prdId", prdId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    var checkSql = "SELECT COUNT(1) FROM tbl_lnkPrd2Packaging WHERE lnkPrd2Packaging_PrdID = @prdId";
                    await using var checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@prdId", prdId);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                    if (exists)
                    {
                        var sql = "UPDATE tbl_lnkPrd2Packaging SET lnkPrd2Packaging_PackagingTypeID = @pkgId WHERE lnkPrd2Packaging_PrdID = @prdId";
                        await using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@pkgId", packagingTypeId);
                        cmd.Parameters.AddWithValue("@prdId", prdId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var sql = "INSERT INTO tbl_lnkPrd2Packaging (lnkPrd2Packaging_PackagingTypeID, lnkPrd2Packaging_PrdID) VALUES (@pkgId, @prdId)";
                        await using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@pkgId", packagingTypeId);
                        cmd.Parameters.AddWithValue("@prdId", prdId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            return Json(new { success = true, message = "Packaging/Accessory type updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("GetStockPrdTag")]
    public async Task<IActionResult> GetStockPrdTag([FromBody] GetStockPrdTagRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(new { d = new List<object>() });

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var keyword = request.Keyword?.ToLower() ?? "";
        var list = new List<AutocompleteTagItem>();

        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT TOP 20 StocktypeTag_ID, StocktypeTag_title 
                        FROM tbl_StockPrdTag 
                        WHERE LOWER(StocktypeTag_title) LIKE @keyword + '%' 
                          AND StocktypeTag_ID NOT IN (SELECT lnkStockPrdTag_tagID FROM tbl_lnkStockPrdTag WHERE lnkStockPrdTag_prdID = @prdId)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@keyword", keyword);
            cmd.Parameters.AddWithValue("@prdId", request.PrdId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AutocompleteTagItem
                {
                    ID = reader.GetInt64(0),
                    StocktypeTag_title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PrdID = request.PrdId
                });
            }
        }

        return Json(new { d = list });
    }

    [HttpPost("SaveStockPrdTag")]
    public async Task<IActionResult> SaveStockPrdTag([FromBody] SaveStockPrdTagRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Json(new { ID = 0, StocktypeTag_title = "Unauthorized", PrdID = request.PrdID });

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        try
        {
            long tagId = request.TagID;
            string tagTitle = request.TagTitle ?? "";

            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                if (tagId == 0)
                {
                    // Search if it exists by title
                    var selectSql = "SELECT StocktypeTag_ID, StocktypeTag_title FROM tbl_StockPrdTag WHERE LOWER(StocktypeTag_title) = LOWER(@title)";
                    await using var selectCmd = new SqlCommand(selectSql, conn);
                    selectCmd.Parameters.AddWithValue("@title", tagTitle.Trim());
                    await using var reader = await selectCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        tagId = reader.GetInt64(0);
                        tagTitle = reader.GetString(1);
                    }
                    reader.Close();
                }

                if (tagId == 0)
                {
                    // Create new tag
                    var insertSql = @"INSERT INTO tbl_StockPrdTag (StocktypeTag_title, StocktypeTag_webstoreID, StocktypeTag_modifiedOn, StocktypeTag_modifiedBy) 
                                      VALUES (@title, @wid, GETDATE(), @userId);
                                      SELECT SCOPE_IDENTITY();";
                    await using var insCmd = new SqlCommand(insertSql, conn);
                    insCmd.Parameters.AddWithValue("@title", tagTitle.Trim());
                    insCmd.Parameters.AddWithValue("@wid", webstoreId);
                    insCmd.Parameters.AddWithValue("@userId", userId);
                    tagId = Convert.ToInt64(await insCmd.ExecuteScalarAsync());
                }

                // Check linking existence
                var linkSql = "SELECT COUNT(1) FROM tbl_lnkStockPrdTag WHERE lnkStockPrdTag_prdID = @prdId AND lnkStockPrdTag_tagID = @tagId";
                await using var linkCmd = new SqlCommand(linkSql, conn);
                linkCmd.Parameters.AddWithValue("@prdId", request.PrdID);
                linkCmd.Parameters.AddWithValue("@tagId", tagId);
                var exists = Convert.ToInt32(await linkCmd.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    var insertLink = @"INSERT INTO tbl_lnkStockPrdTag (lnkStockPrdTag_prdID, lnkStockPrdTag_tagID, lnkStockPrdTag_createdBy, lnkStockPrdTag_createdOn) 
                                       VALUES (@prdId, @tagId, @userId, GETDATE())";
                    await using var insLinkCmd = new SqlCommand(insertLink, conn);
                    insLinkCmd.Parameters.AddWithValue("@prdId", request.PrdID);
                    insLinkCmd.Parameters.AddWithValue("@tagId", tagId);
                    insLinkCmd.Parameters.AddWithValue("@userId", userId);
                    await insLinkCmd.ExecuteNonQueryAsync();

                    // Reload the title from database just in case
                    var titleSql = "SELECT StocktypeTag_title FROM tbl_StockPrdTag WHERE StocktypeTag_ID = @tagId";
                    await using var titleCmd = new SqlCommand(titleSql, conn);
                    titleCmd.Parameters.AddWithValue("@tagId", tagId);
                    var tVal = await titleCmd.ExecuteScalarAsync();
                    if (tVal != null) tagTitle = tVal.ToString() ?? "";

                    return Json(new { ID = tagId, StocktypeTag_title = tagTitle, PrdID = request.PrdID });
                }
                else
                {
                    return Json(new { ID = 0, StocktypeTag_title = "Tag is already linked with this product", PrdID = request.PrdID });
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { ID = 0, StocktypeTag_title = "Error: " + ex.Message, PrdID = request.PrdID });
        }
    }

    [HttpPost("deleteStockPrdTag")]
    public async Task<IActionResult> deleteStockPrdTag([FromBody] DeleteStockPrdTagRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(0);

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = "DELETE FROM tbl_lnkStockPrdTag WHERE lnkStockPrdTag_tagID = @tagId AND lnkStockPrdTag_prdID = @prdId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@tagId", request.TagID);
                cmd.Parameters.AddWithValue("@prdId", request.PrdID);
                await cmd.ExecuteNonQueryAsync();
            }
            return Json(1);
        }
        catch
        {
            return Json(0);
        }
    }

    [HttpPost("UpdateProduct_PrdType")]
    public async Task<IActionResult> UpdateProduct_PrdType([FromBody] UpdateProductPrdTypeRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(0);

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = "UPDATE tbl_products SET product_type = @prdType WHERE product_id = @prdId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@prdType", request.PrdType);
                cmd.Parameters.AddWithValue("@prdId", request.PrdID);
                await cmd.ExecuteNonQueryAsync();
            }
            return Json(1);
        }
        catch
        {
            return Json(0);
        }
    }

    public class PackagingTypeItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public class ProductItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Image1 { get; set; } = "";
        public int ProductType { get; set; }
        public int PackagingTypeId { get; set; }
    }

    public class ProductTagItem
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
    }

    public class GetStockPrdTagRequest
    {
        public string? Keyword { get; set; }
        public long PrdId { get; set; }
    }

    public class AutocompleteTagItem
    {
        public long ID { get; set; }
        public string StocktypeTag_title { get; set; } = "";
        public long PrdID { get; set; }
    }

    public class SaveStockPrdTagRequest
    {
        public long TagID { get; set; }
        public string? TagTitle { get; set; }
        public long PrdID { get; set; }
    }

    public class DeleteStockPrdTagRequest
    {
        public long TagID { get; set; }
        public long PrdID { get; set; }
    }

    public class UpdateProductPrdTypeRequest
    {
        public int PrdType { get; set; }
        public long PrdID { get; set; }
    }
}
