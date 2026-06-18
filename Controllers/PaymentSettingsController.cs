using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Payment Settings module.
/// Route: /seller-payment-settings
/// Migrated from sellerPaymentSettings.aspx.
/// Module 2 permission check.
/// Feature flag: PaymentSettings:Enabled (default OFF = read-only mode).
/// </summary>
[Route("seller-payment-settings")]
[Route("sellerpaymentsettings")]
[Route("sellerPaymentSettings.aspx")]
public class PaymentSettingsController : Controller
{
    private readonly PaymentSettingsService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public PaymentSettingsController(
        PaymentSettingsService service,
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
            return Redirect("/businesslogin?returl=/seller-payment-settings");

        // Module 2 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 2);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var settings = await _service.GetPaymentSettingsAsync(wid);

        // Feature flag check for view display
        var isEnabled = _config["PaymentSettings:Enabled"] == "true";

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

        ViewBag.Settings = settings;
        ViewBag.IsEnabled = isEnabled;
        ViewBag.UserType = userType;

        return View("~/Views/PaymentSettings/Index.cshtml");
    }

    // ─── Save (POST) ──────────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] string bankName,
        [FromForm] string accountName,
        [FromForm] string accountNo,
        [FromForm] string ifscCode,
        [FromForm] string accountType,
        [FromForm] string sortCode,
        [FromForm] string swiftCode,
        [FromForm] string routingNo)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        // Feature flag check — MUST be "true" to allow saves
        if (_config["PaymentSettings:Enabled"] != "true")
        {
            return Json(new { success = false, message = "Payment settings update disabled in migration mode. Contact admin to enable." });
        }

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        var model = new PaymentSettingsSaveModel
        {
            BankName = bankName ?? "",
            AccountName = accountName ?? "",
            AccountNo = accountNo ?? "",
            IFSCCode = ifscCode ?? "",
            AccountType = accountType ?? "",
            SortCode = sortCode ?? "",
            SwiftCode = swiftCode ?? "",
            RoutingNo = routingNo ?? ""
        };

        var success = await _service.SavePaymentSettingsAsync(model, wid, userId);
        return Json(new { success, message = success ? "Bank information saved successfully!" : "Failed to save payment settings." });
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
