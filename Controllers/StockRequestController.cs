using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Stock Request module.
/// Route: /managestockrequest
/// Migrated from managestockrequest.aspx.
/// Module 20 permission check + HQ-only (webshopId == 82).
/// </summary>
[Route("managestockrequest")]
[Route("stockrequest")]
[Route("managestockrequest.aspx")]
public class StockRequestController : Controller
{
    private readonly StockRequestService _stockRequestService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public StockRequestController(
        StockRequestService stockRequestService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _stockRequestService = stockRequestService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] int status = 0,
        [FromQuery] int pageno = 1)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managestockrequest");

        // HQ-only check: webshopId must equal 82 (csbakeryid)
        var csBakeryId = _config["csbakeryid"] ?? "82";
        if (webshopId != csBakeryId)
            return Redirect("/mywebstore");

        // Module 20 permission check
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 20);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        var pageSize = 10;
        var result = await _stockRequestService.GetStockRequestsAsync(pageno, pageSize, search, status);
        var staffList = await _stockRequestService.GetStaffListAsync();

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

        ViewBag.Items = result.Items;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.PendingCount = result.PendingCount;
        ViewBag.POPendingCount = result.POPendingCount;
        ViewBag.POApprovedCount = result.POApprovedCount;
        ViewBag.SentToSupplierCount = result.SentToSupplierCount;
        ViewBag.CompletedCount = result.CompletedCount;
        ViewBag.DeclinedCount = result.DeclinedCount;
        ViewBag.CurrentPage = pageno;
        ViewBag.Search = search ?? "";
        ViewBag.Status = status;
        ViewBag.UserType = userType;
        ViewBag.UserId = userId;
        ViewBag.StaffList = staffList;

        return View("~/Views/StockRequest/Index.cshtml");
    }

    // ─── Save (POST) ──────────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] StockRequestSaveModel model)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var saveResult = await _stockRequestService.SaveStockRequestAsync(model, userId, wid);

        return Json(new { success = saveResult == "1", message = saveResult == "1" ? "Stock request saved successfully." : "Failed to save." });
    }

    // ─── Get By ID (GET) ──────────────────────────────────────────────────────

    [HttpGet("getbyid")]
    public async Task<IActionResult> GetById([FromQuery] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var detail = await _stockRequestService.GetByIdAsync(id);
        if (detail == null)
            return Json(new { success = false, message = "Stock request not found." });

        return Json(new { success = true, data = detail });
    }

    // ─── Approve (POST) ───────────────────────────────────────────────────────

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });
        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Only admin/manager can approve." });

        var success = await _stockRequestService.ApproveAsync(id);
        return Json(new { success, message = success ? "Stock request approved." : "Failed to approve." });
    }

    // ─── Decline (POST) ───────────────────────────────────────────────────────

    [HttpPost("decline")]
    public async Task<IActionResult> Decline([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });
        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Only admin/manager can decline." });

        var success = await _stockRequestService.DeclineAsync(id);
        return Json(new { success, message = success ? "Stock request declined." : "Failed to decline." });
    }

    // ─── Bulk Approve (POST) ──────────────────────────────────────────────────

    [HttpPost("bulkapprove")]
    public async Task<IActionResult> BulkApprove([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });
        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Only admin/manager can approve." });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _stockRequestService.BulkApproveAsync(idList);
        return Json(new { success, message = success ? "Stock requests approved." : "Failed to approve." });
    }

    // ─── Bulk Decline (POST) ──────────────────────────────────────────────────

    [HttpPost("bulkdecline")]
    public async Task<IActionResult> BulkDecline([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });
        if (userType != "1" && userType != "2")
            return Json(new { success = false, message = "Only admin/manager can decline." });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _stockRequestService.BulkDeclineAsync(idList);
        return Json(new { success, message = success ? "Stock requests declined." : "Failed to decline." });
    }

    // ─── Delete (POST) ────────────────────────────────────────────────────────

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromForm] long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var success = await _stockRequestService.DeleteAsync(id);
        return Json(new { success, message = success ? "Stock request deleted." : "Failed to delete." });
    }

    // ─── Bulk Delete (POST) ───────────────────────────────────────────────────

    [HttpPost("bulkdelete")]
    public async Task<IActionResult> BulkDelete([FromForm] string ids)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var idList = ParseIds(ids);
        if (idList.Count == 0)
            return Json(new { success = false, message = "No items selected." });

        var success = await _stockRequestService.BulkDeleteAsync(idList);
        return Json(new { success, message = success ? "Stock requests deleted." : "Failed to delete." });
    }

    // ─── Save Reply (POST) ────────────────────────────────────────────────────

    [HttpPost("savereply")]
    public async Task<IActionResult> SaveReply(
        [FromForm] long requestId,
        [FromForm] string name,
        [FromForm] string message)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var reply = await _stockRequestService.SaveReplyAsync(requestId, userId, name, message);
        if (reply == null)
            return Json(new { success = false, message = "Failed to save reply." });

        return Json(new
        {
            success = true,
            data = new
            {
                id = reply.Id,
                name = reply.Name,
                message = reply.Message,
                modifiedOn = reply.ModifiedOn.ToString("dd-MM | HH:mm")
            }
        });
    }

    // ─── Search Products (GET) ────────────────────────────────────────────────

    [HttpGet("searchproducts")]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string keyword,
        [FromQuery] string prdtype)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var products = await _stockRequestService.SearchProductsAsync(keyword ?? "", prdtype ?? "", wid);
        return Json(products);
    }

    // ─── Stock Location (GET) ─────────────────────────────────────────────────

    [HttpGet("stocklocation")]
    public async Task<IActionResult> StockLocation([FromQuery] long pid)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var html = await _stockRequestService.GetStockLocationHtmlAsync(pid, wid);
        return Json(new { success = true, html });
    }

    // ─── Check PO (GET) ──────────────────────────────────────────────────────

    [HttpGet("checkpo")]
    public async Task<IActionResult> CheckPO([FromQuery] long pid)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (userId == 0) return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var html = await _stockRequestService.CheckProductInActivePOAsync(pid, wid);
        return Json(new { success = true, html = html ?? "" });
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
