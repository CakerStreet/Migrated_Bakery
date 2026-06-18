using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

/// <summary>
/// Service for querying a single order's full detail from tbl_order with related tables.
/// Migrated from bakeryorderdetail.aspx.cs getorderdetail() logic.
/// Phase B1: Read-only, no mutations.
/// </summary>
public class BusinessOrderDetailService
{
    private readonly string _connectionString;

    public BusinessOrderDetailService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets full order detail matching legacy getorderdetail() method.
    /// Returns null if order not found or not accessible by this webshop.
    /// </summary>
    public async Task<OrderDetailResult?> GetOrderDetailAsync(long orderId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Main query matching legacy: o.*, od.*, p.*, c.*, w.*, sd.*, b.*, plus SKU mapping, distance, baking flags
        var sql = @"
            SELECT 
                o.order_ID,
                o.order_date,
                o.order_status,
                o.order_customerName,
                o.order_customerEmail,
                o.order_totalPrice,
                o.order_CSmargin,
                o.order_shopMargin,
                o.order_payoutRefund,
                o.order_csRefund,
                o.order_saletype,
                o.order_isrepeat,
                o.order_followingorderid,
                o.order_forwardedorderid,
                o.order_bakeryID,
                o.order_branchID,
                o.order_shippingCost,
                o.order_quality,
                o.order_customerID,
                o.order_shopID,
                sd.shipping_fName,
                sd.shipping_lName,
                sd.shipping_phone,
                sd.shipping_address,
                sd.shipping_city,
                sd.shipping_county,
                sd.shipping_country,
                sd.shipping_zip,
                b.billing_fName,
                b.billing_lName,
                b.billing_phone,
                b.billing_address,
                b.billing_city,
                b.billing_county,
                b.billing_country,
                b.billing_zip,
                b.billing_emailID,
                c.ordercollection_deliverymode,
                c.ordercollection_Date,
                c.ordercollection_DispatchDate,
                c.ordercollection_OcasionDate,
                c.ordercollection_Ocasion,
                c.ordercollection_Remarks,
                w.webstore_businessName,
                w.webstore_postcode,
                w.webstore_address,
                w.webstore_city,
                w.webstore_State,
                w.webstore_OrderEmail,
                w.webstore_businessPhone,
                ISNULL(' ('+CAST(tbl_PostcodeDistance.Distance AS NVARCHAR(18))+' miles)','') AS PCdistance,
                ISNULL(wbr.WebstoreBranch_isBaking, 0) AS WebstoreBranch_isBaking,
                CASE WHEN wbr.WebstoreBranch_isBaking = 1 THEN 1 
                     ELSE (CASE WHEN ow.OrderWorth_OrderID IS NULL THEN 0 
                                WHEN ow.OrderWorth_IsPending = 0 THEN 1 ELSE 0 END) 
                END AS IsBakingCostAvailable
            FROM tbl_order o 
            INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID 
            INNER JOIN tbl_webstore w ON o.order_bakeryID = w.webstore_ID 
            INNER JOIN tbl_shippingDetail sd ON o.order_ID = sd.shipping_orderID 
            INNER JOIN tbl_billingDetail b ON o.order_ID = b.billing_orderID 
            LEFT JOIN tbl_PostcodeDistance ON Postcode1 = sd.shipping_zip AND Postcode2 = REPLACE(w.webstore_postcode,' ','')
            LEFT OUTER JOIN tbl_WebstoreBranch wbr ON o.order_branchID = wbr.WebstoreBranch_BranchID 
            LEFT OUTER JOIN tbl_OrderWorth ow ON o.order_ID = ow.OrderWorth_OrderID
            WHERE (o.order_bakeryID = @WebshopId OR o.order_branchID = @WebshopId) 
              AND o.order_ID = @OrderId 
              AND o.order_isdeleted = 0";

        OrderDetailResult? result = null;

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@WebshopId", webshopId);
            cmd.Parameters.AddWithValue("@OrderId", orderId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result = new OrderDetailResult
                {
                    OrderId = GetInt64Safe(reader, "order_ID"),
                    OrderDate = GetDateTimeSafe(reader, "order_date"),
                    Status = GetInt32Safe(reader, "order_status"),
                    CustomerName = GetStringSafe(reader, "order_customerName"),
                    CustomerEmail = GetStringSafe(reader, "order_customerEmail"),
                    TotalPrice = GetDecimalSafe(reader, "order_totalPrice"),
                    CSMargin = GetDecimalSafe(reader, "order_CSmargin"),
                    ShopMargin = GetDecimalSafe(reader, "order_shopMargin"),
                    PayoutRefund = GetDecimalSafe(reader, "order_payoutRefund"),
                    CsRefund = GetDecimalSafe(reader, "order_csRefund"),
                    SaleType = GetInt32Safe(reader, "order_saletype"),
                    IsRepeat = GetBoolSafe(reader, "order_isrepeat"),
                    FollowingOrderId = GetInt64Safe(reader, "order_followingorderid"),
                    ForwardedOrderId = GetInt64Safe(reader, "order_forwardedorderid"),
                    BakeryId = GetInt64Safe(reader, "order_bakeryID"),
                    BranchId = GetInt64Safe(reader, "order_branchID"),
                    ShippingCost = GetDecimalSafe(reader, "order_shippingCost"),
                    Qty = GetInt32Safe(reader, "order_quality"),
                    CustomerId = GetInt64Safe(reader, "order_customerID"),
                    ShopId = GetInt64Safe(reader, "order_shopID"),
                    // Shipping
                    ShippingFirstName = GetStringSafe(reader, "shipping_fName"),
                    ShippingLastName = GetStringSafe(reader, "shipping_lName"),
                    ShippingPhone = GetStringSafe(reader, "shipping_phone"),
                    ShippingAddress = GetStringSafe(reader, "shipping_address"),
                    ShippingCity = GetStringSafe(reader, "shipping_city"),
                    ShippingCounty = GetStringSafe(reader, "shipping_county"),
                    ShippingCountry = GetStringSafe(reader, "shipping_country"),
                    ShippingZip = GetStringSafe(reader, "shipping_zip"),
                    // Billing
                    BillingFirstName = GetStringSafe(reader, "billing_fName"),
                    BillingLastName = GetStringSafe(reader, "billing_lName"),
                    BillingPhone = GetStringSafe(reader, "billing_phone"),
                    BillingAddress = GetStringSafe(reader, "billing_address"),
                    BillingCity = GetStringSafe(reader, "billing_city"),
                    BillingCounty = GetStringSafe(reader, "billing_county"),
                    BillingCountry = GetStringSafe(reader, "billing_country"),
                    BillingZip = GetStringSafe(reader, "billing_zip"),
                    BillingEmail = GetStringSafe(reader, "billing_emailID"),
                    // Collection/Delivery
                    DeliveryMode = GetInt32Safe(reader, "ordercollection_deliverymode"),
                    CollectionDate = GetDateTimeSafe(reader, "ordercollection_Date"),
                    DispatchDate = GetDateTimeSafe(reader, "ordercollection_DispatchDate"),
                    OccasionDate = GetDateTimeSafe(reader, "ordercollection_OcasionDate"),
                    Occasion = GetStringSafe(reader, "ordercollection_Ocasion"),
                    CollectionRemarks = GetStringSafe(reader, "ordercollection_Remarks"),
                    // Bakery info
                    BakeryName = GetStringSafe(reader, "webstore_businessName"),
                    BakeryPostcode = GetStringSafe(reader, "webstore_postcode"),
                    BakeryAddress = GetStringSafe(reader, "webstore_address"),
                    BakeryCity = GetStringSafe(reader, "webstore_city"),
                    BakeryState = GetStringSafe(reader, "webstore_State"),
                    BakeryOrderEmail = GetStringSafe(reader, "webstore_OrderEmail"),
                    BakeryBusinessPhone = GetStringSafe(reader, "webstore_businessPhone"),
                    PostcodeDistance = GetStringSafe(reader, "PCdistance"),
                    IsBaking = GetBoolSafe(reader, "WebstoreBranch_isBaking"),
                    IsBakingCostAvailable = GetBoolSafe(reader, "IsBakingCostAvailable")
                };
            }
        }

