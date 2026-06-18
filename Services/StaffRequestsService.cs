using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── View Models ───────────────────────────────────────────────────────────────

public class StaffRequestsViewModel
{
    public List<StaffRequestItem> Requests { get; set; } = new();
    public int FilterStatus { get; set; } // -1=All, 0=Pending, 1=Approved, 2=Declined
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? Message { get; set; }
    public string? MessageClass { get; set; } // "alert-success" or "alert-danger"
}

public class StaffRequestItem
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public string StaffName { get; set; } = "";
    public long StaffId { get; set; }
    public int IsClosed { get; set; } // 0=Available, 1=Not Available, 2=Can Available, 3=Leave
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public string Remarks { get; set; } = "";
    public int ApprovedStatus { get; set; } // 0=Pending, 1=Approved, 2=Declined
    public DateTime? ApprovedOn { get; set; }
    public string ApprovedBy { get; set; } = "";
    public string ApprovedRemarks { get; set; } = "";
    public long RequestedBy { get; set; }
    public string RequestedByName { get; set; } = "";
    public DateTime ModifiedOn { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for the Manage Staff Requests page.
/// Migrated from managestafftimingrequest.aspx.cs.
/// </summary>
public class StaffRequestsService
{
    private readonly string _businessConnection;

    public StaffRequestsService(IConfiguration config)
    {
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated staff requests with optional status filter and date range.
    /// Source: clsStaffRota.GetStaffSpecialDays() called from managestafftimingrequest.aspx.cs
    /// </summary>
    public async Task<StaffRequestsViewModel> GetRequestsAsync(
        int filterStatus, DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        var model = new StaffRequestsViewModel
        {
            FilterStatus = filterStatus,
            StartDate = startDate.ToString("dd/MM/yyyy"),
            EndDate = endDate.ToString("dd/MM/yyyy"),
            CurrentPage = page
        };

        int offset = (page - 1) * pageSize;

        // Count query
        var countSql = @"
            SELECT COUNT(*)
            FROM tbl_staffSpecialDays sd
            WHERE sd.staffSpecialDay_Date >= @startDate AND sd.staffSpecialDay_Date <= @endDate
              AND (@filterStatus = -1 OR sd.staffSpecialDay_approvedsts = @filterStatus)
              AND sd.staffSpecialDay_requestedBy = sd.staffSpecialDay_staffid";

        // Data query with cross-database join
        var dataSql = @"
            SELECT sd.staffSpecialDay_ID, sd.staffSpecialDay_Date, sd.staffSpecialDay_isclosed,
                   sd.staffSpecialDay_from, sd.staffSpecialDay_to, sd.staffSpecialDay_approvedsts,
                   sd.staffSpecialDay_approvedOn, sd.staffSpecialDay_approvedRemarks,
                   sd.staffSpecialDay_requestedBy, sd.staffSpecialDay_staffid, sd.staffSpecialDay_remarks,
                   sd.staffSpecialDay_modifiedOn,
                   staff.customer_Name AS staff_Name,
                   approver.customer_Name AS ApprovedBy,
                   requester.customer_Name AS Requested_Name
            FROM tbl_staffSpecialDays sd
            LEFT JOIN [db_cakerstreet_live].dbo.tbl_bakeryuser staff ON sd.staffSpecialDay_staffid = staff.customer_ID
            LEFT JOIN [db_cakerstreet_live].dbo.tbl_bakeryuser approver ON sd.staffSpecialDay_approvedBy = approver.customer_ID
            LEFT JOIN [db_cakerstreet_live].dbo.tbl_bakeryuser requester ON sd.staffSpecialDay_requestedBy = requester.customer_ID
            WHERE sd.staffSpecialDay_Date >= @startDate AND sd.staffSpecialDay_Date <= @endDate
              AND (@filterStatus = -1 OR sd.staffSpecialDay_approvedsts = @filterStatus)
              AND sd.staffSpecialDay_requestedBy = sd.staffSpecialDay_staffid
            ORDER BY sd.staffSpecialDay_Date DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Get total count
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("@startDate", startDate.Date);
            countCmd.Parameters.AddWithValue("@endDate", endDate.Date);
            countCmd.Parameters.AddWithValue("@filterStatus", filterStatus);
            var count = await countCmd.ExecuteScalarAsync();
            model.TotalCount = Convert.ToInt32(count);
        }

        model.TotalPages = model.TotalCount > 0
            ? (int)Math.Ceiling((double)model.TotalCount / pageSize)
            : 1;

        // Get data
        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            dataCmd.Parameters.AddWithValue("@startDate", startDate.Date);
            dataCmd.Parameters.AddWithValue("@endDate", endDate.Date);
            dataCmd.Parameters.AddWithValue("@filterStatus", filterStatus);
            dataCmd.Parameters.AddWithValue("@offset", offset);
            dataCmd.Parameters.AddWithValue("@pageSize", pageSize);

            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                model.Requests.Add(new StaffRequestItem
                {
                    Id = reader.GetInt64(reader.GetOrdinal("staffSpecialDay_ID")),
                    Date = reader.GetDateTime(reader.GetOrdinal("staffSpecialDay_Date")),
                    IsClosed = reader.GetInt32(reader.GetOrdinal("staffSpecialDay_isclosed")),
                    FromHour = reader.GetInt32(reader.GetOrdinal("staffSpecialDay_from")),
                    ToHour = reader.GetInt32(reader.GetOrdinal("staffSpecialDay_to")),
                    ApprovedStatus = reader.GetInt32(reader.GetOrdinal("staffSpecialDay_approvedsts")),
                    ApprovedOn = reader.IsDBNull(reader.GetOrdinal("staffSpecialDay_approvedOn"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("staffSpecialDay_approvedOn")),
                    ApprovedRemarks = reader.IsDBNull(reader.GetOrdinal("staffSpecialDay_approvedRemarks"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("staffSpecialDay_approvedRemarks")),
                    RequestedBy = reader.GetInt64(reader.GetOrdinal("staffSpecialDay_requestedBy")),
                    StaffId = reader.GetInt64(reader.GetOrdinal("staffSpecialDay_staffid")),
                    Remarks = reader.IsDBNull(reader.GetOrdinal("staffSpecialDay_remarks"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("staffSpecialDay_remarks")),
                    ModifiedOn = reader.IsDBNull(reader.GetOrdinal("staffSpecialDay_modifiedOn"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("staffSpecialDay_modifiedOn")),
                    StaffName = reader.IsDBNull(reader.GetOrdinal("staff_Name"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("staff_Name")),
                    ApprovedBy = reader.IsDBNull(reader.GetOrdinal("ApprovedBy"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("ApprovedBy")),
                    RequestedByName = reader.IsDBNull(reader.GetOrdinal("Requested_Name"))
                        ? ""
                        : reader.GetString(reader.GetOrdinal("Requested_Name"))
                });
            }
        }

        return model;
    }

    /// <summary>
    /// Approves a staff request. Sets staffSpecialDay_approvedsts=1.
    /// Source: managestafftimingrequest.aspx.cs lnkApprove_Click
    /// </summary>
    public async Task ApproveRequestAsync(long requestId, int userId)
    {
        var sql = @"UPDATE tbl_staffSpecialDays 
                    SET staffSpecialDay_approvedsts = 1, 
                        staffSpecialDay_approvedOn = GETDATE(), 
                        staffSpecialDay_approvedBy = @userId 
                    WHERE staffSpecialDay_ID = @id";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", requestId);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Declines a staff request. Sets staffSpecialDay_approvedsts=2 with remarks.
    /// Source: managestafftimingrequest.aspx.cs btnSave_Click
    /// </summary>
    public async Task DeclineRequestAsync(long requestId, int userId, string remarks)
    {
        var sql = @"UPDATE tbl_staffSpecialDays 
                    SET staffSpecialDay_approvedsts = 2, 
                        staffSpecialDay_approvedOn = GETDATE(), 
                        staffSpecialDay_approvedBy = @userId,
                        staffSpecialDay_approvedRemarks = @remarks 
                    WHERE staffSpecialDay_ID = @id";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", requestId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
        await cmd.ExecuteNonQueryAsync();
    }
}
