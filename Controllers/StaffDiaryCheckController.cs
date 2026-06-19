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
/// Manages staff daily diary checks (opening and closing checklists with temperature tasks).
/// Migrated from legacy managestaffdairycheck.aspx / managestaffdairycheck.aspx.cs.
/// Routes: /managestaffdairycheck, /addupdopeningnclosingchecks, /addupdopeningnclosingchecks/{todaydate}
/// </summary>
public class StaffDiaryCheckController : Controller
{
    private readonly IConfiguration _config;
    private readonly BakeryMenuService _menuService;

    public StaffDiaryCheckController(IConfiguration config, BakeryMenuService menuService)
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

    [HttpGet("managestaffdairycheck")]
    [HttpGet("managestaffdairycheck.aspx")]
    [HttpGet("addupdopeningnclosingchecks")]
    [HttpGet("addupdopeningnclosingchecks/{todaydate}")]
    public async Task<IActionResult> Index(
        string? todaydate = null,
        [FromQuery] string? sdate = null,
        [FromQuery] string? edate = null)
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

        // Default date range
        string fromDate = sdate ?? $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        string toDate = edate ?? DateTime.Now.ToString("dd/MM/yyyy");

        // Load staff list
        var staffList = await GetBakeryUsersAsync(webshopId);

        // Load opening/closing check descriptions
        var openingChecks = await GetDairyChecksAsync(1);
        var closingChecks = await GetDairyChecksAsync(2);

        // Load opening/closing tasks
        var openingTasks = await GetDairyTasksAsync(1);
        var closingTasks = await GetDairyTasksAsync(2);

        // Load saved data for today
        var savedOpening = await GetSavedChecklistAsync(today, 1);
        var savedClosing = await GetSavedChecklistAsync(today, 2);
        var savedOpeningTasks = savedOpening != null ? await GetSavedTasksAsync(savedOpening.CheckId) : new Dictionary<long, string>();
        var savedClosingTasks = savedClosing != null ? await GetSavedTasksAsync(savedClosing.CheckId) : new Dictionary<long, string>();

        // Load result grid
        var results = await GetResultDataAsync(webshopIdStr, fromDate, toDate);

