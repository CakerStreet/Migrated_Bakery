using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;
using System.Text.RegularExpressions;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Add/Edit Product page.
/// Route: /addnewcake (legacy URL preserved)
/// Migrated from addnewcake.aspx.
/// Phase 1: Core product fields load + save (Type 1 Cake, Type 2 Stock).
/// </summary>
[Route("addnewcake")]
[Route("edititem")]
[Route("addnewitem")]
[Route("Addnewcake.aspx")]
public class AddNewCakeController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public AddNewCakeController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    // ─── GET /addnewcake ───────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long prdID = 0, [FromQuery] int type = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/addnewcake");

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);

        // Load existing product if editing
        ProductEditModel? product = null;
        if (prdID > 0)
        {
            product = await LoadProductAsync(connectionString, prdID, wid);
            if (product != null) type = product.ProductType;
        }

        // Build cascading category dropdown HTML from Category table
        int prdtype = type;
        int tradeType = 1; // default
        int categoryPrdtype = (prdtype == 3) ? 2 : (prdtype == 6) ? 6 : 1;
        int categoryFor = (tradeType == 2 || prdtype == 2) ? 2 : 1;

        string categoryDropdownHtml = "";
        long productCatId = 0;
        if (prdID > 0 && product != null && product.ProductCatId > 0)
        {
            // Edit mode: build breadcrumb chain of selects
            productCatId = product.ProductCatId;
            var breadcrumb = await LoadCategoryBreadcrumbAsync(connectionString, product.ProductCatId);
            if (!string.IsNullOrEmpty(breadcrumb))
            {
                var breadcrumbIds = breadcrumb.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();

                // For each level, build a <select> with the parent's children and the correct one selected
                long parentId = 0;
                for (int level = 1; level <= breadcrumbIds.Count; level++)
                {
                    var selectedId = breadcrumbIds[level - 1];
                    var options = await LoadCategoryOptionsAsync(connectionString, parentId, level, categoryPrdtype, categoryFor);
                    categoryDropdownHtml += $"<select data-tid='{level}' class='form-control form-inline'>";
                    categoryDropdownHtml += "<option value='-1'>--Select Category--</option>";
                    foreach (var opt in options)
                    {
                        var sel = opt.Id == selectedId ? " selected" : "";
                        categoryDropdownHtml += $"<option value='{opt.Id}'{sel}>{opt.Name}</option>";
                    }
                    categoryDropdownHtml += "</select>";
                    parentId = selectedId;
                }

                // Check if the last selected category has children (if so, add one more empty select)
                var lastId = breadcrumbIds.Last();
                var nextLevel = breadcrumbIds.Count + 1;
                var childOptions = await LoadCategoryOptionsAsync(connectionString, lastId, nextLevel, categoryPrdtype, categoryFor);
                if (childOptions.Count > 0)
                {
                    categoryDropdownHtml += $"<select data-tid='{nextLevel}' class='form-control form-inline'>";
                    categoryDropdownHtml += "<option value='-1'>--Select Category--</option>";
                    foreach (var opt in childOptions)
                    {
                        categoryDropdownHtml += $"<option value='{opt.Id}'>{opt.Name}</option>";
                    }
                    categoryDropdownHtml += "</select>";
                }
            }
            else
            {
                // Breadcrumb failed, fall back to level 1
                var level1Options = await LoadCategoryOptionsAsync(connectionString, 0, 1, categoryPrdtype, categoryFor);
                categoryDropdownHtml = "<select data-tid='1' class='form-control form-inline'><option value='-1'>--Select Category--</option>";
                foreach (var opt in level1Options)
                    categoryDropdownHtml += $"<option value='{opt.Id}'>{opt.Name}</option>";
                categoryDropdownHtml += "</select>";
            }
        }
        else
        {
            // New product: show level 1 dropdown
            var level1Options = await LoadCategoryOptionsAsync(connectionString, 0, 1, categoryPrdtype, categoryFor);
            categoryDropdownHtml = "<select data-tid='1' class='form-control form-inline'><option value='-1'>--Select Category--</option>";
            foreach (var opt in level1Options)
                categoryDropdownHtml += $"<option value='{opt.Id}'>{opt.Name}</option>";
            categoryDropdownHtml += "</select>";
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

        ViewBag.Product = product;
        ViewBag.ProductType = type;
        ViewBag.IsEdit = prdID > 0;
        ViewBag.CategoryDropdownHtml = categoryDropdownHtml;
        ViewBag.ProductCatId = productCatId;

        // Phase 2: Load specifications and sizes for editing
        var specs = new Dictionary<int, string>();
        var sizes = new List<SizeRow>();
        if (prdID > 0)
        {
            specs = await LoadSpecificationsAsync(connectionString, prdID);
            sizes = await LoadSizesAsync(connectionString, prdID);
        }
        ViewBag.Specs = specs;
        ViewBag.Sizes = sizes;

        // Load cake shapes and types for cake/cupcake products
        var cakeShapes = new List<DropdownItem>();
        var cakeTypes = new List<DropdownItem>();
        if (type == 1 || type == 6)
        {
            try { cakeShapes = await LoadCakeShapesAsync(connectionString); } catch { }
            try { cakeTypes = await LoadCakeTypesAsync(connectionString, wid); } catch { }
        }
        ViewBag.CakeShapes = cakeShapes;
        ViewBag.CakeTypes = cakeTypes;

        // Load specification templates for the template dropdown
        var specTemplates = new List<SpecTemplateItem>();
        if (type == 1 || type == 6 || type == 3)
        {
            int templatePrdType = (type == 1) ? 0 : (type == 6) ? 1 : 3;
            try { specTemplates = await LoadSpecificationTemplatesAsync(connectionString, wid, templatePrdType); } catch { }
        }
        ViewBag.SpecTemplates = specTemplates;

        // Load delivery settings for the product
        var deliverySettings = new ProductDeliverySettings();
        if (prdID > 0)
        {
            try { deliverySettings = await LoadDeliverySettingsAsync(connectionString, prdID, wid); } catch { }
        }
        else
        {
            // Default from webstore settings
            deliverySettings = await LoadWebstoreDeliveryDefaultsAsync(connectionString, wid);
        }
        ViewBag.DeliverySettings = deliverySettings;

        // Load selected shape/type for existing products
        var selectedShapeId = 0;
        var selectedTypeId = 0;
        if (prdID > 0 && (type == 1 || type == 6))
        {
            try { (selectedShapeId, selectedTypeId) = await LoadProductShapeTypeAsync(connectionString, prdID); } catch { }
        }
        ViewBag.SelectedShapeId = selectedShapeId;
        ViewBag.SelectedTypeId = selectedTypeId;

        // Max delivery miles from webstore
        var maxDeliveryMiles = 20;
        try
        {
            await using (var connMiles = new SqlConnection(connectionString))
            {
                await connMiles.OpenAsync();
                var milesSql = "SELECT MAX(prdShippingwightBand_maxwt) FROM tbl_prdShippingweightBand WHERE prdShippingwightBand_bakeryID = @wid";
                await using var milesCmd = new SqlCommand(milesSql, connMiles);
                milesCmd.Parameters.AddWithValue("@wid", wid);
                var milesResult = await milesCmd.ExecuteScalarAsync();
                if (milesResult != null && milesResult != DBNull.Value)
                    maxDeliveryMiles = Convert.ToInt32(milesResult);
            }
        }
        catch { /* table may not exist in this DB */ }
        ViewBag.MaxDeliveryMiles = maxDeliveryMiles;

        // Load stock location quantities for stock product types
        var stockQty = 0;
        var stockLocations = new List<StockLocationRow>();
        if (prdID > 0 && (type >= 4 || type == 2))
        {
            try { (stockQty, stockLocations) = await LoadStockLocationsAsync(connectionString, prdID); } catch { }
        }
        ViewBag.StockQty = stockQty;
        ViewBag.StockLocations = stockLocations;

        // Load bakery postcode for delivery display (legacy: litpostcode)
        var bakeryPostcode = "bakery";
        try
        {
            await using var connPC = new SqlConnection(connectionString);
            await connPC.OpenAsync();
            var pcSql = "SELECT bakery_postcode FROM tbl_Bakery WHERE bakery_ID = @wid";
            await using var pcCmd = new SqlCommand(pcSql, connPC);
            pcCmd.Parameters.AddWithValue("@wid", wid);
            var pcResult = await pcCmd.ExecuteScalarAsync();
            if (pcResult != null && pcResult != DBNull.Value && !string.IsNullOrWhiteSpace(pcResult.ToString()))
                bakeryPostcode = pcResult.ToString()!;
        }
        catch { }
        ViewBag.BakeryPostcode = bakeryPostcode;

        // Load stock-specific data for stock product types (2, 4, 5+)
        var accessoryTypes = new List<DropdownItem>();
        var bakeryThemes = new List<DropdownItem>();
        var packagingUnits = new List<DropdownItem>();
        var minQtyAlertQty = "";
        var minQtyAlertEmail = "";
        var wholesalePrice = 0m;
        var productQuantity = 0;
        var packagingUnitId = 0;
        var accessoryTypeId = 0;

        if (type >= 4 || type == 2)
        {
            // Load packaging units
            try
            {
                await using var connPU = new SqlConnection(connectionString);
                await connPU.OpenAsync();
                await using var puCmd = new SqlCommand("SELECT unit_ID, unit_Title FROM tbl_packagingUnit ORDER BY unit_DisplayOrder", connPU);
                await using var puReader = await puCmd.ExecuteReaderAsync();
                while (await puReader.ReadAsync())
                    packagingUnits.Add(new DropdownItem { Id = puReader.GetInt32(0), Title = puReader.GetString(1) });
            }
            catch { }

            // Load min qty alert for types 2, 4 (not 5, 9, 10)
            if ((type == 2 || type == 4) && prdID > 0)
            {
                try
                {
                    await using var connMQ = new SqlConnection(connectionString);
                    await connMQ.OpenAsync();
                    var mqSql = @"SELECT TOP 1 MinQty, EmailId FROM tbl_ProductQtyAlert
                                  WHERE Product_Id IN (SELECT product_ID FROM tbl_Products WHERE product_WebstoreID = @wid AND product_type = @ptype)
                                  ORDER BY PrdQtyID DESC";
                    await using var mqCmd = new SqlCommand(mqSql, connMQ);
                    mqCmd.Parameters.AddWithValue("@wid", wid);
                    mqCmd.Parameters.AddWithValue("@ptype", type);
                    await using var mqReader = await mqCmd.ExecuteReaderAsync();
                    if (await mqReader.ReadAsync())
                    {
                        minQtyAlertQty = mqReader["MinQty"]?.ToString() ?? "";
                        minQtyAlertEmail = mqReader["EmailId"]?.ToString() ?? "";
                    }
                }
                catch { }
            }

            // Load wholesale price / quantity from product for edit mode
            if (prdID > 0)
            {
                try
                {
                    await using var connWS = new SqlConnection(connectionString);
                    await connWS.OpenAsync();
                    var wsSql = "SELECT ISNULL(product_WholesalePrice, 0), ISNULL(product_Quantity, 0) FROM tbl_Products WHERE product_ID = @pid";
                    await using var wsCmd = new SqlCommand(wsSql, connWS);
                    wsCmd.Parameters.AddWithValue("@pid", prdID);
                    await using var wsReader = await wsCmd.ExecuteReaderAsync();
                    if (await wsReader.ReadAsync())
                    {
                        wholesalePrice = wsReader.GetDecimal(0);
                        productQuantity = wsReader.GetInt32(1);
                    }
                }
                catch { }

                // Load packaging unit selection
                try
                {
                    await using var connPUS = new SqlConnection(connectionString);
                    await connPUS.OpenAsync();
                    var pusSql = "SELECT TOP 1 lnkprd2unit_unitID FROM tbl_lnkprd2Packagingunit WHERE lnkprd2unit_prdID = @pid";
                    await using var pusCmd = new SqlCommand(pusSql, connPUS);
                    pusCmd.Parameters.AddWithValue("@pid", prdID);
                    var pusResult = await pusCmd.ExecuteScalarAsync();
                    if (pusResult != null && pusResult != DBNull.Value)
                        packagingUnitId = Convert.ToInt32(pusResult);
                }
                catch { }
            }

            // Type 2 (Accessories): load accessory types and bakery themes
            if (type == 2)
            {
                try
                {
                    await using var connAT = new SqlConnection(connectionString);
                    await connAT.OpenAsync();
                    await using var atCmd = new SqlCommand("SELECT FlavourID, FlavourTitle FROM tbl_Flavour WHERE flavour_Type = 3 ORDER BY DisplayOrder", connAT);
                    await using var atReader = await atCmd.ExecuteReaderAsync();
                    while (await atReader.ReadAsync())
                        accessoryTypes.Add(new DropdownItem { Id = atReader.GetInt32(0), Title = atReader.GetString(1) });
                }
                catch { }

                try
                {
                    await using var connBT = new SqlConnection(connectionString);
                    await connBT.OpenAsync();
                    var btSql = @"SELECT t.accessorytheme_ID, t.accessorytheme_title,
                                  CASE WHEN l.lnkAccessorytheme_ID IS NOT NULL THEN 1 ELSE 0 END AS IsLinked
                                  FROM tbl_accessorytheme t
                                  LEFT JOIN tbl_lnk_prd2Accessorytheme l ON l.lnkAccessorytheme_themeID = t.accessorytheme_ID AND l.lnkAccessorytheme_prdID = @pid
                                  ORDER BY t.accessorytheme_title";
                    await using var btCmd = new SqlCommand(btSql, connBT);
                    btCmd.Parameters.AddWithValue("@pid", prdID);
                    await using var btReader = await btCmd.ExecuteReaderAsync();
                    while (await btReader.ReadAsync())
                        bakeryThemes.Add(new DropdownItem { Id = btReader.GetInt32(0), Title = btReader.GetString(1) });
                }
                catch { }

                // Load selected accessory type
                if (prdID > 0)
                {
                    try
                    {
                        await using var connATS = new SqlConnection(connectionString);
                        await connATS.OpenAsync();
                        var atsSql = "SELECT TOP 1 lnk_FlavourID FROM tbl_lnkprd2FlavourData WHERE lnk_productID = @pid";
                        await using var atsCmd = new SqlCommand(atsSql, connATS);
                        atsCmd.Parameters.AddWithValue("@pid", prdID);
                        var atsResult = await atsCmd.ExecuteScalarAsync();
                        if (atsResult != null && atsResult != DBNull.Value)
                            accessoryTypeId = Convert.ToInt32(atsResult);
                    }
                    catch { }
                }
            }
        }

        ViewBag.AccessoryTypes = accessoryTypes;
        ViewBag.BakeryThemes = bakeryThemes;
        ViewBag.PackagingUnits = packagingUnits;
        ViewBag.MinQtyAlertQty = minQtyAlertQty;
        ViewBag.MinQtyAlertEmail = minQtyAlertEmail;
        ViewBag.WholesalePrice = wholesalePrice;
        ViewBag.ProductQuantity = productQuantity;
        ViewBag.PackagingUnitId = packagingUnitId;
        ViewBag.AccessoryTypeId = accessoryTypeId;

        return View("~/Views/AddNewCake/Index.cshtml");
    }

    // ─── POST /addnewcake/savebasic ────────────────────────────────────────────

    [HttpPost("savebasic")]
    public async Task<IActionResult> SaveBasic([FromBody] ProductSaveRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0 || string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Unauthorized" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            long productId = request.ProductId;
            string seoUrl = GenerateSeoUrl(request.ProductName ?? "product");

            if (productId > 0)
            {
                // UPDATE existing product
                var sql = @"UPDATE tbl_products SET 
                    product_Name = @name, product_desc = @desc, product_largeDesc = @largeDesc,
                    product_code = @code, product_type = @type, product_isActive = @isActive,
                    product_marketPrice = @price, product_startingtPrice = @startPrice,
                    product_preparationday = @prepDay, product_modifiedOn = GETDATE(),
                    product_SEOURL = @seoUrl, product_catID = @catId
                    WHERE product_ID = @id AND product_WebstoreID = @wid";

                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@name", request.ProductName ?? "");
                cmd.Parameters.AddWithValue("@desc", request.ShortDescription ?? "");
                cmd.Parameters.AddWithValue("@largeDesc", request.LongDescription ?? "");
                cmd.Parameters.AddWithValue("@code", request.ProductCode ?? "");
                cmd.Parameters.AddWithValue("@type", request.ProductType);
                cmd.Parameters.AddWithValue("@isActive", request.IsActive);
                cmd.Parameters.AddWithValue("@price", request.Price);
                cmd.Parameters.AddWithValue("@startPrice", request.Price);
                cmd.Parameters.AddWithValue("@prepDay", request.PreparationDay);
                cmd.Parameters.AddWithValue("@seoUrl", seoUrl);
                cmd.Parameters.AddWithValue("@catId", request.CategoryId > 0 ? request.CategoryId : DBNull.Value);
                cmd.Parameters.AddWithValue("@id", productId);
                cmd.Parameters.AddWithValue("@wid", wid);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // INSERT new product
                var sql = @"INSERT INTO tbl_products 
                    (product_Name, product_desc, product_largeDesc, product_code, product_type,
                     product_isActive, product_marketPrice, product_startingtPrice, product_preparationday,
                     product_WebstoreID, product_saletype, product_isdeleted, product_isexpired,
                     product_createdOn, product_modifiedOn, product_SEOURL, product_displayOrder, product_catID)
                    VALUES 
                    (@name, @desc, @largeDesc, @code, @type,
                     @isActive, @price, @startPrice, @prepDay,
                     @wid, 1, 0, 0,
                     GETDATE(), GETDATE(), @seoUrl, 0, @catId);
                    SELECT SCOPE_IDENTITY();";

                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@name", request.ProductName ?? "");
                cmd.Parameters.AddWithValue("@desc", request.ShortDescription ?? "");
                cmd.Parameters.AddWithValue("@largeDesc", request.LongDescription ?? "");
                cmd.Parameters.AddWithValue("@code", request.ProductCode ?? "");
                cmd.Parameters.AddWithValue("@type", request.ProductType);
                cmd.Parameters.AddWithValue("@isActive", request.IsActive);
                cmd.Parameters.AddWithValue("@price", request.Price);
                cmd.Parameters.AddWithValue("@startPrice", request.Price);
                cmd.Parameters.AddWithValue("@prepDay", request.PreparationDay);
                cmd.Parameters.AddWithValue("@wid", wid);
                cmd.Parameters.AddWithValue("@seoUrl", seoUrl);
                cmd.Parameters.AddWithValue("@catId", request.CategoryId > 0 ? request.CategoryId : DBNull.Value);

                var newId = await cmd.ExecuteScalarAsync();
                productId = Convert.ToInt64(newId);
            }

            // Save webstore category linking (simple: delete + re-insert into tbl_lnkPrdStoreCat)
            if (request.CategoryIds != null && request.CategoryIds.Count > 0)
            {
                var delCatSql = "DELETE FROM tbl_lnkPrdStoreCat WHERE lnkPrdStoreCat_prdID = @pid";
                await using (var delCmd = new SqlCommand(delCatSql, conn, tx))
                {
                    delCmd.Parameters.AddWithValue("@pid", productId);
                    await delCmd.ExecuteNonQueryAsync();
                }

                foreach (var catId in request.CategoryIds)
                {
                    var insCatSql = "INSERT INTO tbl_lnkPrdStoreCat (lnkPrdStoreCat_prdID, lnkPrdStoreCat_catID) VALUES (@pid, @cid)";
                    await using var insCmd = new SqlCommand(insCatSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@pid", productId);
                    insCmd.Parameters.AddWithValue("@cid", catId);
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            // Insert product log
            var logSql = @"INSERT INTO tbl_productLog (productLog_prdID, productLog_modifiedby, productLog_Remarks, productLog_modifiedOn, productLog_typeID)
                VALUES (@pid, @uid, @action, GETDATE(), 0)";
            await using (var logCmd = new SqlCommand(logSql, conn, tx))
            {
                logCmd.Parameters.AddWithValue("@pid", productId);
                logCmd.Parameters.AddWithValue("@uid", userId);
                logCmd.Parameters.AddWithValue("@action", request.ProductId > 0 ? "Updated" : "Created");
                await logCmd.ExecuteNonQueryAsync();
            }

            // Save cake shape/type for cake and cupcake products
            if ((request.ProductType == 1 || request.ProductType == 6) && request.CakeShapeId > 0)
            {
                var delShapeSql = "DELETE FROM tbl_lnkPrdShape WHERE product_ID = @pid";
                await using (var dsCmd = new SqlCommand(delShapeSql, conn, tx))
                {
                    dsCmd.Parameters.AddWithValue("@pid", productId);
                    await dsCmd.ExecuteNonQueryAsync();
                }
                var insShapeSql = "INSERT INTO tbl_lnkPrdShape (product_ID, CakeShapeID) VALUES (@pid, @sid)";
                await using (var isCmd = new SqlCommand(insShapeSql, conn, tx))
                {
                    isCmd.Parameters.AddWithValue("@pid", productId);
                    isCmd.Parameters.AddWithValue("@sid", request.CakeShapeId);
                    await isCmd.ExecuteNonQueryAsync();
                }

                if (request.CakeTypeId > 0)
                {
                    var delTypeSql = "DELETE FROM tbl_CakeShape_CakeType WHERE product_ID = @pid";
                    await using (var dtCmd = new SqlCommand(delTypeSql, conn, tx))
                    {
                        dtCmd.Parameters.AddWithValue("@pid", productId);
                        await dtCmd.ExecuteNonQueryAsync();
                    }
                    var insTypeSql = "INSERT INTO tbl_CakeShape_CakeType (product_ID, CakeShapeID, CakeTypeID) VALUES (@pid, @sid, @tid)";
                    await using (var itCmd = new SqlCommand(insTypeSql, conn, tx))
                    {
                        itCmd.Parameters.AddWithValue("@pid", productId);
                        itCmd.Parameters.AddWithValue("@sid", request.CakeShapeId);
                        itCmd.Parameters.AddWithValue("@tid", request.CakeTypeId);
                        await itCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // Save delivery settings
            {
                var delDeliverySql = "DELETE FROM tbl_prdShiping WHERE prdShiping_prdID = @pid";
                await using (var ddCmd = new SqlCommand(delDeliverySql, conn, tx))
                {
                    ddCmd.Parameters.AddWithValue("@pid", productId);
                    await ddCmd.ExecuteNonQueryAsync();
                }
                var insDeliverySql = @"INSERT INTO tbl_prdShiping 
                    (prdShiping_prdID, prdShiping_iscollectable, prdShiping_isdeliverable, prdShiping_deliverMiles)
                    VALUES (@pid, @collect, @deliver, @miles)";
                await using (var idCmd = new SqlCommand(insDeliverySql, conn, tx))
                {
                    idCmd.Parameters.AddWithValue("@pid", productId);
                    idCmd.Parameters.AddWithValue("@collect", request.IsCollectable);
                    idCmd.Parameters.AddWithValue("@deliver", request.IsDeliverable);
                    idCmd.Parameters.AddWithValue("@miles", request.DeliveryMiles);
                    await idCmd.ExecuteNonQueryAsync();
                }

                // Postal delivery
                var delPostalSql = "DELETE FROM tbl_postalcake WHERE postalcake_prdID = @pid";
                await using (var dpCmd = new SqlCommand(delPostalSql, conn, tx))
                {
                    dpCmd.Parameters.AddWithValue("@pid", productId);
                    await dpCmd.ExecuteNonQueryAsync();
                }
                if (request.IsPostalDelivery)
                {
                    var insPostalSql = "INSERT INTO tbl_postalcake (postalcake_prdID) VALUES (@pid)";
                    await using (var ipCmd = new SqlCommand(insPostalSql, conn, tx))
                    {
                        ipCmd.Parameters.AddWithValue("@pid", productId);
                        await ipCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            await tx.CommitAsync();
            return Json(new { success = true, productId, message = request.ProductId > 0 ? "Product updated." : "Product created." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Save failed: " + ex.Message });
        }
    }

    // ─── Private: Load Product ────────────────────────────────────────────────

    private async Task<ProductEditModel?> LoadProductAsync(string connectionString, long prdId, long webstoreId)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT product_ID, product_Name, product_desc, product_largeDesc, product_code,
                           product_type, product_isActive, product_marketPrice, product_preparationday,
                           product_image1, product_SEOURL, product_createdOn, product_catID
                    FROM tbl_products 
                    WHERE product_ID = @id AND product_WebstoreID = @wid AND product_isdeleted = 0";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", prdId);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var product = new ProductEditModel
        {
            ProductId = Convert.ToInt64(reader.GetValue(0)),
            ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
            ShortDescription = reader.IsDBNull(2) ? "" : reader.GetString(2),
            LongDescription = reader.IsDBNull(3) ? "" : reader.GetString(3),
            ProductCode = reader.IsDBNull(4) ? "" : reader.GetString(4),
            ProductType = reader.IsDBNull(5) ? 1 : Convert.ToInt32(reader.GetValue(5)),
            IsActive = reader.IsDBNull(6) ? true : Convert.ToBoolean(reader.GetValue(6)),
            Price = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
            PreparationDay = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8)),
            Image1 = reader.IsDBNull(9) ? "" : reader.GetString(9),
            SeoUrl = reader.IsDBNull(10) ? "" : reader.GetString(10),
            CreatedOn = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11),
            ProductCatId = reader.IsDBNull(12) ? 0 : Convert.ToInt64(reader.GetValue(12))
        };
        reader.Close();

        // Load linked webstore categories
        var catSql = "SELECT lnkPrdStoreCat_catID FROM tbl_lnkPrdStoreCat WHERE lnkPrdStoreCat_prdID = @pid";
        await using var catCmd = new SqlCommand(catSql, conn);
        catCmd.Parameters.AddWithValue("@pid", prdId);
        await using var catReader = await catCmd.ExecuteReaderAsync();
        while (await catReader.ReadAsync())
        {
            product.CategoryIds.Add(Convert.ToInt64(catReader.GetValue(0)));
        }

        return product;
    }

    /// <summary>
    /// Load category options from the Category table for a given parent and level.
    /// </summary>
    private async Task<List<CategoryDropdownItem>> LoadCategoryOptionsAsync(
        string connectionString, long parentId, int level, int prdtype, int categoryFor)
    {
        var items = new List<CategoryDropdownItem>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT category_ID, category_Name FROM tbl_category
                    WHERE catgory_refCategoryID = @parentId AND category_isActive = 1 
                      AND category_level = @level
                    ORDER BY category_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@parentId", parentId);
        cmd.Parameters.AddWithValue("@level", level);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new CategoryDropdownItem
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ParentId = parentId
            });
        }
        return items;
    }

    /// <summary>
    /// Load breadcrumb chain from stored proc getbretcrumb_catidsfromcatId.
    /// Returns comma-separated category IDs from root to leaf.
    /// </summary>
    private async Task<string> LoadCategoryBreadcrumbAsync(string connectionString, long catId)
    {
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("getbretcrumb_catidsfromcatId", conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@catID", catId);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    // ─── POST /addnewcake/getcategories — AJAX endpoint for cascading categories ──

    [HttpPost("getcategories")]
    public async Task<IActionResult> GetCategories([FromBody] GetCategoriesRequest req)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        int nextLevel = req.IntLevel + 1;

        try
        {
            var options = await LoadCategoryOptionsAsync(connectionString, req.CatId, nextLevel, 1, 1);

            if (options.Count > 0)
            {
                // Children exist — build a new <select> element
                var html = $"<select data-tid='{nextLevel}' class='form-control form-inline'>";
                html += "<option value='-1'>--Select Category--</option>";
                foreach (var opt in options)
                {
                    html += $"<option value='{opt.Id}'>{System.Net.WebUtility.HtmlEncode(opt.Name)}</option>";
                }
                html += "</select>";
                return Json(new { dataId = 1, dataStr = html });
            }
            else
            {
                // Leaf category — no children
                return Json(new { dataId = 0, dataStr = "" });
            }
        }
        catch (Exception ex)
        {
            return Json(new { dataId = -1, dataStr = "Error: " + ex.Message });
        }
    }

    private static string GenerateSeoUrl(string name)
    {
        var url = name.ToLower().Trim();
        url = Regex.Replace(url, @"[^a-z0-9\s-]", "");
        url = Regex.Replace(url, @"\s+", "-");
        url = Regex.Replace(url, @"-+", "-");
        return url.Trim('-');
    }

    // ─── POST /addnewcake/savespec — Save specifications via USP ──────────────

    [HttpPost("savespec")]
    public async Task<IActionResult> SaveSpec([FromBody] SpecSaveRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0 || request.ProductId <= 0)
            return Json(new { success = false, message = "Unauthorized or invalid product" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Build TVP data for SpecificationType
            var specTable = new System.Data.DataTable();
            specTable.Columns.Add("typeID", typeof(int));
            specTable.Columns.Add("Value", typeof(string));

            if (!string.IsNullOrWhiteSpace(request.Ingredients))
                specTable.Rows.Add(1, request.Ingredients.Trim());
            if (!string.IsNullOrWhiteSpace(request.Allergens))
                specTable.Rows.Add(2, request.Allergens.Trim());
            if (!string.IsNullOrWhiteSpace(request.Advice))
                specTable.Rows.Add(3, request.Advice.Trim());
            if (!string.IsNullOrWhiteSpace(request.DeliveryDetails))
                specTable.Rows.Add(4, request.DeliveryDetails.Trim());
            if (!string.IsNullOrWhiteSpace(request.Storage))
                specTable.Rows.Add(5, request.Storage.Trim());

            await using var cmd = new SqlCommand("USP_UpdateSpecification", conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@pid", request.ProductId);
            var tvp = cmd.Parameters.AddWithValue("@SpecificationType", specTable);
            tvp.SqlDbType = System.Data.SqlDbType.Structured;
            tvp.TypeName = "SpecificationType";

            await cmd.ExecuteNonQueryAsync();
            return Json(new { success = true, message = "Specifications saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── POST /addnewcake/savesizes — Save sizes/prices via USP ───────────────

    [HttpPost("savesizes")]
    public async Task<IActionResult> SaveSizes([FromBody] SizeSaveRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (userId == 0 || request.ProductId <= 0)
            return Json(new { success = false, message = "Unauthorized or invalid product" });

        if (request.Sizes == null || request.Sizes.Count == 0)
            return Json(new { success = false, message = "No sizes to save." });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        int wid = int.TryParse(webshopId, out var w) ? w : 0;

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Build TVP data for specifications_size
            var sizeTable = new System.Data.DataTable();
            sizeTable.Columns.Add("row_guid", typeof(string));
            sizeTable.Columns.Add("sizetitle", typeof(string));
            sizeTable.Columns.Add("sizeprice", typeof(decimal));
            sizeTable.Columns.Add("CakeMinPortion", typeof(int));
            sizeTable.Columns.Add("CakeMaxPortion", typeof(int));
            sizeTable.Columns.Add("DisplayOrder", typeof(int));

            int order = 0;
            foreach (var s in request.Sizes)
            {
                order++;
                sizeTable.Rows.Add(
                    Guid.NewGuid().ToString(),
                    s.SizeTitle ?? "",
                    s.Price,
                    s.MinPortion,
                    s.MaxPortion,
                    order
                );
            }

            await using var cmd = new SqlCommand("USP_UpdateCakeSizeAndCakePrice", conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@pid", request.ProductId);
            cmd.Parameters.AddWithValue("@typeid", request.TypeId);
            cmd.Parameters.AddWithValue("@shapeid", request.ShapeId);
            cmd.Parameters.AddWithValue("@wid", wid);
            var tvp = cmd.Parameters.AddWithValue("@specifications_size", sizeTable);
            tvp.SqlDbType = System.Data.SqlDbType.Structured;
            tvp.TypeName = "specifications_size";

            await cmd.ExecuteNonQueryAsync();

            // Update product starting price (min of all sizes)
            var minPrice = request.Sizes.Min(s => s.Price);
            var updatePriceSql = "UPDATE tbl_products SET product_marketPrice = @price, product_startingtPrice = @price WHERE product_ID = @pid";
            await using var priceCmd = new SqlCommand(updatePriceSql, conn);
            priceCmd.Parameters.AddWithValue("@price", minPrice);
            priceCmd.Parameters.AddWithValue("@pid", request.ProductId);
            await priceCmd.ExecuteNonQueryAsync();

            return Json(new { success = true, message = "Sizes and prices saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── POST /addnewcake/uploadimage — Upload product image ──────────────────

    [HttpPost("uploadimage")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromForm] long productId, [FromForm] int imageIndex)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (userId == 0 || productId <= 0)
            return Json(new { success = false, message = "Unauthorized or invalid product" });

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "No file provided" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        try
        {
            // Save to local uploads folder (simplified — legacy used S3)
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            Directory.CreateDirectory(uploadsDir);
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{productId}_{imageIndex}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"uploads/products/{fileName}";

            // Update the product's image column
            var colName = imageIndex switch
            {
                1 => "product_image1",
                2 => "product_image2",
                3 => "product_image3",
                4 => "product_image4",
                5 => "product_image5",
                _ => "product_image1"
            };
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = $"UPDATE tbl_products SET {colName} = @path WHERE product_ID = @pid";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@path", relativePath);
            cmd.Parameters.AddWithValue("@pid", productId);
            await cmd.ExecuteNonQueryAsync();

            return Json(new { success = true, imagePath = relativePath, imageUrl = $"/{relativePath}" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Upload failed: " + ex.Message });
        }
    }

    // ─── GET /addnewcake/getsuppliers ─────────────────────────────────────────

    [HttpGet("getsuppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new List<object>());

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);
        var items = new List<object>();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = "SELECT SupplierId, SupplierName FROM tbl_Supplier WHERE WebstoreId = @wid AND IsDeleted = 0 ORDER BY SupplierName";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new { id = reader.GetValue(0), name = reader.IsDBNull(1) ? "" : reader.GetString(1) });
            }
        }
        catch { /* table may not exist */ }
        return Json(items);
    }

    // ─── GET /addnewcake/getlocations ─────────────────────────────────────────

    [HttpGet("getlocations")]
    public async Task<IActionResult> GetLocations()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new List<object>());

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        long wid = long.Parse(webshopId);
        var items = new List<object>();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = "SELECT LocationID, FullLocation FROM tbl_StockLocation WHERE WebstoreId = @wid AND IsDeleted = 0 ORDER BY FullLocation";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new { id = reader.GetValue(0), name = reader.IsDBNull(1) ? "" : reader.GetString(1) });
            }
        }
        catch { /* table may not exist */ }
        return Json(items);
    }

    // ─── POST /addnewcake/savequantity ────────────────────────────────────────

    [HttpPost("savequantity")]
    public async Task<IActionResult> SaveQuantity([FromBody] QuantitySaveRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0 || request.ProductId <= 0)
            return Json(new { success = false, message = "Unauthorized or invalid product" });

        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Update product quantity
            var updateSql = @"UPDATE tbl_products SET product_quantity = ISNULL(product_quantity, 0) + @qty WHERE product_ID = @pid";
            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@qty", request.Quantity);
            updateCmd.Parameters.AddWithValue("@pid", request.ProductId);
            await updateCmd.ExecuteNonQueryAsync();

            // Get updated quantity
            var qtySql = "SELECT ISNULL(product_quantity, 0) FROM tbl_products WHERE product_ID = @pid";
            await using var qtyCmd = new SqlCommand(qtySql, conn);
            qtyCmd.Parameters.AddWithValue("@pid", request.ProductId);
            var totalQty = Convert.ToInt32(await qtyCmd.ExecuteScalarAsync());

            return Json(new { success = true, totalQty, message = "Quantity saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── Private: Load Specifications ─────────────────────────────────────────

    private async Task<Dictionary<int, string>> LoadSpecificationsAsync(string connectionString, long productId)
    {
        var specs = new Dictionary<int, string>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = "SELECT typeID, Value FROM tbl_specification WHERE product_ID = @pid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var typeId = Convert.ToInt32(reader.GetValue(0));
            var value = reader.IsDBNull(1) ? "" : reader.GetString(1);
            specs[typeId] = value;
        }
        return specs;
    }

    // ─── Private: Load Sizes ──────────────────────────────────────────────────

    private async Task<List<SizeRow>> LoadSizesAsync(string connectionString, long productId)
    {
        var sizes = new List<SizeRow>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = @"SELECT cp.CakePriceID, cs.SizeTitle, cp.CakePrice, cp.CakeMinPortion, cp.CakeMaxPortion, cp.cakeprice_displayorder
                    FROM tbl_CakePrice cp
                    INNER JOIN tbl_CakeSize cs ON cp.SizeID = cs.SizeID
                    WHERE cp.product_ID = @pid
                    ORDER BY cp.cakeprice_displayorder, cs.SizeTitle";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sizes.Add(new SizeRow
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                SizeTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Price = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                MinPortion = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                MaxPortion = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                DisplayOrder = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5))
            });
        }
        return sizes;
    }

    // ─── Private: Load Cake Shapes ───────────────────────────────────────────

    private async Task<List<DropdownItem>> LoadCakeShapesAsync(string connectionString)
    {
        var items = new List<DropdownItem>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = "SELECT CakeShapeID, CakeShapeTitle FROM tbl_CakeShape WHERE IsActive = 1 ORDER BY DisplayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DropdownItem
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }

    // ─── Private: Load Cake Types ─────────────────────────────────────────────

    private async Task<List<DropdownItem>> LoadCakeTypesAsync(string connectionString, long webstoreId)
    {
        var items = new List<DropdownItem>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = @"SELECT CakeTypeID, CakeTypeTitle FROM tbl_CakeType 
                    WHERE IsActive = 1 AND (custid = @wid OR Isdefault = 1) 
                    ORDER BY DisplayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DropdownItem
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }

    // ─── Private: Load Product Shape/Type ─────────────────────────────────────

    private async Task<(int shapeId, int typeId)> LoadProductShapeTypeAsync(string connectionString, long productId)
    {
        int shapeId = 0, typeId = 0;
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var shapeSql = "SELECT TOP 1 CakeShapeID FROM tbl_lnkPrdShape WHERE product_ID = @pid";
        await using var shapeCmd = new SqlCommand(shapeSql, conn);
        shapeCmd.Parameters.AddWithValue("@pid", productId);
        var shapeResult = await shapeCmd.ExecuteScalarAsync();
        if (shapeResult != null && shapeResult != DBNull.Value)
            shapeId = Convert.ToInt32(shapeResult);

        if (shapeId > 0)
        {
            var typeSql = "SELECT TOP 1 CakeTypeID FROM tbl_CakeShape_CakeType WHERE product_ID = @pid AND CakeShapeID = @sid";
            await using var typeCmd = new SqlCommand(typeSql, conn);
            typeCmd.Parameters.AddWithValue("@pid", productId);
            typeCmd.Parameters.AddWithValue("@sid", shapeId);
            var typeResult = await typeCmd.ExecuteScalarAsync();
            if (typeResult != null && typeResult != DBNull.Value)
                typeId = Convert.ToInt32(typeResult);
        }

        return (shapeId, typeId);
    }

    // ─── Private: Load Delivery Settings ──────────────────────────────────────

    private async Task<ProductDeliverySettings> LoadDeliverySettingsAsync(string connectionString, long productId, long webstoreId)
    {
        var settings = new ProductDeliverySettings();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT prdShiping_iscollectable, prdShiping_isdeliverable, prdShiping_deliverMiles 
                    FROM tbl_prdShiping WHERE prdShiping_prdID = @pid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            settings.IsCollectable = !reader.IsDBNull(0) && Convert.ToBoolean(reader.GetValue(0));
            settings.IsDeliverable = !reader.IsDBNull(1) && Convert.ToBoolean(reader.GetValue(1));
            settings.DeliveryMiles = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
        }
        reader.Close();

        // Check postal delivery
        var postalSql = "SELECT COUNT(*) FROM tbl_postalcake WHERE postalcake_prdID = @pid";
        await using var postalCmd = new SqlCommand(postalSql, conn);
        postalCmd.Parameters.AddWithValue("@pid", productId);
        var postalCount = Convert.ToInt32(await postalCmd.ExecuteScalarAsync());
        settings.IsPostalDelivery = postalCount > 0;

        return settings;
    }

    // ─── Private: Load Webstore Delivery Defaults ─────────────────────────────

    private async Task<ProductDeliverySettings> LoadWebstoreDeliveryDefaultsAsync(string connectionString, long webstoreId)
    {
        var settings = new ProductDeliverySettings();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = "SELECT webstore_IsDeliverable, webstore_IsCollectable FROM tbl_webstore WHERE webstore_ID = @wid";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                settings.IsDeliverable = !reader.IsDBNull(0) && Convert.ToBoolean(reader.GetValue(0));
                settings.IsCollectable = !reader.IsDBNull(1) && Convert.ToBoolean(reader.GetValue(1));
            }
            reader.Close();

            // Default miles
            try
            {
                var milesSql = "SELECT MAX(prdShippingwightBand_maxwt) FROM tbl_prdShippingweightBand WHERE prdShippingwightBand_bakeryID = @wid";
                await using var milesCmd = new SqlCommand(milesSql, conn);
                milesCmd.Parameters.AddWithValue("@wid", webstoreId);
                var milesResult = await milesCmd.ExecuteScalarAsync();
                if (milesResult != null && milesResult != DBNull.Value)
                    settings.DeliveryMiles = Convert.ToDouble(milesResult);
            }
            catch { /* table may not exist */ }
        }
        catch { /* webstore query failed */ }

        return settings;
    }

    // ─── Private: Load Stock Locations ────────────────────────────────────────

    private async Task<(int totalQty, List<StockLocationRow> locations)> LoadStockLocationsAsync(string connectionString, long productId)
    {
        var locations = new List<StockLocationRow>();
        int totalQty = 0;
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Get product quantity
            var qtySql = "SELECT ISNULL(product_quantity, 0) FROM tbl_products WHERE product_ID = @pid";
            await using var qtyCmd = new SqlCommand(qtySql, conn);
            qtyCmd.Parameters.AddWithValue("@pid", productId);
            var qtyResult = await qtyCmd.ExecuteScalarAsync();
            if (qtyResult != null && qtyResult != DBNull.Value)
                totalQty = Convert.ToInt32(qtyResult);

            // Load stock location details (from tbl_prdStockLocation join tbl_StockLocation)
            var locSql = @"SELECT sl.FullLocation, psl.Qty 
                           FROM tbl_prdStockLocation psl 
                           INNER JOIN tbl_StockLocation sl ON psl.LocationID = sl.LocationID 
                           WHERE psl.PrdID = @pid ORDER BY sl.FullLocation";
            await using var locCmd = new SqlCommand(locSql, conn);
            locCmd.Parameters.AddWithValue("@pid", productId);
            await using var reader = await locCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                locations.Add(new StockLocationRow
                {
                    FullLocation = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Qty = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1))
                });
            }
        }
        catch { /* tables may not exist */ }

        return (totalQty, locations);
    }

    /// <summary>
    /// Load specification templates for the dropdown (matches legacy drpShippingTemplate binding).
    /// Table: tbl_specificationTemplate, filtered by webstore ID and product type.
    /// </summary>
    private async Task<List<SpecTemplateItem>> LoadSpecificationTemplatesAsync(string connectionString, long webstoreId, int prdType)
    {
        var items = new List<SpecTemplateItem>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
                    FROM tbl_specificationTemplate 
                    WHERE specificationTemplate_uid = @uid AND specificationTemplate_prdtype = @prdtype 
                    ORDER BY specificationTemplate_Name";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", webstoreId);
        cmd.Parameters.AddWithValue("@prdtype", prdType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SpecTemplateItem
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }

    /// <summary>
    /// AJAX endpoint: returns template settings (shape, type, prep day, delivery) when user selects a template.
    /// Matches legacy: webservices.aspx/getspecificationDetbyShippingTemplateID_manage
    /// </summary>
    [HttpPost("gettemplatedetails")]
    public async Task<IActionResult> GetTemplateDetails([FromBody] GetTemplateRequest request)
    {
        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return Json(new { dataId = 0, message = "No connection string" });

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // ── 1. Load template settings (tbl_templateSettings) ──
            int shapeId = -1, typeId = -1, preparationDay = 0;
            bool isCollection = false, isDelivery = false, isPostal = false;
            decimal miles = 15m;
            bool found = false;

            var sql = @"SELECT TOP 1 templateSettings_shapeID, templateSettings_typeId, 
                               templateSettings_preparationday, templateSettings_isCollection, 
                               templateSettings_ishanddelivery, templateSettings_isPostalDelivery, 
                               templateSettings_deliverymiles
                        FROM tbl_templateSettings 
                        WHERE templateSettings_templateID = @tid";
            await using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@tid", request.TemplateId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    found = true;
                    shapeId = reader.IsDBNull(0) ? -1 : Convert.ToInt32(reader.GetValue(0));
                    typeId = reader.IsDBNull(1) ? -1 : Convert.ToInt32(reader.GetValue(1));
                    preparationDay = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                    isCollection = !reader.IsDBNull(3) && Convert.ToBoolean(reader.GetValue(3));
                    isDelivery = !reader.IsDBNull(4) && Convert.ToBoolean(reader.GetValue(4));
                    isPostal = !reader.IsDBNull(5) && Convert.ToBoolean(reader.GetValue(5));
                    miles = reader.IsDBNull(6) ? 15m : Convert.ToDecimal(reader.GetValue(6));
                }
            }

            if (!found)
                return Json(new { dataId = 0, message = "Template settings not found." });

            // ── 2. Load sizes linked to this template ──
            var sizes = new List<object>();
            var sizeSql = @"SELECT cs.SizeTitle, cp.CakePrice, 
                                   cp.CakeMinPortion, cp.CakeMaxPortion
                            FROM tbl_CakePrice_template cp
                            INNER JOIN tbl_CakeSize cs ON cs.SizeID = cp.SizeID
                            WHERE cp.templateid = @tid
                            ORDER BY cp.cakeprice_displayorder";
            await using (var sizeCmd = new SqlCommand(sizeSql, conn))
            {
                sizeCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                await using var sizeReader = await sizeCmd.ExecuteReaderAsync();
                while (await sizeReader.ReadAsync())
                {
                    sizes.Add(new
                    {
                        sizeTitle = sizeReader.IsDBNull(0) ? "" : sizeReader.GetString(0),
                        price = sizeReader.IsDBNull(1) ? 0m : Convert.ToDecimal(sizeReader.GetValue(1)),
                        minPortion = sizeReader.IsDBNull(2) ? 0 : Convert.ToInt32(sizeReader.GetValue(2)),
                        maxPortion = sizeReader.IsDBNull(3) ? 0 : Convert.ToInt32(sizeReader.GetValue(3))
                    });
                }
            }

            // ── 3. Load flavour parent groups linked to this template ──
            var parentFlavours = new List<(string Title, int Id, int ViewType)>();
            var flavSql = @"SELECT cf.FlavourTitle, cf.FlavourID, cf.Attribute_ViewType
                            FROM tbl_lnkflvTemplate lf
                            INNER JOIN tbl_CustFlavour cf ON cf.FlavourID = lf.lnkflvTemplate_flvid
                            WHERE lf.lnkflvTemplate_tempId = @tid 
                              AND lf.lnkflvTemplate_sizeID = 0
                              AND cf.IsActive = 1
                              AND cf.Floavour_parentID = 0
                            ORDER BY lf.lnkflvTemplate_displayorder";
            await using (var flavCmd = new SqlCommand(flavSql, conn))
            {
                flavCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                await using var flavReader = await flavCmd.ExecuteReaderAsync();
                while (await flavReader.ReadAsync())
                {
                    parentFlavours.Add((
                        Title: flavReader.IsDBNull(0) ? "" : flavReader.GetString(0),
                        Id: flavReader.IsDBNull(1) ? 0 : Convert.ToInt32(flavReader.GetValue(1)),
                        ViewType: flavReader.IsDBNull(2) ? 0 : Convert.ToInt32(flavReader.GetValue(2))
                    ));
                }
            }

            // ── 4. For each parent flavour, load children from tbl_CustFlavour (Floavour_parentID > 0) ──
            var flavours = new List<object>();
            var childSql = @"SELECT cf.FlavourTitle, cf.FlavourID
                             FROM tbl_CustFlavour cf
                             INNER JOIN tbl_lnkflvTemplate lf ON lf.lnkflvTemplate_flvid = cf.FlavourID
                             WHERE cf.Floavour_parentID = @parentId
                               AND cf.IsActive = 1
                               AND lf.lnkflvTemplate_tempId = @tid
                             ORDER BY lf.lnkflvTemplate_displayorder";
            foreach (var parent in parentFlavours)
            {
                var children = new List<object>();
                await using (var childCmd = new SqlCommand(childSql, conn))
                {
                    childCmd.Parameters.AddWithValue("@parentId", parent.Id);
                    childCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                    await using var childReader = await childCmd.ExecuteReaderAsync();
                    while (await childReader.ReadAsync())
                    {
                        children.Add(new
                        {
                            title = childReader.IsDBNull(0) ? "" : childReader.GetString(0),
                            id = childReader.IsDBNull(1) ? 0 : Convert.ToInt32(childReader.GetValue(1))
                        });
                    }
                }
                flavours.Add(new
                {
                    title = parent.Title,
                    viewType = parent.ViewType,
                    children
                });
            }

            return Json(new
            {
                dataId = 1,
                shapeId,
                typeId,
                preparationDay,
                isCollection,
                isDelivery,
                isPostal,
                miles,
                sizes,
                flavours
            });
        }
        catch (Exception ex)
        {
            return Json(new { dataId = 0, message = ex.Message });
        }
    }

    // ─── Models ───────────────────────────────────────────────────────────────

    public class SpecTemplateItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class GetTemplateRequest
    {
        public long TemplateId { get; set; }
    }

    public class DropdownItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public class ProductDeliverySettings
    {
        public bool IsCollectable { get; set; } = true;
        public bool IsDeliverable { get; set; }
        public bool IsPostalDelivery { get; set; }
        public double DeliveryMiles { get; set; }
    }

    public class ProductEditModel
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string ShortDescription { get; set; } = "";
        public string LongDescription { get; set; } = "";
        public string ProductCode { get; set; } = "";
        public int ProductType { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public decimal Price { get; set; }
        public int PreparationDay { get; set; }
        public string Image1 { get; set; } = "";
        public string SeoUrl { get; set; } = "";
        public DateTime CreatedOn { get; set; }
        public long ProductCatId { get; set; }
        public List<long> CategoryIds { get; set; } = new();
    }

    public class CategoryDropdownItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public long ParentId { get; set; }
    }

    public class ProductSaveRequest
    {
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? ProductCode { get; set; }
        public int ProductType { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public decimal Price { get; set; }
        public int PreparationDay { get; set; }
        public List<long>? CategoryIds { get; set; }
        public long CategoryId { get; set; }  // single category ID from hfCatIDs (cascading dropdown leaf)
        public int CakeShapeId { get; set; }
        public int CakeTypeId { get; set; }
        public bool IsCollectable { get; set; } = true;
        public bool IsDeliverable { get; set; }
        public bool IsPostalDelivery { get; set; }
        public double DeliveryMiles { get; set; }
    }

    public class SizeRow
    {
        public int Id { get; set; }
        public string SizeTitle { get; set; } = "";
        public decimal Price { get; set; }
        public int MinPortion { get; set; }
        public int MaxPortion { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class SpecSaveRequest
    {
        public long ProductId { get; set; }
        public string? Ingredients { get; set; }
        public string? Allergens { get; set; }
        public string? Advice { get; set; }
        public string? DeliveryDetails { get; set; }
        public string? Storage { get; set; }
    }

    public class SizeSaveRequest
    {
        public long ProductId { get; set; }
        public int TypeId { get; set; }
        public int ShapeId { get; set; }
        public List<SizeSaveRow> Sizes { get; set; } = new();
    }

    public class SizeSaveRow
    {
        public string? SizeTitle { get; set; }
        public decimal Price { get; set; }
        public int MinPortion { get; set; }
        public int MaxPortion { get; set; }
    }

    public class StockLocationRow
    {
        public string FullLocation { get; set; } = "";
        public int Qty { get; set; }
    }

    public class QuantitySaveRequest
    {
        public long ProductId { get; set; }
        public decimal BuyingPrice { get; set; }
        public int SupplierId { get; set; }
        public int LocationId { get; set; }
        public int Quantity { get; set; }
        public string? PurchaseOrder { get; set; }
        public string? PoDate { get; set; }
        public string? OrderReceived { get; set; }
        public string? OrDate { get; set; }
    }

    public class GetCategoriesRequest
    {
        public long CatId { get; set; }
        public int IntLevel { get; set; }
    }

    public class BakeryThemeItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public bool IsLinked { get; set; }
    }
}
