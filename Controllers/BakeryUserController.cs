using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Bakery Users module.
/// Route: /bakeryuser
/// Migrated from bakeryusers.aspx.
/// Module 8 permission check.
/// Access control: type 1 all tabs, type 2/3/4 forced routing.
/// </summary>
[Route("bakeryuser")]
[Route("bakeryusers")]
public class BakeryUserController : Controller
{
    private readonly BakeryUserService _userService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakeryUserController(
        BakeryUserService userService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _userService = userService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] int? usertype,
        [FromQuery] int? usersubtype,
        [FromQuery] int? filterstatus,
        [FromQuery] int pageno = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userTypeStr = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        // STEP 1: Auth check
        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/bakeryuser");

        var loggedInUserType = int.TryParse(userTypeStr, out var ut) ? ut : 0;

        // STEP 2: Access control routing
        if (loggedInUserType == 3)
            return Redirect("/mywebstore");

        if (loggedInUserType == 4 && usertype == null)
            return Redirect("/bakeryuser?usertype=3");

        if (loggedInUserType == 4 && usertype != 3)
            return Redirect("/bakeryuser?usertype=3");

        // STEP 3: Module 8 permission check for non-admin/manager
        if (loggedInUserType != 1 && loggedInUserType != 2)
        {
            var hasAccess = await _userService.CheckModuleAccessAsync(userId, 8);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // STEP 4: Determine effective user type filter
        int effectiveUserType = usertype ?? 1; // default to Admin tab
        if (loggedInUserType == 2 || loggedInUserType == 3)
            effectiveUserType = 3; // Manager forced to Staff

        // STEP 5: Determine effective staff type (only relevant for Staff tab)
        int? effectiveStaffType = (effectiveUserType == 3) ? (usersubtype ?? 0) : null;

        // STEP 6: Fetch data
        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var pageSize = 23;
        var result = await _userService.GetBakeryUsersAsync(
            wid, effectiveUserType, effectiveStaffType, filterstatus, search, pageno, pageSize);

        // STEP 7: Set ViewBag for layout
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userTypeStr, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        // STEP 8: Set ViewBag for page data
        ViewBag.Items = result.Items;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.TotalCount = result.TotalCount;
        ViewBag.CurrentPage = pageno;
        ViewBag.Search = search ?? "";
        ViewBag.UserType = effectiveUserType;
        ViewBag.StaffType = effectiveStaffType ?? 0;
        ViewBag.FilterStatus = filterstatus ?? 0;
        ViewBag.LoggedInUserType = loggedInUserType;
        ViewBag.ShowAllTabs = (loggedInUserType == 1);
        ViewBag.ShowStaffFields = (effectiveUserType == 3);

        return View("~/Views/BakeryUser/Index.cshtml");
    }

    // ─── Add (POST) ────────────────────────────────────────────────────────────

    [HttpPost("add")]
    public async Task<IActionResult> Add(
        [FromForm] string email,
        [FromForm] string name,
        [FromForm] string password,
        [FromForm] string? phone,
        [FromForm] int userType)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        // Validate required fields
        if (string.IsNullOrWhiteSpace(email))
            return Json(new { success = false, message = "Email is required." });

        if (string.IsNullOrWhiteSpace(name))
            return Json(new { success = false, message = "Name is required." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new AddBakeryUserModel
        {
            Email = email,
            Name = name,
            Password = password ?? "",
            Phone = phone,
            UserType = userType
        };

        var success = await _userService.AddBakeryUserAsync(model, wid);
        if (!success)
            return Json(new { success = false, message = "Email ID already exists!" });

        return Json(new { success = true, message = "User Added successfully!" });
    }

    // ─── Update (POST) ─────────────────────────────────────────────────────────

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromForm] UpdateBakeryUserModel model)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var success = await _userService.UpdateBakeryUserAsync(model, wid);
        return Json(new { success, message = success ? "User updated successfully." : "Failed to update user." });
    }

    // ─── Save (POST) — Bulk Save with JSON body ────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] List<BulkSaveBakeryUserModel> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (items == null || items.Count == 0)
            return Json(new { success = false, message = "No items to save." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var success = await _userService.BulkSaveAsync(items, wid, userId);
        return Json(new { success, message = success ? "Records saved successfully." : "Failed to save records." });
    }

    // ─── Bulk Active (POST) ───────────────────────────────────────────────────

    [HttpPost("bulkactive")]
    public async Task<IActionResult> BulkActive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _userService.BulkSetActiveAsync(idList, wid, true);
        return Json(new { success, message = success ? "Users set to active." : "Failed to update." });
    }

    // ─── Bulk Inactive (POST) ─────────────────────────────────────────────────

    [HttpPost("bulkinactive")]
    public async Task<IActionResult> BulkInactive([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _userService.BulkSetActiveAsync(idList, wid, false);
        return Json(new { success, message = success ? "Users set to inactive." : "Failed to update." });
    }

    // ─── Bulk Delete (POST) ───────────────────────────────────────────────────

    [HttpPost("bulkdelete")]
    public async Task<IActionResult> BulkDelete([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var idList = ParseIds(ids);

        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _userService.BulkDeleteAsync(idList, wid);
        return Json(new { success, message = success ? "Users deleted successfully." : "Failed to delete." });
    }

    // ─── Remove (POST) — Single user soft remove ──────────────────────────────

    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var success = await _userService.RemoveUserAsync(id, wid);
        return Json(new { success, message = success ? "User removed successfully." : "Failed to remove user." });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static List<long> ParseIds(string ids)
    {
        var result = new List<long>();
        if (string.IsNullOrEmpty(ids)) return result;

        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part.Trim(), out var id))
                result.Add(id);
        }
        return result;
    }
}
