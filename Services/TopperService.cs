using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class TopperProductDetail
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public string? ProductCode { get; set; }
    public string? SeoUrl { get; set; }
}

public class AssignedTopperItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public string? SeoUrl { get; set; }
    public int Qty { get; set; }
    public long ProductTopperId { get; set; }
}

public class SizeTopperItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public string? SeoUrl { get; set; }
    public int InclType { get; set; }
    public int DisplayOrder { get; set; }
    public bool InStock { get; set; }
    public long SizeId { get; set; }
    public int Qty { get; set; }
    public string Remarks { get; set; } = "";
}

public class TopperStockLocationItem
{
    public long LocationId { get; set; }
    public string FullLocation { get; set; } = "";
    public int Qty { get; set; }
    public int TopperQty { get; set; }
    public long ProductId { get; set; }
    public int ProductType { get; set; }
}

public class OrderTopperQtyInput
{
    public long orderTopper_orderID { get; set; }
    public long orderTopper_orderdetailID { get; set; }
    public long orderTopper_prdID { get; set; }
    public long orderTopper_LocID { get; set; }
    public int orderTopper_qty { get; set; }
}

public class CakeSizeItem
{
    public int SizeId { get; set; }
    public string SizeTitle { get; set; } = "";
}

public class TopperService
{
    private readonly string _defaultConnection;

    public TopperService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<TopperProductDetail?> GetProductDetailsAsync(long productId, long webstoreId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT product_ID, product_Name, product_image1, product_code, product_seoURL 
                    FROM tbl_products 
                    WHERE product_WebstoreID = @wid AND product_ID = @pid AND product_isdeleted = 0";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new TopperProductDetail
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProductCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                SeoUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task<CakeSizeItem?> GetCakeSizeAsync(int sizeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE SizeID = @sizeId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sizeId", sizeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CakeSizeItem
            {
                SizeId = reader.GetInt32(0),
                SizeTitle = reader.IsDBNull(1) ? "" : reader.GetString(1)
            };
        }
        return null;
    }

