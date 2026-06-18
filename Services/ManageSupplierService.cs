using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class SupplierItem
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string? Supplier_AddressDetail { get; set; }
    public string? Supplier_Remarks { get; set; }
    public bool Supplier_IsAccessory { get; set; }
    public bool Supplier_IsTopper { get; set; }
    public long WebstoreId { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool Supplier_IsActive { get; set; }
    public bool Suppllier_IsDeleted { get; set; } // Note: legacy typo preserved (double L)
}

public class SupplierListResult
{
    public List<SupplierItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Manage Supplier module.
/// Migrated from managesupplier.aspx.
/// Uses DefaultConnection with tbl_ProductSupplier table.
/// Module 7 permission + HQ-only (webshopId == 82).
/// </summary>
public class ManageSupplierService
{
    private readonly string _defaultConnection;

    public ManageSupplierService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated suppliers for a webstore with optional StartsWith search.
    /// Ordered by CreatedOn DESC.
    /// </summary>
    public async Task<SupplierListResult> GetSuppliersAsync(long webstoreId, int page, int pageSize, string? search)
    {
        var result = new SupplierListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build WHERE clause
        var whereClause = "WHERE Suppllier_IsDeleted = 0 AND WebstoreId = @wid";
        if (!string.IsNullOrEmpty(search))
        {
            whereClause += " AND SupplierName LIKE @search";
        }

        // Get total count
        var countSql = $"SELECT COUNT(1) FROM tbl_ProductSupplier {whereClause}";
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
        var sql = $@"SELECT SupplierId, SupplierName, Supplier_AddressDetail, Supplier_Remarks, 
                            Supplier_IsAccessory, Supplier_IsTopper, WebstoreId, CreatedOn, 
                            Supplier_IsActive, Suppllier_IsDeleted
                     FROM tbl_ProductSupplier 
                     {whereClause}
                     ORDER BY CreatedOn DESC
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
            result.Items.Add(new SupplierItem
            {
                SupplierId = reader.GetInt64(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Supplier_AddressDetail = reader.IsDBNull(2) ? null : reader.GetString(2),
                Supplier_Remarks = reader.IsDBNull(3) ? null : reader.GetString(3),
                Supplier_IsAccessory = !reader.IsDBNull(4) && reader.GetBoolean(4),
                Supplier_IsTopper = !reader.IsDBNull(5) && reader.GetBoolean(5),
                WebstoreId = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                CreatedOn = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                Supplier_IsActive = !reader.IsDBNull(8) && reader.GetBoolean(8),
                Suppllier_IsDeleted = !reader.IsDBNull(9) && reader.GetBoolean(9)
            });
        }

        return result;
    }

    /// <summary>
    /// Gets a single supplier by ID.
    /// </summary>
    public async Task<SupplierItem?> GetByIdAsync(long supplierId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT SupplierId, SupplierName, Supplier_AddressDetail, Supplier_Remarks, 
                           Supplier_IsAccessory, Supplier_IsTopper, WebstoreId, CreatedOn, 
                           Supplier_IsActive, Suppllier_IsDeleted
                    FROM tbl_ProductSupplier 
                    WHERE SupplierId = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", supplierId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SupplierItem
            {
                SupplierId = reader.GetInt64(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Supplier_AddressDetail = reader.IsDBNull(2) ? null : reader.GetString(2),
                Supplier_Remarks = reader.IsDBNull(3) ? null : reader.GetString(3),
                Supplier_IsAccessory = !reader.IsDBNull(4) && reader.GetBoolean(4),
                Supplier_IsTopper = !reader.IsDBNull(5) && reader.GetBoolean(5),
                WebstoreId = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                CreatedOn = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                Supplier_IsActive = !reader.IsDBNull(8) && reader.GetBoolean(8),
                Suppllier_IsDeleted = !reader.IsDBNull(9) && reader.GetBoolean(9)
            };
        }

        return null;
    }