        if (result == null) return null;

        // Get line items (order details)
        await LoadLineItemsAsync(conn, result, webshopId);

        // Get branch detail if bakeryId != branchId
        if (result.BakeryId != result.BranchId)
        {
            await LoadBranchDetailAsync(conn, result);
        }

        // Load baking cost items
        await LoadBakingCostAsync(conn, result);

        return result;
    }

    private async Task LoadLineItemsAsync(SqlConnection conn, OrderDetailResult result, string webshopId)
    {
        var detailSql = @"
            SELECT 
                od.orderDetail_ID,
                od.orderDetail_orderID,
                od.orderDetail_productID,
                od.orderDetail_productName,
                od.orderDetail_ProductImage,
                od.orderDetail_Quantity,
                od.orderDetail_price,
                od.orderDetail_totalPrice,
                od.orderDetail_totalMargin,
                od.orderDetail_SizeID,
                od.orderDetail_prdType,
                p.product_ID,
                p.product_Name,
                p.product_image1,
                p.product_SEOURL,
                p.product_type,
                p.product_isexpired,
                p.Product_CDNSts,
                sp.product_id AS sp_prdid,
                sp.product_code AS sp_sku,
                sp.product_name AS sp_prdname,
                CASE WHEN p.product_isexpired = 1 AND s.SkuMapping_newPrdID IS NULL THEN 0 ELSE 1 END AS IsCustomizedPrdMapped
            FROM tbl_orderDetail od 
            INNER JOIN tbl_products p ON od.orderDetail_productID = p.product_ID 
            LEFT OUTER JOIN tbl_skumapping s ON s.SkuMapping_newPrdID = od.orderDetail_productID 
            LEFT OUTER JOIN tbl_products sp ON s.SkuMapping_refPrdID = sp.product_id
            WHERE od.orderDetail_orderID = @OrderId
            ORDER BY od.orderDetail_ID DESC";

        await using var cmd = new SqlCommand(detailSql, conn);
        cmd.Parameters.AddWithValue("@OrderId", result.OrderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<OrderDetailLineItem>();

        while (await reader.ReadAsync())
        {
            items.Add(new OrderDetailLineItem
            {
                OrderDetailId = GetInt64Safe(reader, "orderDetail_ID"),
                ProductId = GetInt64Safe(reader, "product_ID"),
                ProductName = GetStringSafe(reader, "product_Name"),
                ProductImage = GetStringSafe(reader, "product_image1"),
                ProductSeoUrl = GetStringSafe(reader, "product_SEOURL"),
                ProductCdnSts = GetStringSafe(reader, "Product_CDNSts"),
                Quantity = GetInt32Safe(reader, "orderDetail_Quantity"),
                UnitPrice = GetDecimalSafe(reader, "orderDetail_price"),
                TotalPrice = GetDecimalSafe(reader, "orderDetail_totalPrice"),
                TotalMargin = GetDecimalSafe(reader, "orderDetail_totalMargin"),
                ProductType = GetInt32Safe(reader, "product_type"),
                SizeId = GetInt32Safe(reader, "orderDetail_SizeID"),
                PrdType = GetInt32Safe(reader, "orderDetail_prdType"),
                IsExpired = GetBoolSafe(reader, "product_isexpired"),
                IsCustomizedPrdMapped = GetInt32Safe(reader, "IsCustomizedPrdMapped") == 1,
                SkuPrdId = GetInt64Safe(reader, "sp_prdid"),
                SkuPrdName = GetStringSafe(reader, "sp_prdname"),
                SkuCode = GetStringSafe(reader, "sp_sku")
            });
        }

        result.LineItems = items;

        // Load attributes, files, and topper items for each line item
        await reader.CloseAsync();
        foreach (var item in result.LineItems)
        {
            await LoadAttributesAsync(conn, item);
            await LoadFilesAsync(conn, item, result.OrderId);
            // Load toppers (typeId=4), accessories (typeId=2), cutters (typeId=5), packaging (typeId=7), supplies (typeId=8)
            item.Toppers = await LoadTopperItemsAsync(conn, webshopId, result.OrderId.ToString(), item.OrderDetailId.ToString(), item.ProductId.ToString(), 4);
            item.Accessories = await LoadTopperItemsAsync(conn, webshopId, result.OrderId.ToString(), item.OrderDetailId.ToString(), item.ProductId.ToString(), 2);
            item.Cutters = await LoadCutterItemsAsync(conn, webshopId, item.ProductId, item.SkuPrdId, item.ProductType, item.SizeId);
            item.Packaging = await LoadTopperItemsAsync(conn, webshopId, result.OrderId.ToString(), item.OrderDetailId.ToString(), item.ProductId.ToString(), 7);
            item.Supplies = await LoadTopperItemsAsync(conn, webshopId, result.OrderId.ToString(), item.OrderDetailId.ToString(), item.ProductId.ToString(), 8);
        }
    }

    private async Task LoadAttributesAsync(SqlConnection conn, OrderDetailLineItem item)
    {
        // Get shape, size, type from tbl_orderDetail + lookup tables (matching legacy bindAtrByID logic)
        var sql = @"
            SELECT 
                od.orderDetail_shapeId,
                od.orderDetail_SizeID,
                od.orderDetail_TypeID,
                od.orderDetail_ShapeText AS ShapeText,
                od.orderDetail_Quantity,
                ISNULL(cs.CakeShapeTitle, '') AS ShapeTitle,
                ISNULL(cs.CakeShapeCustomText, 0) AS CakeShapeCustomText,
                ISNULL(ct.CakeTypeTitle, '') AS TypeTitle,
                ISNULL(sz.SizeTitle, '') AS SizeTitle
            FROM tbl_orderDetail od
            LEFT JOIN tbl_CakeShape cs ON od.orderDetail_shapeId = cs.CakeShapeID
            LEFT JOIN tbl_CakeType ct ON od.orderDetail_TypeID = ct.CakeTypeID
            LEFT JOIN tbl_CakeSize sz ON od.orderDetail_SizeID = sz.SizeID
            WHERE od.orderDetail_ID = @OrderDetailId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderDetailId", item.OrderDetailId);

        var attrs = new List<OrderDetailAttribute>();
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var shapeTitle = GetStringSafe(reader, "ShapeTitle");
            var typeTitle = GetStringSafe(reader, "TypeTitle");
            var sizeTitle = GetStringSafe(reader, "SizeTitle");
            var shapeText = GetStringSafe(reader, "ShapeText");
            var shapeCustomText = GetInt32Safe(reader, "CakeShapeCustomText");

            if (!string.IsNullOrEmpty(shapeTitle))
                attrs.Add(new OrderDetailAttribute { Title = "Shape", Value = shapeTitle });
            if (shapeCustomText > 0 && !string.IsNullOrEmpty(shapeText))
                attrs.Add(new OrderDetailAttribute { Title = shapeCustomText == 1 ? "Letters A-Z" : "Numbers 0-9", Value = shapeText });
            if (!string.IsNullOrEmpty(typeTitle))
                attrs.Add(new OrderDetailAttribute { Title = "Type", Value = typeTitle });
            if (!string.IsNullOrEmpty(sizeTitle))
                attrs.Add(new OrderDetailAttribute { Title = "Size", Value = sizeTitle });
        }
        await reader.CloseAsync();

        // Also get flavour/attribute details from tbl_orderAttDet
        var attSql = @"
            SELECT 
                oad.orderAttDet_flavourType,
                oad.orderAttDet_AttIDs,
                ISNULL(a.Attribute_Name, '') AS AttributeName,
                ISNULL(ag.AttributeGroup_Name, '') AS GroupName
            FROM tbl_orderAttDet oad
            LEFT JOIN tbl_Attribute a ON oad.orderAttDet_AttIDs = a.Attribute_ID
            LEFT JOIN tbl_AttributeGroup ag ON a.Attribute_GroupID = ag.AttributeGroup_ID
            WHERE oad.orderAttDet_orderdetID = @OrderDetailId";

        await using var attCmd = new SqlCommand(attSql, conn);
        attCmd.Parameters.AddWithValue("@OrderDetailId", item.OrderDetailId);

        try
        {
            await using var attReader = await attCmd.ExecuteReaderAsync();
            while (await attReader.ReadAsync())
            {
                var groupName = GetStringSafe(attReader, "GroupName");
                var attrName = GetStringSafe(attReader, "AttributeName");
                if (!string.IsNullOrEmpty(attrName))
                {
                    attrs.Add(new OrderDetailAttribute
                    {
                        Title = !string.IsNullOrEmpty(groupName) ? groupName : "Attribute",
                        Value = attrName
                    });
                }
            }
        }
        catch
        {
            // tbl_orderAttDet may not exist in all environments - gracefully skip
        }

        item.Attributes = attrs;
    }

    private async Task LoadFilesAsync(SqlConnection conn, OrderDetailLineItem item, long orderId)
    {
        // Get files from tbl_ProductFile joined with tbl_lnkprdfile2size, plus tbl_orderBakeryFiles
        var sql = @"
            SELECT pf.ProductFile, pf.ProductFileTitle, pf.ProductID
            FROM tbl_ProductFile pf
            INNER JOIN tbl_lnkprdfile2size lnk ON pf.ProductFileID = lnk.PrdFileID AND lnk.PrdID = pf.ProductID
            WHERE pf.ProductID = @ProductId AND pf.IsAddtoOrder = 1 AND lnk.SizeID = @SizeId
            UNION ALL
            SELECT obf.orderBakeryFiles_fileName AS ProductFile, 
                   obf.orderBakeryFiles_title AS ProductFileTitle,
                   obf.orderBakeryFiles_productID AS ProductID
            FROM tbl_orderBakeryFiles obf
            WHERE obf.orderBakeryFiles_OrderID = @OrderId 
              AND obf.orderBakeryFiles_OrderDetailID = @OrderDetailId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
        cmd.Parameters.AddWithValue("@SizeId", item.SizeId);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@OrderDetailId", item.OrderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var files = new List<OrderDetailFile>();
        while (await reader.ReadAsync())
        {
            files.Add(new OrderDetailFile
            {
                FileName = GetStringSafe(reader, "ProductFile"),
                FileTitle = GetStringSafe(reader, "ProductFileTitle"),
                ProductId = GetInt64Safe(reader, "ProductID")
            });
        }
        item.Files = files;
    }

    private async Task LoadBranchDetailAsync(SqlConnection conn, OrderDetailResult result)
    {
        var sql = @"
            SELECT webstore_businessName, webstore_address, webstore_city, webstore_State, 
                   webstore_postcode, webstore_OrderEmail, webstore_businessPhone
            FROM tbl_webstore 
            WHERE webstore_ID = @BranchId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BranchId", result.BranchId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            result.BranchName = GetStringSafe(reader, "webstore_businessName");
            result.BranchAddress = GetStringSafe(reader, "webstore_address");
            result.BranchCity = GetStringSafe(reader, "webstore_city");
            result.BranchState = GetStringSafe(reader, "webstore_State");
            result.BranchPostcode = GetStringSafe(reader, "webstore_postcode");
            result.BranchOrderEmail = GetStringSafe(reader, "webstore_OrderEmail");
            result.BranchBusinessPhone = GetStringSafe(reader, "webstore_businessPhone");
        }
    }

    /// <summary>
    /// Loads topper/accessory/packaging/supply items from tbl_orderTopper joined with location hierarchy.
    /// Matches legacy GetLocation() method exactly.
    /// </summary>
    private async Task<List<OrderTopperItem>> LoadTopperItemsAsync(SqlConnection conn, string wid, string orderId, string orderDetailId, string prdId, int prdType)
    {
        var sql = @";WITH RCTE AS
(
select LocationID, LocationTitle, cast(LocationTitle as varchar(2000)) as FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
from tbl_location where ParentLocationId = 0 and location_isactive = 1 and location_isdeleted = 0 and webstoreid = @wid  

UNION ALL

SELECT rh.LocationID, rh.LocationTitle, cast(rc.FullLocation + ' > ' + rh.LocationTitle as varchar(2000)) as FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder FROM dbo.tbl_location rh
INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID where rh.Location_IsDeleted = 0 and location_isactive = 1
) select RCTE.LocationID, FullLocation, P.product_Name, P.product_image1, O.orderTopper_qty from RCTE 
inner join tbl_orderTopper O on O.orderTopper_LocID=RCTE.LocationID
inner join tbl_products P on P.Product_Id=O.orderTopper_prdID
where product_type = @prdtype and orderTopper_orderID=@orderid and orderTopper_orderdetailID=@orderdetid and Lvl = 3 order by DisplayOrder";

        var items = new List<OrderTopperItem>();
        try
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            cmd.Parameters.AddWithValue("@orderid", orderId);
            cmd.Parameters.AddWithValue("@orderdetid", orderDetailId);
            cmd.Parameters.AddWithValue("@prdtype", prdType);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new OrderTopperItem
                {
                    ProductName = GetStringSafe(reader, "product_Name"),
                    ProductImage = GetStringSafe(reader, "product_image1"),
                    Qty = GetInt32Safe(reader, "orderTopper_qty"),
                    FullLocation = GetStringSafe(reader, "FullLocation")
                });
            }
        }
        catch { /* tbl_orderTopper/tbl_location may not exist in all envs */ }
        return items;
    }

    /// <summary>
    /// Loads cutter items from tbl_StockLocation joined with location hierarchy.
    /// Matches legacy repCutters query with product_type=5 and tbl_Product_Topper.
    /// </summary>
    private async Task<List<OrderTopperItem>> LoadCutterItemsAsync(SqlConnection conn, string wid, long productId, long skuPrdId, int productType, int sizeId)
    {
        var pid = skuPrdId > 0 ? skuPrdId.ToString() : productId.ToString();
        var sizeFilter = productType == 1 ? " and pt.sizeID = @sizeID" : "";

        var sql = $@";WITH RCTE AS
(
select LocationID, LocationTitle, cast(LocationTitle as varchar(2000)) as FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
from tbl_location where ParentLocationId = 0 and location_isactive = 1 and location_isdeleted = 0 and webstoreid = @wid  

UNION ALL

SELECT rh.LocationID, rh.LocationTitle, cast(rc.FullLocation + ' > ' + rh.LocationTitle as varchar(2000)) as FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder FROM dbo.tbl_location rh
INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID where rh.Location_IsDeleted = 0 and location_isactive = 1
) select RCTE.LocationID, FullLocation, P.product_Name, P.product_image1, P.Product_Id from RCTE 
inner join tbl_StockLocation O on O.LocationID=RCTE.LocationID
inner join tbl_products P on P.Product_Id=O.Product_Id
inner join tbl_Product_Topper pt on P.Product_Id=pt.Topper_PrdId {sizeFilter}
inner join
(
select T_PrdId,max(totalcount) max_permonth,min(totalcount) min_permonth,avg(totalcount) avg_permonth from 
(
select t.Topper_PrdId T_PrdId, count(1) countprd,year(orderDetail_date) year_Name,
     month(orderDetail_date) month_name,sum(Qty) totalcount
from tbl_Product_Topper t 
inner join tbl_orderDetail od on od.orderDetail_productID=t.Product_Id
where DATEDIFF(d, od.orderDetail_date, getdate()) <= 28*7
group by t.Topper_PrdId,year(orderDetail_date),
     month(orderDetail_date)
	
	 ) tbl
	 group by T_PrdId
	 ) t1 on pt.Topper_PrdId=t1.T_PrdId
where product_type=5 and pt.Product_Id=@pid and Lvl = 3 {sizeFilter} order by DisplayOrder";

        var items = new List<OrderTopperItem>();
        try
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            cmd.Parameters.AddWithValue("@pid", pid);
            if (productType == 1)
            {
                cmd.Parameters.AddWithValue("@sizeID", sizeId);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new OrderTopperItem
                {
                    ProductName = GetStringSafe(reader, "product_Name"),
                    ProductImage = GetStringSafe(reader, "product_image1"),
                    Qty = 0, // Cutters don't show qty in legacy
                    FullLocation = GetStringSafe(reader, "FullLocation")
                });
            }
        }
        catch { /* tables may not exist */ }
        return items;
    }

    /// <summary>
    /// Loads baking cost items for the order if IsBakingCostAvailable is false and status is 0.
    /// Matches legacy BindBakingCost() method.
    /// </summary>
    private async Task LoadBakingCostAsync(SqlConnection conn, OrderDetailResult result)
    {
        if (result.IsBakingCostAvailable || result.Status != 0)
            return;

        var sql = @"SELECT 
            od.orderDetail_orderID, od.orderDetail_productID, od.orderDetail_productName,
            od.orderDetail_ProductImage, od.orderDetail_SizeID, od.orderDetail_Quantity,
            baking_cost = CASE WHEN ow.OrderWorthDet_UnitBakingCost IS NULL 
                THEN (CASE WHEN p.product_type IN (1, 6) THEN ISNULL(f.CakeBaseCost, 0) ELSE p.product_startingtPrice END)
                ELSE ow.OrderWorthDet_UnitBakingCost END
            FROM tbl_orderDetail od 
            INNER JOIN tbl_products p ON od.orderDetail_productID = p.product_ID
            LEFT OUTER JOIN tbl_OrderWorthDet ow ON od.orderDetail_orderID = ow.OrderWorthDet_OrderID 
                AND od.orderDetail_productID = ow.OrderWorthDet_PrdId 
                AND od.orderDetail_SizeID = ow.OrderWorthDet_SizeId
            LEFT OUTER JOIN tbl_lnkprdtemplate pt ON od.orderDetail_productID = pt.lnkprdtemplate_prdId 
            LEFT OUTER JOIN tbl_TemplatePriceFormula f ON pt.lnkprdtemplate_templateID = f.TemplateID 
                AND f.SizeID = od.orderDetail_SizeID
            WHERE od.orderDetail_orderID = @OrderId
            ORDER BY orderDetail_ID DESC";

        try
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderId", result.OrderId);

            await using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<BakingCostItem>();
            while (await reader.ReadAsync())
            {
                items.Add(new BakingCostItem
                {
                    OrderId = GetInt64Safe(reader, "orderDetail_orderID"),
                    ProductId = GetInt64Safe(reader, "orderDetail_productID"),
                    ProductName = GetStringSafe(reader, "orderDetail_productName"),
                    ProductImage = GetStringSafe(reader, "orderDetail_ProductImage"),
                    SizeId = GetInt32Safe(reader, "orderDetail_SizeID"),
                    Quantity = GetInt32Safe(reader, "orderDetail_Quantity"),
                    BakingCost = GetDecimalSafe(reader, "baking_cost")
                });
            }
            result.BakingCostItems = items;
        }
        catch { /* tables may not exist in all envs */ }
    }

    /// <summary>
    /// Saves baking cost for a single product in an order.
    /// Matches legacy btnSaveBakingCost_Click -> SaveOrderBakingCost.
    /// </summary>
    public async Task SaveBakingCostAsync(long orderId, long productId, int sizeId, int quantity, decimal unitBakingCost)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Upsert into tbl_OrderWorthDet
        var sql = @"
            IF EXISTS (SELECT 1 FROM tbl_OrderWorthDet WHERE OrderWorthDet_OrderID = @OrderId AND OrderWorthDet_PrdId = @ProductId AND OrderWorthDet_SizeId = @SizeId)
            BEGIN
                UPDATE tbl_OrderWorthDet 
                SET OrderWorthDet_UnitBakingCost = @UnitCost,
                    OrderWorthDet_Qty = @Qty,
                    OrderWorthDet_TotalBakingCost = @UnitCost * @Qty
                WHERE OrderWorthDet_OrderID = @OrderId AND OrderWorthDet_PrdId = @ProductId AND OrderWorthDet_SizeId = @SizeId
            END
            ELSE
            BEGIN
                INSERT INTO tbl_OrderWorthDet (OrderWorthDet_OrderID, OrderWorthDet_PrdId, OrderWorthDet_SizeId, OrderWorthDet_Qty, OrderWorthDet_UnitBakingCost, OrderWorthDet_TotalBakingCost)
                VALUES (@OrderId, @ProductId, @SizeId, @Qty, @UnitCost, @UnitCost * @Qty)
            END";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@SizeId", sizeId);
        cmd.Parameters.AddWithValue("@Qty", quantity);
        cmd.Parameters.AddWithValue("@UnitCost", unitBakingCost);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Saves total baking cost for the order (upserts tbl_OrderWorth).
    /// Matches legacy SaveOrderTotalBakingCost.
    /// </summary>
    public async Task SaveOrderTotalBakingCostAsync(long orderId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            DECLARE @TotalCost DECIMAL(18,2) = (SELECT ISNULL(SUM(OrderWorthDet_TotalBakingCost), 0) FROM tbl_OrderWorthDet WHERE OrderWorthDet_OrderID = @OrderId);
            IF EXISTS (SELECT 1 FROM tbl_OrderWorth WHERE OrderWorth_OrderID = @OrderId)
            BEGIN
                UPDATE tbl_OrderWorth SET OrderWorth_TotalBakingCost = @TotalCost, OrderWorth_IsPending = 0 WHERE OrderWorth_OrderID = @OrderId
            END
            ELSE
            BEGIN
                INSERT INTO tbl_OrderWorth (OrderWorth_OrderID, OrderWorth_TotalBakingCost, OrderWorth_IsPending) VALUES (@OrderId, @TotalCost, 0)
            END";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        await cmd.ExecuteNonQueryAsync();
    }

    // Safe type helpers (same pattern as BusinessOrdersService)
    private static string GetStringSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return "";
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(string)) return reader.GetString(ordinal);
        return reader.GetValue(ordinal)?.ToString() ?? "";
    }

    private static long GetInt64Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(long)) return reader.GetInt64(ordinal);
        if (fieldType == typeof(int)) return reader.GetInt32(ordinal);
        if (fieldType == typeof(short)) return reader.GetInt16(ordinal);
        var val = reader.GetValue(ordinal)?.ToString() ?? "";
        return long.TryParse(val, out var result) ? result : 0;
    }

    private static int GetInt32Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(int)) return reader.GetInt32(ordinal);
        if (fieldType == typeof(short)) return reader.GetInt16(ordinal);
        if (fieldType == typeof(long)) return (int)reader.GetInt64(ordinal);
        if (fieldType == typeof(byte)) return reader.GetByte(ordinal);
        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal GetDecimalSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0m;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(decimal)) return reader.GetDecimal(ordinal);
        if (fieldType == typeof(double)) return (decimal)reader.GetDouble(ordinal);
        if (fieldType == typeof(float)) return (decimal)reader.GetFloat(ordinal);
        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool GetBoolSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return false;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(bool)) return reader.GetBoolean(ordinal);
        return Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static DateTime GetDateTimeSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
        return reader.GetDateTime(ordinal);
    }

    // ─── MUTATION METHODS (matching legacy code-behind) ─────────────────────────

    /// <summary>
    /// Updates order status. Matches legacy OrderJobAssinged_onclick, OrderProcessed_onclick,
    /// OrderUnderDelivery_onclick, OrderCompleted_onclick patterns.
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(long orderId, int newStatus, long webshopId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_order SET order_status = @Status 
                    WHERE order_ID = @OrderId 
                      AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Status", newStatus);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows > 0)
        {
            await AddOrderLogAsync(conn, orderId, newStatus, userId);
        }
        return rows > 0;
    }

    /// <summary>
    /// Confirms order (legacy lnkApprove_onclick): confirms + sets status to 5 (Job Assigned).
    /// The legacy called ConfirmOrderAndSaveTopperQuantityByOrderID then set status=5.
    /// </summary>
    public async Task<bool> ConfirmOrderAsync(long orderId, long webshopId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Get saleType
        var saleTypeSql = "SELECT order_saletype FROM tbl_order WHERE order_ID = @OrderId";
        await using var stCmd = new SqlCommand(saleTypeSql, conn);
        stCmd.Parameters.AddWithValue("@OrderId", orderId);
        var saleTypeObj = await stCmd.ExecuteScalarAsync();
        var saleType = saleTypeObj != null ? Convert.ToInt32(saleTypeObj) : 1;

        // Set status to 5 (Job Assigned) — matching legacy which does confirm then immediately sets to 5
        var sql = @"UPDATE tbl_order SET order_status = 5 
                    WHERE order_ID = @OrderId 
                      AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows > 0)
        {
            await AddOrderLogAsync(conn, orderId, 1, userId); // log confirm
            await AddOrderLogAsync(conn, orderId, 5, userId); // log job assigned
        }
        return rows > 0;
    }

    /// <summary>
    /// Cancels order with reason/remarks (legacy btnCancelOrder_Click).
    /// Sets status=11, records cancel reason and remarks.
    /// </summary>
    public async Task<bool> CancelOrderAsync(long orderId, long webshopId, int userId,
        string cancelReason, string cancelRemarks, bool notifyCustomer)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_order 
                    SET order_status = 11, 
                        order_CancelRemarks = @Remarks, 
                        order_CancelReason = @Reason 
                    WHERE order_ID = @OrderId 
                      AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Remarks", cancelRemarks ?? "");
        cmd.Parameters.AddWithValue("@Reason", cancelReason ?? "");
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows > 0)
        {
            await AddOrderLogAsync(conn, orderId, 11, userId);
        }
        return rows > 0;
    }

    /// <summary>
    /// Soft-deletes order (legacy Ordedeleted_onclick). Sets order_isdeleted=1.
    /// </summary>
    public async Task<bool> RemoveOrderAsync(long orderId, long webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_order SET order_isdeleted = 1 
                    WHERE order_ID = @OrderId 
                      AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// Forwards an order by creating a forwarded copy (matching legacy forward order).
    /// Sets the current order's order_followingorderid and returns the new order ID.
    /// </summary>
    public async Task<long> ForwardOrderAsync(long orderId, long webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Create a new order cloned from the original, marking it as forwarded
        var sql = @"
            DECLARE @NewOrderId BIGINT;

            INSERT INTO tbl_order (
                order_date, order_status, order_customerName, order_customerEmail,
                order_totalPrice, order_CSmargin, order_shopMargin,
                order_saletype, order_isrepeat, order_forwardedorderid,
                order_bakeryID, order_branchID, order_shippingCost,
                order_quality, order_customerID, order_shopID, order_isdeleted
            )
            SELECT 
                GETDATE(), 0, order_customerName, order_customerEmail,
                order_totalPrice, order_CSmargin, order_shopMargin,
                order_saletype, 1, @OrderId,
                order_bakeryID, order_branchID, order_shippingCost,
                order_quality, order_customerID, order_shopID, 0
            FROM tbl_order 
            WHERE order_ID = @OrderId 
              AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId);

            SET @NewOrderId = SCOPE_IDENTITY();

            -- Copy order details
            INSERT INTO tbl_orderDetail (
                orderDetail_orderID, orderDetail_productID, orderDetail_productName,
                orderDetail_ProductImage, orderDetail_Quantity, orderDetail_price,
                orderDetail_totalPrice, orderDetail_totalMargin, orderDetail_SizeID,
                orderDetail_prdType, orderDetail_shapeId, orderDetail_TypeID,
                orderDetail_date
            )
            SELECT 
                @NewOrderId, orderDetail_productID, orderDetail_productName,
                orderDetail_ProductImage, orderDetail_Quantity, orderDetail_price,
                orderDetail_totalPrice, orderDetail_totalMargin, orderDetail_SizeID,
                orderDetail_prdType, orderDetail_shapeId, orderDetail_TypeID,
                GETDATE()
            FROM tbl_orderDetail WHERE orderDetail_orderID = @OrderId;

            -- Copy collection
            INSERT INTO tbl_ordercollection (
                ordercollection_OrderID, ordercollection_deliverymode,
                ordercollection_Date, ordercollection_DispatchDate,
                ordercollection_OcasionDate, ordercollection_Ocasion,
                ordercollection_Remarks
            )
            SELECT 
                @NewOrderId, ordercollection_deliverymode,
                ordercollection_Date, ordercollection_DispatchDate,
                ordercollection_OcasionDate, ordercollection_Ocasion,
                ordercollection_Remarks
            FROM tbl_ordercollection WHERE ordercollection_OrderID = @OrderId;

            -- Copy shipping
            INSERT INTO tbl_shippingDetail (
                shipping_orderID, shipping_fName, shipping_lName, shipping_phone,
                shipping_address, shipping_city, shipping_county, shipping_country, shipping_zip
            )
            SELECT 
                @NewOrderId, shipping_fName, shipping_lName, shipping_phone,
                shipping_address, shipping_city, shipping_county, shipping_country, shipping_zip
            FROM tbl_shippingDetail WHERE shipping_orderID = @OrderId;

            -- Copy billing
            INSERT INTO tbl_billingDetail (
                billing_orderID, billing_fName, billing_lName, billing_phone,
                billing_address, billing_city, billing_county, billing_country, billing_zip,
                billing_emailID
            )
            SELECT 
                @NewOrderId, billing_fName, billing_lName, billing_phone,
                billing_address, billing_city, billing_county, billing_country, billing_zip,
                billing_emailID
            FROM tbl_billingDetail WHERE billing_orderID = @OrderId;

            -- Link current order to the new forwarded order
            UPDATE tbl_order SET order_followingorderid = @NewOrderId WHERE order_ID = @OrderId;

            SELECT @NewOrderId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private async Task AddOrderLogAsync(SqlConnection conn, long orderId, int status, int userId)
    {
        var sql = @"INSERT INTO tbl_orderlog (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn)
                    VALUES (@UserId, @Status, @OrderId, GETDATE())";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await cmd.ExecuteNonQueryAsync();
    }
}

