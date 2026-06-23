using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

/// <summary>
/// CRM Search Tag Module — migrated from legacy crmsearchtag.aspx / crmsearchtag.aspx.cs.
/// Legacy Module ID: 20
/// Legacy route: /crmsearchtag?searchfor=0&pno=1&status=1&sort=11&filterp=keyword
/// Migrated route: /managesearchtags
/// 
/// Phase 1: Read-only — list, search, filter, sort, pagination.
/// No mutations in this phase.
/// </summary>
public class ManageSearchTagService
{
    private readonly string _connectionString;
    private const int PageSize = 20; // Legacy uses 20

    public ManageSearchTagService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    // ─── List / Search Tags (matches legacy bindgrid) ────────────────────────────

    /// <summary>
    /// Replicates legacy crmsearchtag.aspx.cs → bindgrid() method.
    /// Builds dynamic SQL matching legacy ROW_NUMBER pattern.
    /// </summary>
    public async Task<SearchTagListResult> GetTagsAsync(
        int searchFor = 0,      // 0=Cakes, 1=Cupcakes, 2=Party Accessory (rblSearchTagFor)
        int status = 1,         // 0=All, 1=Active, 2=Inactive (rblActive) — legacy default=1
        string filterp = "",    // Keyword search text (txtName / filterp querystring)
        int searchType = 0,     // 0=Anywhere, 1=Starts, 2=Ends, 3=Exact (rdsearchtype)
        int sort = 11,          // Sort option (drpsort) — legacy default=11
        int page = 1,
        int? parentTagId = null) // Legacy ?tagID=X — show sub-tags of this parent
    {
        var result = new SearchTagListResult
        {
            CurrentPage = page,
            PageSize = PageSize,
            SearchFor = searchFor,
            Status = status,
            FilterP = filterp,
            SearchType = searchType,
            Sort = sort,
            ParentTagId = parentTagId
        };

        // If parentTagId specified, look up parent tag text for breadcrumb
        if (parentTagId.HasValue && parentTagId.Value > 0)
        {
            await using var connP = new SqlConnection(_connectionString);
            await connP.OpenAsync();
            await using var cmdP = new SqlCommand("SELECT tags_text FROM tbl_Searchtags WHERE tags_ID = @id", connP);
            cmdP.Parameters.AddWithValue("@id", parentTagId.Value);
            result.ParentTagText = (await cmdP.ExecuteScalarAsync())?.ToString() ?? "";
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // ── Build WHERE (matches legacy strFilter) ───────────────────────────────
        var where = "1=1";

        // Active filter — legacy: rblActive
        if (status == 1)
            where += " AND tags_IsActive = 1";
        else if (status == 2)
            where += " AND tags_IsActive = 0";
        // status == 0 → all, no filter

        // Category filter — legacy: rblSearchTagFor / searchfor querystring
        where += $" AND tags_for = {searchFor}";

        // Sub-tag: exclude parent tag (legacy line 175)
        if (parentTagId.HasValue && parentTagId.Value > 0)
            where += $" AND tags_ID <> {parentTagId.Value}";

        // Keyword filter — legacy: filterp + rdsearchtype
        if (!string.IsNullOrWhiteSpace(filterp))
        {
            var keyword = filterp.Trim().Replace("+", " ");

            switch (searchType)
            {
                case 1: // Starts with
                    where += $" AND tags_text LIKE @filterp + '%'";
                    break;
                case 2: // Ends with
                    where += $" AND tags_text LIKE '%' + @filterp";
                    break;
                case 3: // Exact match
                    where += " AND tags_text = @filterp";
                    break;
                default: // 0 = Anywhere (with comma splitting for multi-keyword)
                    // Legacy: keyword.Replace(" ,",",").Replace(", ",",") then split on ","
                    // Each part becomes AND tags_text LIKE '%part%'
                    var parts = keyword.Replace(" ,", ",").Replace(", ", ",").Split(',', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        where += $" AND tags_text LIKE '%' + @filterp{i} + '%'";
                    }
                    break;
            }
        }

        // ── Build JOIN for sub-tag mode (legacy line 176) ────────────────────────
        var lnkTagsJoin = "";
        var lnkTagsCol = "";
        if (parentTagId.HasValue && parentTagId.Value > 0)
        {
            lnkTagsJoin = $" LEFT JOIN tbl_lnkTags ON lnkTags_tagID = tags_Id AND lnkTags_parenttagID = {parentTagId.Value}";
            lnkTagsCol = ", CASE WHEN lnkTags_tagID > 0 THEN 1 ELSE 0 END sortsubtags, ISNULL(lnkTags_displayOrder, 1100) AS lnkTags_sortOrder";
        }

        // ── Build ORDER BY (matches legacy sort options) ────────────────────────
        // Both inner (ROW_NUMBER) and outer ORDER BY operate on subquery aliases,
        // so use sortsubtags and lnkTags_sortOrder (not raw lnkTags_tagID / lnkTags_displayOrder)
        var orderPrefix = (parentTagId.HasValue && parentTagId.Value > 0)
            ? "sortsubtags DESC, "
            : "";

        var baseSortOrder = sort switch
        {
            0 => "tags_createdOn DESC, tags_displayorder",
            1 => "tags_text, tags_displayorder",
            2 => "tags_countsearch DESC, tags_displayorder",
            3 => "ISNULL(popularTags_displayOrder,1100), tags_countsearch DESC, tags_displayorder",
            4 => "countprd DESC, tags_displayorder",
            5 => "countprd, tags_displayorder",
            _ => (parentTagId.HasValue && parentTagId.Value > 0)
                ? "lnkTags_sortOrder, tags_displayorder, tags_countsearch DESC"
                : "tags_displayorder, tags_text, tags_countsearch DESC" // 11 = default
        };
        var orderBy = orderPrefix + baseSortOrder;

        // ── Count query (no JOINs — avoid duplicate inflation) ─────────────────
        var countSql = $"SELECT COUNT(1) FROM tbl_Searchtags WHERE {where}";

        await using var cmdCount = new SqlCommand(countSql, conn);
        AddFilterParameters(cmdCount, filterp, searchType);
        result.TotalTags = Convert.ToInt32(await cmdCount.ExecuteScalarAsync() ?? 0);
        result.TotalPages = (int)Math.Ceiling((double)result.TotalTags / PageSize);

        if (page > result.TotalPages && result.TotalPages > 0) page = result.TotalPages;
        result.CurrentPage = page;

        // ── Main data query (matches legacy ROW_NUMBER pattern) ──────────────────
        // Legacy computes:
        //   countprd — active linked bakery products
        //   countdnld — downloaded google images
        //   breadcrumb — via dbo.getbretcrumb_taglist_fortag()
        double startingRecord = (page * PageSize) - PageSize; // 0-based start
        double totalRecord = page * PageSize;                  // 0-based end

        // popularTags JOIN only needed for sort=3
        var popularJoin = (sort == 3) ? "LEFT JOIN tbl_popularTags ON popularTags_tagID = tags_ID" : "";
        var popularCol = (sort == 3) ? ", ISNULL(popularTags_displayOrder, 1100) AS popularTags_displayOrder" : "";

        var dataSql = $@"
SELECT * FROM (
    SELECT ROW_NUMBER() OVER(ORDER BY {orderBy}) AS row, *
    FROM (
        SELECT 
            tags_ID, tags_text, tags_url, tags_displayorder, tags_IsActive, tags_for,
            tags_countsearch, tags_createdOn, tags_Showatfront
            {popularCol}
            {lnkTagsCol},
            (SELECT COUNT(1) FROM tbl_lnkPrd2tag
             WHERE lnkPrd2tag_tagID = tags_ID
               AND lnkPrd2tag_apiID = 0
               AND lnkPrd2tag_prdID IN (
                   SELECT product_ID FROM tbl_products p
                   WHERE p.product_isactive = 1
                     AND p.product_isdeleted = 0
                     AND p.product_isexpired = 0
                     AND product_iswsp = 0
               )
            ) countprd,
            ISNULL(
                (SELECT COUNT(1) FROM tbl_googlesearch
                 INNER JOIN tbl_lnkPrd2tag lnk2 ON lnk2.lnkPrd2tag_prdID = googlesearch_ID AND lnk2.lnkPrd2tag_apiID = 1
                 WHERE googlesearch_isdeleted = 0
                   AND googlesearch_isdownloaded = 1
                   AND lnk2.lnkPrd2tag_tagID = tags_ID),
                0
            ) countdnld
        FROM tbl_Searchtags
            {lnkTagsJoin}
            {popularJoin}
        WHERE {where}
    ) rowi
) RowP
WHERE row > @startRow AND row <= @endRow
ORDER BY {orderBy}";

        await using var cmdData = new SqlCommand(dataSql, conn);
        AddFilterParameters(cmdData, filterp, searchType);
        cmdData.Parameters.AddWithValue("@startRow", (int)startingRecord);
        cmdData.Parameters.AddWithValue("@endRow", (int)totalRecord);

        await using var reader = await cmdData.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Tags.Add(new SearchTagItem
            {
                TagId = Convert.ToInt32(reader["tags_ID"]),
                Text = reader["tags_text"]?.ToString() ?? "",
                Url = reader["tags_url"]?.ToString() ?? "",
                DisplayOrder = reader["tags_displayorder"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tags_displayorder"]),
                IsActive = reader["tags_IsActive"] != DBNull.Value && Convert.ToBoolean(reader["tags_IsActive"]),
                TagsFor = reader["tags_for"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tags_for"]),
                SearchCount = reader["tags_countsearch"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tags_countsearch"]),
                CreatedOn = reader["tags_createdOn"] == DBNull.Value ? null : Convert.ToDateTime(reader["tags_createdOn"]),
                ShowAtFront = reader["tags_Showatfront"] != DBNull.Value && Convert.ToBoolean(reader["tags_Showatfront"]),
                ProductCount = reader["countprd"] == DBNull.Value ? 0 : Convert.ToInt32(reader["countprd"]),
                DownloadedCount = reader["countdnld"] == DBNull.Value ? 0 : Convert.ToInt32(reader["countdnld"])
            });
        }

        return result;
    }

    /// <summary>
    /// Adds parameterized filter values for keyword search.
    /// </summary>
    private void AddFilterParameters(SqlCommand cmd, string filterp, int searchType)
    {
        if (string.IsNullOrWhiteSpace(filterp)) return;

        var keyword = filterp.Trim().Replace("+", " ");

        if (searchType == 0) // Anywhere with comma splitting
        {
            var parts = keyword.Replace(" ,", ",").Replace(", ", ",").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@filterp{i}", parts[i].Trim());
            }
        }
        else
        {
            cmd.Parameters.AddWithValue("@filterp", keyword);
        }
    }

    // ─── Phase 2: Inline Update (legacy btnUpdate_onClick L766-836) ────────────

    /// <summary>
    /// Inline update tags_text, tags_url, tags_displayorder for checked rows.
    /// Legacy SQL: UPDATE tbl_Searchtags SET tags_text=@text, tags_url=@url, tags_displayorder=@order WHERE tags_ID=@id
    /// </summary>
    public async Task<int> UpdateTagsAsync(List<TagUpdateItem> updates)
    {
        if (updates == null || updates.Count == 0) return 0;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        int updated = 0;
        foreach (var item in updates)
        {
            // Matches legacy: UPDATE tbl_Searchtags SET tags_text='...', tags_url='...', tags_displayorder=... WHERE tags_ID IN (...)
            var sql = @"UPDATE tbl_Searchtags 
                        SET tags_text = @text, tags_url = @url, tags_displayorder = @order 
                        WHERE tags_ID = @tagId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@text", item.Text?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@url", item.Url?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@order", item.DisplayOrder);
            cmd.Parameters.AddWithValue("@tagId", item.TagId);

            updated += await cmd.ExecuteNonQueryAsync();
        }

        return updated;
    }

    // ─── Phase 2: Per-row Active Toggle (legacy lnkActive_OnClick L1033) ─────

    /// <summary>
    /// Toggle tags_IsActive for a single tag.
    /// Legacy SQL: UPDATE tbl_Searchtags SET tags_IsActive = 1/0 WHERE tags_ID = @id
    /// </summary>
    public async Task<bool> ToggleActiveAsync(int tagId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Read current state then toggle
        var sql = @"UPDATE tbl_Searchtags 
                    SET tags_IsActive = CASE WHEN tags_IsActive = 1 THEN 0 ELSE 1 END 
                    WHERE tags_ID = @tagId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ─── Phase 2: Bulk Activate/Deactivate (legacy ActiveDeactiveTags L843-890) ──

    /// <summary>
    /// Bulk set tags_IsActive for selected tag IDs.
    /// Legacy SQL: UPDATE tbl_Searchtags SET tags_IsActive = @val WHERE tags_ID IN (...)
    /// </summary>
    public async Task<int> BulkSetActiveAsync(List<int> tagIds, bool active)
    {
        if (tagIds == null || tagIds.Count == 0) return 0;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build parameterized IN clause
        var paramNames = new List<string>();
        var cmd = new SqlCommand();
        for (int i = 0; i < tagIds.Count; i++)
        {
            paramNames.Add($"@id{i}");
            cmd.Parameters.AddWithValue($"@id{i}", tagIds[i]);
        }

        cmd.CommandText = $"UPDATE tbl_Searchtags SET tags_IsActive = @active WHERE tags_ID IN ({string.Join(",", paramNames)})";
        cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
        cmd.Connection = conn;

        return await cmd.ExecuteNonQueryAsync();
    }

    // ─── Phase 2: Toggle Show at Front (legacy lnkUnlinked_OnClick L988) ─────

    /// <summary>
    /// Toggle tags_Showatfront for a single tag.
    /// Legacy SQL: UPDATE tbl_Searchtags SET tags_Showatfront = 1/0 WHERE tags_ID = @id
    /// </summary>
    public async Task<bool> ToggleShowAtFrontAsync(int tagId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_Searchtags 
                    SET tags_Showatfront = CASE WHEN tags_Showatfront = 1 THEN 0 ELSE 1 END 
                    WHERE tags_ID = @tagId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        return await cmd.ExecuteNonQueryAsync() > 0;
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

    // ─── Phase 3: Search Products by Keywords (legacy btnlinknewproducts_submit_Click L1231) ──

    /// <summary>
    /// Search products matching keywords with include/exclude, filtered by product type.
    /// In LINK mode: shows products NOT already linked to the selected tags.
    /// In UNLINK mode: shows products already linked to the selected tags.
    /// Legacy pattern: comma-separated keywords → AND LIKE conditions.
    /// </summary>
    public async Task<List<KeywordSearchProduct>> SearchProductsByKeywordAsync(
        string keywords, string excludeKeywords, int productType, List<int> tagIds, bool unlinkMode)
    {
        var results = new List<KeywordSearchProduct>();
        if (string.IsNullOrWhiteSpace(keywords) || tagIds == null || tagIds.Count == 0)
            return results;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build keyword LIKE conditions (legacy: comma → AND LIKE)
        var kwParts = keywords.Replace(", ", ",").Replace(" ,", ",")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        var likeClauses = new List<string>();
        var cmd = new SqlCommand { Connection = conn };
        for (int i = 0; i < kwParts.Length; i++)
        {
            likeClauses.Add($"product_Name LIKE '%' + @kw{i} + '%'");
            cmd.Parameters.AddWithValue($"@kw{i}", kwParts[i].Trim());
        }

        // Build exclude keyword NOT LIKE conditions
        var notLikeClauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(excludeKeywords))
        {
            var exParts = excludeKeywords.Replace(", ", ",").Replace(" ,", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < exParts.Length; i++)
            {
                notLikeClauses.Add($"product_Name NOT LIKE '%' + @exkw{i} + '%'");
                cmd.Parameters.AddWithValue($"@exkw{i}", exParts[i].Trim());
            }
        }

        // Build tag IDs for IN clause (parameterized)
        var tagParamNames = new List<string>();
        for (int i = 0; i < tagIds.Count; i++)
        {
            tagParamNames.Add($"@tid{i}");
            cmd.Parameters.AddWithValue($"@tid{i}", tagIds[i]);
        }
        var tagInClause = string.Join(",", tagParamNames);

        // WHERE: product_isdeleted=0 AND product_isactive=1 AND (keyword LIKEs) AND (exclude NOT LIKEs)
        var where = "product_isdeleted = 0 AND product_isactive = 1";
        if (likeClauses.Count > 0) where += $" AND ({string.Join(" AND ", likeClauses)})";
        if (notLikeClauses.Count > 0) where += $" AND ({string.Join(" AND ", notLikeClauses)})";

        // Product type filter (legacy: product_type=X when not "0"/all)
        if (productType > 0)
        {
            where += $" AND product_type = @ptype";
            cmd.Parameters.AddWithValue("@ptype", productType);
        }

        // IN/NOT IN subquery (legacy: product_ID [NOT] IN (SELECT lnkPrd2tag_prdID ...))
        var inOp = unlinkMode ? "IN" : "NOT IN";
        where += $" AND product_ID {inOp} (SELECT lnkPrd2tag_prdID FROM tbl_lnkPrd2tag WHERE lnkPrd2tag_tagID IN ({tagInClause}) AND lnkPrd2tag_apiID = 0)";

        // SELECT columns matching legacy (with tagIDs aggregation via FOR XML PATH)
        var sql = $@"SELECT TOP 200
            product_ID, product_image1, product_Name, product_code, product_startingtPrice,
            ISNULL(STUFF(
                (SELECT ', ' + CONVERT(nvarchar(10), lnkPrd2tag_tagID)
                 FROM tbl_lnkPrd2tag 
                 WHERE lnkPrd2tag_prdID = product_ID AND lnkPrd2tag_apiID = 0
                 FOR XML PATH (''))
                , 1, 1, ''), '0') AS tagIDs
            FROM tbl_products
            WHERE {where}
            ORDER BY product_startingtPrice DESC";

        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new KeywordSearchProduct
            {
                ProductId = Convert.ToInt64(reader["product_ID"]),
                Image = reader["product_image1"]?.ToString() ?? "",
                Name = reader["product_Name"]?.ToString() ?? "",
                Code = reader["product_code"]?.ToString() ?? "",
                Price = reader["product_startingtPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["product_startingtPrice"]),
                LinkedTagIds = reader["tagIDs"]?.ToString() ?? "0"
            });
        }

        return results;
    }

    // ─── Phase 3: Bulk Link Products to Tags (legacy btnSubmitlinkprdtotags_submit_Click LINK) ──

    /// <summary>
    /// Bulk link: for each product × each tag, INSERT INTO tbl_lnkPrd2tag if not exists.
    /// Legacy: lnkPrd2tag_searchrank = 1003, lnkPrd2tag_apiID = 0, lnkPrd2tag_isdeleted = 0
    /// </summary>
    public async Task<int> BulkLinkProductsToTagsAsync(List<int> tagIds, List<long> productIds)
    {
        if (tagIds == null || tagIds.Count == 0 || productIds == null || productIds.Count == 0)
            return 0;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        int linked = 0;
        foreach (var prdId in productIds)
        {
            foreach (var tagId in tagIds)
            {
                // Check existence (matches legacy: db.lnkPrd2tag.Where(...).Any())
                await using var cmdCheck = new SqlCommand(
                    "SELECT COUNT(*) FROM tbl_lnkPrd2tag WHERE lnkPrd2tag_prdID = @prdId AND lnkPrd2tag_tagID = @tagId AND lnkPrd2tag_apiID = 0", conn);
                cmdCheck.Parameters.AddWithValue("@prdId", prdId);
                cmdCheck.Parameters.AddWithValue("@tagId", tagId);
                var exists = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync() ?? 0) > 0;

                if (!exists)
                {
                    // INSERT (matches legacy: lnkPrd2tag_searchrank=1003, apiID=0, isdeleted=false, IsDefault=false)
                    await using var cmdInsert = new SqlCommand(@"
                        INSERT INTO tbl_lnkPrd2tag 
                            (lnkPrd2tag_prdID, lnkPrd2tag_tagID, lnkPrd2tag_apiID, lnkPrd2tag_isdeleted, lnkPrd2tag_IsDefault, lnkPrd2tag_searchrank)
                        VALUES (@prdId, @tagId, 0, 0, 0, 1003)", conn);
                    cmdInsert.Parameters.AddWithValue("@prdId", prdId);
                    cmdInsert.Parameters.AddWithValue("@tagId", tagId);
                    await cmdInsert.ExecuteNonQueryAsync();
                    linked++;
                }
            }
        }

        return linked;
    }

    // ─── Phase 3: Bulk Unlink Products from Tags (legacy batch DELETE) ───────────

    /// <summary>
    /// Bulk unlink: DELETE FROM tbl_lnkPrd2tag WHERE prdID IN (...) AND apiID=0 AND tagID IN (...)
    /// Matches legacy: clsCustomDelete batch DELETE pattern.
    /// </summary>
    public async Task<int> BulkUnlinkProductsFromTagsAsync(List<int> tagIds, List<long> productIds)
    {
        if (tagIds == null || tagIds.Count == 0 || productIds == null || productIds.Count == 0)
            return 0;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build parameterized IN clauses (matches legacy batch DELETE)
        var cmd = new SqlCommand { Connection = conn };
        var prdParams = new List<string>();
        for (int i = 0; i < productIds.Count; i++)
        {
            prdParams.Add($"@prd{i}");
            cmd.Parameters.AddWithValue($"@prd{i}", productIds[i]);
        }
        var tagParams = new List<string>();
        for (int i = 0; i < tagIds.Count; i++)
        {
            tagParams.Add($"@tag{i}");
            cmd.Parameters.AddWithValue($"@tag{i}", tagIds[i]);
        }

        cmd.CommandText = $@"DELETE FROM tbl_lnkPrd2tag 
            WHERE lnkPrd2tag_prdID IN ({string.Join(",", prdParams)}) 
              AND lnkPrd2tag_apiID = 0 
              AND lnkPrd2tag_tagID IN ({string.Join(",", tagParams)})";

        return await cmd.ExecuteNonQueryAsync();
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

    // Filter state (for URL building / form repopulation)
    public int SearchFor { get; set; }      // 0=Cakes, 1=Cupcakes, 2=Party Accessory
    public int Status { get; set; } = 1;    // 0=All, 1=Active, 2=Inactive
    public string FilterP { get; set; } = "";
    public int SearchType { get; set; }     // 0=Anywhere, 1=Starts, 2=Ends, 3=Exact
    public int Sort { get; set; } = 11;     // Legacy default sort

    // Sub-tag mode (legacy ?tagID=X)
    public int? ParentTagId { get; set; }
    public string ParentTagText { get; set; } = "";

    // Legacy compat — keep old property name
    public string Search { get => FilterP; set => FilterP = value; }
}

public class SearchTagItem
{
    public int TagId { get; set; }
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
    public bool ShowAtFront { get; set; }
    public int DisplayOrder { get; set; }
    public int ProductCount { get; set; }

    // New fields matching legacy
    public bool IsActive { get; set; }
    public int TagsFor { get; set; }        // 0=cakes, 1=cupcakes, 2=party accessory
    public int SearchCount { get; set; }     // tags_countsearch
    public DateTime? CreatedOn { get; set; }  // tags_createdOn
    public int DownloadedCount { get; set; }  // countdnld
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

/// <summary>
/// Used by Phase 2 inline update — matches legacy btnUpdate_onClick per-row data.
/// </summary>
public class TagUpdateItem
{
    public int TagId { get; set; }
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Phase 3: Product result from keyword search — matches legacy prdlist class.
/// </summary>
public class KeywordSearchProduct
{
    public long ProductId { get; set; }
    public string Image { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal Price { get; set; }
    public string LinkedTagIds { get; set; } = "0";
}