    /// <summary>
    /// Saves (add or update) a supplier. Returns false if duplicate name exists.
    /// On insert: sets CreatedOn, Supplier_IsActive=true, Suppllier_IsDeleted=false.
    /// </summary>
    public async Task<bool> SaveAsync(SupplierItem item, long webstoreId)
    {
        // Check for duplicate name
        long? excludeId = item.SupplierId > 0 ? item.SupplierId : null;
        if (await IsDuplicateNameAsync(item.SupplierName, webstoreId, excludeId))
            return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        if (item.SupplierId == 0)
        {
            // INSERT new supplier
            var sql = @"INSERT INTO tbl_ProductSupplier 
                        (SupplierName, Supplier_AddressDetail, Supplier_Remarks, Supplier_IsAccessory, 
                         Supplier_IsTopper, WebstoreId, CreatedOn, Supplier_IsActive, Suppllier_IsDeleted)
                        VALUES (@name, @address, @remarks, @isAccessory, @isTopper, @webstoreId, @createdOn, 1, 0)";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", item.SupplierName.Trim());
            cmd.Parameters.AddWithValue("@address", (object?)item.Supplier_AddressDetail?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@remarks", (object?)item.Supplier_Remarks?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isAccessory", item.Supplier_IsAccessory);
            cmd.Parameters.AddWithValue("@isTopper", item.Supplier_IsTopper);
            cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // UPDATE existing supplier
            var sql = @"UPDATE tbl_ProductSupplier 
                        SET SupplierName = @name, 
                            Supplier_AddressDetail = @address, 
                            Supplier_Remarks = @remarks, 
                            Supplier_IsAccessory = @isAccessory, 
                            Supplier_IsTopper = @isTopper
                        WHERE SupplierId = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", item.SupplierName.Trim());
            cmd.Parameters.AddWithValue("@address", (object?)item.Supplier_AddressDetail?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@remarks", (object?)item.Supplier_Remarks?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isAccessory", item.Supplier_IsAccessory);
            cmd.Parameters.AddWithValue("@isTopper", item.Supplier_IsTopper);
            cmd.Parameters.AddWithValue("@id", item.SupplierId);

            await cmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Bulk set active/inactive for matching supplier IDs within a webstore.
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

            var sql = $@"UPDATE tbl_ProductSupplier 
                         SET Supplier_IsActive = @isActive 
                         WHERE WebstoreId = @wid AND SupplierId IN ({string.Join(",", paramNames)})";

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
    /// Bulk delete suppliers (soft delete + cascade delete from tbl_Product_Supplier_Linking).
    /// Uses a transaction to ensure both operations succeed or neither does.
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids, long webstoreId)
    {
        if (ids.Count == 0) return false;

        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();
            await using var transaction = conn.BeginTransaction();

            try
            {
                // Build parameterized IN clause
                var paramNames = new List<string>();
                for (int i = 0; i < ids.Count; i++)
                    paramNames.Add($"@id{i}");

                var inClause = string.Join(",", paramNames);

                // 1. Soft delete suppliers
                var sql1 = $@"UPDATE tbl_ProductSupplier 
                              SET Suppllier_IsDeleted = 1 
                              WHERE WebstoreId = @wid AND SupplierId IN ({inClause})";

                await using (var cmd1 = new SqlCommand(sql1, conn, transaction))
                {
                    cmd1.Parameters.AddWithValue("@wid", webstoreId);
                    for (int i = 0; i < ids.Count; i++)
                        cmd1.Parameters.AddWithValue($"@id{i}", ids[i]);

                    await cmd1.ExecuteNonQueryAsync();
                }

                // 2. Hard delete from linking table
                var sql2 = $@"DELETE FROM tbl_Product_Supplier_Linking 
                              WHERE SupplierId IN ({inClause})";

                await using (var cmd2 = new SqlCommand(sql2, conn, transaction))
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd2.Parameters.AddWithValue($"@id{i}", ids[i]);

                    await cmd2.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a supplier name already exists (case-insensitive) within the webstore, excluding deleted items.
    /// Optionally excludes a specific ID (for edit scenarios).
    /// </summary>
    public async Task<bool> IsDuplicateNameAsync(string name, long webstoreId, long? excludeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT COUNT(1) FROM tbl_ProductSupplier 
                    WHERE Suppllier_IsDeleted = 0 
                      AND WebstoreId = @wid 
                      AND LOWER(SupplierName) = LOWER(@name)";

        if (excludeId.HasValue)
            sql += " AND SupplierId <> @excludeId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@name", name.Trim());

        if (excludeId.HasValue)
            cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
