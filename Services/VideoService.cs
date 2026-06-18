using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CakerStreet.Business.Services;

public class ProductDetails
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image1 { get; set; } = "";
    public string SeoUrl { get; set; } = "";
}

public class PrdVideoItem
{
    public long VideoId { get; set; }
    public long ProductId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string VideoThumb { get; set; } = "";
    public string Video { get; set; } = "";
    public int DisplayOrder { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}

public class VideoService
{
    private readonly string _connectionString;

    public VideoService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<ProductDetails?> GetProductDetailsAsync(long prdId, long webshopId)
    {
        var sql = @"SELECT product_ID, product_Name, product_code, product_desc, product_image1, product_SEOURL 
                    FROM tbl_products 
                    WHERE product_ID = @id AND product_WebstoreID = @wid AND product_isdeleted = 0";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", prdId);
        cmd.Parameters.AddWithValue("@wid", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProductDetails
            {
                ProductId = Convert.ToInt64(reader["product_ID"]),
                Name = Convert.ToString(reader["product_Name"]) ?? "",
                Code = Convert.ToString(reader["product_code"]) ?? "",
                Description = Convert.ToString(reader["product_desc"]) ?? "",
                Image1 = Convert.ToString(reader["product_image1"]) ?? "",
                SeoUrl = Convert.ToString(reader["product_SEOURL"]) ?? ""
            };
        }
        return null;
    }

    public async Task SaveProductDescriptionAsync(long prdId, string description)
    {
        var sql = "UPDATE tbl_products SET product_desc = @desc, product_modifiedOn = GETDATE() WHERE product_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@desc", description ?? "");
        cmd.Parameters.AddWithValue("@id", prdId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<PrdVideoItem>> GetVideosByProductIdAsync(long prdId)
    {
        var list = new List<PrdVideoItem>();
        var sql = @"SELECT prdVideo_ID, prdVideo_PrdID, prdVideo_Title, prdVideo_Desc, prdVideo_VideoThumb, 
                           prdVideo_Video, prdVideo_displayOrder, prdVideo_modifiedOn, prdVideo_createdOn 
                    FROM tbl_prdVideo 
                    WHERE prdVideo_PrdID = @pid 
                    ORDER BY prdVideo_displayOrder";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", prdId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PrdVideoItem
            {
                VideoId = Convert.ToInt64(reader["prdVideo_ID"]),
                ProductId = Convert.ToInt64(reader["prdVideo_PrdID"]),
                Title = Convert.ToString(reader["prdVideo_Title"]) ?? "",
                Description = Convert.ToString(reader["prdVideo_Desc"]) ?? "",
                VideoThumb = Convert.ToString(reader["prdVideo_VideoThumb"]) ?? "",
                Video = Convert.ToString(reader["prdVideo_Video"]) ?? "",
                DisplayOrder = Convert.ToInt32(reader["prdVideo_displayOrder"]),
                ModifiedOn = Convert.ToDateTime(reader["prdVideo_modifiedOn"]),
                CreatedOn = Convert.ToDateTime(reader["prdVideo_createdOn"])
            });
        }
        return list;
    }

    public async Task<PrdVideoItem?> GetVideoByIdAsync(long id)
    {
        var sql = @"SELECT prdVideo_ID, prdVideo_PrdID, prdVideo_Title, prdVideo_Desc, prdVideo_VideoThumb, 
                           prdVideo_Video, prdVideo_displayOrder, prdVideo_modifiedOn, prdVideo_createdOn 
                    FROM tbl_prdVideo 
                    WHERE prdVideo_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PrdVideoItem
            {
                VideoId = Convert.ToInt64(reader["prdVideo_ID"]),
                ProductId = Convert.ToInt64(reader["prdVideo_PrdID"]),
                Title = Convert.ToString(reader["prdVideo_Title"]) ?? "",
                Description = Convert.ToString(reader["prdVideo_Desc"]) ?? "",
                VideoThumb = Convert.ToString(reader["prdVideo_VideoThumb"]) ?? "",
                Video = Convert.ToString(reader["prdVideo_Video"]) ?? "",
                DisplayOrder = Convert.ToInt32(reader["prdVideo_displayOrder"]),
                ModifiedOn = Convert.ToDateTime(reader["prdVideo_modifiedOn"]),
                CreatedOn = Convert.ToDateTime(reader["prdVideo_createdOn"])
            };
        }
        return null;
    }

    public async Task SaveVideoAsync(PrdVideoItem item)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        if (item.VideoId > 0)
        {
            var sql = @"UPDATE tbl_prdVideo 
                        SET prdVideo_Title = @title, prdVideo_Desc = @desc, 
                            prdVideo_Video = @video, prdVideo_VideoThumb = @thumb, 
                            prdVideo_modifiedOn = GETDATE() 
                        WHERE prdVideo_ID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", item.Title ?? "");
            cmd.Parameters.AddWithValue("@desc", item.Description ?? "");
            cmd.Parameters.AddWithValue("@video", item.Video ?? "");
            cmd.Parameters.AddWithValue("@thumb", item.VideoThumb ?? "");
            cmd.Parameters.AddWithValue("@id", item.VideoId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Get max display order
            int maxOrder = 0;
            var maxSql = "SELECT ISNULL(MAX(prdVideo_displayOrder), 0) FROM tbl_prdVideo WHERE prdVideo_PrdID = @pid";
            await using (var maxCmd = new SqlCommand(maxSql, conn))
            {
                maxCmd.Parameters.AddWithValue("@pid", item.ProductId);
                maxOrder = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());
            }

            var sql = @"INSERT INTO tbl_prdVideo 
                        (prdVideo_PrdID, prdVideo_Title, prdVideo_Desc, prdVideo_Video, prdVideo_VideoThumb, 
                         prdVideo_displayOrder, prdVideo_createdOn, prdVideo_modifiedOn) 
                        VALUES 
                        (@pid, @title, @desc, @video, @thumb, @order, GETDATE(), GETDATE())";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", item.ProductId);
            cmd.Parameters.AddWithValue("@title", item.Title ?? "");
            cmd.Parameters.AddWithValue("@desc", item.Description ?? "");
            cmd.Parameters.AddWithValue("@video", item.Video ?? "");
            cmd.Parameters.AddWithValue("@thumb", item.VideoThumb ?? "");
            cmd.Parameters.AddWithValue("@order", maxOrder + 1);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task UpdateDisplayOrderAsync(long id, int displayOrder)
    {
        var sql = "UPDATE tbl_prdVideo SET prdVideo_displayOrder = @order WHERE prdVideo_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@order", displayOrder);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteVideoAsync(long id)
    {
        var sql = "DELETE FROM tbl_prdVideo WHERE prdVideo_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static void GenerateThumbnails(string filename, string tbfilename, int newWidth, int newHeight)
    {
        using (Bitmap mybitmap = new Bitmap(filename))
        {
            double currentWidth = mybitmap.Width;
            double currentHeight = mybitmap.Height;

            double multiplier;
            if (currentHeight > currentWidth)
                multiplier = (double)newHeight / currentHeight;
            else
                multiplier = (double)newWidth / currentWidth;

            int finalWidth = Convert.ToInt32(currentWidth * multiplier);
            int finalHeight = Convert.ToInt32(currentHeight * multiplier);

            using (Bitmap tbimage = new Bitmap(finalWidth, finalHeight))
            {
                using (Graphics g = Graphics.FromImage(tbimage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(mybitmap, 0, 0, finalWidth, finalHeight);
                }
                tbimage.Save(tbfilename, ImageFormat.Jpeg);
            }
        }
    }
}
