using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class DailyChecklistController : Controller
{
    private readonly DailyChecklistService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public DailyChecklistController(
        DailyChecklistService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    private async Task PopulateLayoutMetadataAsync()
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

    [HttpGet("dailydairychecklists")]
    [HttpGet("dailydairy_checklists")]
    [HttpGet("DailyDairy_checklists.aspx")]
    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();
        return View("~/Views/DailyChecklist/Index.cshtml");
    }

    [HttpGet("managedailycheck_openingnclosing")]
    public async Task<IActionResult> OpeningClosingList([FromQuery] string sdate = "", [FromQuery] string edate = "")
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        // Default date values matching legacy logic
        if (string.IsNullOrEmpty(sdate))
        {
            sdate = $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        }
        if (string.IsNullOrEmpty(edate))
        {
            edate = DateTime.Now.ToString("dd/MM/yyyy");
        }

        var list = await _service.GetChecksListAsync(sdate, edate);

        ViewBag.StartDate = sdate;
        ViewBag.EndDate = edate;
        ViewBag.Checklists = list;

        return View("~/Views/DailyChecklist/OpeningClosingList.cshtml");
    }

    [HttpGet("addupdopeningnclosingchecks")]
    [HttpGet("managestaffdairycheck")]
    [HttpGet("managestaffdairycheck.aspx")]
    [HttpGet("addupdopeningnclosingchecks/{todaydate}")]
    public async Task<IActionResult> AddUpdateChecklist(string? todaydate = null, [FromQuery] string? msg = null)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        long webshopId = 82;
        long.TryParse(webshopIdStr, out webshopId);

        DateTime today = DateTime.Today;
        if (!string.IsNullOrEmpty(todaydate))
        {
            var parts = todaydate.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[0], out var day) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var year))
            {
                today = new DateTime(year, month, day);
            }
        }

        var staffList = await _service.GetBakeryUsersAsync(webshopId);
        var openingChecks = await _service.GetDairyChecksAsync(1);
        var closingChecks = await _service.GetDairyChecksAsync(2);
        var openingTasks = await _service.GetDairyTasksAsync(1);
        var closingTasks = await _service.GetDairyTasksAsync(2);

        var savedOpening = await _service.GetSavedChecklistAsync(today, 1);
        var savedClosing = await _service.GetSavedChecklistAsync(today, 2);

        var savedOpeningTasks = savedOpening != null ? await _service.GetSavedTasksDataAsync(savedOpening.StaffDairyCheckId) : new Dictionary<long, string>();
        var savedClosingTasks = savedClosing != null ? await _service.GetSavedTasksDataAsync(savedClosing.StaffDairyCheckId) : new Dictionary<long, string>();

        ViewBag.TodayDate = today;
        ViewBag.StaffList = staffList;
        ViewBag.OpeningChecks = openingChecks;
        ViewBag.ClosingChecks = closingChecks;
        ViewBag.OpeningTasks = openingTasks;
        ViewBag.ClosingTasks = closingTasks;

        ViewBag.SavedOpening = savedOpening;
        ViewBag.SavedClosing = savedClosing;
        ViewBag.SavedOpeningTasks = savedOpeningTasks;
        ViewBag.SavedClosingTasks = savedClosingTasks;
        ViewBag.SuccessMessage = msg;

        return View("~/Views/DailyChecklist/AddUpdateChecklist.cshtml");
    }

    [HttpPost("addupdopeningnclosingchecks/submit")]
    public async Task<IActionResult> SubmitChecklist(
        [FromForm] int checkType,
        [FromForm] long staffId,
        [FromForm] bool allDone,
        [FromForm] string problem,
        [FromForm] string dateStr)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        DateTime date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr))
        {
            if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                date = parsedDate;
            }
            else if (DateTime.TryParseExact(dateStr, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate))
            {
                date = parsedDate;
            }
            else if (DateTime.TryParse(dateStr, out parsedDate))
            {
                date = parsedDate;
            }
        }

        var taskInputs = new Dictionary<long, string>();
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("task_"))
            {
                if (long.TryParse(key.Substring(5), out var taskId))
                {
                    taskInputs[taskId] = Request.Form[key].ToString();
                }
            }
        }

        await _service.SaveChecklistAsync(staffId, checkType, allDone, problem, date, taskInputs, userId);

        string label = checkType == 1 ? "Opening" : "Closing";
        return Redirect($"/addupdopeningnclosingchecks/{date:dd-MM-yyyy}?msg={Uri.EscapeDataString(label + " checklist has been saved successfully.")}");
    }

    [HttpGet("managedailycheck_cleaning")]
    public async Task<IActionResult> CleaningList([FromQuery] string sdate = "", [FromQuery] string edate = "")
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        if (string.IsNullOrEmpty(sdate))
        {
            sdate = $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        }
        if (string.IsNullOrEmpty(edate))
        {
            edate = DateTime.Now.ToString("dd/MM/yyyy");
        }

        var list = await _service.GetCleaningChecksListAsync(sdate, edate);

        ViewBag.StartDate = sdate;
        ViewBag.EndDate = edate;
        ViewBag.Checklists = list;

        return View("~/Views/DailyChecklist/CleaningList.cshtml");
    }

    [HttpGet("addupdcleaningchecks")]
    [HttpGet("managecleaningchecklist")]
    [HttpGet("managecleaningchecklist.aspx")]
    [HttpGet("addupdcleaningchecks/{todaydate}")]
    public async Task<IActionResult> AddUpdateCleaning(string? todaydate = null, [FromQuery] string? msg = null, [FromQuery] string sdate = "", [FromQuery] string edate = "")
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        long webshopId = 82;
        long.TryParse(webshopIdStr, out webshopId);

        DateTime today = DateTime.Today;
        if (!string.IsNullOrEmpty(todaydate))
        {
            var parts = todaydate.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[0], out var day) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var year))
            {
                today = new DateTime(year, month, day);
            }
        }

        if (string.IsNullOrEmpty(sdate))
        {
            sdate = $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        }
        if (string.IsNullOrEmpty(edate))
        {
            edate = DateTime.Now.ToString("dd/MM/yyyy");
        }

        var staffList = await _service.GetBakeryUsersAsync(webshopId);
        var checklistItems = await _service.GetCleaningChecklistItemsAsync();
        var savedChecklist = await _service.GetSavedCleaningChecklistAsync(today);

        List<CleaningChecklistDetail> savedDetails = new List<CleaningChecklistDetail>();
        List<CleaningChecklistNote> savedNotes = new List<CleaningChecklistNote>();

        if (savedChecklist != null)
        {
            savedDetails = await _service.GetSavedCleaningChecklistDetailsAsync(savedChecklist.CleaningChecklistDoneId);
            savedNotes = await _service.GetSavedCleaningChecklistNotesAsync(savedChecklist.CleaningChecklistDoneId);
        }

        var historicalList = await _service.GetCleaningChecksListAsync(sdate, edate);

        ViewBag.TodayDate = today;
        ViewBag.StaffList = staffList;
        ViewBag.ChecklistItems = checklistItems;
        ViewBag.SavedChecklist = savedChecklist;
        ViewBag.SavedDetails = savedDetails;
        ViewBag.SavedNotes = savedNotes;
        ViewBag.SuccessMessage = msg;
        ViewBag.StartDate = sdate;
        ViewBag.EndDate = edate;
        ViewBag.HistoricalList = historicalList;

        return View("~/Views/DailyChecklist/AddUpdateCleaning.cshtml");
    }

    [HttpPost("addupdcleaningchecks/submit")]
    public async Task<IActionResult> SubmitCleaningChecklist(
        [FromForm] bool checklistAllDone,
        [FromForm] string remarks,
        [FromForm] string dateStr,
        [FromForm] List<long> checkedItems)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        DateTime date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr))
        {
            if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                date = parsedDate;
            }
            else if (DateTime.TryParseExact(dateStr, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate))
            {
                date = parsedDate;
            }
            else if (DateTime.TryParse(dateStr, out parsedDate))
            {
                date = parsedDate;
            }
        }

        var details = new List<CleaningChecklistDetail>();
        if (checkedItems != null)
        {
            foreach (var itemId in checkedItems)
            {
                var staffIdVal = Request.Form[$"staff_{itemId}"].ToString();
                if (long.TryParse(staffIdVal, out var staffId))
                {
                    details.Add(new CleaningChecklistDetail
                    {
                        ChecklistId = itemId,
                        StaffId = staffId
                    });
                }
            }
        }

        var notes = new List<string>();
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("note_"))
            {
                var val = Request.Form[key].ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    notes.Add(val);
                }
            }
        }

        await _service.SaveCleaningChecklistAsync(userId, checklistAllDone, remarks, date, details, notes);

        return Redirect($"/addupdcleaningchecks/{date:dd-MM-yyyy}?msg={Uri.EscapeDataString("Cleaning Checklist has been saved successfully.")}");
    }

    [HttpGet("haccp")]
    public async Task<IActionResult> Haccp()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();
        return View("~/Views/DailyChecklist/Haccp.cshtml");
    }

    [HttpGet("addupduploadchecks")]
    public async Task<IActionResult> UploadChecks([FromQuery] string? msg = null, [FromQuery] long? editId = null)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        long webshopId = 82;
        long.TryParse(webshopIdStr, out webshopId);

        var categories = await _service.GetChecklistCategoriesAsync();
        var staffList = await _service.GetBakeryUsersAsync(webshopId);

        var categoryFiles = new Dictionary<long, List<UploadedFileItem>>();
        foreach (var cat in categories)
        {
            categoryFiles[cat.ChecklistCatId] = await _service.GetUploadedFilesByCatIdAsync(cat.ChecklistCatId);
        }

        UploadedFileItem? editItem = null;
        if (editId.HasValue && editId.Value > 0)
        {
            editItem = await _service.GetUploadedFileByIdAsync(editId.Value);
        }

        ViewBag.Categories = categories;
        ViewBag.StaffList = staffList;
        ViewBag.CategoryFiles = categoryFiles;
        ViewBag.SuccessMessage = msg;
        ViewBag.EditItem = editItem;
        ViewBag.TodayDate = DateTime.Today;

        return View("~/Views/DailyChecklist/UploadChecks.cshtml");
    }

    [HttpPost("addupduploadchecks/submit")]
    public async Task<IActionResult> SubmitUploadChecks(
        [FromForm] long checklistFileUploadedId,
        [FromForm] int checklistCatId,
        [FromForm] int staffId,
        [FromForm] string fileTitle,
        [FromForm] string docDateStr,
        [FromForm] string remarks,
        IFormFile? fileUpload)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        DateTime docDate = DateTime.Today;
        if (!string.IsNullOrEmpty(docDateStr))
        {
            if (DateTime.TryParseExact(docDateStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                docDate = parsedDate;
            }
            else if (DateTime.TryParse(docDateStr, out parsedDate))
            {
                docDate = parsedDate;
            }
        }

        string fileName = "";
        string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "docs");

        if (fileUpload != null && fileUpload.Length > 0)
        {
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            string safeTitle = string.IsNullOrWhiteSpace(fileTitle) ? fileUpload.FileName : fileTitle;
            string safeBase = System.Text.RegularExpressions.Regex.Replace(safeTitle.ToLower(), @"[^a-z0-9_.-]", "-");
            fileName = $"{safeBase}-{DateTime.Now.Ticks}{Path.GetExtension(fileUpload.FileName)}";

            string filePath = Path.Combine(uploadDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await fileUpload.CopyToAsync(stream);
            }

            if (checklistFileUploadedId > 0)
            {
                var oldItem = await _service.GetUploadedFileByIdAsync(checklistFileUploadedId);
                if (oldItem != null && !string.IsNullOrEmpty(oldItem.File))
                {
                    string oldPath = Path.Combine(uploadDir, oldItem.File);
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }
            }
        }
        else if (checklistFileUploadedId > 0)
        {
            var oldItem = await _service.GetUploadedFileByIdAsync(checklistFileUploadedId);
            if (oldItem != null)
            {
                fileName = oldItem.File;
            }
        }

        if (string.IsNullOrEmpty(fileTitle))
        {
            fileTitle = fileName;
        }

        await _service.SaveUploadedFileAsync(checklistFileUploadedId, checklistCatId, staffId, userId, fileName, docDate, fileTitle, remarks);

        return Redirect($"/addupduploadchecks?msg={Uri.EscapeDataString("Checklist document has been saved successfully.")}");
    }

    [HttpPost("addupduploadchecks/delete/{id}")]
    public async Task<IActionResult> DeleteUploadChecks(long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        var item = await _service.GetUploadedFileByIdAsync(id);
        if (item != null)
        {
            string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "docs");
            string filePath = Path.Combine(uploadDir, item.File);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            await _service.DeleteUploadedFileAsync(id);
        }

        return Redirect("/addupdcleaningchecks?msg=" + Uri.EscapeDataString("Document has been deleted successfully."));
    }

    [HttpGet("addupdatedairy")]
    public async Task<IActionResult> AddUpdateDairy()
    {
        await PopulateLayoutMetadataAsync();
        return View("~/Views/DailyChecklist/AddUpdateDairy.cshtml");
    }
}
