using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class ProductDocProductDetail
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public string? ProductCode { get; set; }
    public string? SeoUrl { get; set; }
    public int ProductType { get; set; }
    public bool IsGooglePrd { get; set; }
    public int PrdApiType { get; set; }
}

public class ProductDocFileItem
{
    public long ProductFileID { get; set; }
    public long ProductId { get; set; }
    public string ProductFile { get; set; } = "";
    public string ProductFileTitle { get; set; } = "";
    public bool IsAddtoOrder { get; set; }
    public DateTime CreatedOn { get; set; }
    public int DisplayOrder { get; set; }
    public int RecNo { get; set; }
}

public class ProductDocSizeItem
{
    public int SizeID { get; set; }
    public string SizeTitle { get; set; } = "";
    public bool IsLinked { get; set; }
}

public class ProductDocSaveModel
{
    public int RecNo { get; set; }
    public long ProductFileID { get; set; }
    public long ProductId { get; set; }
    public string ProductFileTitle { get; set; } = "";
    public bool IsAddtoOrder { get; set; }
    public int IsDeleted { get; set; }
    public string ProductFile { get; set; } = "";
    public List<ProductDocFileSizeModel> lstProductFileSize { get; set; } = new();
}

public class ProductDocFileSizeModel
{
    public int SizeID { get; set; }
    public bool IsAdded { get; set; }
}

public class ManageProductDocService
{
    private readonly string _connectionString;

