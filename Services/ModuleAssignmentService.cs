using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class StaffSelectItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class RoleSelectItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class ModuleItem
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = "";
    public string ModuleUrl { get; set; } = "";
    public bool IsAssigned { get; set; }
    public int AuthorizeId { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Module Assignment management.
/// Migrated from manageModuleAssignment.aspx.
/// Uses BusinessConnection for tbl_module, tbl_moduleAssignment, tbl_RoleMaster.
/// Uses DefaultConnection for tbl_bakeryuser (staff list).
/// Admin-only (userType 1).
/// </summary>
public class ModuleAssignmentService
{
    private readonly string _businessConnection;
    private readonly string _defaultConnection;

    public ModuleAssignmentService(IConfiguration config)
    {
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets staff list (customer_type=3, isActive=1) from tbl_bakeryuser (DefaultConnection).
    /// Returns Name as "customer_Name (customer_EmailID)".
    /// </summary>
    public async Task<List<StaffSelectItem>> GetStaffListAsync(long webstoreId)
    {
        var items = new List<StaffSelectItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT customer_ID, customer_Name + ' (' + Customer_EmailID + ')' AS customer_Name
                    FROM tbl_bakeryuser
                    WHERE customer_type = 3 AND customer_isActive = 1 AND customer_webshopID = @webstoreId
                    ORDER BY customer_type DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new StaffSelectItem
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets roles from tbl_RoleMaster (BusinessConnection) ordered by DisplayOrder.
    /// </summary>
    public async Task<List<RoleSelectItem>> GetRolesAsync()
    {
        var items = new List<RoleSelectItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"SELECT RoleID, RoleTitle FROM tbl_RoleMaster ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new RoleSelectItem
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets all modules from tbl_module (BusinessConnection) ordered by module_sortID,
    /// LEFT JOINed with tbl_moduleAssignment to mark assigned modules.
    /// </summary>
    public async Task<List<ModuleItem>> GetModulesWithAssignmentsAsync(long webstoreId, int? userId, int? roleId)
    {
        var items = new List<ModuleItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"SELECT m.module_ID, m.module_name, m.module_url,
                           CASE WHEN a.moduleAssignment_moduleID IS NOT NULL THEN 1 ELSE 0 END AS IsAssigned,
                           ISNULL(a.moduleAssignment_authorizeID, 0) AS AuthorizeId
                    FROM tbl_module m
                    LEFT JOIN tbl_moduleAssignment a 
                        ON a.moduleAssignment_moduleID = m.module_ID
                        AND a.moduleAssignment_webstoreID = @webstoreId";

        if (userId.HasValue && userId.Value > 0)
            sql += " AND a.moduleAssignment_userID = @userId";
        else if (roleId.HasValue && roleId.Value > 0)
            sql += " AND a.moduleAssignment_roleID = @roleId";
        else
            sql += " AND 1 = 0"; // No selection — no assignments matched

        sql += " ORDER BY m.module_sortID";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        if (userId.HasValue && userId.Value > 0)
            cmd.Parameters.AddWithValue("@userId", userId.Value);
        else if (roleId.HasValue && roleId.Value > 0)
            cmd.Parameters.AddWithValue("@roleId", roleId.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ModuleItem
            {
                ModuleId = reader.GetInt32(0),
                ModuleName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ModuleUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
                IsAssigned = reader.GetInt32(3) == 1,
                AuthorizeId = reader.GetInt32(4)
            });
        }

        return items;
    }

    /// <summary>
    /// Saves module assignments: DELETE existing + INSERT new for the selected user/role.
    /// Uses transaction. authorizeID is always set to 4 (full access).
    /// </summary>
    public async Task<bool> SaveAssignmentsAsync(long webstoreId, int? userId, int? roleId, List<int> moduleIds, int modifiedBy)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            // DELETE existing assignments for this user/role + webstoreId
            var deleteSql = @"DELETE FROM tbl_moduleAssignment 
                              WHERE moduleAssignment_webstoreID = @webstoreId";

            if (userId.HasValue && userId.Value > 0)
                deleteSql += " AND moduleAssignment_userID = @userId";
            else if (roleId.HasValue && roleId.Value > 0)
                deleteSql += " AND moduleAssignment_roleID = @roleId";
            else
                return false; // No valid selection

            await using (var deleteCmd = new SqlCommand(deleteSql, conn, transaction))
            {
                deleteCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                if (userId.HasValue && userId.Value > 0)
                    deleteCmd.Parameters.AddWithValue("@userId", userId.Value);
                else
                    deleteCmd.Parameters.AddWithValue("@roleId", roleId!.Value);

                await deleteCmd.ExecuteNonQueryAsync();
            }

            // INSERT new assignments for each selected moduleId
            foreach (var moduleId in moduleIds)
            {
                var insertSql = @"INSERT INTO tbl_moduleAssignment 
                                  (moduleAssignment_userID, moduleAssignment_roleID, 
                                   moduleAssignment_moduleID, moduleAssignment_webstoreID, 
                                   moduleAssignment_authorizeID)
                                  VALUES (@userIdVal, @roleIdVal, @moduleId, @webstoreId, 4)";

                await using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("@userIdVal", userId.HasValue && userId.Value > 0 ? userId.Value : 0);
                insertCmd.Parameters.AddWithValue("@roleIdVal", roleId.HasValue && roleId.Value > 0 ? roleId.Value : 0);
                insertCmd.Parameters.AddWithValue("@moduleId", moduleId);
                insertCmd.Parameters.AddWithValue("@webstoreId", webstoreId);

                await insertCmd.ExecuteNonQueryAsync();
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
}
