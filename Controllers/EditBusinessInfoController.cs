using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Edit Business Info module.
/// Route: /editbusinessinfo
/// Migrated from editStoreInfo.aspx.
/// Module 1 permission check.
/// </summary>
[Route("editbusinessinfo")]
[Route("editstoreinfo")]
[Route("editStoreInfo.aspx")]
public class EditBusinessInfoController : Controller
{
    private readonly EditBusinessInfoService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public EditBusinessInfoController(
        EditBusinessInfoService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/editbusinessinfo");

        // Module 1 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 1);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        // Load all data
        var businessInfo = await _service.GetBusinessInfoAsync(wid);
        var timings = await _service.GetTimingsAsync(wid);
        var specialDays = await _service.GetSpecialDaysAsync(wid);

        // Set ViewBag for layout
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        ViewBag.BusinessInfo = businessInfo;
        ViewBag.Timings = timings;
        ViewBag.SpecialDays = specialDays;
        ViewBag.UserType = userType;

        return View("~/Views/EditBusinessInfo/Index.cshtml");
    }

    // ─── Save Business Info (POST) ─────────────────────────────────────────────

    [HttpPost("saveinfo")]
    public async Task<IActionResult> SaveInfo(
        [FromForm] string businessName,
        [FromForm] string businessPhone,
        [FromForm] string orderEmail,
        [FromForm] string quoteSMSNo,
        [FromForm] string businessDescription,
        [FromForm] string county,
        [FromForm] string city,
        [FromForm] string storeURL,
        [FromForm] string postcode,
        [FromForm] string address)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new BusinessInfoSaveModel
        {
            BusinessName = businessName ?? "",
            BusinessPhone = businessPhone ?? "",
            OrderEmail = orderEmail ?? "",
            QuoteSMSNo = quoteSMSNo ?? "",
            BusinessDescription = businessDescription ?? "",
            County = county ?? "",
            City = city ?? "",
            StoreURL = storeURL ?? "",
            Postcode = postcode ?? "",
            Address = address ?? ""
        };

        var success = await _service.SaveBusinessInfoAsync(model, wid);
        return Json(new { success, message = success ? "Business info updated successfully." : "Failed to save business info." });
    }

    // ─── Save Timings (POST) ───────────────────────────────────────────────────

    [HttpPost("savetimings")]
    public async Task<IActionResult> SaveTimings([FromBody] List<StoreTimingSaveModel> timings)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        if (timings == null || timings.Count == 0)
            return Json(new { success = false, message = "No timing data provided." });

        var success = await _service.SaveTimingsAsync(timings, wid);
        return Json(new { success, message = success ? "Store timings updated successfully." : "Failed to save timings." });
    }

    // ─── Save Special Day (POST) ───────────────────────────────────────────────

    [HttpPost("savespecialday")]
    public async Task<IActionResult> SaveSpecialDay(
        [FromForm] long id,
        [FromForm] string date,
        [FromForm] int fromHour,
        [FromForm] int toHour,
        [FromForm] bool isClosed)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new SpecialDaySaveModel
        {
            Id = id,
            Date = date ?? "",
            FromHour = fromHour,
            ToHour = toHour,
            IsClosed = isClosed
        };

        var success = await _service.SaveSpecialDayAsync(model, wid);
        return Json(new { success, message = success ? "Special day saved successfully." : "Failed to save special day." });
    }

    // ─── Delete Special Day (POST) ─────────────────────────────────────────────

    [HttpPost("deletespecialday")]
    public async Task<IActionResult> DeleteSpecialDay([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var success = await _service.DeleteSpecialDayAsync(id);
        return Json(new { success, message = success ? "Special day deleted." : "Failed to delete special day." });
    }

    // ─── Save Delivery Settings (POST) ─────────────────────────────────────────

    [HttpPost("savedelivery")]
    public async Task<IActionResult> SaveDelivery(
        [FromForm] bool isDeliverable,
        [FromForm] decimal deliveryMinOrder,
        [FromForm] decimal deliveryMiles,
        [FromForm] bool isCollectable)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new DeliverySettingsSaveModel
        {
            IsDeliverable = isDeliverable,
            DeliveryMinOrder = deliveryMinOrder,
            DeliveryMiles = deliveryMiles,
            IsCollectable = isCollectable
        };

        var success = await _service.SaveDeliverySettingsAsync(model, wid);
        return Json(new { success, message = success ? "Delivery settings updated successfully." : "Failed to save delivery settings." });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> CheckModuleAccessAsync(int userId, int moduleId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        await using var conn = new SqlConnection(connectionString);
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
