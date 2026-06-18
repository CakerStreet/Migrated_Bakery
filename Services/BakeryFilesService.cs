using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class BakeryFilesOrderInfo
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
    public bool IsGooglePrd { get; set; }
    public int PrdApiType { get; set; }
    public long OrderBakeryId { get; set; }
    public int OrderDetailSizeId { get; set; }
}

public class ProductFileItem
{
    public long ProductFileId { get; set; }
    public long ProductId { get; set; }
    public string ProductFile { get; set; } = "";
    public string ProductFileTitle { get; set; } = "";
    public bool IsAddtoOrder { get; set; }
    public int DisplayOrder { get; set; }
}

public class CakeSizeForFileItem
{
    public long PrdFileId { get; set; }
    public int SizeId { get; set; }
    public string SizeTitle { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public class OrderBakeryFileModel
{
    public int RecNo { get; set; }
    public long ProductFileID { get; set; } // maps to tbl_orderBakeryFiles_ID
    public long OrderId { get; set; }
    public long OrderDetailID { get; set; }
    public long ProductId { get; set; }
    public string ProductFileTitle { get; set; } = "";
    public string ProductFile { get; set; } = ""; // FileName
    public int IsDeleted { get; set; }
}

public class BakeryFilesService
{
    private readonly string _connectionString;

    public BakeryFilesService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<BakeryFilesOrderInfo?> GetOrderAndProductDetailsAsync(long orderId, long orderDetailId)
    {
        await using var conn = new SqlConnection(_connectionString);
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
            return new BakeryFilesOrderInfo
            {
                OrderDetailId = Convert.ToInt64(reader["orderDetail_ID"]),
                OrderId = Convert.ToInt64(reader["orderDetail_orderID"]),
                ProductId = Convert.ToInt64(reader["product_ID"]),
                OrderDetailSizeId = reader["orderDetail_SizeID"] != DBNull.Value ? Convert.ToInt32(reader["orderDetail_SizeID"]) : 0,
                OrderBakeryId = reader["order_bakeryID"] != DBNull.Value ? Convert.ToInt64(reader["order_bakeryID"]) : 0,
                ProductName = reader["product_Name"]?.ToString() ?? "",
                ProductCode = reader["product_code"]?.ToString() ?? "",
                ProductImage = reader["product_image1"]?.ToString() ?? "",
                ProductSeoUrl = reader["product_seoURL"]?.ToString() ?? "",
                IsGooglePrd = Convert.ToInt32(reader["IsGooglePrd"]) == 1,
                PrdApiType = 0
            };
        }
        return null;
    }

    public async Task<BakeryFilesOrderInfo?> GetProductDetailsOnlyAsync(long prdId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT p.product_ID, p.product_Name, p.product_image1, p.product_code, p.product_seoURL,
                           CASE WHEN g.google_prdID IS NULL THEN 0 ELSE 1 END AS IsGooglePrd
                    FROM tbl_products p 
                    LEFT OUTER JOIN tbl_googlefeedprd g ON p.product_id = g.google_prdID
                    WHERE p.product_ID = @prdId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@prdId", prdId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new BakeryFilesOrderInfo
            {
                OrderDetailId = 0,
                OrderId = 0,
                ProductId = Convert.ToInt64(reader["product_ID"]),
                OrderDetailSizeId = 0,
                OrderBakeryId = 0,
                ProductName = reader["product_Name"]?.ToString() ?? "",
                ProductCode = reader["product_code"]?.ToString() ?? "",
                ProductImage = reader["product_image1"]?.ToString() ?? "",
                ProductSeoUrl = reader["product_seoURL"]?.ToString() ?? "",
                IsGooglePrd = Convert.ToInt32(reader["IsGooglePrd"]) == 1,
                PrdApiType = 0
            };
        }
        return null;
    }

