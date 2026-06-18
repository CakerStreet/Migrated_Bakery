using Microsoft.Data.SqlClient;
using CakerStreet.Business.Helpers;

namespace CakerStreet.Business.Services;

// ─── View Models ───────────────────────────────────────────────────────────────

public class StaffDashboardViewModel
{
    public long SelectedStaffId { get; set; }
    public string SelectedStaffName { get; set; } = "";
    public bool IsAdminView { get; set; }
    public List<StaffDropdownItem> StaffList { get; set; } = new();
    public int CurrentMonth { get; set; }
    public int CurrentYear { get; set; }
    public List<CalendarDayCell> CalendarDays { get; set; } = new();
    public List<WorkingHourRow> WorkingHours { get; set; } = new();
    public List<SpecialDayViewModel> SpecialDays { get; set; } = new();
    public int StaffRequestCount { get; set; }
}

public class SpecialDayViewModel
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public DateTime Date { get; set; }
    public int IsClosed { get; set; }          // 2=Can Available, 3=Leave Request
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public string Remarks { get; set; } = "";
    public int ApprovedStatus { get; set; }    // 0=Pending, 1=Approved, 2=Declined
    public DateTime ApprovedOn { get; set; }
    public long ApprovedBy { get; set; }
    public long RequestedBy { get; set; }
    public string ApprovedName { get; set; } = "";
    public string RequestedName { get; set; } = "";
    public string ApprovedRemarks { get; set; } = "";

    public string AvailabilityText => IsClosed switch
    {
        2 => "Can Available, (if Needed)",
        3 => "Leave Request",
        _ => "Unknown"
    };

    public string StatusText => ApprovedStatus switch
    {
        0 => "Pending",
        1 => "Approved",
        2 => "Declined",
        _ => "Pending"
    };

    public string StatusCssClass => ApprovedStatus == 1 ? "app" : "dec";

    /// <summary>Whether the admin placed this request on behalf of staff (requestedBy != staffId)</summary>
    public bool IsAdminRequested => RequestedBy != StaffId;
}

public class StaffDropdownItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class CalendarDayCell
{
    public DateTime Date { get; set; }
    public int DayOfMonth { get; set; }
    public string CssClass { get; set; } = "";
    public bool IsCurrentMonth { get; set; }
}

public class WorkingHourRow
{
    public string DayName { get; set; } = "";
    public int DayId { get; set; }
    public int AvailabilityStatus { get; set; }
    public int FromHour { get; set; }
    public int ToHour { get; set; }
}

// ─── Mutation Request/Response Models ───────────────────────────────────────────

public class SaveWorkingHoursRequest
{
    public long StaffId { get; set; }
    public List<TimingEntry> Entries { get; set; } = new();
}

public class TimingEntry
{
    public int DayId { get; set; }              // 1-7 (Mon-Sun)
    public int AvailabilityStatus { get; set; } // 0, 1, or 2
    public int FromHour { get; set; }           // -1 (Flexible) or 0-23
    public int ToHour { get; set; }             // -1 (Flexible) or 0-23
}

public class MutationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SubmitLeaveRequestPayload
{
    public long StaffId { get; set; }
    public string FromDate { get; set; } = "";  // dd/MM/yyyy
    public string ToDate { get; set; } = "";    // dd/MM/yyyy
    public string Remarks { get; set; } = "";   // max 500 chars
}

public class SubmitSpecialAvailabilityPayload
{
    public long StaffId { get; set; }
    public string Date { get; set; } = "";      // dd/MM/yyyy
    public int FromHour { get; set; }           // 0-23
    public int ToHour { get; set; }             // 0-23
}

public class DeleteSpecialDayPayload
{
    public long Id { get; set; }
    public long StaffId { get; set; }
}

public class ApproveSpecialDayPayload
{
    public long Id { get; set; }
    public long StaffId { get; set; }
}