    public async Task<List<AssignedTopperItem>> GetAssignedToppersAsync(long productId, long webstoreId, int typeId, int sizeId)
    {
        var list = new List<AssignedTopperItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT p.product_ID, p.product_Name, p.product_image1, p.product_seoURL, t.Qty, t.ProductTopperID 
                    FROM tbl_products p 
                    INNER JOIN tbl_product_topper t ON p.product_ID = t.Topper_PrdId 
                    WHERE p.product_WebstoreID = @wid AND t.product_id = @pid AND p.product_type = @typeId";

        if (sizeId > 0)
        {
            sql += " AND t.sizeID = @sizeId";
        }

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        if (sizeId > 0)
        {
            cmd.Parameters.AddWithValue("@sizeId", sizeId);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AssignedTopperItem
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                SeoUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                Qty = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                ProductTopperId = reader.GetInt64(5)
            });
        }
        return list;
    }

    public async Task<List<TopperProductDetail>> GetAvailableToppersAsync(string keyword, long productId, int typeId, long webstoreId)
    {
        var list = new List<TopperProductDetail>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT product_id, product_name, product_image1 
                    FROM tbl_products 
                    WHERE product_type = @typeId AND product_webstoreid = @wid AND product_isdeleted = 0 
                      AND product_id NOT IN (SELECT Topper_PrdId FROM tbl_product_topper WHERE product_id = @pid) 
                      AND product_name LIKE @keyword";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TopperProductDetail
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<bool> AddProductTopperAsync(long productId, long topperPrdId, int sizeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Check exists
        var checkSql = @"SELECT COUNT(1) FROM tbl_product_topper 
                         WHERE product_id = @pid AND Topper_PrdId = @topperPrdId" + (sizeId > 0 ? " AND sizeID = @sizeId" : "");
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@pid", productId);
            checkCmd.Parameters.AddWithValue("@topperPrdId", topperPrdId);
            if (sizeId > 0) checkCmd.Parameters.AddWithValue("@sizeId", sizeId);
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
            {
                return false; // Already exists
            }
        }

        var sql = @"INSERT INTO tbl_product_topper (product_id, Topper_PrdId, sizeID, Qty, CreateDate) 
                    VALUES (@pid, @topperPrdId, @sizeId, 1, GETDATE())";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@topperPrdId", topperPrdId);
        cmd.Parameters.AddWithValue("@sizeId", sizeId);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task RemoveProductTopperAsync(long topperPrdId, long productId, int sizeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "DELETE FROM tbl_product_topper WHERE Topper_PrdId = @topperPrdId AND product_id = @pid" + (sizeId > 0 ? " AND sizeID = @sizeId" : "");
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@topperPrdId", topperPrdId);
        cmd.Parameters.AddWithValue("@pid", productId);
        if (sizeId > 0) cmd.Parameters.AddWithValue("@sizeId", sizeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateTopperQtyAsync(long productTopperId, int qty)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "UPDATE tbl_product_topper SET Qty = @qty WHERE ProductTopperID = @id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qty", qty);
        cmd.Parameters.AddWithValue("@id", productTopperId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // ─── LinkTopper Support ────────────────────────────────────────────────────────

    public async Task<List<CakeSizeItem>> GetLinkedSizesAsync(long productId)
    {
        var list = new List<CakeSizeItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT cp.SizeID, cs.SizeTitle 
                    FROM tbl_CakePrice cp 
                    INNER JOIN tbl_CakeSize cs ON cs.SizeID = cp.SizeID 
                    WHERE cp.product_ID = @pid 
                    ORDER BY cp.cakeprice_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CakeSizeItem
            {
                SizeId = reader.GetInt32(0),
                SizeTitle = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<SizeTopperItem>> GetSizeToppersAsync(long productId, long webstoreId, int typeId, int sizeId)
    {
        var list = new List<SizeTopperItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT p.product_id, p.product_name, p.product_image1, p.product_seourl, 
                           l.lnkprdTopper_incltype, l.lnkprdTopper_displayorder, l.lnkprdTopper_instock, 
                           l.SizeID, l.qty, l.Remarks 
                    FROM tbl_products p 
                    INNER JOIN tbl_lnkprdTopper l ON p.product_id = l.lnkprdTopper_topperPrdID 
                    WHERE l.SizeID = @sizeId AND p.product_WebstoreID = @wid 
                      AND l.lnkprdTopper_prdID = @pid AND p.product_type = @typeId 
                    ORDER BY l.lnkprdTopper_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sizeId", sizeId);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@typeId", typeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SizeTopperItem
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                SeoUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                InclType = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                DisplayOrder = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                InStock = reader.IsDBNull(6) ? false : Convert.ToBoolean(reader.GetValue(6)),
                SizeId = reader.IsDBNull(7) ? 0 : Convert.ToInt64(reader.GetValue(7)),
                Qty = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8)),
                Remarks = reader.IsDBNull(9) ? "" : reader.GetString(9)
            });
        }
        return list;
    }

    public async Task<List<TopperProductDetail>> GetAvailableSizeToppersAsync(string keyword, long productId, int typeId, long webstoreId)
    {
        var list = new List<TopperProductDetail>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT product_id, product_name, product_image1 
                    FROM tbl_products 
                    WHERE product_type = @typeId AND product_webstoreid = @wid AND product_isdeleted = 0 
                      AND product_id NOT IN (
                          SELECT q1.lnkprdTopper_topperPrdID 
                          FROM (
                              SELECT l.lnkprdTopper_topperPrdID, SizeCount = COUNT(cp.SizeID) 
                              FROM tbl_CakePrice cp 
                              INNER JOIN tbl_CakeSize cs ON cs.SizeID = cp.SizeID 
                              CROSS JOIN (SELECT DISTINCT lnkprdTopper_topperPrdID FROM tbl_lnkprdTopper WHERE lnkprdTopper_prdID = @pid) AS l 
                              WHERE cp.product_ID = @pid 
                              GROUP BY l.lnkprdTopper_topperPrdID
                          ) q1 
                          INNER JOIN (
                              SELECT lnkprdTopper_topperPrdID, SizeCount = COUNT(sizeID) 
                              FROM tbl_lnkprdTopper 
                              WHERE lnkprdTopper_prdID = @pid 
                              GROUP BY lnkprdTopper_topperPrdID
                          ) q2 ON q1.lnkprdTopper_topperPrdID = q2.lnkprdTopper_topperPrdID AND q1.SizeCount = q2.SizeCount
                      ) 
                      AND product_name LIKE @keyword";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TopperProductDetail
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<int> SaveSizeTopperAsync(long productId, long topperPrdId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Get missing sizes
        var selectSql = @"SELECT cp.product_ID, cp.SizeID 
                          FROM tbl_CakePrice cp 
                          INNER JOIN tbl_CakeSize cs ON cs.SizeID = cp.SizeID 
                          WHERE cp.product_ID = @pid 
                          EXCEPT 
                          SELECT lnkprdTopper_prdID, SizeID 
                          FROM tbl_lnkprdTopper 
                          WHERE lnkprdTopper_prdID = @pid AND lnkprdTopper_topperPrdID = @topperId";

        var missingSizes = new List<int>();
        await using (var selectCmd = new SqlCommand(selectSql, conn))
        {
            selectCmd.Parameters.AddWithValue("@pid", productId);
            selectCmd.Parameters.AddWithValue("@topperId", topperPrdId);
            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                missingSizes.Add(reader.GetInt32(1));
            }
        }

        if (missingSizes.Count == 0)
        {
            return 0; // Already exists for all sizes
        }

        int maxDisplay = 1;
        var maxSql = "SELECT ISNULL(MAX(lnkprdTopper_displayorder), 1) FROM tbl_lnkprdTopper WHERE lnkprdTopper_prdID = @pid";
        await using (var maxCmd = new SqlCommand(maxSql, conn))
        {
            maxCmd.Parameters.AddWithValue("@pid", productId);
            maxDisplay = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());
        }

        int orderIndex = 1;
        foreach (var sizeId in missingSizes)
        {
            var insertSql = @"INSERT INTO tbl_lnkprdTopper (lnkprdTopper_prdID, lnkprdTopper_topperPrdID, SizeID, Qty, Remarks, lnkprdTopper_incltype, lnkprdTopper_displayorder, lnkprdTopper_instock) 
                              VALUES (@pid, @topperId, @sizeId, 1, '', 3, @displayOrder, 1)";
            await using var insertCmd = new SqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@pid", productId);
            insertCmd.Parameters.AddWithValue("@topperId", topperPrdId);
            insertCmd.Parameters.AddWithValue("@sizeId", sizeId);
            insertCmd.Parameters.AddWithValue("@displayOrder", maxDisplay + orderIndex);
            await insertCmd.ExecuteNonQueryAsync();
            orderIndex++;
        }

        return 1;
    }

    public async Task UpdateSizeTopperMappingAsync(long productId, long topperPrdId, int sizeId, string pricing, string mandatory, string stock, int displayOrder, int qty, string remarks)
    {
        int inclType = 3;
        if (pricing == "Included" && mandatory == "Optional") inclType = 1;
        else if (pricing == "Included" && mandatory == "Mandatory") inclType = 2;
        else if (pricing == "Excluded" && mandatory == "Optional") inclType = 3;
        else if (pricing == "Excluded" && mandatory == "Mandatory") inclType = 4;

        bool inStock = stock == "In Stock";

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_lnkprdTopper 
                    SET lnkprdTopper_displayorder = @displayOrder, 
                        lnkprdTopper_incltype = @inclType, 
                        lnkprdTopper_instock = @inStock, 
                        qty = @qty, 
                        Remarks = @remarks 
                    WHERE lnkprdTopper_prdID = @pid AND lnkprdTopper_topperPrdID = @topperId AND sizeID = @sizeId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
        cmd.Parameters.AddWithValue("@inclType", inclType);
        cmd.Parameters.AddWithValue("@inStock", inStock);
        cmd.Parameters.AddWithValue("@qty", qty);
        cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@topperId", topperPrdId);
        cmd.Parameters.AddWithValue("@sizeId", sizeId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveSizeTopperAsync(long productId, long topperPrdId, int sizeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "DELETE FROM tbl_lnkprdTopper WHERE lnkprdTopper_topperPrdID = @topperId AND lnkprdTopper_prdID = @pid AND SizeID = @sizeId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@topperId", topperPrdId);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@sizeId", sizeId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── OrderTopper Support ───────────────────────────────────────────────────────

    public async Task<Dictionary<string, object>?> GetOrderDetailWithProductAndOrderAsync(long orderId, long orderDetailId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT od.orderDetail_ID, od.orderDetail_orderID, od.orderDetail_productID, od.orderDetail_SizeID,
                           o.order_bakeryID, p.product_ID, p.product_Name, p.product_image1, p.product_code, p.product_seoURL,
                           CASE WHEN g.google_prdID IS NULL THEN 0 ELSE 1 END AS IsGooglePrd
                    FROM tbl_orderDetail od 
                    INNER JOIN tbl_order o ON od.orderDetail_orderID = o.order_ID 
                    LEFT OUTER JOIN tbl_skumapping s ON s.SkuMapping_newPrdID = od.orderDetail_productID 
                    INNER JOIN tbl_products p ON p.product_ID = CASE WHEN s.SkuMapping_refPrdID IS NULL THEN od.orderDetail_productID ELSE s.SkuMapping_refPrdID END 
                    LEFT OUTER JOIN tbl_googlefeedprd g ON p.product_id = g.google_prdID
                    WHERE o.order_ID = @orderId AND od.orderDetail_ID = @orderDetailId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
            }
            return dict;
        }
        return null;
    }

    public async Task<List<TopperProductDetail>> GetOrderToppersAsync(long productId, int typeId, int sizeId)
    {
        var list = new List<TopperProductDetail>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        string sql;
        if (typeId == 4)
        {
            sql = @"SELECT att.product_ID, att.product_Name, att.product_image1, att.product_seoURL 
                    FROM tbl_products att 
                    INNER JOIN tbl_product_topper lnk ON att.product_ID = lnk.Topper_PrdId 
                    WHERE att.product_type = @typeId AND lnk.product_Id = @pid";
        }
        else
        {
            sql = @"SELECT att.product_ID, att.product_Name, att.product_image1, att.product_seoURL 
                    FROM tbl_products att 
                    INNER JOIN tbl_product_topper lnk ON att.product_ID = lnk.Topper_PrdId 
                    WHERE att.product_type = @typeId AND lnk.sizeID = @sizeId AND lnk.product_Id = @pid";
        }

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        cmd.Parameters.AddWithValue("@pid", productId);
        if (typeId != 4)
        {
            cmd.Parameters.AddWithValue("@sizeId", sizeId);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TopperProductDetail
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                SeoUrl = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return list;
    }

    public async Task<List<TopperStockLocationItem>> GetLocationsWithStockAsync(long productId, long topperPrdId, long webstoreId)
    {
        var list = new List<TopperStockLocationItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @";WITH RCTE AS (
            SELECT LocationID, LocationTitle, CAST(LocationTitle AS VARCHAR(2000)) AS FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
            FROM tbl_location 
            WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid  
            UNION ALL
            SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle AS VARCHAR(2000)) AS FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
            FROM dbo.tbl_location rh
            INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID 
            WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
        ) 
        SELECT RCTE.LocationID, FullLocation, tbl_StockLocation.Qty AS Qty, tbl_Product_Topper.Qty AS topperQty, tbl_StockLocation.Product_Id, P.product_type 
        FROM RCTE 
        INNER JOIN tbl_StockLocation ON tbl_StockLocation.LocationID = RCTE.LocationID AND tbl_StockLocation.Product_Id = @pid 
        INNER JOIN tbl_products P ON P.product_ID = tbl_StockLocation.product_ID 
        INNER JOIN tbl_product_topper ON tbl_product_topper.topper_prdID = @pid AND tbl_product_topper.product_ID = @mainPid
        WHERE Lvl = 3 
        ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", topperPrdId);
        cmd.Parameters.AddWithValue("@mainPid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TopperStockLocationItem
            {
                LocationId = reader.GetInt64(0),
                FullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Qty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                TopperQty = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                ProductId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                ProductType = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5))
            });
        }
        return list;
    }

    public async Task SaveOrderToppersQtyAsync(long orderId, long orderDetailId, List<OrderTopperQtyInput> inputs, int userId)
    {
        if (inputs == null || inputs.Count == 0) return;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            // 1. Group by product ID and update tbl_products.product_quantity
            var grouped = inputs.GroupBy(i => i.orderTopper_prdID)
                                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.orderTopper_qty) })
                                .ToList();

            foreach (var item in grouped)
            {
                var productUpdateSql = @"UPDATE tbl_products 
                                         SET product_quantity = product_quantity - @qty 
                                         WHERE product_ID = @pid AND product_type IN (4, 2, 7, 8)";
                await using var cmd = new SqlCommand(productUpdateSql, conn, transaction);
                cmd.Parameters.AddWithValue("@qty", item.Qty);
                cmd.Parameters.AddWithValue("@pid", item.ProductId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Loop through each input and update tbl_stocklocation + merge tbl_orderTopper
            foreach (var item in inputs)
            {
                // Update tbl_stocklocation.Qty
                var stockUpdateSql = @"UPDATE tbl_stocklocation 
                                       SET Qty = Qty - @qty 
                                       WHERE Product_Id = @pid AND LocationID = @locId";
                await using (var cmd = new SqlCommand(stockUpdateSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@qty", item.orderTopper_qty);
                    cmd.Parameters.AddWithValue("@pid", item.orderTopper_prdID);
                    cmd.Parameters.AddWithValue("@locId", item.orderTopper_LocID);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Check if already in tbl_orderTopper (mimicking MERGE WHEN NOT MATCHED)
                var checkSql = @"SELECT COUNT(1) FROM tbl_orderTopper 
                                 WHERE orderTopper_orderID = @orderId AND orderTopper_orderdetailID = @orderDetailId 
                                   AND orderTopper_prdID = @prdId AND orderTopper_LocID = @locId";
                bool exists = false;
                await using (var cmd = new SqlCommand(checkSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@orderId", item.orderTopper_orderID);
                    cmd.Parameters.AddWithValue("@orderDetailId", item.orderTopper_orderdetailID);
                    cmd.Parameters.AddWithValue("@prdId", item.orderTopper_prdID);
                    cmd.Parameters.AddWithValue("@locId", item.orderTopper_LocID);
                    exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }

                if (!exists)
                {
                    var insertSql = @"INSERT INTO tbl_orderTopper (orderTopper_orderID, orderTopper_orderdetailID, orderTopper_prdID, orderTopper_LocID, orderTopper_qty, orderTopper_modifiedOn) 
                                      VALUES (@orderId, @orderDetailId, @prdId, @locId, @qty, GETDATE())";
                    await using var cmd = new SqlCommand(insertSql, conn, transaction);
                    cmd.Parameters.AddWithValue("@orderId", item.orderTopper_orderID);
                    cmd.Parameters.AddWithValue("@orderDetailId", item.orderTopper_orderdetailID);
                    cmd.Parameters.AddWithValue("@prdId", item.orderTopper_prdID);
                    cmd.Parameters.AddWithValue("@locId", item.orderTopper_LocID);
                    cmd.Parameters.AddWithValue("@qty", item.orderTopper_qty);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
