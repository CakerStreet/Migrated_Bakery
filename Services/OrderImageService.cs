using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Services;

public class OrderImageInfo
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public int ApiType { get; set; }
}

public class OrderImageService
{
    private readonly string _connectionString;

    public OrderImageService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<OrderImageInfo?> GetOrderImageInfoAsync(long orderId)
    {
        var sql = @"
            SELECT 
                o.order_ID,
                od.orderDetail_productID,
                p.product_name,
                p.product_code,
                od.orderDetail_ProductImage,
                CASE WHEN g.google_prdID IS NULL THEN 0 ELSE 1 END AS IsGooglePrd
            FROM tbl_orderDetail od 
            INNER JOIN tbl_order o ON od.orderDetail_orderID = o.order_ID 
            LEFT OUTER JOIN tbl_skumapping s ON s.SkuMapping_newPrdID = od.orderDetail_productID
            INNER JOIN tbl_products p ON p.product_Id = CASE WHEN s.SkuMapping_refPrdID IS NULL THEN od.orderDetail_productID ELSE s.SkuMapping_refPrdID END
            LEFT OUTER JOIN tbl_googlefeedprd g ON p.product_id = g.google_prdID
            WHERE o.order_ID = @OrderId AND o.order_isdeleted = 0";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new OrderImageInfo
            {
                OrderId = Convert.ToInt64(reader["order_ID"]),
                ProductId = Convert.ToInt64(reader["orderDetail_productID"]),
                ProductName = Convert.ToString(reader["product_name"]) ?? "",
                ProductCode = Convert.ToString(reader["product_code"]) ?? "",
                ProductImage = Convert.ToString(reader["orderDetail_ProductImage"]) ?? "",
                ApiType = Convert.ToInt32(reader["IsGooglePrd"])
            };
        }
        return null;
    }

    public async Task UpdateOrderDetailImageAsync(long orderId, string imagePath)
    {
        var sql = "UPDATE tbl_orderDetail SET orderDetail_ProductImage = @imagePath WHERE orderDetail_orderID = @orderId";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@imagePath", imagePath ?? "");
        cmd.Parameters.AddWithValue("@orderId", orderId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateProductAndOrderImageAsync(long productId, string filename, int apitype, string imageRes, string webshopLogo)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 1. Get product image details (old image)
        string oldImg = "";
        var getSql = "SELECT product_image1 FROM tbl_products WHERE product_ID = @pid";
        await using (var getCmd = new SqlCommand(getSql, conn))
        {
            getCmd.Parameters.AddWithValue("@pid", productId);
            oldImg = Convert.ToString(await getCmd.ExecuteScalarAsync()) ?? "";
        }

        // 2. Update tbl_products
        var updateProductSql = @"
            UPDATE tbl_products 
            SET product_image1 = @filename, 
                product_image1Resolution = @imgRes, 
                Product_CDNSts = 0, 
                Product_image1isURL = 0, 
                product_modifiedOn = GETDATE() 
            WHERE product_ID = @pid";

        await using (var updPrdCmd = new SqlCommand(updateProductSql, conn))
        {
            updPrdCmd.Parameters.AddWithValue("@filename", filename);
            updPrdCmd.Parameters.AddWithValue("@imgRes", imageRes);
            updPrdCmd.Parameters.AddWithValue("@pid", productId);
            await updPrdCmd.ExecuteNonQueryAsync();
        }

        // 3. Update tbl_productImage
        if (!string.IsNullOrEmpty(oldImg))
        {
            var updatePrdImgSql = @"
                UPDATE tbl_productImage 
                SET productImage_CDNSts = 0, 
                    productImage_isURL = 0, 
                    productImage_imagename = @filename, 
                    productImage_imageResolution = @imgRes 
                WHERE productImage_prdID = @pid AND LOWER(productImage_imagename) = @oldImg";

            await using var updImgCmd = new SqlCommand(updatePrdImgSql, conn);
            updImgCmd.Parameters.AddWithValue("@filename", filename);
            updImgCmd.Parameters.AddWithValue("@imgRes", imageRes);
            updImgCmd.Parameters.AddWithValue("@pid", productId);
            updImgCmd.Parameters.AddWithValue("@oldImg", oldImg.ToLower());
            int rows = await updImgCmd.ExecuteNonQueryAsync();

            if (rows == 0)
            {
                // Create if not found
                await InsertProductImageAsync(conn, productId, filename, imageRes);
            }
        }
        else
        {
            var updatePrdImgDefaultSql = @"
                UPDATE tbl_productImage 
                SET productImage_imagename = @filename, 
                    productImage_imageResolution = @imgRes, 
                    productImage_CDNSts = 0, 
                    productImage_isURL = 0 
                WHERE productImage_prdID = @pid AND productImage_isdefaultimage = 1 AND productImage_imgNo = 1";

            await using var updImgDefCmd = new SqlCommand(updatePrdImgDefaultSql, conn);
            updImgDefCmd.Parameters.AddWithValue("@filename", filename);
            updImgDefCmd.Parameters.AddWithValue("@imgRes", imageRes);
            updImgDefCmd.Parameters.AddWithValue("@pid", productId);
            int rows = await updImgDefCmd.ExecuteNonQueryAsync();

            if (rows == 0)
            {
                await InsertProductImageAsync(conn, productId, filename, imageRes);
            }
        }

        // 4. Update orderDetail through tbl_orderImageUpdate
        var prdImagePath = webshopLogo + "upload/Product_images/resized_500_500/" + filename;
        var orderUpdateSql = @"
            IF EXISTS (
                SELECT 1 FROM tbl_orderImageUpdate m 
                INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid
            )
            BEGIN
                UPDATE d 
                SET d.orderDetail_ProductImage = @prdimage 
                FROM tbl_orderImageUpdate m 
                INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid;

                UPDATE m 
                SET m.IsUpdated = 1 
                FROM tbl_orderImageUpdate m 
                INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid;
            END";

        await using (var orderUpdCmd = new SqlCommand(orderUpdateSql, conn))
        {
            orderUpdCmd.Parameters.AddWithValue("@pid", productId);
            orderUpdCmd.Parameters.AddWithValue("@prdimage", prdImagePath);
            await orderUpdCmd.ExecuteNonQueryAsync();
        }

        // 5. Insert tbl_prdUpdated
        var prdUpdatedSql = @"
            INSERT INTO tbl_prdUpdated (prdUpdated_apiID, prdUpdated_prdID, prdUpdated_createdOn, prdUpdated_updateType) 
            VALUES (@apiID, @pid, GETDATE(), 1)";

        await using (var updLogCmd = new SqlCommand(prdUpdatedSql, conn))
        {
            updLogCmd.Parameters.AddWithValue("@apiID", apitype == 1 ? 1 : 0);
            updLogCmd.Parameters.AddWithValue("@pid", productId);
            await updLogCmd.ExecuteNonQueryAsync();
        }

        // 6. Update tbl_googlefeedprd if apitype != 1
        if (apitype != 1)
        {
            var googlefeedSql = "UPDATE tbl_googlefeedprd SET IsModified = 1 WHERE google_prdID = @pid";
            await using var gfeedCmd = new SqlCommand(googlefeedSql, conn);
            gfeedCmd.Parameters.AddWithValue("@pid", productId);
            await gfeedCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task InsertProductImageAsync(SqlConnection conn, long productId, string filename, string imgRes)
    {
        var sql = @"
            INSERT INTO tbl_productImage (
                productImage_imagename, productImage_imageResolution, productImage_CDNSts, 
                productImage_createdOn, productImage_imagetype, productImage_imgNo, 
                productImage_isdefaultimage, productImage_isURL, productImage_prdID
            ) VALUES (
                @filename, @imgRes, 0, GETDATE(), 1, 1, 1, 0, @pid
            )";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@filename", filename);
        cmd.Parameters.AddWithValue("@imgRes", imgRes);
        cmd.Parameters.AddWithValue("@pid", productId);
        await cmd.ExecuteNonQueryAsync();
    }
}
