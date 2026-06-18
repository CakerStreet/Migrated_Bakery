using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class BakeryIngredientItem
{
    public long BakeryIngredient_ID { get; set; }
    public string BakeryIngredient_title { get; set; } = "";
    public string BakeryIngredient_Unit { get; set; } = "KG"; // KG or Ltr
    public decimal BakeryIngredient_qty { get; set; }
    public decimal BakeryIngredient_minQty { get; set; }
    public long BakeryIngredient_webstoreID { get; set; }
    public bool BakeryIngredient_IsDeleted { get; set; }
    public DateTime BakeryIngredient_modifiedOn { get; set; }
    public int BakeryIngredient_modifiedby { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Bakery Ingredient management.
/// Migrated from managebakeryingredient.aspx.
/// Uses DefaultConnection with tbl_BakeryIngredient table.
/// Module 9 permission.
/// </summary>
public class BakeryIngredientService
{
    private readonly string _defaultConnection;

    public BakeryIngredientService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets all non-deleted ingredients for a webstore, ordered by title.
    /// </summary>
    public async Task<List<BakeryIngredientItem>> GetAllAsync(long webstoreId)
    {
        var items = new List<BakeryIngredientItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT BakeryIngredient_ID, BakeryIngredient_title, BakeryIngredient_Unit, 
                           BakeryIngredient_qty, BakeryIngredient_minQty, BakeryIngredient_webstoreID, 
                           BakeryIngredient_IsDeleted, BakeryIngredient_modifiedOn, BakeryIngredient_modifiedby
                    FROM tbl_BakeryIngredient 
                    WHERE BakeryIngredient_IsDeleted = 0 AND BakeryIngredient_webstoreID = @wid 
                    ORDER BY BakeryIngredient_title";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BakeryIngredientItem
            {
                BakeryIngredient_ID = reader.GetInt64(0),
                BakeryIngredient_title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                BakeryIngredient_Unit = reader.IsDBNull(2) ? "KG" : reader.GetString(2),
                BakeryIngredient_qty = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                BakeryIngredient_minQty = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                BakeryIngredient_webstoreID = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                BakeryIngredient_IsDeleted = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                BakeryIngredient_modifiedOn = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                BakeryIngredient_modifiedby = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
            });
        }

        return items;
    }

    /// <summary>
    /// Adds a new ingredient. Returns false if duplicate name exists.
    /// </summary>
    public async Task<bool> AddAsync(BakeryIngredientItem item, long webstoreId, int userId)
    {
        // Check for duplicate name first
        if (await IsDuplicateNameAsync(item.BakeryIngredient_title, webstoreId, null))
            return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"INSERT INTO tbl_BakeryIngredient 
                    (BakeryIngredient_title, BakeryIngredient_Unit, BakeryIngredient_qty, BakeryIngredient_minQty, 
                     BakeryIngredient_webstoreID, BakeryIngredient_IsDeleted, BakeryIngredient_modifiedOn, BakeryIngredient_modifiedby)
                    VALUES (@title, @unit, @qty, @minQty, @webstoreId, 0, @modifiedOn, @modifiedBy)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@title", item.BakeryIngredient_title.Trim());
        cmd.Parameters.AddWithValue("@unit", item.BakeryIngredient_Unit);
        cmd.Parameters.AddWithValue("@qty", item.BakeryIngredient_qty);
        cmd.Parameters.AddWithValue("@minQty", item.BakeryIngredient_minQty);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
        cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
        cmd.Parameters.AddWithValue("@modifiedBy", userId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Updates an existing ingredient (title, unit, qty, minQty, modifiedOn, modifiedby).
    /// </summary>
    public async Task<bool> UpdateAsync(BakeryIngredientItem item, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_BakeryIngredient 
                        SET BakeryIngredient_title = @title, 
                            BakeryIngredient_Unit = @unit, 
                            BakeryIngredient_qty = @qty, 
                            BakeryIngredient_minQty = @minQty, 
                            BakeryIngredient_modifiedOn = @modifiedOn, 
                            BakeryIngredient_modifiedby = @modifiedBy 
                        WHERE BakeryIngredient_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", item.BakeryIngredient_title.Trim());
            cmd.Parameters.AddWithValue("@unit", item.BakeryIngredient_Unit);
            cmd.Parameters.AddWithValue("@qty", item.BakeryIngredient_qty);
            cmd.Parameters.AddWithValue("@minQty", item.BakeryIngredient_minQty);
            cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedBy", userId);
            cmd.Parameters.AddWithValue("@id", item.BakeryIngredient_ID);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk saves (updates) all items in a single connection.
    /// </summary>
    public async Task<bool> BulkSaveAsync(List<BakeryIngredientItem> items, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_BakeryIngredient 
                        SET BakeryIngredient_title = @title, 
                            BakeryIngredient_Unit = @unit, 
                            BakeryIngredient_qty = @qty, 
                            BakeryIngredient_minQty = @minQty, 
                            BakeryIngredient_modifiedOn = @modifiedOn, 
                            BakeryIngredient_modifiedby = @modifiedBy 
                        WHERE BakeryIngredient_ID = @id";

            foreach (var item in items)
            {
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@title", item.BakeryIngredient_title.Trim());
                cmd.Parameters.AddWithValue("@unit", item.BakeryIngredient_Unit);
                cmd.Parameters.AddWithValue("@qty", item.BakeryIngredient_qty);
                cmd.Parameters.AddWithValue("@minQty", item.BakeryIngredient_minQty);
                cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                cmd.Parameters.AddWithValue("@modifiedBy", userId);
                cmd.Parameters.AddWithValue("@id", item.BakeryIngredient_ID);

                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Soft deletes an ingredient (sets IsDeleted = 1).
    /// </summary>
    public async Task<bool> DeleteAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = "UPDATE tbl_BakeryIngredient SET BakeryIngredient_IsDeleted = 1 WHERE BakeryIngredient_ID = @id";

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
    /// Checks if a name already exists (case-insensitive) within the webstore, excluding deleted items.
    /// Optionally excludes a specific ID (for edit scenarios).
    /// </summary>
    public async Task<bool> IsDuplicateNameAsync(string name, long webstoreId, long? excludeId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT COUNT(1) FROM tbl_BakeryIngredient 
                    WHERE BakeryIngredient_IsDeleted = 0 
                      AND BakeryIngredient_webstoreID = @wid 
                      AND LOWER(BakeryIngredient_title) = LOWER(@name)";

        if (excludeId.HasValue)
            sql += " AND BakeryIngredient_ID <> @excludeId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@name", name.Trim());

        if (excludeId.HasValue)
            cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
