using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Manages cleaning checklists - allows staff to check off cleaning items, assign staff,
/// add notes, and view historical cleaning records by date range.
/// Migrated from legacy managecleaningchecklist.aspx / managecleaningchecklist.aspx.cs.
/// Routes: /managecleaningchecklist, /addupdcleaningchecks, /addupdcleaningchecks/{todaydate}
/// </summary>
public class CleaningChecklistController : Controller
{
    private readonly IConfiguration _config;
    private readonly BakeryMenuService _menuService;

    public CleaningChecklistController(IConfiguration config, BakeryMenuService menuService)
    {
        _config = config;
        _menuService = menuService;
    }

    private string ConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";
    private string StaffConnStr => _config["ConnectionStrings:StaffAssessment"] ?? ConnStr;

    private async Task PopulateLayoutAsync()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
    }

    [HttpGet("managecleaningchecklist")]
    [HttpGet("managecleaningchecklist.aspx")]
    [HttpGet("addupdcleaningchecks")]
    [HttpGet("addupdcleaningchecks/{todaydate}")]
    public async Task<IActionResult> Index(
        string? todaydate = null,
        [FromQuery] string? sdate = null,
        [FromQuery] string? edate = null,
        [FromQuery] string? msg = null)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 82;

        await PopulateLayoutAsync();

        DateTime today = DateTime.Today;
        bool showBackButton = false;
        if (!string.IsNullOrEmpty(todaydate))
        {
            var parts = todaydate.Split('-');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var day) &&
                int.TryParse(parts[1], out var month) &&
                int.TryParse(parts[2], out var year))
            {
                today = new DateTime(year, month, day);
                showBackButton = true;
            }
        }

        string fromDate = sdate ?? $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        string toDate = edate ?? DateTime.Now.ToString("dd/MM/yyyy");

        // Load staff list
        var staffList = await GetBakeryUsersAsync(webshopId);

        // Load cleaning checklist items
        var checklistItems = await GetCleaningChecklistItemsAsync();

        // Load saved data for today
        SavedCleaningChecklist? saved = null;
        var savedDetails = new List<CleaningChecklistDetail>();
        var savedNotes = new List<CleaningNote>();

        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();

        var sqlSaved = "SELECT TOP 1 CleaningChecklistDone_ID, CleaningChecklistDone_isDone, CleaningChecklistDone_remarks, CleaningChecklistDone_byID FROM tbl_CleaningChecklistDone WHERE CAST(CleaningChecklistDone_createdOn AS DATE) = @today";
        await using (var cmd = new SqlCommand(sqlSaved, conn))
        {
            cmd.Parameters.AddWithValue("@today", today.Date);
            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                saved = new SavedCleaningChecklist
                {
                    DoneId = rdr.GetInt64(0),
                    IsDone = rdr.GetBoolean(1),
                    Remarks = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    ByUserId = rdr.GetInt64(3)
                };
            }
        }

        if (saved != null)
        {
            // Load details
            var sqlDet = "SELECT CleaningChecklistDonedet_ChecklistID, CleaningChecklistDonedet_staffID FROM tbl_CleaningChecklistDonedet WHERE CleaningChecklistDonedet_doneID = @did";
            await using (var cmd = new SqlCommand(sqlDet, conn))
            {
                cmd.Parameters.AddWithValue("@did", saved.DoneId);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    savedDetails.Add(new CleaningChecklistDetail { ChecklistId = rdr.GetInt64(0), StaffId = rdr.GetInt64(1) });
            }

            // Load notes
            var sqlNotes = "SELECT CleaningChecklistDoneNotes_ID, CleaningChecklistDoneNotes_doneID, CleaningChecklistDoneNotes_byID, CleaningChecklistDoneNotes_notes FROM tbl_CleaningChecklistDoneNotes WHERE CleaningChecklistDoneNotes_doneID = @did";
            await using (var cmd = new SqlCommand(sqlNotes, conn))
            {
                cmd.Parameters.AddWithValue("@did", saved.DoneId);
                await using var rdr = await cmd.ExecuteReaderAsync();
                int counter = 1;
                while (await rdr.ReadAsync())
                {
                    savedNotes.Add(new CleaningNote
                    {
                        Id = counter++,
                        NoteId = rdr.GetInt64(0),
                        DoneId = rdr.GetInt64(1),
                        ByUserId = rdr.GetInt64(2),
                        Notes = rdr.IsDBNull(3) ? "" : rdr.GetString(3)
                    });
                }
            }
        }

        // Load result grid
        var results = await GetResultDataAsync(fromDate, toDate);

        ViewBag.TodayDate = today;
        ViewBag.ShowBackButton = showBackButton;
        ViewBag.StaffList = staffList;
        ViewBag.ChecklistItems = checklistItems;
        ViewBag.SavedChecklist = saved;
        ViewBag.SavedDetails = savedDetails;
        ViewBag.SavedNotes = savedNotes;
        ViewBag.Results = results;
        ViewBag.ShowResults = results.Rows.Count > 0;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.SuccessMessage = msg;
        ViewBag.CurrentUserId = userId;

        return View("~/Views/CleaningChecklist/Index.cshtml");
    }

    [HttpPost("addupdcleaningchecks/submit")]
    public async Task<IActionResult> Submit(
        [FromForm] bool checklistAllDone,
        [FromForm] string? remarks,
        [FromForm] string dateStr)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0) return Redirect("/businesslogin");

        DateTime date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr))
            DateTime.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date);

        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();

        // Find or create CleaningChecklistDone
        long doneId = 0;
        bool isNew = false;
        var sqlFind = "SELECT TOP 1 CleaningChecklistDone_ID FROM tbl_CleaningChecklistDone WHERE CAST(CleaningChecklistDone_createdOn AS DATE) = @today";
        await using (var cmd = new SqlCommand(sqlFind, conn))
        {
            cmd.Parameters.AddWithValue("@today", date.Date);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null) doneId = Convert.ToInt64(result);
            else isNew = true;
        }

        if (isNew)
        {
            var sqlIns = @"INSERT INTO tbl_CleaningChecklistDone (CleaningChecklistDone_createdOn, CleaningChecklistDone_byID, CleaningChecklistDone_isDone, CleaningChecklistDone_modifiedOn, CleaningChecklistDone_remarks)
                           VALUES (@date, @uid, @done, @now, @remarks); SELECT SCOPE_IDENTITY();";
            await using var cmd = new SqlCommand(sqlIns, conn);
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@uid", (long)userId);
            cmd.Parameters.AddWithValue("@done", checklistAllDone);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            doneId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        else
        {
            var sqlUpd = "UPDATE tbl_CleaningChecklistDone SET CleaningChecklistDone_byID=@uid, CleaningChecklistDone_isDone=@done, CleaningChecklistDone_modifiedOn=@now, CleaningChecklistDone_remarks=@remarks WHERE CleaningChecklistDone_ID=@id";
            await using var cmd = new SqlCommand(sqlUpd, conn);
            cmd.Parameters.AddWithValue("@uid", (long)userId);
            cmd.Parameters.AddWithValue("@done", checklistAllDone);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmd.Parameters.AddWithValue("@id", doneId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Save checked items
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("chk_"))
            {
                string checklistIdStr = key.Substring(4);
                long checklistId = long.Parse(checklistIdStr);
                long staffId = long.Parse(Request.Form[$"staff_{checklistIdStr}"].ToString());

                // Upsert detail
                var sqlDetFind = "SELECT COUNT(1) FROM tbl_CleaningChecklistDonedet WHERE CleaningChecklistDonedet_doneID=@did AND CleaningChecklistDonedet_ChecklistID=@clid";
                await using (var cmdFind = new SqlCommand(sqlDetFind, conn))
                {
                    cmdFind.Parameters.AddWithValue("@did", doneId);
                    cmdFind.Parameters.AddWithValue("@clid", checklistId);
                    int cnt = Convert.ToInt32(await cmdFind.ExecuteScalarAsync());
                    if (cnt > 0)
                    {
                        var sqlDetUpd = "UPDATE tbl_CleaningChecklistDonedet SET CleaningChecklistDonedet_staffID=@sid, CleaningChecklistDonedet_modifiedOn=@now WHERE CleaningChecklistDonedet_doneID=@did AND CleaningChecklistDonedet_ChecklistID=@clid";
                        await using var cmdUpd = new SqlCommand(sqlDetUpd, conn);
                        cmdUpd.Parameters.AddWithValue("@sid", staffId);
                        cmdUpd.Parameters.AddWithValue("@now", DateTime.Now);
                        cmdUpd.Parameters.AddWithValue("@did", doneId);
                        cmdUpd.Parameters.AddWithValue("@clid", checklistId);
                        await cmdUpd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var sqlDetIns = @"INSERT INTO tbl_CleaningChecklistDonedet (CleaningChecklistDonedet_doneID, CleaningChecklistDonedet_ChecklistID, CleaningChecklistDonedet_staffID, CleaningChecklistDonedet_modifiedOn)
                                          VALUES (@did, @clid, @sid, @now)";
                        await using var cmdIns = new SqlCommand(sqlDetIns, conn);
                        cmdIns.Parameters.AddWithValue("@did", doneId);
                        cmdIns.Parameters.AddWithValue("@clid", checklistId);
                        cmdIns.Parameters.AddWithValue("@sid", staffId);
                        cmdIns.Parameters.AddWithValue("@now", DateTime.Now);
                        await cmdIns.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Save notes
        var sqlDelNotes = "DELETE FROM tbl_CleaningChecklistDoneNotes WHERE CleaningChecklistDoneNotes_doneID=@did AND CleaningChecklistDoneNotes_byID=@uid";
        await using (var cmdDel = new SqlCommand(sqlDelNotes, conn))
        {
            cmdDel.Parameters.AddWithValue("@did", doneId);
            cmdDel.Parameters.AddWithValue("@uid", (long)userId);
            await cmdDel.ExecuteNonQueryAsync();
        }

        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("note_"))
            {
                string noteText = Request.Form[key].ToString();
                if (!string.IsNullOrWhiteSpace(noteText))
                {
                    var sqlNoteIns = @"INSERT INTO tbl_CleaningChecklistDoneNotes (CleaningChecklistDoneNotes_byID, CleaningChecklistDoneNotes_createdOn, CleaningChecklistDoneNotes_doneID, CleaningChecklistDoneNotes_modifiedOn, CleaningChecklistDoneNotes_notes)
                                       VALUES (@uid, @now, @did, @now, @notes)";
                    await using var cmdNoteIns = new SqlCommand(sqlNoteIns, conn);
                    cmdNoteIns.Parameters.AddWithValue("@uid", (long)userId);
                    cmdNoteIns.Parameters.AddWithValue("@now", DateTime.Now);
                    cmdNoteIns.Parameters.AddWithValue("@did", doneId);
                    cmdNoteIns.Parameters.AddWithValue("@notes", noteText);
                    await cmdNoteIns.ExecuteNonQueryAsync();
                }
            }
        }

        return Redirect($"/addupdcleaningchecks/{date:dd-MM-yyyy}?msg={Uri.EscapeDataString("Cleaning Checklist has been saved successfully")}");
    }

    #region Data Access

    private async Task<List<StaffItem>> GetBakeryUsersAsync(long webshopId)
    {
        var list = new List<StaffItem>();
        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        var sql = "SELECT customer_ID, customer_Name FROM tbl_bakeryuser WHERE customer_isActive = 1 AND customer_webshopID = @wid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new StaffItem { Id = rdr.GetInt64(0), Name = rdr.GetString(1) });
        return list;
    }

    private async Task<List<ChecklistItem>> GetCleaningChecklistItemsAsync()
    {
        var list = new List<ChecklistItem>();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = "SELECT CleaningChecklist_ID, CleaningChecklist_item, CleaningChecklist_frequency, CleaningChecklist_Precautions, CleaningChecklist_methods, CleaningChecklist_staffID FROM tbl_CleaningChecklist ORDER BY CleaningChecklist_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new ChecklistItem
            {
                ChecklistId = rdr.GetInt64(0),
                Item = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                Frequency = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                Precautions = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                Methods = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                DefaultStaffId = rdr.IsDBNull(5) ? 0 : rdr.GetInt64(5)
            });
        }
        return list;
    }

    private async Task<DataTable> GetResultDataAsync(string fromDate, string toDate)
    {
        var dt = new DataTable();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = @"SELECT
            isnull((select customer_name from db_Cakerstreet_live.dbo.tbl_bakeryuser bu where bu.customer_ID=O.CleaningChecklistDone_byID),'') customername,
            isnull(O.CleaningChecklistDone_remarks,'') ProblemDuringChecklist,
            O.CleaningChecklistDone_createdOn daydate
            FROM tbl_CleaningChecklistDone O
            WHERE (CAST(O.CleaningChecklistDone_createdOn AS DATE) between @fromDate and @toDate)
            ORDER BY O.CleaningChecklistDone_createdOn desc";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@fromDate", fromDate);
        cmd.Parameters.AddWithValue("@toDate", toDate);
        dt.Load(await cmd.ExecuteReaderAsync());
        return dt;
    }

    #endregion

    #region Models

    public class StaffItem { public long Id { get; set; } public string Name { get; set; } = ""; }
    public class ChecklistItem
    {
        public long ChecklistId { get; set; }
        public string Item { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Precautions { get; set; } = "";
        public string Methods { get; set; } = "";
        public long DefaultStaffId { get; set; }
    }
    public class SavedCleaningChecklist
    {
        public long DoneId { get; set; }
        public bool IsDone { get; set; }
        public string Remarks { get; set; } = "";
        public long ByUserId { get; set; }
    }
    public class CleaningChecklistDetail { public long ChecklistId { get; set; } public long StaffId { get; set; } }
    public class CleaningNote
    {
        public int Id { get; set; }
        public long NoteId { get; set; }
        public long DoneId { get; set; }
        public long ByUserId { get; set; }
        public string Notes { get; set; } = "";
    }

    #endregion
}
