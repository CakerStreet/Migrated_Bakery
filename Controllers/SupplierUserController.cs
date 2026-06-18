using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Supplier Users module.
/// Route: /supplierusers
/// Migrated from supplierusers.aspx.
/// Access control: type 4 forced to usertype=3, type 3 redirect to /mywebstore.
/// </summary>
[Route("supplierusers")]
public class SupplierUserController : Controller
{
    private readonly SupplierUserService _userService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public SupplierUserController(
        SupplierUserService userService,
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
        [FromQuery] long? sid,
        [FromQuery] string? search,
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
            return Redirect("/businesslogin?returl=/supplierusers");

        var loggedInUserType = int.TryParse(userTypeStr, out var ut) ? ut : 0;

        // STEP 2: Access control routing
        if (loggedInUserType == 3)
            return Redirect("/mywebstore");

        if (loggedInUserType == 4)
        {
            // Type 4 forced to usertype=3 (legacy behavior)
            // They can still view supplier users page but with restricted access
        }

        // STEP 3: Fetch suppliers for dropdown
        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var suppliers = await _userService.GetSuppliersForDropdownAsync(wid);

        // STEP 4: Fetch supplier users (only if supplier selected)
        SupplierUserListResult result = new();
        if (sid.HasValue && sid.Value > 0)
        {
            var pageSize = 23;
            result = await _userService.GetSupplierUsersAsync(
                wid, sid, filterstatus, search, pageno, pageSize);
        }

        // STEP 5: Set ViewBag for layout
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userTypeStr, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        // STEP 6: Set ViewBag for page data
        ViewBag.Items = result.Items;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.TotalCount = result.TotalCount;
        ViewBag.CurrentPage = pageno;
        ViewBag.Search = search ?? "";
        ViewBag.FilterStatus = filterstatus ?? 0;
        ViewBag.Suppliers = suppliers;
        ViewBag.SelectedSupplierId = sid ?? 0;
        ViewBag.LoggedInUserType = loggedInUserType;

        return View("~/Views/SupplierUser/Index.cshtml");
    }

    // ─── Add (POST) ────────────────────────────────────────────────────────────

    [HttpPost("add")]
    public async Task<IActionResult> Add(
        [FromForm] string email,
        [FromForm] string name,
        [FromForm] string password,
        [FromForm] long supplierId)
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

        if (supplierId <= 0)
            return Json(new { success = false, message = "Supplier is required." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new AddSupplierUserModel
        {
            Email = email,
            Name = name,
            Password = password ?? "",
            SupplierId = supplierId
        };

        var success = await _userService.AddSupplierUserAsync(model, wid, supplierId);
        if (!success)
            return Json(new { success = false, message = "Email ID already exists!" });

        return Json(new { success = true, message = "User Added successfully!" });
    }

    // ─── Save (POST) — Bulk Save with JSON body ────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] List<BulkSaveSupplierUserModel> items)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        if (items == null || items.Count == 0)
            return Json(new { success = false, message = "No items to save." });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var success = await _userService.BulkSaveAsync(items, wid);
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
