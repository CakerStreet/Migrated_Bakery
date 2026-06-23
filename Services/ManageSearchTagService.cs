using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

/// <summary>
/// CRM/Data Management utility service for search tag-product association cleanup.
/// Reads tbl_Searchtags and tbl_lnkPrd2tag from DefaultConnection (db_cakerstreet_live).
/// Link/Unlink operates on tbl_lnkPrd2tag only.
/// NOT a frontend search fix — this is for developer/admin data cleanup.
/// </summary>
public class ManageSearchTagService
{
    private readonly string _connectionString;

    public ManageSearchTagService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    // ─── List / Search Tags ──────────────────────────────────────────────────────

    public async Task<SearchTagListResult> GetTagsAsync(string search = "", int page = 1, int pageSize = 50)
    {
        var result = new SearchTagListResult { CurrentPage = page, PageSize = pageSize, Search = search };

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build WHERE
        var where = "t.tags_IsActive = 1";
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += " AND (t.tags_text LIKE '%' + @search + '%' OR t.tags_url LIKE '%' + @search + '%')";
            parameters.Add(new SqlParameter("@search", search));
        }

        // Count
        var countSql = $"SELECT COUNT(*) FROM tbl_Searchtags t WHERE {where}";
        await using var cmdCount = new SqlCommand(countSql, conn);
        foreach (var p in parameters) cmdCount.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
        result.TotalTags = (int)(await cmdCount.ExecuteScalarAsync() ?? 0);
        result.TotalPages = (int)Math.Ceiling((double)result.TotalTags / pageSize);

