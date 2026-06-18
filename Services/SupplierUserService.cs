using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class SupplierUserItem
{
    public long CustomerId { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
    public long WebshopId { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? SupplierId { get; set; }
}

public class SupplierUserListResult
{
    public List<SupplierUserItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class SupplierDropdownItem
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
}

public class AddSupplierUserModel
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public long SupplierId { get; set; }
}

public class BulkSaveSupplierUserModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Supplier User management module.
/// Migrated from supplierusers.aspx.
/// Uses DefaultConnection (tbl_bakeryuser) and BusinessConnection (suppliers.tbl_lnkSupplier_User).
/// Cross-DB approach: query BusinessConnection for supplier user IDs, then query DefaultConnection for user details.
/// </summary>
public class SupplierUserService
{
    private readonly string _defaultConnection;
    private readonly string _businessConnection;

    public SupplierUserService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Gets suppliers for dropdown filter.
    /// SELECT from tbl_ProductSupplier WHERE WebstoreId=@wid AND Suppllier_IsDeleted=0 ORDER BY SupplierName.
    /// </summary>
    public async Task<List<SupplierDropdownItem>> GetSuppliersForDropdownAsync(long webshopId)
    {
        var result = new List<SupplierDropdownItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT SupplierId, SupplierName 
                    FROM tbl_ProductSupplier 
                    WHERE WebstoreId = @wid AND Suppllier_IsDeleted = 0 
                    ORDER BY SupplierName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new SupplierDropdownItem
            {
                SupplierId = reader.GetInt64(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return result;
    }

    /// <summary>
    /// Gets paginated supplier users with optional filters.
    /// Cross-DB approach: query BusinessConnection for customer_IDs linked to supplier,
    /// then query DefaultConnection for user details.
    /// </summary>
    public async Task<SupplierUserListResult> GetSupplierUsersAsync(
        long webshopId, long? supplierId, int? statusFilter, string? search, int page, int pageSize = 23)
    {
        var result = new SupplierUserListResult();

        if (!supplierId.HasValue || supplierId.Value == 0)
            return result;

        // Step 1: Get customer_IDs from BusinessConnection (suppliers.tbl_lnkSupplier_User)
        var linkedCustomerIds = new List<long>();
        await using (var bizConn = new SqlConnection(_businessConnection))
        {
            await bizConn.OpenAsync();
            var linkSql = @"SELECT customer_ID FROM suppliers.tbl_lnkSupplier_User WHERE SupplierId = @sid";
            await using var linkCmd = new SqlCommand(linkSql, bizConn);
            linkCmd.Parameters.AddWithValue("@sid", supplierId.Value);

            await using var linkReader = await linkCmd.ExecuteReaderAsync();
            while (await linkReader.ReadAsync())
            {
                linkedCustomerIds.Add(linkReader.GetInt64(0));
            }
        }

        if (linkedCustomerIds.Count == 0)
            return result;

        // Step 2: Query DefaultConnection for user details with those IDs
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build parameterized IN clause for linked IDs
        var idParamNames = new List<string>();
        for (int i = 0; i < linkedCustomerIds.Count; i++)
            idParamNames.Add($"@lid{i}");

        var inClause = string.Join(",", idParamNames);

        // Build WHERE clause
        var whereClause = $"WHERE customer_webshopID = @webshopId AND customer_type = 11 AND customer_ID IN ({inClause})";

        if (statusFilter == 1)
            whereClause += " AND customer_isActive = 1";
        else if (statusFilter == 2)
            whereClause += " AND customer_isActive = 0";

        if (!string.IsNullOrEmpty(search))
            whereClause += " AND (customer_Name LIKE @search OR customer_EmailID LIKE @search)";

        // Get total count
        var countSql = $"SELECT COUNT(1) FROM tbl_bakeryuser {whereClause}";
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("@webshopId", webshopId);
            if (!string.IsNullOrEmpty(search))
                countCmd.Parameters.AddWithValue("@search", "%" + search + "%");
            for (int i = 0; i < linkedCustomerIds.Count; i++)
                countCmd.Parameters.AddWithValue($"@lid{i}", linkedCustomerIds[i]);

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            result.TotalCount = totalCount;
            result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        }

        // Get paginated items
        var sql = $@"SELECT customer_ID, customer_EmailID, customer_Name, customer_password,
                            customer_isActive, customer_isOpen, customer_webshopID, customer_createdOn
                     FROM tbl_bakeryuser
                     {whereClause}
                     ORDER BY customer_createdOn DESC
                     OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("@search", "%" + search + "%");
        for (int i = 0; i < linkedCustomerIds.Count; i++)
            cmd.Parameters.AddWithValue($"@lid{i}", linkedCustomerIds[i]);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Items.Add(new SupplierUserItem
            {
                CustomerId = reader.GetInt64(0),
                Email = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Password = reader.IsDBNull(3) ? "" : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4),
                IsOpen = !reader.IsDBNull(5) && reader.GetBoolean(5),
                WebshopId = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                CreatedOn = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                SupplierId = supplierId
            });
        }

        return result;
    }

    /// <summary>
    /// Adds a new supplier user.
    /// 1) Check duplicate email (DefaultConnection)
    /// 2) INSERT into tbl_bakeryuser type=11 with SCOPE_IDENTITY (DefaultConnection)
    /// 3) INSERT into suppliers.tbl_lnkSupplier_User (BusinessConnection)
    /// Sequential, NOT transactional (matching legacy).
    /// </summary>
    public async Task<bool> AddSupplierUserAsync(AddSupplierUserModel model, long webshopId, long supplierId)
    {
        // Duplicate email check (global)
        if (await IsDuplicateEmailAsync(model.Email))
            return false;

        // Step 1: Insert user into DefaultConnection
        long newUserId;
        await using (var conn = new SqlConnection(_defaultConnection))
        {
            await conn.OpenAsync();

            var sql = @"INSERT INTO tbl_bakeryuser 
                        (customer_EmailID, customer_Name, customer_password, customer_type,
                         customer_stafftype, customer_isActive, customer_isOpen, customer_webshopID,
                         customer_ExpiredOn, customer_createdOn, customer_ccCode)
                        VALUES (@email, @name, @password, 11,
                                0, 1, 1, @webshopId,
                                @expiredOn, @createdOn, @ccCode);
                        SELECT SCOPE_IDENTITY();";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@email", model.Email.Trim());
            cmd.Parameters.AddWithValue("@name", model.Name.Trim());
            cmd.Parameters.AddWithValue("@password", model.Password);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);
            cmd.Parameters.AddWithValue("@expiredOn", DateTime.Now.AddYears(1));
            cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);
            cmd.Parameters.AddWithValue("@ccCode", Guid.NewGuid().ToString());

            newUserId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        // Step 2: Insert link into BusinessConnection
        await using (var bizConn = new SqlConnection(_businessConnection))
        {
            await bizConn.OpenAsync();

            var linkSql = @"INSERT INTO suppliers.tbl_lnkSupplier_User (customer_ID, SupplierId)
                            VALUES (@customerId, @supplierId)";

            await using var linkCmd = new SqlCommand(linkSql, bizConn);
            linkCmd.Parameters.AddWithValue("@customerId", newUserId);
            linkCmd.Parameters.AddWithValue("@supplierId", supplierId);
            await linkCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Updates a supplier user's name and password.
    /// UPDATE name, password WHERE id AND webshopId (DefaultConnection).
    /// </summary>
    public async Task<bool> UpdateSupplierUserAsync(long id, string name, string password, long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_bakeryuser 
                    SET customer_Name = @name, customer_password = @password,
                        customer_type = 11, customer_stafftype = 0
                    WHERE customer_ID = @id AND customer_webshopID = @webshopId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@password", password);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Bulk save: loop UPDATE each user (DefaultConnection).
    /// </summary>
    public async Task<bool> BulkSaveAsync(List<BulkSaveSupplierUserModel> items, long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        foreach (var item in items)
        {
            var sql = @"UPDATE tbl_bakeryuser 
                        SET customer_Name = @name, customer_password = @pwd,
                            customer_type = 11, customer_stafftype = 0
                        WHERE customer_ID = @id AND customer_webshopID = @webshopId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", item.Name.Trim());
            cmd.Parameters.AddWithValue("@pwd", item.Password);
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);
            await cmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Bulk set active/inactive for matching user IDs within a webshop.
    /// Uses parameterized IN clause.
    /// </summary>
    public async Task<bool> BulkSetActiveAsync(List<long> ids, long webshopId, bool isActive)
    {
        if (ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var paramNames = new List<string>();
        for (int i = 0; i < ids.Count; i++)
            paramNames.Add($"@id{i}");

        var sql = $@"UPDATE tbl_bakeryuser 
                     SET customer_isActive = @isActive 
                     WHERE customer_webshopID = @webshopId AND customer_ID IN ({string.Join(",", paramNames)})";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@isActive", isActive);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        for (int i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Bulk delete supplier users.
    /// 1) DELETE from tbl_bakeryuser WHERE type<>1 (DefaultConnection)
    /// 2) DELETE from suppliers.tbl_lnkSupplier_User (BusinessConnection)
    /// Sequential, matching legacy.
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids, long webshopId)
    {
        if (ids.Count == 0) return false;

        // Build parameterized IN clause
        var paramNames = new List<string>();
        for (int i = 0; i < ids.Count; i++)
            paramNames.Add($"@id{i}");

        var inClause = string.Join(",", paramNames);

        // Step 1: DELETE from tbl_bakeryuser (DefaultConnection)
        await using (var conn = new SqlConnection(_defaultConnection))
        {
            await conn.OpenAsync();

            var sql = $@"DELETE FROM tbl_bakeryuser 
                         WHERE customer_type <> 1 
                           AND customer_webshopID = @webshopId 
                           AND customer_ID IN ({inClause})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await cmd.ExecuteNonQueryAsync();
        }

        // Step 2: DELETE from suppliers.tbl_lnkSupplier_User (BusinessConnection)
        await using (var bizConn = new SqlConnection(_businessConnection))
        {
            await bizConn.OpenAsync();

            // Re-build param names for this connection
            var bizParamNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                bizParamNames.Add($"@id{i}");

            var bizSql = $@"DELETE FROM suppliers.tbl_lnkSupplier_User 
                            WHERE customer_ID IN ({string.Join(",", bizParamNames)})";

            await using var bizCmd = new SqlCommand(bizSql, bizConn);
            for (int i = 0; i < ids.Count; i++)
                bizCmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await bizCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    /// <summary>
    /// Soft remove: sets customer_isOpen=0 (user hidden but not deleted).
    /// </summary>
    public async Task<bool> RemoveUserAsync(long userId, long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_bakeryuser 
                    SET customer_isOpen = 0 
                    WHERE customer_ID = @userId AND customer_webshopID = @webshopId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Checks if an email already exists in tbl_bakeryuser (global, not per-webshop).
    /// Same as BakeryUserService.
    /// </summary>
    public async Task<bool> IsDuplicateEmailAsync(string email)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT COUNT(1) FROM tbl_bakeryuser WHERE customer_EmailID = @email";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email.Trim());

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
