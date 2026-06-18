using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class LocationItem
{
    public long LocationID { get; set; }
    public string LocationTitle { get; set; } = "";
    public long ParentLocationId { get; set; }
    public long WebstoreId { get; set; }
    public int DisplayOrder { get; set; }
    public bool Location_IsActive { get; set; }
    public bool Location_IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class LocationListResult
{
    public List<LocationItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class BreadcrumbItem
{
    public long LocationID { get; set; }
    public string LocationTitle { get; set; } = "";
    public long ParentLocationId { get; set; }
    public int Lvl { get; set; }
    public int MaxLevel { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Manage Location module.
/// Migrated from managelocation.aspx.
/// Uses DefaultConnection with tbl_location table.
/// Module 7 permission check.
/// Supports hierarchical navigation with breadcrumb and recursive delete.
/// </summary>
public class ManageLocationService
{
    private readonly string _defaultConnection;

    public ManageLocationService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated locations for a given parent with optional StartsWith search.
    /// Ordered by DisplayOrder ASC.
    /// </summary>
    public async Task<LocationListResult> GetLocationsAsync(long webshopId, long parentId, string? search, int page, int pageSize)
    {
        var result = new LocationListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build WHERE clause
        var whereClause = "WHERE Location_IsDeleted = 0 AND WebstoreId = @wid AND ParentLocationId = @parentId";
        if (!string.IsNullOrEmpty(search))
        {
            whereClause += " AND LocationTitle LIKE @search";
        }

        // Get total count
        var countSql = $"SELECT COUNT(1) FROM tbl_location {whereClause}";
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("@wid", webshopId);
            countCmd.Parameters.AddWithValue("@parentId", parentId);
            if (!string.IsNullOrEmpty(search))
                countCmd.Parameters.AddWithValue("@search", search + "%");

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            result.TotalCount = totalCount;
            result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        }

        // Get paginated items
        var sql = $@"SELECT LocationID, LocationTitle, ParentLocationId, WebstoreId, 
                            DisplayOrder, Location_IsActive, Location_IsDeleted, CreatedOn
                     FROM tbl_location 
                     {whereClause}
                     ORDER BY DisplayOrder ASC
                     OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        cmd.Parameters.AddWithValue("@parentId", parentId);
        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("@search", search + "%");
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Items.Add(new LocationItem
            {
                LocationID = reader.GetInt64(0),
                LocationTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ParentLocationId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                WebstoreId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                DisplayOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Location_IsActive = !reader.IsDBNull(5) && reader.GetBoolean(5),
                Location_IsDeleted = !reader.IsDBNull(6) && reader.GetBoolean(6),
                CreatedOn = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
            });
        }

        return result;
    }

    /// <summary>
    /// Gets breadcrumb trail by walking UP from the given locationId to root.
    /// Returns items ordered from root to current (lvl DESC from CTE).
    /// </summary>
    public async Task<List<BreadcrumbItem>> GetBreadcrumbAsync(long locationId)
    {
        var items = new List<BreadcrumbItem>();
        if (locationId == 0) return items;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @";WITH RCTE AS (
    SELECT LocationID, LocationTitle, ParentLocationId, 1 AS Lvl FROM tbl_location 
    WHERE LocationID = @id AND Location_IsDeleted = 0
    UNION ALL
    SELECT rh.LocationID, rh.LocationTitle, rh.ParentLocationId, Lvl+1 AS Lvl FROM tbl_location rh
    INNER JOIN RCTE rc ON rh.LocationID = rc.ParentLocationId WHERE rh.Location_IsDeleted = 0
)
SELECT LocationID, LocationTitle, ParentLocationId, Lvl, (SELECT MAX(lvl) FROM RCTE) AS MaxLevel 
FROM RCTE r ORDER BY lvl DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", locationId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BreadcrumbItem
            {
                LocationID = reader.GetInt64(0),
                LocationTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ParentLocationId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                Lvl = reader.GetInt32(3),
                MaxLevel = reader.GetInt32(4)
            });
        }

        return items;
    }

    /// <summary>
    /// Returns the depth level of the given location from root.
    /// Level 1 = root items, Level 2 = children of root, etc.
    /// Returns 0 if no id provided (root view).
    /// </summary>
    public async Task<int> GetLevelAsync(long locationId)
    {
        if (locationId == 0) return 0;

        var breadcrumb = await GetBreadcrumbAsync(locationId);
        if (breadcrumb.Count > 0)
            return breadcrumb[0].MaxLevel;

        return 0;
    }

    /// <summary>
    /// Gets a single location by ID.
    /// </summary>
    public async Task<LocationItem?> GetByIdAsync(long locationId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT LocationID, LocationTitle, ParentLocationId, WebstoreId, 
                           DisplayOrder, Location_IsActive, Location_IsDeleted, CreatedOn
                    FROM tbl_location 
                    WHERE LocationID = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", locationId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new LocationItem
            {
                LocationID = reader.GetInt64(0),
                LocationTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ParentLocationId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                WebstoreId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                DisplayOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Location_IsActive = !reader.IsDBNull(5) && reader.GetBoolean(5),
                Location_IsDeleted = !reader.IsDBNull(6) && reader.GetBoolean(6),
                CreatedOn = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
            };
        }

        return null;
    }

    /// <summary>
    /// Saves (add or update) a location. Returns false if duplicate title exists within same parent.
    /// On insert: sets CreatedOn, Location_IsActive=true, Location_IsDeleted=false.
    /// </summary>
    public async Task<bool> SaveAsync(LocationItem item, long webshopId, long parentId)
    {
        // Check for duplicate title within same parent
        long? excludeId = item.LocationID > 0 ? item.LocationID : null;
        if (await IsDuplicateTitleAsync(item.LocationTitle, webshopId, parentId, excludeId))
            return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        if (item.LocationID == 0)
        {
            // INSERT new location
            var sql = @"INSERT INTO tbl_location 
                        (LocationTitle, ParentLocationId, WebstoreId, DisplayOrder, Location_IsActive, Location_IsDeleted, CreatedOn)
                        VALUES (@title, @parentId, @webstoreId, @displayOrder, 1, 0, @createdOn)";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", item.LocationTitle.Trim());
            cmd.Parameters.AddWithValue("@parentId", parentId);
            cmd.Parameters.AddWithValue("@webstoreId", webshopId);
            cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
            cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // UPDATE existing location
            var sql = @"UPDATE tbl_location 
                        SET LocationTitle = @title, 
                            DisplayOrder = @displayOrder
                        WHERE LocationID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", item.LocationTitle.Trim());
            cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
            cmd.Parameters.AddWithValue("@id", item.LocationID);

            await cmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Bulk set active/inactive for matching location IDs within a webstore.
    /// </summary>
    public async Task<bool> BulkSetActiveAsync(List<long> ids, long webshopId, bool isActive)
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

            var sql = $@"UPDATE tbl_location 
                         SET Location_IsActive = @isActive 
                         WHERE WebstoreId = @wid AND LocationID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@isActive", isActive);
            cmd.Parameters.AddWithValue("@wid", webshopId);
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
    /// Recursive soft-delete using CTE — deletes the location AND all its children/grandchildren.
    /// Walks DOWN from target to all descendants.
    /// </summary>
    public async Task<bool> RecursiveDeleteAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @";WITH RCTE AS (
    SELECT LocationID, ParentLocationId, 1 AS Lvl FROM tbl_location 
    WHERE LocationID = @id AND Location_IsDeleted = 0
    UNION ALL
    SELECT rh.LocationID, rh.ParentLocationId, Lvl+1 AS Lvl FROM tbl_location rh
    INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID WHERE rh.Location_IsDeleted = 0
)
UPDATE tbl_location SET Location_IsDeleted = 1 WHERE LocationID IN (SELECT LocationID FROM RCTE)";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Updates the display order for a single location.
    /// </summary>
    public async Task<bool> UpdateDisplayOrderAsync(long id, int displayOrder)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = "UPDATE tbl_location SET DisplayOrder = @displayOrder WHERE LocationID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a location title already exists (case-insensitive) within the same parent and webstore, excluding deleted items.
    /// Optionally excludes a specific ID (for edit scenarios).
    /// </summary>
    public async Task<bool> IsDuplicateTitleAsync(string title, long webshopId, long parentId, long? excludeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT COUNT(1) FROM tbl_location 
                    WHERE Location_IsDeleted = 0 
                      AND WebstoreId = @wid 
                      AND ParentLocationId = @parentId
                      AND LOWER(LocationTitle) = LOWER(@title)";

        if (excludeId.HasValue)
            sql += " AND LocationID <> @excludeId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        cmd.Parameters.AddWithValue("@parentId", parentId);
        cmd.Parameters.AddWithValue("@title", title.Trim());

        if (excludeId.HasValue)
            cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
