using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

/// <summary>
/// Service for mapping expired/customized products to active products via tbl_SkuMapping.
/// Migrated from mapcustomizedcake.aspx.cs.
/// </summary>
public class MapCustomizedCakeService
{
    private readonly string _connectionString;

    public MapCustomizedCakeService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets the expired product info by ID and webshop.
    /// </summary>
    public async Task<ExpiredProductInfo?> GetExpiredProductAsync(long productId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT product_ID, product_name, product_code, product_image1, product_seourl
                    FROM tbl_products
                    WHERE product_isexpired = 1
                      AND product_WebstoreID = @WebshopId
                      AND Product_ID = @ProductId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ExpiredProductInfo
            {
                ProductId = GetInt64Safe(reader, "product_ID"),
                ProductName = GetStringSafe(reader, "product_name"),
                ProductCode = GetStringSafe(reader, "product_code"),
                ProductImage = GetStringSafe(reader, "product_image1"),
                ProductSeoUrl = GetStringSafe(reader, "product_seourl")
            };
        }

        return null;
    }

    /// <summary>
    /// Gets currently mapped (linked) products for an expired product.
    /// </summary>
    public async Task<List<MappedProductInfo>> GetMappedProductsAsync(long expiredProductId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT ep.SkuMapping_newPrdId, p.product_id, product_name, product_image1, product_seourl, product_code
                    FROM tbl_products P
                    INNER JOIN tbl_SkuMapping ep ON p.product_ID = ep.SkuMapping_refPrdID
                    WHERE P.product_WebstoreID = @WebshopId
                      AND ep.SkuMapping_newPrdId = @ExpiredProductId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);
        cmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);

        var results = new List<MappedProductInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new MappedProductInfo
            {
                SkuMappingNewPrdId = GetInt64Safe(reader, "SkuMapping_newPrdId"),
                ProductId = GetInt64Safe(reader, "product_id"),
                ProductName = GetStringSafe(reader, "product_name"),
                ProductImage = GetStringSafe(reader, "product_image1"),
                ProductSeoUrl = GetStringSafe(reader, "product_seourl"),
                ProductCode = GetStringSafe(reader, "product_code")
            });
        }

        return results;
    }

    /// <summary>
    /// Searches active products by keyword, excluding already-mapped products.
    /// </summary>
    public async Task<List<SearchProductResult>> SearchProductsAsync(string keyword, long expiredProductId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT product_id, product_name, product_image1
                    FROM tbl_products
                    WHERE product_webstoreid = @WebshopId
                      AND product_isdeleted = 0
                      AND product_isexpired = 0
                      AND product_ID NOT IN (
                          SELECT SkuMapping_refPrdID FROM tbl_SkuMapping WHERE SkuMapping_newPrdId = @ExpiredProductId
                      )
                      AND product_name LIKE '%' + @Keyword + '%'";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);
        cmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);
        cmd.Parameters.AddWithValue("@Keyword", keyword);

        var results = new List<SearchProductResult>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SearchProductResult
            {
                ProductId = GetInt64Safe(reader, "product_id"),
                ProductName = GetStringSafe(reader, "product_name"),
                ProductImage = GetStringSafe(reader, "product_image1")
            });
        }

        return results;
    }

    /// <summary>
    /// Gets a single product detail by ID (for the "Link Item" preview card).
    /// </summary>
    public async Task<SearchProductResult?> GetProductDetailAsync(long productId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT product_id, product_name, product_code, product_image1
                    FROM tbl_products
                    WHERE product_webstoreid = @WebshopId
                      AND product_isdeleted = 0
                      AND product_isexpired = 0
                      AND product_id = @ProductId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SearchProductResult
            {
                ProductId = GetInt64Safe(reader, "product_id"),
                ProductName = GetStringSafe(reader, "product_name") + " (" + GetStringSafe(reader, "product_code") + ")",
                ProductImage = GetStringSafe(reader, "product_image1")
            };
        }

        return null;
    }

    /// <summary>
    /// Links an active product to an expired product (INSERT or UPDATE in tbl_SkuMapping).
    /// </summary>
    public async Task<bool> LinkProductAsync(long expiredProductId, long activeProductId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if mapping already exists for this expired product
        var checkSql = @"SELECT COUNT(1) FROM tbl_SkuMapping WHERE SkuMapping_newPrdId = @ExpiredProductId";
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);
        var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

        if (exists)
        {
            // Update existing mapping
            var updateSql = @"UPDATE tbl_SkuMapping
                              SET SkuMapping_refPrdID = @ActiveProductId, SkuMapping_modifiedOn = GETDATE()
                              WHERE SkuMapping_newPrdId = @ExpiredProductId";
            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@ActiveProductId", activeProductId);
            updateCmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Insert new mapping
            var insertSql = @"INSERT INTO tbl_SkuMapping (SkuMapping_newPrdId, SkuMapping_refPrdID, SkuMapping_CRFID, SkuMapping_CRFQuoteID, SkuMapping_modifiedOn)
                              VALUES (@ExpiredProductId, @ActiveProductId, 0, 0, GETDATE())";
            await using var insertCmd = new SqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);
            insertCmd.Parameters.AddWithValue("@ActiveProductId", activeProductId);
            await insertCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Unlinks (deletes) all mappings for an expired product from tbl_SkuMapping.
    /// </summary>
    public async Task<bool> UnlinkProductAsync(long expiredProductId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"DELETE FROM tbl_SkuMapping WHERE SkuMapping_newPrdId = @ExpiredProductId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ExpiredProductId", expiredProductId);
        await cmd.ExecuteNonQueryAsync();

        return true;
    }

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
}

// --- Models ---

public class ExpiredProductInfo
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
}

public class MappedProductInfo
{
    public long SkuMappingNewPrdId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
    public string ProductCode { get; set; } = "";
}

public class SearchProductResult
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
}