        ViewBag.TodayDate = today;
        ViewBag.ShowBackButton = showBackButton;
        ViewBag.StaffList = staffList;
        ViewBag.OpeningChecks = openingChecks;
        ViewBag.ClosingChecks = closingChecks;
        ViewBag.OpeningTasks = openingTasks;
        ViewBag.ClosingTasks = closingTasks;
        ViewBag.SavedOpening = savedOpening;
        ViewBag.SavedClosing = savedClosing;
        ViewBag.SavedOpeningTasks = savedOpeningTasks;
        ViewBag.SavedClosingTasks = savedClosingTasks;
        ViewBag.Results = results;
        ViewBag.ShowResults = results.Rows.Count > 0;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View("~/Views/StaffDiaryCheck/Index.cshtml");
    }

    [HttpPost("addupdopeningnclosingchecks/submit")]
    public async Task<IActionResult> Submit(
        [FromForm] int checkType,
        [FromForm] long staffId,
        [FromForm] bool allDone,
        [FromForm] string? problem,
        [FromForm] string dateStr)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        DateTime date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr))
        {
            DateTime.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date);
        }

        // Collect task inputs from form
        var taskInputs = new Dictionary<long, string>();
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("task_") && long.TryParse(key.Substring(5), out var taskId))
            {
                taskInputs[taskId] = Request.Form[key].ToString();
            }
        }

        await SaveChecklistAsync(checkType, staffId, allDone, problem ?? "", date, taskInputs, userId);

        string label = checkType == 1 ? "Opening" : "Closing";
        return Redirect($"/addupdopeningnclosingchecks/{date:dd-MM-yyyy}?msg={Uri.EscapeDataString(label + " checklist has been saved successfully.")}");
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

    private async Task<List<CheckItem>> GetDairyChecksAsync(int checkType)
    {
        var list = new List<CheckItem>();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = "SELECT CheckTitle FROM tbl_Dairy_Checks WHERE CheckType = @ct ORDER BY DisplayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ct", checkType);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new CheckItem { Title = rdr.GetString(0) });
        return list;
    }

    private async Task<List<TaskItem>> GetDairyTasksAsync(int checkType)
    {
        var list = new List<TaskItem>();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = "SELECT task_ID, task_title FROM tbl_dairyTask WHERE task_checktype = @ct ORDER BY task_displayorder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ct", checkType);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskItem { TaskId = rdr.GetInt64(0), Title = rdr.GetString(1) });
        return list;
    }

    private async Task<SavedChecklist?> GetSavedChecklistAsync(DateTime today, int checkType)
    {
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = @"SELECT TOP 1 Staff_DairyCheckID, ChecklistDone, ProblemDuringChecklist, customer_ID, ChecklistData
                    FROM tbl_Staff_DairyChecks
                    WHERE CAST(CreatedOn AS DATE) = @today AND CheckType = @ct";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", today.Date);
        cmd.Parameters.AddWithValue("@ct", checkType);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (await rdr.ReadAsync())
        {
            return new SavedChecklist
            {
                CheckId = rdr.GetInt64(0),
                AllDone = rdr.GetBoolean(1),
                Problem = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                StaffId = rdr.GetInt64(3),
                ChecklistData = rdr.IsDBNull(4) ? "" : rdr.GetString(4)
            };
        }
        return null;
    }

    private async Task<Dictionary<long, string>> GetSavedTasksAsync(long checkId)
    {
        var dict = new Dictionary<long, string>();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = "SELECT stafftask_taskID, stafftask_input FROM tbl_staff_dairytask WHERE stafftask_checkID = @cid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", checkId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            dict[rdr.GetInt64(0)] = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
        return dict;
    }

    private async Task<DataTable> GetResultDataAsync(string webshopId, string fromDate, string toDate)
    {
        var dt = new DataTable();
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();
        var sql = @"SELECT
            isnull((select customer_name from db_Cakerstreet_live.dbo.tbl_bakeryuser bu where bu.customer_ID=O.customer_ID),'') customername_opening,
            isnull(O.ProblemDuringChecklist,'') ProblemDuringChecklist_opening,
            case when C.customer_ID is null then '' else
            isnull((select customer_name from db_Cakerstreet_live.dbo.tbl_bakeryuser bu where bu.customer_ID=C.customer_ID),'') end customername_closing,
            isnull(C.ProblemDuringChecklist,'') ProblemDuringChecklist_closing,
            isnull(O.ChecklistData,'') ChecklistData_opening,
            isnull(C.ChecklistData,'') ChecklistData_closing,
            O.CreatedOn daydate
            FROM tbl_Staff_DairyChecks O
            left join tbl_Staff_DairyChecks C on (datepart(d,O.CreatedOn)=datepart(d,C.CreatedOn) and datepart(m,O.CreatedOn)=datepart(m,C.CreatedOn) and datepart(yy,O.CreatedOn)=datepart(yy,C.CreatedOn)) and C.CheckType=2
            WHERE O.CheckType=1 and (CAST(O.CreatedOn AS DATE) between @fromDate and @toDate)
            order by O.CreatedOn desc";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@fromDate", fromDate);
        cmd.Parameters.AddWithValue("@toDate", toDate);
        dt.Load(await cmd.ExecuteReaderAsync());
        return dt;
    }

    private async Task SaveChecklistAsync(int checkType, long staffId, bool allDone, string problem, DateTime date, Dictionary<long, string> taskInputs, int userId)
    {
        await using var conn = new SqlConnection(StaffConnStr);
        await conn.OpenAsync();

        // Check if record exists
        long checkId = 0;
        bool exists = false;
        var sqlFind = "SELECT TOP 1 Staff_DairyCheckID FROM tbl_Staff_DairyChecks WHERE CAST(CreatedOn AS DATE) = @today AND CheckType = @ct";
        await using (var cmdFind = new SqlCommand(sqlFind, conn))
        {
            cmdFind.Parameters.AddWithValue("@today", date.Date);
            cmdFind.Parameters.AddWithValue("@ct", checkType);
            var result = await cmdFind.ExecuteScalarAsync();
            if (result != null)
            {
                checkId = Convert.ToInt64(result);
                exists = true;
            }
        }

        if (exists)
        {
            var sqlUpd = @"UPDATE tbl_Staff_DairyChecks SET ChecklistDone=@done, ProblemDuringChecklist=@problem, ChecklistData='', customer_ID=@staffId WHERE Staff_DairyCheckID=@id";
            await using var cmdUpd = new SqlCommand(sqlUpd, conn);
            cmdUpd.Parameters.AddWithValue("@done", allDone);
            cmdUpd.Parameters.AddWithValue("@problem", problem);
            cmdUpd.Parameters.AddWithValue("@staffId", staffId);
            cmdUpd.Parameters.AddWithValue("@id", checkId);
            await cmdUpd.ExecuteNonQueryAsync();
        }
        else
        {
            var sqlIns = @"INSERT INTO tbl_Staff_DairyChecks (ChecklistDone, CheckType, CreatedBy, CreatedOn, customer_ID, ProblemDuringChecklist, ChecklistData)
                           VALUES (@done, @ct, @uid, @date, @staffId, @problem, '');
                           SELECT SCOPE_IDENTITY();";
            await using var cmdIns = new SqlCommand(sqlIns, conn);
            cmdIns.Parameters.AddWithValue("@done", allDone);
            cmdIns.Parameters.AddWithValue("@ct", checkType);
            cmdIns.Parameters.AddWithValue("@uid", (long)userId);
            cmdIns.Parameters.AddWithValue("@date", date);
            cmdIns.Parameters.AddWithValue("@staffId", staffId);
            cmdIns.Parameters.AddWithValue("@problem", problem);
            checkId = Convert.ToInt64(await cmdIns.ExecuteScalarAsync());
        }

        // Save tasks and build checklist data string
        string checklistData = "";
        foreach (var kvp in taskInputs)
        {
            long taskId = kvp.Key;
            string input = kvp.Value;

            // Get task title
            string taskTitle = "";
            var sqlTitle = "SELECT task_title FROM tbl_dairyTask WHERE task_ID = @tid";
            await using (var cmdTitle = new SqlCommand(sqlTitle, conn))
            {
                cmdTitle.Parameters.AddWithValue("@tid", taskId);
                taskTitle = (await cmdTitle.ExecuteScalarAsync())?.ToString() ?? "";
            }

            // Upsert task data
            var sqlTaskFind = "SELECT COUNT(1) FROM tbl_staff_dairytask WHERE stafftask_checkID=@cid AND stafftask_taskID=@tid";
            await using (var cmdTaskFind = new SqlCommand(sqlTaskFind, conn))
            {
                cmdTaskFind.Parameters.AddWithValue("@cid", checkId);
                cmdTaskFind.Parameters.AddWithValue("@tid", taskId);
                int cnt = Convert.ToInt32(await cmdTaskFind.ExecuteScalarAsync());
                if (cnt > 0)
                {
                    var sqlTaskUpd = "UPDATE tbl_staff_dairytask SET stafftask_input=@input, stafftask_modifiedOn=@now, stafftask_modifiedBy=@uid WHERE stafftask_checkID=@cid AND stafftask_taskID=@tid";
                    await using var cmdTaskUpd = new SqlCommand(sqlTaskUpd, conn);
                    cmdTaskUpd.Parameters.AddWithValue("@input", input);
                    cmdTaskUpd.Parameters.AddWithValue("@now", DateTime.Now);
                    cmdTaskUpd.Parameters.AddWithValue("@uid", userId);
                    cmdTaskUpd.Parameters.AddWithValue("@cid", checkId);
                    cmdTaskUpd.Parameters.AddWithValue("@tid", taskId);
                    await cmdTaskUpd.ExecuteNonQueryAsync();
                }
                else
                {
                    var sqlTaskIns = @"INSERT INTO tbl_staff_dairytask (stafftask_taskID, stafftask_checkID, stafftask_input, stafftask_createdOn, stafftask_createdby, stafftask_modifiedOn, stafftask_modifiedBy)
                                       VALUES (@tid, @cid, @input, @now, @uid, @now, @uid)";
                    await using var cmdTaskIns = new SqlCommand(sqlTaskIns, conn);
                    cmdTaskIns.Parameters.AddWithValue("@tid", taskId);
                    cmdTaskIns.Parameters.AddWithValue("@cid", checkId);
                    cmdTaskIns.Parameters.AddWithValue("@input", input);
                    cmdTaskIns.Parameters.AddWithValue("@now", DateTime.Now);
                    cmdTaskIns.Parameters.AddWithValue("@uid", userId);
                    await cmdTaskIns.ExecuteNonQueryAsync();
                }
            }

            checklistData += taskTitle + ": " + input + " °C <br/>";
        }

        // Update checklist data
        var sqlUpdData = "UPDATE tbl_Staff_DairyChecks SET ChecklistData=@data WHERE Staff_DairyCheckID=@id";
        await using (var cmdUpdData = new SqlCommand(sqlUpdData, conn))
        {
            cmdUpdData.Parameters.AddWithValue("@data", checklistData);
            cmdUpdData.Parameters.AddWithValue("@id", checkId);
            await cmdUpdData.ExecuteNonQueryAsync();
        }
    }

    #endregion

    #region Models

    public class StaffItem { public long Id { get; set; } public string Name { get; set; } = ""; }
    public class CheckItem { public string Title { get; set; } = ""; }
    public class TaskItem { public long TaskId { get; set; } public string Title { get; set; } = ""; }
    public class SavedChecklist
    {
        public long CheckId { get; set; }
        public bool AllDone { get; set; }
        public string Problem { get; set; } = "";
        public long StaffId { get; set; }
        public string ChecklistData { get; set; } = "";
    }

    #endregion
}
