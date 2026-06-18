using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class ThemeItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public bool IsPopular { get; set; }
    public bool IsActive { get; set; }
}

public class ThemeListResult
{
    public List<ThemeItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Manage Party Themes module.
/// Migrated from managethemes.aspx.
/// Uses DefaultConnection with tbl_accessorytheme table.
/// Module 10 permission.
/// </summary>
public class ManageThemesService
{
    private readonly string _defaultConnection;

    public ManageThemesService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated themes for a webstore with optional search and status filter.
    /// Ordered by isPopular DESC, title ASC.
    /// </summary>
    public async Task<ThemeListResult> GetThemesAsync(long webstoreId, string? search, int statusFilter, int page, int pageSize)
    {
        var result = new ThemeListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build WHERE clause
        var whereClause = "WHERE accessorytheme_webstoreID = @wid AND accessorytheme_isdeleted = 0";
        if (statusFilter == 1)
            whereClause += " AND accessorytheme_isactive = 1";
        else if (statusFilter == 2)
            whereClause += " AND accessorytheme_isactive = 0";

        if (!string.IsNullOrEmpty(search))
            whereClause += " AND accessorytheme_title LIKE @search";

        // Get total count
        var countSql = $"SELECT COUNT(1) FROM tbl_accessorytheme {whereClause}";
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("@wid", webstoreId);
            if (!string.IsNullOrEmpty(search))
                countCmd.Parameters.AddWithValue("@search", search + "%");

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            result.TotalCount = totalCount;
            result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        }

        // Get paginated items
        var sql = $@"SELECT accessorytheme_ID, accessorytheme_title, accessorytheme_URL, 
                            accessorytheme_isPopular, accessorytheme_isactive
                     FROM tbl_accessorytheme 
                     {whereClause}
                     ORDER BY accessorytheme_isPopular DESC, accessorytheme_title
                     OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("@search", search + "%");
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Items.Add(new ThemeItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Url = reader.IsDBNull(2) ? "" : reader.GetString(2),
                IsPopular = !reader.IsDBNull(3) && reader.GetBoolean(3),
                IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4)
            });
        }

        return result;
    }

    /// <summary>
    /// Updates a single theme's title, URL (auto-generated slug), and isPopular flag.
    /// </summary>
    public async Task<bool> UpdateThemeAsync(long id, string title, bool isPopular, long webstoreId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var url = FormatTitleAsUrl(title);

            var sql = @"UPDATE tbl_accessorytheme 
                        SET accessorytheme_title = @title, 
                            accessorytheme_URL = @url,
                            accessorytheme_isPopular = @isPopular,
                            accessorytheme_modifiedOn = GETDATE()
                        WHERE accessorytheme_ID = @id AND accessorytheme_webstoreID = @wid";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", title.Trim());
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@isPopular", isPopular);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@wid", webstoreId);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk set active/inactive for matching theme IDs within a webstore.
    /// </summary>
    public async Task<bool> BulkSetActiveAsync(List<long> ids, long webstoreId, bool isActive)
    {
        if (ids.Count == 0) return false;

        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Build parameterized IN clause
            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                paramNames.Add($"@id{i}");

            var sql = $@"UPDATE tbl_accessorytheme 
                         SET accessorytheme_isactive = @isActive, accessorytheme_modifiedOn = GETDATE()
                         WHERE accessorytheme_webstoreID = @wid AND accessorytheme_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@isActive", isActive);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk soft-delete themes (set accessorytheme_isdeleted = 1).
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids, long webstoreId)
    {
        if (ids.Count == 0) return false;

        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Build parameterized IN clause
            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                paramNames.Add($"@id{i}");

            var sql = $@"UPDATE tbl_accessorytheme 
                         SET accessorytheme_isdeleted = 1, accessorytheme_modifiedOn = GETDATE()
                         WHERE accessorytheme_webstoreID = @wid AND accessorytheme_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Formats a title as a URL slug: lowercase, spaces→hyphens, remove special chars.
    /// Matches legacy clsglobaltext.strFormattitleURL behavior.
    /// </summary>
    private static string FormatTitleAsUrl(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        var slug = title.Trim().ToLower();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug;
    }
}
