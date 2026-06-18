using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class BakeryUserItem
{
    public long CustomerId { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
    public int UserType { get; set; }
    public int StaffType { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
    public long WebshopId { get; set; }
    public DateTime? ExpiredOn { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? CcCode { get; set; }
    public int? IsTemporary { get; set; }

    // Salary fields (from LEFT JOIN tbl_bakeryusersal)
    public decimal? SalaryPerHour { get; set; }
    public decimal? SalaryContributionPer { get; set; }
    public decimal? IncentivePer { get; set; }
    public string? TandaId { get; set; }
    public string? TandaPin { get; set; }
}

public class BakeryUserListResult
{
    public List<BakeryUserItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class AddBakeryUserModel
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
    public int UserType { get; set; }
}

public class UpdateBakeryUserModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
    public int UserType { get; set; }
    public int StaffType { get; set; }
    public int? IsTemporary { get; set; }
}

public class BulkSaveBakeryUserModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
    public int UserType { get; set; }
    public int StaffType { get; set; }
    public int? IsTemporary { get; set; }
    // Salary fields (only for staff type=3)
    public decimal? SalaryPerHour { get; set; }
    public decimal? SalaryContributionPer { get; set; }
    public decimal? IncentivePer { get; set; }
    public string? TandaId { get; set; }
    public string? TandaPin { get; set; }
}

public class SalaryInfoModel
{
    public long UserId { get; set; }
    public decimal SalaryPerHour { get; set; }
    public decimal SalaryContributionPer { get; set; }
    public decimal IncentivePer { get; set; }
    public string? TandaId { get; set; }
    public string? TandaPin { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Bakery User management module.
/// Migrated from bakeryusers.aspx.
/// Uses DefaultConnection with tbl_bakeryuser and tbl_bakeryusersal tables.
/// Module 8 permission check.
/// </summary>
public class BakeryUserService
{
    private readonly string _defaultConnection;

    public BakeryUserService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated bakery users with optional filters.
    /// LEFT JOINs tbl_bakeryusersal for salary data (staff only).
    /// </summary>
    public async Task<BakeryUserListResult> GetBakeryUsersAsync(
        long webshopId, int userType, int? staffType,
        int? statusFilter, string? search, int page, int pageSize = 23)
    {
        var result = new BakeryUserListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build WHERE clause
        var whereClause = "WHERE customer_webshopID = @webshopId AND customer_type = @userType";

        if (staffType.HasValue)
            whereClause += " AND customer_stafftype = @staffType";

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
            countCmd.Parameters.AddWithValue("@userType", userType);
            if (staffType.HasValue)
                countCmd.Parameters.AddWithValue("@staffType", staffType.Value);
            if (!string.IsNullOrEmpty(search))
                countCmd.Parameters.AddWithValue("@search", "%" + search + "%");

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            result.TotalCount = totalCount;
            result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        }

        // Get paginated items with LEFT JOIN for salary data
        var sql = $@"SELECT u.customer_ID, u.customer_EmailID, u.customer_Name, u.customer_password,
                            u.customer_phone, u.customer_type, u.customer_stafftype, u.customer_isActive,
                            u.customer_isOpen, u.customer_webshopID, u.customer_ExpiredOn, u.customer_createdOn,
                            u.customer_ccCode, u.customer_istemporary,
                            s.userSal_salPerhour, s.userSal_salcontributionPer, s.userSal_incentivePer,
                            s.userSal_tandaID, s.userSal_tandapin
                     FROM tbl_bakeryuser u
                     LEFT JOIN tbl_bakeryusersal s ON s.userSal_userID = u.customer_ID
                     {whereClause}
                     ORDER BY u.customer_createdOn DESC
                     OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        cmd.Parameters.AddWithValue("@userType", userType);
        if (staffType.HasValue)
            cmd.Parameters.AddWithValue("@staffType", staffType.Value);
        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("@search", "%" + search + "%");
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Items.Add(new BakeryUserItem
            {
                CustomerId = reader.GetInt64(0),
                Email = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Password = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
                UserType = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                StaffType = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                IsActive = !reader.IsDBNull(7) && reader.GetBoolean(7),
                IsOpen = !reader.IsDBNull(8) && reader.GetBoolean(8),
                WebshopId = reader.IsDBNull(9) ? 0 : reader.GetInt64(9),
                ExpiredOn = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                CreatedOn = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                CcCode = reader.IsDBNull(12) ? null : reader.GetString(12),
                IsTemporary = reader.IsDBNull(13) ? null : (int?)Convert.ToInt32(reader.GetValue(13)),
                SalaryPerHour = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                SalaryContributionPer = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                IncentivePer = reader.IsDBNull(16) ? null : reader.GetDecimal(16),
                TandaId = reader.IsDBNull(17) ? null : reader.GetString(17),
                TandaPin = reader.IsDBNull(18) ? null : reader.GetString(18)
            });
        }

        return result;
    }

    /// <summary>
    /// Adds a new bakery user. Returns false if email already exists (global check).
    /// Defaults: isActive=1, isOpen=1, stafftype=0, ExpiredOn=NOW+1yr, createdOn=NOW, ccCode=GUID.
    /// </summary>
    public async Task<bool> AddBakeryUserAsync(AddBakeryUserModel model, long webshopId)
    {
        // Duplicate email check (global)
        if (await IsDuplicateEmailAsync(model.Email))
            return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"INSERT INTO tbl_bakeryuser 
                    (customer_EmailID, customer_Name, customer_password, customer_phone,
                     customer_type, customer_stafftype, customer_isActive, customer_isOpen,
                     customer_webshopID, customer_ExpiredOn, customer_createdOn, customer_ccCode)
                    VALUES (@email, @name, @password, @phone,
                            @userType, 0, 1, 1,
                            @webshopId, @expiredOn, @createdOn, @ccCode)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", model.Email.Trim());
        cmd.Parameters.AddWithValue("@name", model.Name.Trim());
        cmd.Parameters.AddWithValue("@password", model.Password);
        cmd.Parameters.AddWithValue("@phone", (object?)model.Phone?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userType", model.UserType);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        cmd.Parameters.AddWithValue("@expiredOn", DateTime.Now.AddYears(1));
        cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);
        cmd.Parameters.AddWithValue("@ccCode", Guid.NewGuid().ToString());

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Updates an existing bakery user's name, password, phone, type, staffType, isTemporary.
    /// Scoped to webshopId for isolation.
    /// </summary>
    public async Task<bool> UpdateBakeryUserAsync(UpdateBakeryUserModel model, long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_bakeryuser 
                    SET customer_Name = @name, customer_password = @password,
                        customer_phone = @phone, customer_type = @userType,
                        customer_stafftype = @staffType, customer_istemporary = @isTemporary
                    WHERE customer_ID = @id AND customer_webshopID = @webshopId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", model.Name.Trim());
        cmd.Parameters.AddWithValue("@password", model.Password);
        cmd.Parameters.AddWithValue("@phone", (object?)model.Phone?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userType", model.UserType);
        cmd.Parameters.AddWithValue("@staffType", model.StaffType);
        cmd.Parameters.AddWithValue("@isTemporary", (object?)model.IsTemporary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", model.Id);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>
    /// Bulk save: loops through items, updates each user record, and upserts salary for staff (type=3).
    /// </summary>
    public async Task<bool> BulkSaveAsync(List<BulkSaveBakeryUserModel> items, long webshopId, int modifiedByUserId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        foreach (var item in items)
        {
            // Update user record
            var updateSql = @"UPDATE tbl_bakeryuser 
                              SET customer_Name = @name, customer_password = @pwd,
                                  customer_phone = @phone, customer_type = @type,
                                  customer_stafftype = @staffType, customer_istemporary = @isTemp
                              WHERE customer_ID = @id AND customer_webshopID = @webshopId";

            await using (var cmd = new SqlCommand(updateSql, conn))
            {
                cmd.Parameters.AddWithValue("@name", item.Name.Trim());
                cmd.Parameters.AddWithValue("@pwd", item.Password);
                cmd.Parameters.AddWithValue("@phone", (object?)item.Phone?.Trim() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", item.UserType);
                cmd.Parameters.AddWithValue("@staffType", item.StaffType);
                cmd.Parameters.AddWithValue("@isTemp", (object?)item.IsTemporary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@webshopId", webshopId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Upsert salary if staff type (type=3) and salary data provided
            if (item.UserType == 3 && item.SalaryPerHour.HasValue)
            {
                await SaveSalaryInfoAsync(new SalaryInfoModel
                {
                    UserId = item.Id,
                    SalaryPerHour = item.SalaryPerHour ?? 0,
                    SalaryContributionPer = item.SalaryContributionPer ?? 0,
                    IncentivePer = item.IncentivePer ?? 0,
                    TandaId = item.TandaId,
                    TandaPin = item.TandaPin
                }, modifiedByUserId);
            }
        }

        return true;
    }

    /// <summary>
    /// Upserts salary info: checks if record exists for userId, then UPDATE or INSERT.
    /// </summary>
    public async Task<bool> SaveSalaryInfoAsync(SalaryInfoModel model, int modifiedByUserId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Check if salary record exists
        var checkSql = "SELECT COUNT(1) FROM tbl_bakeryusersal WHERE userSal_userID = @userId";
        bool exists;
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@userId", model.UserId);
            exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
        }

        if (exists)
        {
            var updateSql = @"UPDATE tbl_bakeryusersal 
                              SET userSal_salPerhour = @salPerHour,
                                  userSal_salcontributionPer = @salContribution,
                                  userSal_incentivePer = @incentive,
                                  userSal_tandaID = @tandaId,
                                  userSal_tandapin = @tandaPin,
                                  userSal_modifiedOn = @modifiedOn,
                                  userSal_modifiedBy = @modifiedBy
                              WHERE userSal_userID = @userId";

            await using var cmd = new SqlCommand(updateSql, conn);
            cmd.Parameters.AddWithValue("@salPerHour", model.SalaryPerHour);
            cmd.Parameters.AddWithValue("@salContribution", model.SalaryContributionPer);
            cmd.Parameters.AddWithValue("@incentive", model.IncentivePer);
            cmd.Parameters.AddWithValue("@tandaId", (object?)model.TandaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tandaPin", (object?)model.TandaPin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedBy", modifiedByUserId);
            cmd.Parameters.AddWithValue("@userId", model.UserId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertSql = @"INSERT INTO tbl_bakeryusersal 
                              (userSal_userID, userSal_salPerhour, userSal_salcontributionPer,
                               userSal_incentivePer, userSal_tandaID, userSal_tandapin,
                               userSal_modifiedOn, userSal_modifiedBy)
                              VALUES (@userId, @salPerHour, @salContribution,
                                      @incentive, @tandaId, @tandaPin,
                                      @modifiedOn, @modifiedBy)";

            await using var cmd = new SqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@userId", model.UserId);
            cmd.Parameters.AddWithValue("@salPerHour", model.SalaryPerHour);
            cmd.Parameters.AddWithValue("@salContribution", model.SalaryContributionPer);
            cmd.Parameters.AddWithValue("@incentive", model.IncentivePer);
            cmd.Parameters.AddWithValue("@tandaId", (object?)model.TandaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tandaPin", (object?)model.TandaPin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
            cmd.Parameters.AddWithValue("@modifiedBy", modifiedByUserId);
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

        // Build parameterized IN clause
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
    /// Bulk delete bakery users. Hard DELETE with admin protection (customer_type <> 1).
    /// Scoped to webshopId.
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids, long webshopId)
    {
        if (ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build parameterized IN clause
        var paramNames = new List<string>();
        for (int i = 0; i < ids.Count; i++)
            paramNames.Add($"@id{i}");

        var sql = $@"DELETE FROM tbl_bakeryuser 
                     WHERE customer_type <> 1 
                       AND customer_webshopID = @webshopId 
                       AND customer_ID IN ({string.Join(",", paramNames)})";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        for (int i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

        await cmd.ExecuteNonQueryAsync();
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

    /// <summary>
    /// Checks if a user has access to a specific module via tbl_moduleAssignment.
    /// Same pattern as ManageSupplierController.
    /// </summary>
    public async Task<bool> CheckModuleAccessAsync(int userId, int moduleId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT COUNT(1) FROM tbl_moduleAssignment 
                    WHERE moduleAssignment_userID = @userId 
                      AND moduleAssignment_moduleID = @moduleId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@moduleId", moduleId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