    public ManageProductDocService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<ProductDocProductDetail?> GetProductDetailsAsync(long productId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT p.product_ID, p.product_Name, p.product_image1, p.product_code, p.product_seoURL, p.product_type,
                           CASE WHEN g.google_prdID IS NULL THEN 0 ELSE 1 END AS IsGooglePrd,
                           0 AS prd_apitype
                    FROM tbl_products p 
                    LEFT OUTER JOIN tbl_googlefeedprd g ON p.product_id = g.google_prdID
                    WHERE p.Product_ID = @pid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProductDocProductDetail
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProductCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                SeoUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                ProductType = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                IsGooglePrd = Convert.ToBoolean(reader.GetValue(6)),
                PrdApiType = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7))
            };
        }
        return null;
    }

    public async Task<List<ProductDocFileItem>> GetProductFilesAsync(long productId)
    {
        var list = new List<ProductDocFileItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT ProductFileID, ProductID, ProductFile, ProductFileTitle, IsAddtoOrder, CreatedOn, DisplayOrder
                    FROM tbl_ProductFile
                    WHERE ProductID = @pid
                    ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        int index = 1;
        while (await reader.ReadAsync())
        {
            list.Add(new ProductDocFileItem
            {
                ProductFileID = reader.GetInt64(0),
                ProductId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1)),
                ProductFile = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ProductFileTitle = reader.IsDBNull(3) ? "" : reader.GetString(3),
                IsAddtoOrder = reader.IsDBNull(4) ? false : Convert.ToBoolean(reader.GetValue(4)),
                CreatedOn = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                DisplayOrder = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                RecNo = index++
            });
        }
        return list;
    }

    public async Task<List<ProductDocSizeItem>> GetSizesForProductFileAsync(long productId, long productFileId)
    {
        var list = new List<ProductDocSizeItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT cs.SizeID, cs.SizeTitle,
                           CASE WHEN lnk.PrdFileID IS NULL THEN 0 ELSE 1 END AS IsLinked
                    FROM tbl_CakePrice cp
                    INNER JOIN tbl_CakeSize cs ON cs.SizeID = cp.SizeID
                    LEFT OUTER JOIN tbl_lnkprdfile2size lnk ON lnk.SizeID = cs.SizeID 
                        AND lnk.PrdFileID = @fileId AND lnk.PrdID = @pid
                    WHERE cp.product_ID = @pid
                    ORDER BY cp.cakeprice_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.Parameters.AddWithValue("@fileId", productFileId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductDocSizeItem
            {
                SizeID = reader.GetInt32(0),
                SizeTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsLinked = Convert.ToBoolean(reader.GetValue(2))
            });
        }
        return list;
    }

    public async Task<int> SaveProductDocumentsAsync(List<ProductDocSaveModel> lstPM)
    {
        if (lstPM == null || lstPM.Count == 0) return 0;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var filesToDelete = lstPM.Where(w => w.IsDeleted == 1 && !string.IsNullOrEmpty(w.ProductFile))
                                 .Select(s => s.ProductFile).ToList();

        int displayOrder = 1;
        foreach (var pm in lstPM)
        {
            if (pm.IsDeleted == 0)
            {
                if (pm.ProductFileID == 0)
                {
                    // Insert new
                    var insertSql = @"INSERT INTO tbl_ProductFile (DisplayOrder, ProductFileTitle, ProductID, IsAddtoOrder, ProductFile, CreatedOn)
                                      VALUES (@displayOrder, @title, @pid, @addToOrder, @file, GETDATE());
                                      SELECT SCOPE_IDENTITY();";
                    await using var insertCmd = new SqlCommand(insertSql, conn);
                    insertCmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                    insertCmd.Parameters.AddWithValue("@title", pm.ProductFileTitle ?? "");
                    insertCmd.Parameters.AddWithValue("@pid", pm.ProductId);
                    insertCmd.Parameters.AddWithValue("@addToOrder", pm.IsAddtoOrder);
                    insertCmd.Parameters.AddWithValue("@file", pm.ProductFile ?? "");
                    var newId = await insertCmd.ExecuteScalarAsync();
                    long newFileId = Convert.ToInt64(newId);

                    // Save size linkings
                    await SaveProductDocSizeLinkingAsync(conn, pm.lstProductFileSize, newFileId, pm.ProductId);
                }
                else
                {
                    // Check if existing file changed - get current file name
                    string? currentFile = null;
                    var getFileSql = "SELECT ProductFile FROM tbl_ProductFile WHERE ProductFileID = @fileId";
                    await using (var getCmd = new SqlCommand(getFileSql, conn))
                    {
                        getCmd.Parameters.AddWithValue("@fileId", pm.ProductFileID);
                        var result = await getCmd.ExecuteScalarAsync();
                        currentFile = result?.ToString();
                    }

                    if (currentFile != pm.ProductFile && !string.IsNullOrEmpty(currentFile))
                    {
                        filesToDelete.Add(currentFile);
                    }

                    // Update existing
                    var updateSql = @"UPDATE tbl_ProductFile 
                                      SET DisplayOrder = @displayOrder, 
                                          ProductFileTitle = @title, 
                                          ProductID = @pid, 
                                          IsAddtoOrder = @addToOrder, 
                                          ProductFile = @file
                                      WHERE ProductFileID = @fileId";
                    await using var updateCmd = new SqlCommand(updateSql, conn);
                    updateCmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                    updateCmd.Parameters.AddWithValue("@title", pm.ProductFileTitle ?? "");
                    updateCmd.Parameters.AddWithValue("@pid", pm.ProductId);
                    updateCmd.Parameters.AddWithValue("@addToOrder", pm.IsAddtoOrder);
                    updateCmd.Parameters.AddWithValue("@file", pm.ProductFile ?? "");
                    updateCmd.Parameters.AddWithValue("@fileId", pm.ProductFileID);
                    await updateCmd.ExecuteNonQueryAsync();

                    // Save size linkings
                    await SaveProductDocSizeLinkingAsync(conn, pm.lstProductFileSize, pm.ProductFileID, pm.ProductId);
                }

                displayOrder++;
            }
        }

        // Delete marked records
        var delIds = lstPM.Where(w => w.IsDeleted == 1 && w.ProductFileID > 0).Select(s => s.ProductFileID).ToList();
        if (delIds.Count > 0)
        {
            long productId = lstPM[0].ProductId;
            string idList = string.Join(",", delIds);

            // Delete size linkings first
            var delLinkSql = $"DELETE FROM tbl_lnkprdfile2size WHERE PrdID = @pid AND PrdFileID IN ({idList})";
            await using (var delLinkCmd = new SqlCommand(delLinkSql, conn))
            {
                delLinkCmd.Parameters.AddWithValue("@pid", productId);
                await delLinkCmd.ExecuteNonQueryAsync();
            }

            // Delete product files
            var delFileSql = $"DELETE FROM tbl_ProductFile WHERE ProductID = @pid AND ProductFileID IN ({idList})";
            await using (var delFileCmd = new SqlCommand(delFileSql, conn))
            {
                delFileCmd.Parameters.AddWithValue("@pid", productId);
                await delFileCmd.ExecuteNonQueryAsync();
            }
        }

        // Delete physical files
        // Note: physical file deletion should be handled by the controller since it has access to the filesystem

        return 1;
    }

    public List<string> GetFilesToDelete(List<ProductDocSaveModel> lstPM)
    {
        return lstPM.Where(w => w.IsDeleted == 1 && !string.IsNullOrEmpty(w.ProductFile))
                    .Select(s => s.ProductFile).ToList();
    }

    private async Task SaveProductDocSizeLinkingAsync(SqlConnection conn, List<ProductDocFileSizeModel> sizes, long productFileId, long productId)
    {
        if (sizes == null || sizes.Count == 0) return;

        foreach (var size in sizes)
        {
            if (size.IsAdded)
            {
                // Check if exists
                var checkSql = "SELECT COUNT(1) FROM tbl_lnkprdfile2size WHERE PrdID = @pid AND PrdFileID = @fileId AND SizeID = @sizeId";
                await using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@pid", productId);
                    checkCmd.Parameters.AddWithValue("@fileId", productFileId);
                    checkCmd.Parameters.AddWithValue("@sizeId", size.SizeID);
                    int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        var insertSql = "INSERT INTO tbl_lnkprdfile2size (PrdID, PrdFileID, SizeID) VALUES (@pid, @fileId, @sizeId)";
                        await using var insertCmd = new SqlCommand(insertSql, conn);
                        insertCmd.Parameters.AddWithValue("@pid", productId);
                        insertCmd.Parameters.AddWithValue("@fileId", productFileId);
                        insertCmd.Parameters.AddWithValue("@sizeId", size.SizeID);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            else
            {
                // Remove linking
                var deleteSql = "DELETE FROM tbl_lnkprdfile2size WHERE PrdID = @pid AND PrdFileID = @fileId AND SizeID = @sizeId";
                await using var deleteCmd = new SqlCommand(deleteSql, conn);
                deleteCmd.Parameters.AddWithValue("@pid", productId);
                deleteCmd.Parameters.AddWithValue("@fileId", productFileId);
                deleteCmd.Parameters.AddWithValue("@sizeId", size.SizeID);
                await deleteCmd.ExecuteNonQueryAsync();
            }
        }
    }
}