    public async Task<List<ProductFileItem>> GetProductFilesAsync(long productId)
    {
        var list = new List<ProductFileItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT ProductFileID, ProductID, ProductFile, ProductFileTitle, DisplayOrder, IsAddtoOrder
                    FROM tbl_ProductFile
                    WHERE ProductID = @productId
                    ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@productId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductFileItem
            {
                ProductFileId = reader.GetInt64(0),
                ProductId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                ProductFile = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ProductFileTitle = reader.IsDBNull(3) ? "" : reader.GetString(3),
                DisplayOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsAddtoOrder = reader.IsDBNull(5) ? false : reader.GetBoolean(5)
            });
        }
        return list;
    }

    public async Task<List<CakeSizeForFileItem>> GetCakeSizesForProductFilesAsync(long productId)
    {
        var list = new List<CakeSizeForFileItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT l.PrdFileID, s.SizeID, s.SizeTitle, s.DisplayOrder
                    FROM tbl_CakeSize s
                    INNER JOIN tbl_CakePrice p ON s.SizeID = p.SizeID
                    INNER JOIN tbl_lnkprdfile2size l ON p.product_ID = l.PrdID AND p.SizeID = l.SizeID
                    WHERE p.product_ID = @productId
                    ORDER BY s.DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@productId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CakeSizeForFileItem
            {
                PrdFileId = reader.GetInt64(0),
                SizeId = reader.GetInt32(1),
                SizeTitle = reader.IsDBNull(2) ? "" : reader.GetString(2),
                DisplayOrder = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            });
        }
        return list;
    }

    public async Task<List<OrderBakeryFileModel>> GetOrderBakeryFilesAsync(long orderId, long orderDetailId)
    {
        var list = new List<OrderBakeryFileModel>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT orderBakeryFiles_ID, orderBakeryFiles_OrderID, orderBakeryFiles_OrderDetailID,
                           orderBakeryFiles_productID, orderBakeryFiles_fileName, orderBakeryFiles_title,
                           orderBakeryFiles_displayorder
                    FROM tbl_orderBakeryFiles
                    WHERE orderBakeryFiles_OrderID = @orderId AND orderBakeryFiles_OrderDetailID = @orderDetailId
                    ORDER BY orderBakeryFiles_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        int count = 1;
        while (await reader.ReadAsync())
        {
            list.Add(new OrderBakeryFileModel
            {
                RecNo = count++,
                ProductFileID = reader.GetInt64(0),
                OrderId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                OrderDetailID = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                ProductId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                ProductFile = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ProductFileTitle = reader.IsDBNull(5) ? "" : reader.GetString(5),
                IsDeleted = 0
            });
        }
        return list;
    }

    public async Task<ProductFileItem?> GetProductFileByIdAsync(long productFileId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT ProductFileID, ProductID, ProductFile, ProductFileTitle, DisplayOrder, IsAddtoOrder
                    FROM tbl_ProductFile
                    WHERE ProductFileID = @pfid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pfid", productFileId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProductFileItem
            {
                ProductFileId = reader.GetInt64(0),
                ProductId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                ProductFile = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ProductFileTitle = reader.IsDBNull(3) ? "" : reader.GetString(3),
                DisplayOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsAddtoOrder = reader.IsDBNull(5) ? false : reader.GetBoolean(5)
            };
        }
        return null;
    }

    public async Task SaveBakeryDocumentsAsync(List<OrderBakeryFileModel> files, long productId, long orderId, long orderDetailId)
    {
        if (files == null || files.Count == 0) return;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            // Collect list of files to delete from disk (we can return this to delete physically)
            var filesToDelete = new List<string>();

            // 1. Handle deleted rows
            var delIds = files.Where(f => f.IsDeleted == 1 && f.ProductFileID > 0).Select(f => f.ProductFileID).ToList();
            if (delIds.Count > 0)
            {
                // Retrieve filenames before deleting
                var selectDelSql = $"SELECT orderBakeryFiles_fileName FROM tbl_orderBakeryFiles WHERE orderBakeryFiles_ID IN ({string.Join(",", delIds)})";
                await using (var cmd = new SqlCommand(selectDelSql, conn, transaction))
                {
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var fn = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        if (!string.IsNullOrEmpty(fn)) filesToDelete.Add(fn);
                    }
                }

                var deleteSql = $"DELETE FROM tbl_orderBakeryFiles WHERE orderBakeryFiles_ID IN ({string.Join(",", delIds)})";
                await using (var cmd = new SqlCommand(deleteSql, conn, transaction))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // 2. Insert or update active rows
            int displayOrder = 1;
            var activeFiles = files.Where(f => f.IsDeleted == 0).ToList();

            foreach (var pf in activeFiles)
            {
                if (pf.ProductFileID == 0)
                {
                    // Insert
                    var insertSql = @"INSERT INTO tbl_orderBakeryFiles (orderBakeryFiles_OrderID, orderBakeryFiles_OrderDetailID, orderBakeryFiles_productID, orderBakeryFiles_fileName, orderBakeryFiles_title, orderBakeryFiles_displayorder, orderBakeryFiles_createdOn)
                                      VALUES (@orderId, @orderDetailId, @productId, @fileName, @title, @displayOrder, GETDATE())";
                    await using (var insertCmd = new SqlCommand(insertSql, conn, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("@orderId", orderId);
                        insertCmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
                        insertCmd.Parameters.AddWithValue("@productId", productId);
                        insertCmd.Parameters.AddWithValue("@fileName", pf.ProductFile ?? "");
                        insertCmd.Parameters.AddWithValue("@title", pf.ProductFileTitle ?? "");
                        insertCmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Check if file changed
                    var checkSql = "SELECT orderBakeryFiles_fileName FROM tbl_orderBakeryFiles WHERE orderBakeryFiles_ID = @id";
                    string oldFileName = "";
                    await using (var checkCmd = new SqlCommand(checkSql, conn, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@id", pf.ProductFileID);
                        oldFileName = (await checkCmd.ExecuteScalarAsync())?.ToString() ?? "";
                    }

                    if (oldFileName != pf.ProductFile && !string.IsNullOrEmpty(oldFileName))
                    {
                        filesToDelete.Add(oldFileName);
                    }

                    // Update
                    var updateSql = @"UPDATE tbl_orderBakeryFiles
                                      SET orderBakeryFiles_title = @title,
                                          orderBakeryFiles_fileName = @fileName,
                                          orderBakeryFiles_displayorder = @displayOrder
                                      WHERE orderBakeryFiles_ID = @id";
                    await using (var updateCmd = new SqlCommand(updateSql, conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@title", pf.ProductFileTitle ?? "");
                        updateCmd.Parameters.AddWithValue("@fileName", pf.ProductFile ?? "");
                        updateCmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                        updateCmd.Parameters.AddWithValue("@id", pf.ProductFileID);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
                displayOrder++;
            }

            await transaction.CommitAsync();

            // Perform actual physical file deletion safely
            string fileDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "sku", productId.ToString(), "files");
            if (Directory.Exists(fileDir))
            {
                foreach (var file in filesToDelete)
                {
                    string filePath = Path.Combine(fileDir, file);
                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { /* ignore disk errors */ }
                    }
                }
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SaveTopperQuantityAsync(List<OrderTopperQtyInput> inputs)
    {
        if (inputs == null || inputs.Count == 0) return;

        await using var conn = new SqlConnection(_connectionString);
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

                // Check if already in tbl_orderTopper
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

    public async Task<List<Dictionary<string, object>>> GetAccessoryDetailsAsync(long orderId, long orderDetailId)
    {
        var list = new List<Dictionary<string, object>>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT att.product_ID, att.product_image1, att.product_SEOURL, att.product_Name, att.product_quantity
                    FROM tbl_orderAttDet
                    INNER JOIN tbl_products att ON product_ID = orderAttDet_ParentAttId
                    WHERE orderAttDet_orderID = @orderId AND orderAttDet_orderdetID = @orderDetailId AND orderAttDet_flavourType = 3";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>
            {
                ["product_ID"] = reader["product_ID"],
                ["product_image1"] = reader["product_image1"]?.ToString() ?? "",
                ["product_SEOURL"] = reader["product_SEOURL"]?.ToString() ?? "",
                ["product_Name"] = reader["product_Name"]?.ToString() ?? "",
                ["product_quantity"] = reader["product_quantity"]
            };
            list.Add(dict);
        }
        return list;
    }

    public async Task<List<Dictionary<string, object>>> GetTopperStockLocationsAsync(long mainProductId, long topperPrdId, long webstoreId)
    {
        var list = new List<Dictionary<string, object>>();
        await using var conn = new SqlConnection(_connectionString);
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
        SELECT RCTE.LocationID, FullLocation, tbl_StockLocation.Qty AS Qty, tbl_products.product_ID, tbl_products.product_type 
        FROM RCTE 
        INNER JOIN tbl_StockLocation ON tbl_StockLocation.LocationID = RCTE.LocationID AND tbl_StockLocation.Product_Id = @pid 
        INNER JOIN tbl_products ON tbl_products.product_ID = tbl_StockLocation.product_ID 
        WHERE Lvl = 3 
        ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", topperPrdId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>
            {
                ["LocationID"] = reader["LocationID"],
                ["FullLocation"] = reader["FullLocation"]?.ToString() ?? "",
                ["Qty"] = reader["Qty"],
                ["Product_Id"] = reader["product_ID"],
                ["product_type"] = reader["product_type"]
            };
            list.Add(dict);
        }
        return list;
    }

    public async Task<List<Dictionary<string, object>>> GetPersonalisedCakeSvgsAsync(long orderId, long orderDetailId)
    {
        var list = new List<Dictionary<string, object>>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT cc.customCakeImg_ID, cc.customCakeImg_text, pc.personalisedCake_ID
                    FROM tbl_personalisedCake pc
                    INNER JOIN tbl_customCakeImg cc ON cc.customCakeImg_refID = pc.personalisedCake_ID
                    WHERE pc.personalisedCake_orderID = @orderId AND pc.personalisedCake_orderdetID = @orderDetailId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>
            {
                ["customCakeImg_ID"] = reader["customCakeImg_ID"],
                ["customCakeImg_text"] = reader["customCakeImg_text"]?.ToString() ?? "",
                ["personalisedCake_ID"] = reader["personalisedCake_ID"]
            };
            list.Add(dict);
        }
        return list;
    }
}
