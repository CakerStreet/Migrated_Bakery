using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class AllergenDropdownItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class AllergenMatrixRow
{
    public int RowId { get; set; }
    public long AllergenMatrixID { get; set; }
    public long CakeTypeID { get; set; }
    public long FlvID1 { get; set; }
    public long FlvID2 { get; set; }
    public long FlvID3 { get; set; }
    public string Ingredient { get; set; } = "";
    public string Allergens { get; set; } = "";
}

public class AllergenMatrixSaveItem
{
    public long AllergenMatrixID { get; set; }
    public long CakeTypeID { get; set; }
    public long FlvID1 { get; set; }
    public long FlvID2 { get; set; }
    public long FlvID3 { get; set; }
    public string Ingredient { get; set; } = "";
    public string Allergens { get; set; } = "";
}

public class AllergenMatrixSaveRequest
{
    public List<AllergenMatrixSaveItem> Items { get; set; } = new();
    public string DeletedIds { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Manage Allergen Matrix module.
/// Migrated from manageallergenmatrix.aspx.
/// Uses DefaultConnection with tbl_caketype, tbl_custflavour, tbl_AllergenMatrix tables.
/// </summary>
public class AllergenMatrixService
{
    private readonly string _defaultConnection;

    public AllergenMatrixService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets active cake types for dropdown.
    /// </summary>
    public async Task<List<AllergenDropdownItem>> GetCakeTypesAsync()
    {
        var items = new List<AllergenDropdownItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT CakeTypeID, CakeTypeTitle FROM tbl_caketype WHERE isActive = 1";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new AllergenDropdownItem
            {
                Id = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets flavours for dropdown by parent ID.
    /// parentId: 2200=Dietary, 2201=Sponge, 2202=Filling
    /// </summary>
    public async Task<List<AllergenDropdownItem>> GetFlavoursAsync(long parentId)
    {
        var items = new List<AllergenDropdownItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT FlavourID, FlavourShortName FROM tbl_custflavour WHERE IsActive = 1 AND floavour_parentID = @parentId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@parentId", parentId);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new AllergenDropdownItem
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets allergen matrix rows filtered by cake type and optional flavour IDs.
    /// Uses direct query for portability.
    /// </summary>
    public async Task<List<AllergenMatrixRow>> GetMatrixAsync(long cakeTypeId, long dietaryId, long spongeId, long fillingId)
    {
        var rows = new List<AllergenMatrixRow>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT AllergenMatrixID, CakeTypeID, FlvID1, FlvID2, FlvID3, Ingredient, Alergens
                    FROM tbl_AllergenMatrix
                    WHERE (CakeTypeID = @cakeTypeId AND FlvID1 = 0 AND FlvID2 = 0 AND FlvID3 = 0)
                       OR (@dietaryId <> 0 AND CakeTypeID = @cakeTypeId AND FlvID1 = @dietaryId AND FlvID2 = 0 AND FlvID3 = 0)
                       OR (@dietaryId <> 0 AND @spongeId <> 0 AND CakeTypeID = @cakeTypeId AND FlvID1 = @dietaryId AND FlvID2 = @spongeId AND FlvID3 = 0)
                       OR (@dietaryId <> 0 AND @spongeId <> 0 AND @fillingId <> 0 AND CakeTypeID = @cakeTypeId AND FlvID1 = @dietaryId AND FlvID2 = @spongeId AND FlvID3 = @fillingId)
                    ORDER BY AllergenMatrixID";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cakeTypeId", cakeTypeId);
        cmd.Parameters.AddWithValue("@dietaryId", dietaryId);
        cmd.Parameters.AddWithValue("@spongeId", spongeId);
        cmd.Parameters.AddWithValue("@fillingId", fillingId);

        await using var reader = await cmd.ExecuteReaderAsync();
        int rowId = 1;
        while (await reader.ReadAsync())
        {
            rows.Add(new AllergenMatrixRow
            {
                RowId = rowId++,
                AllergenMatrixID = reader.GetInt64(0),
                CakeTypeID = reader.GetInt64(1),
                FlvID1 = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                FlvID2 = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                FlvID3 = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                Ingredient = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Allergens = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }

        return rows;
    }

    /// <summary>
    /// Saves allergen matrix rows (insert/update) and deletes removed rows.
    /// Uses direct SQL for portability.
    /// </summary>
    public async Task<bool> SaveMatrixAsync(List<AllergenMatrixSaveItem> items, string deletedIds)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Delete removed rows
            if (!string.IsNullOrWhiteSpace(deletedIds))
            {
                var ids = deletedIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var paramNames = new List<string>();
                for (int i = 0; i < ids.Length; i++)
                    paramNames.Add($"@delId{i}");

                if (paramNames.Count > 0)
                {
                    var deleteSql = $"DELETE FROM tbl_AllergenMatrix WHERE AllergenMatrixID IN ({string.Join(",", paramNames)})";
                    await using var deleteCmd = new SqlCommand(deleteSql, conn);
                    for (int i = 0; i < ids.Length; i++)
                    {
                        if (long.TryParse(ids[i].Trim(), out var delId))
                            deleteCmd.Parameters.AddWithValue($"@delId{i}", delId);
                        else
                            deleteCmd.Parameters.AddWithValue($"@delId{i}", 0);
                    }
                    await deleteCmd.ExecuteNonQueryAsync();
                }
            }

            // Insert or update each item
            foreach (var item in items)
            {
                if (item.AllergenMatrixID == 0)
                {
                    // INSERT
                    var insertSql = @"INSERT INTO tbl_AllergenMatrix 
                                      (CakeTypeID, FlvID1, FlvID2, FlvID3, Ingredient, Alergens, Create_Date)
                                      VALUES (@cakeTypeId, @flvId1, @flvId2, @flvId3, @ingredient, @allergens, @createDate)";

                    await using var insertCmd = new SqlCommand(insertSql, conn);
                    insertCmd.Parameters.AddWithValue("@cakeTypeId", item.CakeTypeID);
                    insertCmd.Parameters.AddWithValue("@flvId1", item.FlvID1);
                    insertCmd.Parameters.AddWithValue("@flvId2", item.FlvID2);
                    insertCmd.Parameters.AddWithValue("@flvId3", item.FlvID3);
                    insertCmd.Parameters.AddWithValue("@ingredient", item.Ingredient ?? "");
                    insertCmd.Parameters.AddWithValue("@allergens", item.Allergens ?? "");
                    insertCmd.Parameters.AddWithValue("@createDate", DateTime.Now);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // UPDATE
                    var updateSql = @"UPDATE tbl_AllergenMatrix 
                                      SET CakeTypeID = @cakeTypeId, FlvID1 = @flvId1, FlvID2 = @flvId2, 
                                          FlvID3 = @flvId3, Ingredient = @ingredient, Alergens = @allergens
                                      WHERE AllergenMatrixID = @id";

                    await using var updateCmd = new SqlCommand(updateSql, conn);
                    updateCmd.Parameters.AddWithValue("@cakeTypeId", item.CakeTypeID);
                    updateCmd.Parameters.AddWithValue("@flvId1", item.FlvID1);
                    updateCmd.Parameters.AddWithValue("@flvId2", item.FlvID2);
                    updateCmd.Parameters.AddWithValue("@flvId3", item.FlvID3);
                    updateCmd.Parameters.AddWithValue("@ingredient", item.Ingredient ?? "");
                    updateCmd.Parameters.AddWithValue("@allergens", item.Allergens ?? "");
                    updateCmd.Parameters.AddWithValue("@id", item.AllergenMatrixID);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