        // Tags with product count
        var offset = (page - 1) * pageSize;
        var sql = $@"SELECT t.tags_ID, t.tags_text, t.tags_url, t.tags_Showatfront, t.tags_displayorder,
                        (SELECT COUNT(*) FROM tbl_lnkPrd2tag lnk 
                         WHERE lnk.lnkPrd2tag_tagID = t.tags_ID AND lnk.lnkPrd2tag_apiID = 0) AS ProductCount
                     FROM tbl_Searchtags t
                     WHERE {where}
                     ORDER BY t.tags_displayorder, t.tags_text
                     OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        foreach (var p in parameters) cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Tags.Add(new SearchTagItem
            {
                TagId = Convert.ToInt32(reader[0]),
                Text = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Url = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ShowAtFront = !reader.IsDBNull(3) && reader.GetBoolean(3),
                DisplayOrder = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]),
                ProductCount = Convert.ToInt32(reader[5])
            });
        }

        return result;
    }

    // ─── Get Linked Products for a Tag ───────────────────────────────────────────

    public async Task<TagProductsResult> GetTagProductsAsync(int tagId, int page = 1, int pageSize = 50)
    {
        var result = new TagProductsResult { TagId = tagId, CurrentPage = page, PageSize = pageSize };

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Get tag info
        await using var cmdTag = new SqlCommand(
            "SELECT tags_text, tags_url FROM tbl_Searchtags WHERE tags_ID = @tagId", conn);
        cmdTag.Parameters.AddWithValue("@tagId", tagId);
        await using var tagReader = await cmdTag.ExecuteReaderAsync();
        if (await tagReader.ReadAsync())
        {
            result.TagText = tagReader.IsDBNull(0) ? "" : tagReader.GetString(0);
            result.TagUrl = tagReader.IsDBNull(1) ? "" : tagReader.GetString(1);
        }
        await tagReader.CloseAsync();

        // Count
        await using var cmdCount = new SqlCommand(
            "SELECT COUNT(*) FROM tbl_lnkPrd2tag WHERE lnkPrd2tag_tagID = @tagId AND lnkPrd2tag_apiID = 0", conn);
        cmdCount.Parameters.AddWithValue("@tagId", tagId);
        result.TotalProducts = (int)(await cmdCount.ExecuteScalarAsync() ?? 0);
        result.TotalPages = (int)Math.Ceiling((double)result.TotalProducts / pageSize);

        // Products
        var offset = (page - 1) * pageSize;
        var sql = @"SELECT lnk.lnkPrd2tag_ID, lnk.lnkPrd2tag_prdID, 
                           p.product_Name, p.product_seourl, p.product_image1,
                           p.product_isActive, p.product_type, lnk.lnkPrd2tag_searchrank
                    FROM tbl_lnkPrd2tag lnk
                    INNER JOIN tbl_products p ON p.product_ID = lnk.lnkPrd2tag_prdID
                    WHERE lnk.lnkPrd2tag_tagID = @tagId AND lnk.lnkPrd2tag_apiID = 0
                    ORDER BY lnk.lnkPrd2tag_searchrank DESC, p.product_Name
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Products.Add(new LinkedProduct
            {
                LinkId = reader.GetInt64(0),
                ProductId = reader.GetInt64(1),
                ProductName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                SeoUrl = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Image = reader.IsDBNull(4) ? "" : reader.GetString(4),
                IsActive = !reader.IsDBNull(5) && Convert.ToBoolean(reader[5]),
                ProductType = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader[6]),
                SearchRank = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader[7])
            });
        }

        return result;
    }

    // ─── Link Product to Tag ────────────────────────────────────────────────────

    public async Task<bool> LinkProductToTagAsync(int tagId, long productId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if already linked
        await using var cmdCheck = new SqlCommand(
            "SELECT COUNT(*) FROM tbl_lnkPrd2tag WHERE lnkPrd2tag_tagID = @tagId AND lnkPrd2tag_prdID = @prdId AND lnkPrd2tag_apiID = 0", conn);
        cmdCheck.Parameters.AddWithValue("@tagId", tagId);
        cmdCheck.Parameters.AddWithValue("@prdId", productId);
        var exists = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync() ?? 0) > 0;
        if (exists) return false; // Already linked

        // Insert — matches legacy pattern
        var sql = @"INSERT INTO tbl_lnkPrd2tag (lnkPrd2tag_prdID, lnkPrd2tag_tagID, lnkPrd2tag_searchrank, lnkPrd2tag_apiID, lnkPrd2tag_isdeleted)
                    VALUES (@prdId, @tagId, 0, 0, 0)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@prdId", productId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    // ─── Unlink Product from Tag ────────────────────────────────────────────────

    public async Task<bool> UnlinkProductFromTagAsync(int tagId, long productId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = "DELETE FROM tbl_lnkPrd2tag WHERE lnkPrd2tag_tagID = @tagId AND lnkPrd2tag_prdID = @prdId AND lnkPrd2tag_apiID = 0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.Parameters.AddWithValue("@prdId", productId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // ─── Search Products (for linking) ──────────────────────────────────────────

    public async Task<List<LinkedProduct>> SearchProductsAsync(string keyword)
    {
        var products = new List<LinkedProduct>();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2) return products;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT TOP 20 product_ID, product_Name, product_seourl, product_image1, product_isActive, product_type
                    FROM tbl_products 
                    WHERE (product_Name LIKE '%' + @keyword + '%' OR CAST(product_ID AS VARCHAR) = @keyword)
                      AND product_isdeleted = 0
                    ORDER BY product_Name";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@keyword", keyword);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new LinkedProduct
            {
                ProductId = Convert.ToInt64(reader[0]),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                SeoUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Image = reader.IsDBNull(3) ? "" : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && Convert.ToBoolean(reader[4]),
                ProductType = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5])
            });
        }

        return products;
    }
}

// ─── Models ────────────────────────────────────────────────────────────────────

public class SearchTagListResult
{
    public List<SearchTagItem> Tags { get; set; } = new();
    public int TotalTags { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public string Search { get; set; } = "";
}

public class SearchTagItem
{
    public int TagId { get; set; }
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
    public bool ShowAtFront { get; set; }
    public int DisplayOrder { get; set; }
    public int ProductCount { get; set; }
}

public class TagProductsResult
{
    public int TagId { get; set; }
    public string TagText { get; set; } = "";
    public string TagUrl { get; set; } = "";
    public List<LinkedProduct> Products { get; set; } = new();
    public int TotalProducts { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}

public class LinkedProduct
{
    public long LinkId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string SeoUrl { get; set; } = "";
    public string Image { get; set; } = "";
    public bool IsActive { get; set; }
    public int ProductType { get; set; }
    public int SearchRank { get; set; }
}
