using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("manageassorted")]
public class ManageAssortedController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageAssortedController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Index(long id, [FromQuery] int? sizeId = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect($"/businesslogin?returl=/manageassorted/{id}");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        long webstoreId = long.Parse(webshopId);

        // Load parent product detail
        ProductDetailModel? product = null;
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT product_image1, product_seoURL, product_name, product_ID, product_code 
                        FROM tbl_products 
                        WHERE product_WebstoreID = @wid AND Product_ID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                product = new ProductDetailModel
                {
                    Image1 = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    SeoUrl = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Id = reader.GetInt64(3),
                    Code = reader.IsDBNull(4) ? "" : reader.GetString(4)
                };
            }
        }

        if (product == null)
        {
            return Redirect("/mywebstore");
        }

        // Get cake size if specified
        string sizeTitle = "";
        if (sizeId > 0)
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = "SELECT SizeTitle FROM tbl_CakeSize WHERE SizeID = @sizeId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sizeId", sizeId.Value);
                var val = await cmd.ExecuteScalarAsync();
                if (val != null) sizeTitle = val.ToString() ?? "";
            }
        }

        // Load assorted items
        var items = new List<AssortedItem>();
        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT p.product_id, p.product_name, p.product_image1, p.product_seourl, 
                               a.AssortedBox_Qty, a.AssortedBox_DisplayOrder, a.AssortedBox_RefProductID
                        FROM tbl_products p 
                        INNER JOIN tbl_AssortedBox a ON p.product_ID = a.AssortedBox_RefProductID 
                        WHERE p.product_WebstoreID = @wid 
                          AND a.AssortedBox_ProductID = @id 
                          AND p.product_type = 3 
                          AND p.product_saletype = 2 
                        ORDER BY a.AssortedBox_DisplayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new AssortedItem
                {
                    ProductId = reader.GetInt64(0),
                    ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ProductImage = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ProductSeoUrl = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Qty = reader.GetInt32(4),
                    DisplayOrder = reader.GetInt32(5),
                    RefProductId = reader.GetInt64(6)
                });
            }
        }

        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.Product = product;
        ViewBag.SizeTitle = sizeTitle;
        ViewBag.Items = items;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        if (TempData["SuccessMsg"] != null) ViewBag.SuccessMsg = TempData["SuccessMsg"].ToString();
        if (TempData["ErrorMsg"] != null) ViewBag.ErrorMsg = TempData["ErrorMsg"].ToString();

        return View("~/Views/ManageAssorted/Index.cshtml");
    }

    [HttpPost("{id:long}/save-topper")]
    public async Task<IActionResult> SaveTopper(long id, [FromForm] long hfTopperID, [FromForm] int txtQty)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        if (hfTopperID == 0)
        {
            TempData["ErrorMsg"] = "Please select a topper.";
            return Redirect($"/manageassorted/{id}");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                // Check duplicate
                var existSql = @"SELECT COUNT(1) FROM tbl_AssortedBox 
                                 WHERE AssortedBox_ProductID = @id AND AssortedBox_RefProductID = @refId";
                await using var existCmd = new SqlCommand(existSql, conn);
                existCmd.Parameters.AddWithValue("@id", id);
                existCmd.Parameters.AddWithValue("@refId", hfTopperID);
                var exists = Convert.ToInt32(await existCmd.ExecuteScalarAsync()) > 0;

                if (exists)
                {
                    TempData["ErrorMsg"] = "This cake is already assigned to this product.";
                }
                else
                {
                    // Find max display order
                    var maxSql = "SELECT ISNULL(MAX(AssortedBox_DisplayOrder), 0) FROM tbl_AssortedBox WHERE AssortedBox_ProductID = @id";
                    await using var maxCmd = new SqlCommand(maxSql, conn);
                    maxCmd.Parameters.AddWithValue("@id", id);
                    var maxOrder = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());

                    // Get quantity
                    var qty = txtQty > 0 ? txtQty : 1;

                    var insertSql = @"INSERT INTO tbl_AssortedBox (AssortedBox_ProductID, AssortedBox_RefProductID, AssortedBox_Qty, AssortedBox_DisplayOrder) 
                                      VALUES (@id, @refId, @qty, @displayOrder)";
                    await using var insCmd = new SqlCommand(insertSql, conn);
                    insCmd.Parameters.AddWithValue("@id", id);
                    insCmd.Parameters.AddWithValue("@refId", hfTopperID);
                    insCmd.Parameters.AddWithValue("@qty", qty);
                    insCmd.Parameters.AddWithValue("@displayOrder", maxOrder + 1);
                    await insCmd.ExecuteNonQueryAsync();

                    TempData["SuccessMsg"] = "Topper added successfully.";
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Failed to add topper: " + ex.Message;
        }

        return Redirect($"/manageassorted/{id}");
    }

    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, [FromForm] long refProductId)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"DELETE FROM tbl_AssortedBox 
                            WHERE AssortedBox_ProductID = @id AND AssortedBox_RefProductID = @refId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@refId", refProductId);
                await cmd.ExecuteNonQueryAsync();

                TempData["SuccessMsg"] = "Cake removed successfully.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Failed to remove cake: " + ex.Message;
        }

        return Redirect($"/manageassorted/{id}");
    }

    [HttpPost("{id:long}/update-qty")]
    public async Task<IActionResult> UpdateQty(long id, [FromForm] int qty)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(0);

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = "UPDATE tbl_AssortedBox SET AssortedBox_Qty = @qty WHERE AssortedBox_ProductID = @id";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@qty", qty);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                return Json(1);
            }
        }
        catch
        {
            return Json(0);
        }
    }

    [HttpPost("{id:long}/update-display-order")]
    public async Task<IActionResult> UpdateDisplayOrder(long id, [FromForm] long refProductId, [FromForm] int displayOrder)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(0);

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var sql = @"UPDATE tbl_AssortedBox SET AssortedBox_DisplayOrder = @displayOrder 
                            WHERE AssortedBox_ProductID = @id AND AssortedBox_RefProductID = @refId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@refId", refProductId);
                await cmd.ExecuteNonQueryAsync();
                return Json(1);
            }
        }
        catch
        {
            return Json(0);
        }
    }

    [HttpPost("{id:long}/GetTopperList")]
    public async Task<IActionResult> GetTopperList(long id, [FromBody] GetTopperListRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId)) return Json(new { d = new List<object>() });

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var list = new List<TopperAutocompleteItem>();

        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT product_id, product_name, product_image1 
                        FROM tbl_products 
                        WHERE product_type = 3 AND product_saletype = 2 
                          AND product_webstoreid = @wid AND product_isdeleted = 0
                          AND product_id NOT IN (SELECT AssortedBox_RefProductID FROM tbl_AssortedBox WHERE AssortedBox_ProductID = @id) 
                          AND product_name LIKE '%' + @keyword + '%'";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", long.Parse(webshopId));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@keyword", request.Keyword ?? "");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TopperAutocompleteItem
                {
                    product_id = reader.GetInt64(0),
                    product_name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    product_image1 = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
        }

        return Json(new { d = list });
    }

    public class GetTopperListRequest
    {
        public string? Keyword { get; set; }
    }

    public class ProductDetailModel
    {
        public string Image1 { get; set; } = "";
        public string SeoUrl { get; set; } = "";
        public string Name { get; set; } = "";
        public long Id { get; set; }
        public string Code { get; set; } = "";
    }

    public class AssortedItem
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string ProductImage { get; set; } = "";
        public string ProductSeoUrl { get; set; } = "";
        public int Qty { get; set; }
        public int DisplayOrder { get; set; }
        public long RefProductId { get; set; }
    }

    public class TopperAutocompleteItem
    {
        public long product_id { get; set; }
        public string product_name { get; set; } = "";
        public string product_image1 { get; set; } = "";
    }
}
