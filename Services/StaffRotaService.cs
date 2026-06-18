using Microsoft.Data.SqlClient;
using CakerStreet.Business.Helpers;

namespace CakerStreet.Business.Services;

// ─── View Models ───────────────────────────────────────────────────────────────

public class StaffRotaViewModel
{
    public DateTime StartDate { get; set; }
    public bool IsEditMode { get; set; }
    public bool IsAuthUser { get; set; }
    public List<StaffRotaDayHeader> DayHeaders { get; set; } = new();
    public List<StaffRotaRow> StaffRows { get; set; } = new();
}

public class StaffRotaDayHeader
{
    public int DayID { get; set; }
    public string DayName { get; set; } = "";
    public DateTime Date { get; set; }
    public string CakeCount { get; set; } = "0"; // Format: "cakeCount/cupcakeCount" or just count
}

public class StaffRotaRow
{
    public long StaffId { get; set; }
    public string StaffName { get; set; } = "";
    public string StaffPhone { get; set; } = "";
    public bool IsTemporary { get; set; }
    public bool IsCurrentUser { get; set; }
    public List<StaffRotaDayCell> Days { get; set; } = new();
}

public class StaffRotaDayCell
{
    public int Status { get; set; } // 0=undefined, 1=not available, 2=can available, 3=leave, 4+=available
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public string Remarks { get; set; } = "";
    public string CssClass { get; set; } = ""; // bgAvail, bgcanAvail, bgLeave, bgclose, or empty
    public string DisplayText { get; set; } = "";
    public DateTime DayDate { get; set; }
    public int IsFound { get; set; } // 0=undefined, 1=special, 2=normal
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for the Staff Rota grid (Phase 1 — read-only).
/// Migrated from staffRota.aspx.cs.
/// </summary>
public class StaffRotaService
{
    private readonly string _defaultConnection;
    private readonly string _businessConnection;

    public StaffRotaService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Builds the 7-day staff rota grid.
    /// Source: staffRota.aspx.cs bindList() + getstaffrotaDay_list()
    /// </summary>
    public async Task<StaffRotaViewModel> GetRotaAsync(long webshopId, DateTime startDate, long currentUserId)
    {
        var model = new StaffRotaViewModel { StartDate = startDate };

        // Build 7-day headers
        var dayHeaders = new List<StaffRotaDayHeader>();
        for (int i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);
            dayHeaders.Add(new StaffRotaDayHeader
            {
                DayID = AssignedTasksNavHelper.GetDayId(date),
                DayName = date.DayOfWeek.ToString(),
                Date = date,
                CakeCount = "0"
            });
        }

        // Get cake counts per day (matching legacy day tab counts)
        await using var defaultConn = new SqlConnection(_defaultConnection);
        await defaultConn.OpenAsync();
        var countSql = @"SELECT CAST(ordercollection_dispatchDate AS DATE) AS dispDate, COUNT(*) AS cnt
            FROM tbl_order o
            INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID
            INNER JOIN tbl_orderDetail d ON d.orderDetail_orderID = o.order_ID
            WHERE order_branchID IN (
                SELECT WebstoreBranch_BranchID FROM tbl_WebstoreBranch 
                WHERE (WebstoreBranch_WebstoreID = @wid AND WebstoreBranch_isBaking = 0) 
                   OR WebstoreBranch_BranchID = @wid
            )
            AND order_isPurchased = 1 AND order_isdeleted = 0
            AND order_status IN (0, 1, 3, 5)
            AND ordercollection_dispatchDate >= @startDate 
            AND ordercollection_dispatchDate < @endDate
            GROUP BY CAST(ordercollection_dispatchDate AS DATE)";
        await using (var cmd = new SqlCommand(countSql, defaultConn))
        {
            cmd.Parameters.AddWithValue("@wid", webshopId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", startDate.AddDays(7));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = reader.GetDateTime(0).Date;
                var count = reader.GetInt32(1);
                var header = dayHeaders.FirstOrDefault(h => h.Date.Date == date);
                if (header != null) header.CakeCount = count.ToString();
            }
        }

        model.DayHeaders = dayHeaders;

        // Query staff from DefaultConnection (tbl_bakeryuser)
        var staffList = await GetStaffAsync(webshopId);

        // Query normal timings from BusinessConnection (tbl_staffTiming)
        var normalTimings = await GetNormalTimingsAsync();

        // Query special days from BusinessConnection (tbl_staffSpecialDays)
        var specialDays = await GetSpecialDaysAsync(startDate, startDate.AddDays(7));

        // Build rows
        foreach (var staff in staffList)
        {
            var row = new StaffRotaRow
            {
                StaffId = staff.Id,
                StaffName = staff.Name,
                StaffPhone = staff.Phone,
                IsTemporary = staff.IsTemporary,
                IsCurrentUser = staff.Id == currentUserId
            };

            foreach (var header in dayHeaders)
            {
                var cell = BuildCell(staff.Id, staff.IsTemporary, header.Date, header.DayID, specialDays, normalTimings);
                row.Days.Add(cell);
            }

            model.StaffRows.Add(row);
        }