public class DeclineSpecialDayPayload
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public string Remarks { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for the Staff Dashboard page (Phase 1 — read-only).
/// Migrated from staffDashboard.aspx.cs.
/// </summary>
public class StaffDashboardService
{
    private readonly string _defaultConnection;
    private readonly string _businessConnection;

    public StaffDashboardService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Builds the staff dashboard view model for a given staff member and month.
    /// </summary>
    public async Task<StaffDashboardViewModel> GetDashboardAsync(long webshopId, long staffId, int month, int year)
    {
        var model = new StaffDashboardViewModel
        {
            SelectedStaffId = staffId,
            CurrentMonth = month,
            CurrentYear = year
        };

        // Get staff list for dropdown
        model.StaffList = await GetStaffListAsync(webshopId);

        // Set selected staff name
        var selectedStaff = model.StaffList.FirstOrDefault(s => s.Id == staffId);
        model.SelectedStaffName = selectedStaff?.Name ?? "";

        if (staffId <= 0) return model;

        // Get working hours
        model.WorkingHours = await GetWorkingHoursAsync(staffId);

        // Get calendar days
        model.CalendarDays = await BuildCalendarAsync(staffId, month, year, model.WorkingHours);

        // Get special days list for display
        model.SpecialDays = await GetSpecialDaysListAsync(staffId, month, year);

        // Get staff request count (admin badge)
        model.StaffRequestCount = await GetStaffRequestCountAsync();

        return model;
    }

    // ─── Mutation Methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Saves working hours for a staff member (7 days, UPSERT pattern).
    /// Matches legacy webservices.aspx/AddUpdate_staffTimings.
    /// </summary>
    public async Task<MutationResult> SaveWorkingHoursAsync(long staffId, List<TimingEntry> entries)
    {
        // Validation: exactly 7 entries
        if (entries == null || entries.Count != 7)
        {
            return new MutationResult { Success = false, ErrorMessage = "Exactly 7 day entries are required." };
        }

        // Validation: unique dayId values 1–7
        var dayIds = entries.Select(e => e.DayId).ToList();
        if (dayIds.Distinct().Count() != 7 || dayIds.Any(d => d < 1 || d > 7))
        {
            return new MutationResult { Success = false, ErrorMessage = "Entries must contain unique dayId values 1 through 7." };
        }

        // Validation: availabilityStatus 0–2, fromHour/toHour -1 to 23, fromHour < toHour when both are not -1
        foreach (var entry in entries)
        {
            if (entry.AvailabilityStatus < 0 || entry.AvailabilityStatus > 2)
            {
                return new MutationResult { Success = false, ErrorMessage = $"Invalid availability status for day {entry.DayId}." };
            }

            if (entry.FromHour < -1 || entry.FromHour > 23)
            {
                return new MutationResult { Success = false, ErrorMessage = $"Invalid from hour for day {entry.DayId}." };
            }

            if (entry.ToHour < -1 || entry.ToHour > 23)
            {
                return new MutationResult { Success = false, ErrorMessage = $"Invalid to hour for day {entry.DayId}." };
            }

            if (entry.FromHour != -1 && entry.ToHour != -1 && entry.FromHour >= entry.ToHour && entry.AvailabilityStatus != 1)
            {
                return new MutationResult { Success = false, ErrorMessage = "From time must be earlier than To time." };
            }
        }

        // Execute UPSERT within a transaction
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            foreach (var entry in entries)
            {
                // Check if row exists
                var checkSql = "SELECT COUNT(1) FROM tbl_staffTiming WHERE staffTiming_staffid = @staffId AND staffTiming_dayid = @dayId";
                await using var checkCmd = new SqlCommand(checkSql, conn, transaction);
                checkCmd.Parameters.AddWithValue("@staffId", staffId);
                checkCmd.Parameters.AddWithValue("@dayId", entry.DayId);
                var exists = (int)(await checkCmd.ExecuteScalarAsync())! > 0;

                if (exists)
                {
                    var updateSql = @"UPDATE tbl_staffTiming 
                        SET staffTiming_isclosed = @availability, staffTiming_from = @fromHour, 
                            staffTiming_to = @toHour, staffTiming_modifiedOn = GETDATE()
                        WHERE staffTiming_staffid = @staffId AND staffTiming_dayid = @dayId";
                    await using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                    updateCmd.Parameters.AddWithValue("@staffId", staffId);
                    updateCmd.Parameters.AddWithValue("@dayId", entry.DayId);
                    updateCmd.Parameters.AddWithValue("@availability", entry.AvailabilityStatus);
                    updateCmd.Parameters.AddWithValue("@fromHour", entry.FromHour);
                    updateCmd.Parameters.AddWithValue("@toHour", entry.ToHour);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertSql = @"INSERT INTO tbl_staffTiming (staffTiming_staffid, staffTiming_dayid, staffTiming_isclosed, 
                        staffTiming_from, staffTiming_to, staffTiming_isTemporary, staffTiming_modifiedOn)
                        VALUES (@staffId, @dayId, @availability, @fromHour, @toHour, 0, GETDATE())";
                    await using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                    insertCmd.Parameters.AddWithValue("@staffId", staffId);
                    insertCmd.Parameters.AddWithValue("@dayId", entry.DayId);
                    insertCmd.Parameters.AddWithValue("@availability", entry.AvailabilityStatus);
                    insertCmd.Parameters.AddWithValue("@fromHour", entry.FromHour);
                    insertCmd.Parameters.AddWithValue("@toHour", entry.ToHour);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return new MutationResult { Success = true };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return new MutationResult { Success = false, ErrorMessage = "Save failed. Please try again." };
        }
    }

    /// <summary>
    /// Submits a leave request for a staff member (one row per day in range).
    /// Matches legacy staffDashboard.aspx.cs btnSaveNewRequest_Click lines 360-385.
    /// </summary>
    public async Task<MutationResult> SubmitLeaveRequestAsync(long staffId, DateTime fromDate, DateTime toDate, string remarks)
    {
        // Validation: fromDate >= today (server date, time ignored)
        var today = DateTime.Today;
        if (fromDate.Date < today)
        {
            return new MutationResult { Success = false, ErrorMessage = "Past date(s) are not accepted" };
        }

        // Validation: toDate >= fromDate
        if (toDate.Date < fromDate.Date)
        {
            return new MutationResult { Success = false, ErrorMessage = "from date should be less than or equal to to date" };
        }

        // Validation: range <= 30 days
        int totalDays = (toDate.Date - fromDate.Date).Days + 1;
        if (totalDays > 30)
        {
            return new MutationResult { Success = false, ErrorMessage = "Maximum 30 days per request" };
        }

        // Validation: remarks <= 500 chars
        if (!string.IsNullOrEmpty(remarks) && remarks.Length > 500)
        {
            return new MutationResult { Success = false, ErrorMessage = "Remarks must not exceed 500 characters" };
        }

        // Execute INSERT within a transaction (one row per day in range)
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            var insertSql = @"INSERT INTO tbl_staffSpecialDays (staffSpecialDay_staffid, staffSpecialDay_Date, staffSpecialDay_isclosed,
                staffSpecialDay_from, staffSpecialDay_to, staffSpecialDay_approvedsts, staffSpecialDay_remarks,
                staffSpecialDay_modifiedOn, staffSpecialDay_approvedOn, staffSpecialDay_approvedBy,
                staffSpecialDay_approvedRemarks, staffSpecialDay_requestedBy)
                VALUES (@staffId, @date, 3, 0, 0, 0, @remarks, GETDATE(), GETDATE(), 0, '', @staffId)";

            for (int i = 0; i < totalDays; i++)
            {
                var currentDate = fromDate.Date.AddDays(i);
                await using var cmd = new SqlCommand(insertSql, conn, transaction);
                cmd.Parameters.AddWithValue("@staffId", staffId);
                cmd.Parameters.AddWithValue("@date", currentDate);
                cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return new MutationResult { Success = true };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return new MutationResult { Success = false, ErrorMessage = "Save failed. Please try again." };
        }
    }

    /// <summary>
    /// Submits a special availability entry for a staff member (single row).
    /// Matches legacy staffDashboard.aspx.cs btnSaveNewRequest_Click with hfRequestType="2".
    /// </summary>
    public async Task<MutationResult> SubmitSpecialAvailabilityAsync(long staffId, DateTime date, int fromHour, int toHour)
    {
        // Validation: date >= today (server date, time ignored)
        var today = DateTime.Today;
        if (date.Date < today)
        {
            return new MutationResult { Success = false, ErrorMessage = "Past date(s) are not accepted" };
        }

        // Validation: fromHour and toHour in 0–23
        if (fromHour < 0 || fromHour > 23)
        {
            return new MutationResult { Success = false, ErrorMessage = "From hour must be between 0 and 23" };
        }

        if (toHour < 0 || toHour > 23)
        {
            return new MutationResult { Success = false, ErrorMessage = "To hour must be between 0 and 23" };
        }

        // Validation: fromHour < toHour
        if (fromHour >= toHour)
        {
            return new MutationResult { Success = false, ErrorMessage = "From time must be earlier than To time" };
        }

        // Execute INSERT within a transaction (single row)
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            var insertSql = @"INSERT INTO tbl_staffSpecialDays (staffSpecialDay_staffid, staffSpecialDay_Date, staffSpecialDay_isclosed,
                staffSpecialDay_from, staffSpecialDay_to, staffSpecialDay_approvedsts, staffSpecialDay_remarks,
                staffSpecialDay_modifiedOn, staffSpecialDay_approvedOn, staffSpecialDay_approvedBy,
                staffSpecialDay_approvedRemarks, staffSpecialDay_requestedBy)
                VALUES (@staffId, @date, 2, @fromHour, @toHour, 0, '', GETDATE(), GETDATE(), 0, '', @staffId)";

            await using var cmd = new SqlCommand(insertSql, conn, transaction);
            cmd.Parameters.AddWithValue("@staffId", staffId);
            cmd.Parameters.AddWithValue("@date", date.Date);
            cmd.Parameters.AddWithValue("@fromHour", fromHour);
            cmd.Parameters.AddWithValue("@toHour", toHour);
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return new MutationResult { Success = true };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return new MutationResult { Success = false, ErrorMessage = "Save failed. Please try again." };
        }
    }

    // ─── Special Days CRUD Methods ──────────────────────────────────────────────

    /// <summary>
    /// Deletes a special day entry. Only allowed if approvedsts == 0 (pending).
    /// Source: legacy rpSpecialDays_ItemCommand with "DeleteSP".
    /// </summary>
    public async Task<MutationResult> DeleteSpecialDayAsync(long id, long staffId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        var sql = "DELETE FROM tbl_staffSpecialDays WHERE staffSpecialDay_ID = @id AND staffSpecialDay_staffid = @staffId AND staffSpecialDay_approvedsts = 0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? new MutationResult { Success = true }
            : new MutationResult { Success = false, ErrorMessage = "Record not found or already processed." };
    }

    /// <summary>
    /// Approves a special day entry (sets approvedsts=1).
    /// Source: legacy lnkApprove_Click.
    /// </summary>
    public async Task<MutationResult> ApproveSpecialDayAsync(long id, long approvedByStaffId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        var sql = @"UPDATE tbl_staffSpecialDays 
            SET staffSpecialDay_approvedsts = 1, staffSpecialDay_approvedOn = GETDATE(), staffSpecialDay_approvedBy = @approvedBy
            WHERE staffSpecialDay_ID = @id AND staffSpecialDay_approvedsts = 0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@approvedBy", approvedByStaffId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? new MutationResult { Success = true }
            : new MutationResult { Success = false, ErrorMessage = "Record not found or already processed." };
    }

    /// <summary>
    /// Declines a special day entry (sets approvedsts=2 with remarks).
    /// Source: legacy btnSave_Click (decline modal save).
    /// </summary>
    public async Task<MutationResult> DeclineSpecialDayAsync(long id, long approvedByUserId, string remarks)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        var sql = @"UPDATE tbl_staffSpecialDays 
            SET staffSpecialDay_approvedsts = 2, staffSpecialDay_approvedOn = GETDATE(), 
                staffSpecialDay_approvedBy = @approvedBy, staffSpecialDay_approvedRemarks = @remarks
            WHERE staffSpecialDay_ID = @id AND staffSpecialDay_approvedsts = 0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@approvedBy", approvedByUserId);
        cmd.Parameters.AddWithValue("@remarks", remarks?.Replace("'", "") ?? "");
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? new MutationResult { Success = true }
            : new MutationResult { Success = false, ErrorMessage = "Record not found or already processed." };
    }

    /// <summary>
    /// Gets the count of pending staff requests (for badge display).
    /// Source: legacy Page_Load countreq logic.
    /// </summary>
    public async Task<int> GetStaffRequestCountAsync()
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        var sql = @"SELECT COUNT(1) FROM tbl_staffSpecialDays 
            WHERE staffSpecialDay_Date >= CAST(GETDATE() AS DATE) 
              AND staffSpecialDay_approvedsts = 0 
              AND staffSpecialDay_requestedBy = staffSpecialDay_staffid";
        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<StaffDropdownItem>> GetStaffListAsync(long webshopId)
    {
        var list = new List<StaffDropdownItem>();
        var sql = @"SELECT customer_ID, customer_Name 
            FROM tbl_bakeryuser 
            WHERE customer_webshopID = @wid 
              AND customer_type = 3 
              AND customer_stafftype = 1 
              AND customer_isOpen = 1 
              AND customer_isActive = 1 
              AND (customer_istemporary = 0 OR customer_istemporary = 2) 
            ORDER BY customer_istemporary, customer_Name";

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffDropdownItem
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    private async Task<List<WorkingHourRow>> GetWorkingHoursAsync(long staffId)
    {
        var list = new List<WorkingHourRow>();
        var sql = @"SELECT staffTiming_dayid, staffTiming_isclosed, staffTiming_from, staffTiming_to 
            FROM tbl_staffTiming 
            WHERE staffTiming_staffid = @staffId";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        await using var reader = await cmd.ExecuteReaderAsync();

        var timingsByDay = new Dictionary<int, WorkingHourRow>();
        while (await reader.ReadAsync())
        {
            int dayId = reader.GetInt32(0);
            timingsByDay[dayId] = new WorkingHourRow
            {
                DayId = dayId,
                DayName = AssignedTasksNavHelper.GetDayName(dayId),
                AvailabilityStatus = reader.GetInt32(1),
                FromHour = reader.GetInt32(2),
                ToHour = reader.GetInt32(3)
            };
        }

        // Build full Monday-Sunday list
        for (int d = 1; d <= 7; d++)
        {
            if (timingsByDay.TryGetValue(d, out var row))
            {
                list.Add(row);
            }
            else
            {
                list.Add(new WorkingHourRow
                {
                    DayId = d,
                    DayName = AssignedTasksNavHelper.GetDayName(d),
                    AvailabilityStatus = 0,
                    FromHour = -1,
                    ToHour = -1
                });
            }
        }

        return list;
    }

    private async Task<List<CalendarDayCell>> BuildCalendarAsync(long staffId, int month, int year, List<WorkingHourRow> workingHours)
    {
        // Get special days for this month
        var specialDays = await GetSpecialDaysAsync(staffId, month, year);

        var cells = new List<CalendarDayCell>();
        var firstOfMonth = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        // Find the Monday on or before the first of the month (calendar starts on Monday)
        int dayOfWeekOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7; // Monday=0
        var calendarStart = firstOfMonth.AddDays(-dayOfWeekOffset);

        // Build 6 weeks (42 cells) to cover all possible month layouts
        for (int i = 0; i < 42; i++)
        {
            var date = calendarStart.AddDays(i);
            bool isCurrentMonth = date.Month == month && date.Year == year;

            string cssClass = "";
            if (isCurrentMonth)
            {
                cssClass = GetCellCssClass(date, specialDays, workingHours);
            }

            cells.Add(new CalendarDayCell
            {
                Date = date,
                DayOfMonth = date.Day,
                CssClass = cssClass,
                IsCurrentMonth = isCurrentMonth
            });
        }

        return cells;
    }

    private async Task<List<SpecialDayRecord>> GetSpecialDaysAsync(long staffId, int month, int year)
    {
        var list = new List<SpecialDayRecord>();
        var sql = @"SELECT staffSpecialDay_Date, staffSpecialDay_isclosed, staffSpecialDay_approvedsts 
            FROM tbl_staffSpecialDays 
            WHERE staffSpecialDay_staffid = @staffId 
              AND MONTH(staffSpecialDay_Date) = @month 
              AND YEAR(staffSpecialDay_Date) = @year";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        cmd.Parameters.AddWithValue("@month", month);
        cmd.Parameters.AddWithValue("@year", year);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SpecialDayRecord
            {
                Date = reader.GetDateTime(0).Date,
                IsClosed = reader.GetInt32(1),
                ApprovedSts = reader.GetInt32(2)
            });
        }
        return list;
    }

    /// <summary>
    /// Gets the full special days list for display in the dashboard (with names resolved).
    /// Source: legacy GetstaffSpecialDays() method.
    /// </summary>
    private async Task<List<SpecialDayViewModel>> GetSpecialDaysListAsync(long staffId, int month, int year)
    {
        var list = new List<SpecialDayViewModel>();
        var sql = @"SELECT s.staffSpecialDay_ID, s.staffSpecialDay_staffid, s.staffSpecialDay_Date, 
                s.staffSpecialDay_isclosed, s.staffSpecialDay_from, s.staffSpecialDay_to,
                s.staffSpecialDay_remarks, s.staffSpecialDay_approvedsts, s.staffSpecialDay_approvedOn,
                s.staffSpecialDay_approvedBy, s.staffSpecialDay_requestedBy,
                s.staffSpecialDay_approvedRemarks,
                ISNULL(approver.customer_Name, '') AS approvedName,
                ISNULL(requester.customer_Name, '') AS requestedName
            FROM tbl_staffSpecialDays s
            LEFT JOIN [{0}].dbo.tbl_bakeryuser approver ON approver.customer_ID = s.staffSpecialDay_approvedBy
            LEFT JOIN [{0}].dbo.tbl_bakeryuser requester ON requester.customer_ID = s.staffSpecialDay_requestedBy
            WHERE s.staffSpecialDay_staffid = @staffId 
              AND MONTH(s.staffSpecialDay_Date) = @month 
              AND YEAR(s.staffSpecialDay_Date) = @year
            ORDER BY s.staffSpecialDay_Date DESC";

        // Extract database name from DefaultConnection for cross-db join
        var defaultDbName = ExtractDatabaseName(_defaultConnection);
        var formattedSql = string.Format(sql, defaultDbName);

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(formattedSql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        cmd.Parameters.AddWithValue("@month", month);
        cmd.Parameters.AddWithValue("@year", year);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SpecialDayViewModel
            {
                Id = reader.GetInt64(0),
                StaffId = reader.GetInt64(1),
                Date = reader.GetDateTime(2),
                IsClosed = reader.GetInt32(3),
                FromHour = reader.GetInt32(4),
                ToHour = reader.GetInt32(5),
                Remarks = reader.IsDBNull(6) ? "" : reader.GetString(6),
                ApprovedStatus = reader.GetInt32(7),
                ApprovedOn = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                ApprovedBy = reader.GetInt64(9),
                RequestedBy = reader.GetInt64(10),
                ApprovedRemarks = reader.IsDBNull(11) ? "" : reader.GetString(11),
                ApprovedName = reader.IsDBNull(12) ? "" : reader.GetString(12),
                RequestedName = reader.IsDBNull(13) ? "" : reader.GetString(13)
            });
        }
        return list;
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        // Parse "Initial Catalog=xxx" or "Database=xxx" from connection string
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("Initial Catalog=".Length).Trim();
            if (trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("Database=".Length).Trim();
        }
        return "db_handicraft";
    }

    /// <summary>
    /// Determines the CSS class for a calendar cell.
    /// Source: staffDashboard.aspx.cs clCollectionDate_OnDayRender logic.
    /// - If special day exists with approvedsts &lt; 2: colour by isclosed
    ///   (1=bgclose, 2=bgcanAvail, 3=bgLeave, 0=bgAvail)
    /// - If no special day but normal timing exists for that dayOfWeek: colour by isclosed
    /// - Otherwise: empty (undefined)
    /// </summary>
    private static string GetCellCssClass(DateTime date, List<SpecialDayRecord> specialDays, List<WorkingHourRow> workingHours)
    {
        // Check special day (approvedsts < 2 means pending or approved, not declined)
        var sd = specialDays.FirstOrDefault(s => s.Date == date.Date && s.ApprovedSts < 2);
        if (sd != null)
        {
            return sd.IsClosed switch
            {
                1 => "bgclose",
                2 => "bgcanAvail",
                3 => "bgLeave",
                0 => "bgAvail",
                _ => ""
            };
        }

        // Check normal timing for this day of week
        int dayId = AssignedTasksNavHelper.GetDayId(date);
        var timing = workingHours.FirstOrDefault(w => w.DayId == dayId);
        if (timing != null)
        {
            return timing.AvailabilityStatus switch
            {
                1 => "bgclose",
                2 => "bgcanAvail",
                0 => "bgAvail",
                _ => ""
            };
        }

        return "";
    }

    // ─── Internal record types ─────────────────────────────────────────────────

    private class SpecialDayRecord
    {
        public DateTime Date { get; set; }
        public int IsClosed { get; set; }
        public int ApprovedSts { get; set; }
    }
}