#region Models

/// <summary>
/// Full order detail result for the order detail page.
/// </summary>
public class OrderDetailResult
{
    // Order level
    public long OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public int Status { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public decimal TotalPrice { get; set; }
    public decimal CSMargin { get; set; }
    public decimal ShopMargin { get; set; }
    public decimal PayoutRefund { get; set; }
    public decimal CsRefund { get; set; }
    public int SaleType { get; set; }
    public bool IsRepeat { get; set; }
    public long FollowingOrderId { get; set; }
    public long ForwardedOrderId { get; set; }
    public long BakeryId { get; set; }
    public long BranchId { get; set; }
    public decimal ShippingCost { get; set; }
    public int Qty { get; set; }
    public long CustomerId { get; set; }
    public long ShopId { get; set; }

    // Shipping
    public string ShippingFirstName { get; set; } = "";
    public string ShippingLastName { get; set; } = "";
    public string ShippingPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string ShippingCity { get; set; } = "";
    public string ShippingCounty { get; set; } = "";
    public string ShippingCountry { get; set; } = "";
    public string ShippingZip { get; set; } = "";

    // Billing
    public string BillingFirstName { get; set; } = "";
    public string BillingLastName { get; set; } = "";
    public string BillingPhone { get; set; } = "";
    public string BillingAddress { get; set; } = "";
    public string BillingCity { get; set; } = "";
    public string BillingCounty { get; set; } = "";
    public string BillingCountry { get; set; } = "";
    public string BillingZip { get; set; } = "";
    public string BillingEmail { get; set; } = "";

    // Collection/Delivery
    public int DeliveryMode { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime DispatchDate { get; set; }
    public DateTime OccasionDate { get; set; }
    public string Occasion { get; set; } = "";
    public string CollectionRemarks { get; set; } = "";

    // Bakery info
    public string BakeryName { get; set; } = "";
    public string BakeryPostcode { get; set; } = "";
    public string BakeryAddress { get; set; } = "";
    public string BakeryCity { get; set; } = "";
    public string BakeryState { get; set; } = "";
    public string BakeryOrderEmail { get; set; } = "";
    public string BakeryBusinessPhone { get; set; } = "";
    public string PostcodeDistance { get; set; } = "";
    public bool IsBaking { get; set; }
    public bool IsBakingCostAvailable { get; set; }

    // Branch info (when bakeryId != branchId)
    public string BranchName { get; set; } = "";
    public string BranchAddress { get; set; } = "";
    public string BranchCity { get; set; } = "";
    public string BranchState { get; set; } = "";
    public string BranchPostcode { get; set; } = "";
    public string BranchOrderEmail { get; set; } = "";
    public string BranchBusinessPhone { get; set; } = "";

    // Line items
    public List<OrderDetailLineItem> LineItems { get; set; } = new();

    // Baking cost items (only populated when IsBakingCostAvailable==false && Status==0)
    public List<BakingCostItem> BakingCostItems { get; set; } = new();

    // Computed properties
    public bool IsFranchise => BakeryId != BranchId;
    public string ShippingFullName => $"{ShippingFirstName} {ShippingLastName}".Trim();
    public string BillingFullName => $"{BillingFirstName} {BillingLastName}".Trim();
    public decimal BakeryPayout => TotalPrice - CSMargin - ShopMargin;
    public decimal TotalRefund => PayoutRefund + CsRefund;

    public string StatusText => Status switch
    {
        0 => "Pending",
        1 => "Confirmed",
        2 => "Processed",
        3 => "Under Delivery",
        4 => "Completed",
        5 => "Job Assigned",
        10 => "Cancelled",
        11 => "Cancelled",
        _ => "Unknown"
    };

    public string StatusButtonClass => Status switch
    {
        0 or 10 or 11 => "btn-danger",
        4 => "btn-info",
        _ => "btn-success"
    };

    /// <summary>
    /// Gets the order ID display text with repeat/forwarded indicators (matching legacy).
    /// </summary>
    public string OrderIdDisplay
    {
        get
        {
            var text = OrderId.ToString();
            if (ForwardedOrderId > 0)
                text += $" (Forwarded from #{ForwardedOrderId})";
            if (IsRepeat)
                text += " (Repeat)";
            return text;
        }
    }
}

/// <summary>
/// Individual line item in the order detail.
/// </summary>
public class OrderDetailLineItem
{
    public long OrderDetailId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
    public string ProductCdnSts { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalMargin { get; set; }
    public int ProductType { get; set; }
    public int SizeId { get; set; }
    public int PrdType { get; set; }
    public bool IsExpired { get; set; }
    public bool IsCustomizedPrdMapped { get; set; }
    // SKU mapping
    public long SkuPrdId { get; set; }
    public string SkuPrdName { get; set; } = "";
    public string SkuCode { get; set; } = "";
    // Attributes
    public List<OrderDetailAttribute> Attributes { get; set; } = new();
    // Files
    public List<OrderDetailFile> Files { get; set; } = new();
    // Topper items per section
    public List<OrderTopperItem> Toppers { get; set; } = new();
    public List<OrderTopperItem> Accessories { get; set; } = new();
    public List<OrderTopperItem> Cutters { get; set; } = new();
    public List<OrderTopperItem> Packaging { get; set; } = new();
    public List<OrderTopperItem> Supplies { get; set; } = new();

    /// <summary>
    /// Calculates the display unit price (after removing margins) matching legacy logic.
    /// </summary>
    public decimal GetDisplayUnitPrice(int saleType, decimal csMargin, decimal shopMargin)
    {
        if (saleType != 2)
        {
            if (Quantity == 1)
                return UnitPrice - csMargin - shopMargin;
            return (TotalPrice - csMargin - shopMargin) / Quantity;
        }
        else
        {
            if (Quantity == 1)
                return UnitPrice - TotalMargin;
            return UnitPrice - (TotalMargin / Quantity);
        }
    }

    /// <summary>
    /// Calculates the display total price (after removing margins) matching legacy logic.
    /// </summary>
    public decimal GetDisplayTotalPrice(int saleType, decimal csMargin, decimal shopMargin)
    {
        if (saleType != 2)
            return TotalPrice - csMargin - shopMargin;
        return TotalPrice - TotalMargin;
    }
}

/// <summary>
/// Product attribute (size, shape, type, etc.)
/// </summary>
public class OrderDetailAttribute
{
    public string Title { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// File/document attached to an order detail line item.
/// </summary>
public class OrderDetailFile
{
    public string FileName { get; set; } = "";
    public string FileTitle { get; set; } = "";
    public long ProductId { get; set; }
}

/// <summary>
/// Topper/Accessory/Cutter/Packaging/Supply item attached to an order detail line item.
/// </summary>
public class OrderTopperItem
{
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public int Qty { get; set; }
    public string FullLocation { get; set; } = "";
}

/// <summary>
/// Baking cost line item for the baking cost table.
/// </summary>
public class BakingCostItem
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public int SizeId { get; set; }
    public int Quantity { get; set; }
    public decimal BakingCost { get; set; }
}

#endregion