        return model;
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<StaffRecord>> GetStaffAsync(long webshopId)
    {
        var list = new List<StaffRecord>();
        var sql = @"SELECT customer_ID, customer_Name, customer_phone, customer_istemporary 
            FROM tbl_bakeryuser 
            WHERE customer_webshopID = @wid 
              AND customer_isOpen = 1 
              AND customer_isActive = 1 
              AND customer_type = 3 
              AND customer_stafftype = 1 
              AND (customer_istemporary = 0 OR customer_istemporary = 2) 
            ORDER BY customer_istemporary, customer_Name";

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffRecord
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Phone = reader.IsDBNull(2) ? "" : reader.GetString(2),
                // Legacy: customer_istemporary == 2 means temporary
                IsTemporary = reader.GetInt32(3) == 2
            });
        }
        return list;
    }

    private async Task<List<NormalTiming>> GetNormalTimingsAsync()
    {
        var list = new List<NormalTiming>();
        var sql = @"SELECT staffTiming_ID, staffTiming_staffid, staffTiming_dayid, 
                           staffTiming_isclosed, staffTiming_from, staffTiming_to 
                    FROM tbl_staffTiming";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new NormalTiming
            {
                Id = reader.GetInt64(0),
                StaffId = reader.GetInt64(1),
                DayId = reader.GetInt32(2),
                IsClosed = reader.GetInt32(3),
                From = reader.GetInt32(4),
                To = reader.GetInt32(5)
            });
        }
        return list;
    }

    private async Task<List<SpecialDay>> GetSpecialDaysAsync(DateTime startDate, DateTime endDate)
    {
        var list = new List<SpecialDay>();
        var sql = @"SELECT staffSpecialDay_ID, staffSpecialDay_staffid, staffSpecialDay_Date, 
                           staffSpecialDay_isclosed, staffSpecialDay_from, staffSpecialDay_to, 
                           staffSpecialDay_approvedsts, staffSpecialDay_requestedBy 
                    FROM tbl_staffSpecialDays 
                    WHERE staffSpecialDay_Date >= @startDate AND staffSpecialDay_Date < @endDate";

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@startDate", startDate.Date);
        cmd.Parameters.AddWithValue("@endDate", endDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SpecialDay
            {
                Id = reader.GetInt64(0),
                StaffId = reader.GetInt64(1),
                Date = reader.GetDateTime(2).Date,
                IsClosed = reader.GetInt32(3),
                From = reader.GetInt32(4),
                To = reader.GetInt32(5),
                ApprovedSts = reader.GetInt32(6),
                RequestedBy = reader.GetInt64(7)
            });
        }
        return list;
    }

    /// <summary>
    /// Builds a single cell for a staff member on a given day.
    /// Source: staffRota.aspx.cs getstaffrotaDay_list() + getReqstatus() + getReqstatusMessage()
    /// </summary>
    private StaffRotaDayCell BuildCell(long staffId, bool isTemporary, DateTime date, int dayId,
        List<SpecialDay> specialDays, List<NormalTiming> normalTimings)
    {
        // Check for special day (approvedsts < 2)
        var sd = specialDays.FirstOrDefault(s =>
            s.Date == date.Date && s.StaffId == staffId && s.ApprovedSts < 2);

        if (sd != null)
        {
            // Apply getReqstatus logic
            int effectiveClosed = GetReqStatus(sd.IsClosed, sd.ApprovedSts);
            string remarks = GetReqStatusMessage(sd.IsClosed, sd.ApprovedSts);

            return effectiveClosed switch
            {
                1 => new StaffRotaDayCell
                {
                    Status = 1,
                    CssClass = "bgclose",
                    DisplayText = remarks,
                    FromHour = sd.From,
                    ToHour = sd.To,
                    Remarks = remarks,
                    DayDate = date,
                    IsFound = 1
                },
                2 => new StaffRotaDayCell
                {
                    Status = 2,
                    CssClass = "bgcanAvail",
                    DisplayText = GetTimingString(sd.From, sd.To),
                    FromHour = sd.From,
                    ToHour = sd.To,
                    Remarks = remarks,
                    DayDate = date,
                    IsFound = 1
                },
                3 => new StaffRotaDayCell
                {
                    Status = 3,
                    CssClass = "bgLeave",
                    DisplayText = remarks,
                    FromHour = sd.From,
                    ToHour = sd.To,
                    Remarks = remarks,
                    DayDate = date,
                    IsFound = 1
                },
                0 => new StaffRotaDayCell
                {
                    // isclosed=0 with approvedsts=1 → available (approved)
                    Status = 4,
                    CssClass = "bgAvail",
                    DisplayText = GetTimingString(sd.From, sd.To),
                    FromHour = sd.From,
                    ToHour = sd.To,
                    Remarks = "",
                    DayDate = date,
                    IsFound = 1
                },
                _ => new StaffRotaDayCell { Status = 0, DayDate = date, IsFound = 1 }
            };
        }

        // No special day — check normal timing (only for non-temporary staff)
        if (!isTemporary)
        {
            var nd = normalTimings.FirstOrDefault(n => n.DayId == dayId && n.StaffId == staffId);
            if (nd != null)
            {
                return nd.IsClosed switch
                {
                    1 => new StaffRotaDayCell
                    {
                        Status = 1,
                        CssClass = "bgclose",
                        DisplayText = "",
                        FromHour = nd.From,
                        ToHour = nd.To,
                        DayDate = date,
                        IsFound = 2
                    },
                    2 => new StaffRotaDayCell
                    {
                        Status = 2,
                        CssClass = "bgcanAvail",
                        DisplayText = GetTimingString(nd.From, nd.To),
                        FromHour = nd.From,
                        ToHour = nd.To,
                        DayDate = date,
                        IsFound = 2
                    },
                    _ => new StaffRotaDayCell
                    {
                        Status = 4,
                        CssClass = "bgAvail",
                        DisplayText = GetTimingString(nd.From, nd.To),
                        FromHour = nd.From,
                        ToHour = nd.To,
                        DayDate = date,
                        IsFound = 2
                    }
                };
            }
        }

        // Undefined
        return new StaffRotaDayCell { Status = 0, DayDate = date, IsFound = 0 };
    }

    /// <summary>
    /// Source: staffRota.aspx.cs getReqstatus(int isclosed, int sts)
    /// </summary>
    private static int GetReqStatus(int isClosed, int approvedSts)
    {
        if (isClosed == 2 && approvedSts == 1) return 0; // approved availability → show as available
        if (isClosed == 3 && approvedSts == 1) return 1; // approved leave → show as not available (bgclose)
        return isClosed;
    }

    /// <summary>
    /// Source: staffRota.aspx.cs getReqstatusMessage(staffSpecialDays obj)
    /// </summary>
    private static string GetReqStatusMessage(int isClosed, int approvedSts)
    {
        if (isClosed == 3 && approvedSts == 1) return "On Leave";
        if (isClosed == 3 && approvedSts == 0) return "Leave Requested";
        return "";
    }

    /// <summary>
    /// Source: staffRota.aspx.cs gettimingstring(int strfrom, int To)
    /// </summary>
    private static string GetTimingString(int from, int to)
    {
        if (from == -1 && to == -1) return "Flexible";
        string fromStr = from == -1 ? "Flexible" : from.ToString("00") + ":00";
        string toStr = to == -1 ? "Flexible" : to.ToString("00") + ":00";
        return fromStr + " - " + toStr;
    }

    /// <summary>
    /// Saves a staff availability request. Source: staffRota.aspx.cs btnSave_Click
    /// </summary>
    public async Task<bool> SaveStaffAvailabilityRequestAsync(
        long staffId, long requestedByUserId, DateTime requestDate,
        int availability, int fromTime, int toTime, string remarks)
    {
        if (requestDate.Date < DateTime.Today || requestedByUserId == staffId)
            return false;

        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Check if entry already exists
        var checkSql = "SELECT COUNT(*) FROM tbl_staffSpecialDays WHERE staffSpecialDay_staffid = @staffId AND staffSpecialDay_Date = @dt";
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@staffId", staffId);
            checkCmd.Parameters.AddWithValue("@dt", requestDate.Date);
            var count = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            if (count > 0) return false;
        }

        var sql = @"INSERT INTO tbl_staffSpecialDays 
            (staffSpecialDay_staffid, staffSpecialDay_requestedBy, staffSpecialDay_Date,
             staffSpecialDay_from, staffSpecialDay_to, staffSpecialDay_isclosed,
             staffSpecialDay_approvedsts, staffSpecialDay_remarks,
             staffSpecialDay_modifiedOn, staffSpecialDay_approvedOn, staffSpecialDay_approvedBy, staffSpecialDay_approvedRemarks)
            VALUES (@staffId, @requestedBy, @dt, @from, @to, @isclosed, 0, @remarks, GETDATE(), GETDATE(), 0, '')";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);
        cmd.Parameters.AddWithValue("@requestedBy", requestedByUserId);
        cmd.Parameters.AddWithValue("@dt", requestDate.Date);
        cmd.Parameters.AddWithValue("@from", fromTime);
        cmd.Parameters.AddWithValue("@to", toTime);
        cmd.Parameters.AddWithValue("@isclosed", availability);
        cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    // ─── Internal record types ─────────────────────────────────────────────────

    private class StaffRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool IsTemporary { get; set; }
    }

    private class NormalTiming
    {
        public long Id { get; set; }
        public long StaffId { get; set; }
        public int DayId { get; set; }
        public int IsClosed { get; set; }
        public int From { get; set; }
        public int To { get; set; }
    }

    private class SpecialDay
    {
        public long Id { get; set; }
        public long StaffId { get; set; }
        public DateTime Date { get; set; }
        public int IsClosed { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public int ApprovedSts { get; set; }
        public long RequestedBy { get; set; }
    }
}
