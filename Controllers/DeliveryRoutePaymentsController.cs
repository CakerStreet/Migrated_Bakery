using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;
using System.Globalization;

namespace CakerStreet.Business.Controllers;

// ─── Request Models ────────────────────────────────────────────────────────────

public class MarkRoutesPaidRequest
{
    public List<long> RouteIds { get; set; } = new();
    public string Remarks { get; set; } = "";
}

/// <summary>
/// Controller for the Delivery Route Payments module.
/// Route: /managedeliveryroutespayments
/// Migrated from managedeliveryroutesPayments.aspx.
/// Module 16 auth check. Payout mutations are FEATURE FLAGGED (DeliveryRoutePayments:Enabled).
/// </summary>
[Route("managedeliveryroutespayments")]
public class DeliveryRoutePaymentsController : Controller
{
    private readonly DeliveryRoutePaymentsService _paymentsService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public DeliveryRoutePaymentsController(
        DeliveryRoutePaymentsService paymentsService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _paymentsService = paymentsService;
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? from = null, string? to = null,
        int driverID = -1, int status = -1, int pageno = 1)
    {
        // Auth check from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin?returl=/managedeliveryroutespayments");

        // Permission check: userType 1/2 auto-allowed, else check module 16
        if (userType != "1" && userType != "2")
        {
            var hasAccess = await CheckModuleAccessAsync(userId, 16);
            if (!hasAccess)
                return Redirect("/businessorders");
        }

        // Default date range: last 7 days to today (matching legacy redirect behavior)
        DateTime fromDate, toDate;

        if (!string.IsNullOrEmpty(from) && DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
        {
            fromDate = parsedFrom;
        }
        else
        {
            fromDate = DateTime.Today.AddDays(-7);
        }

        if (!string.IsNullOrEmpty(to) && DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
        {
            toDate = parsedTo;
        }
        else
        {
            toDate = DateTime.Today;
        }

        if (!long.TryParse(webshopId, out var webshopIdLong))
            return Redirect("/businesslogin?returl=/managedeliveryroutespayments");

        // Get drivers for dropdown
        var drivers = await _paymentsService.GetDriversAsync(webshopIdLong);

        // Get routes
        var result = await _paymentsService.GetRoutesAsync(
            webshopIdLong, fromDate, toDate, driverID, status, pageno, 24);

        // Feature flag
        var paymentsEnabled = _config.GetValue<bool>("DeliveryRoutePayments:Enabled", false);

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

        // Pass data to view
        ViewBag.Drivers = drivers;
        ViewBag.RoutePayments = result;
        ViewBag.FromDate = fromDate.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.ToString("yyyy-MM-dd");
        ViewBag.SelectedDriverId = driverID;
        ViewBag.SelectedStatus = status;
        ViewBag.CurrentPage = pageno;
        ViewBag.PaymentsEnabled = paymentsEnabled;

        return View("~/Views/DeliveryRoutePayments/Index.cshtml");
    }

    // ─── Mark Paid (POST — FEATURE FLAGGED) ────────────────────────────────────

    [HttpPost("markpaid")]
    public async Task<IActionResult> MarkPaid([FromBody] MarkRoutesPaidRequest? request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        // FEATURE FLAG CHECK
        var paymentsEnabled = _config.GetValue<bool>("DeliveryRoutePayments:Enabled", false);

        if (!paymentsEnabled)
        {
            return Json(new { success = false, message = "Route payments disabled in migration mode" });
        }

        if (request == null || request.RouteIds.Count == 0)
            return Json(new { success = false, message = "No routes selected" });

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            // 1. Get unpaid routes with their charges
            var routeIdParams = request.RouteIds.Select((id, i) => $"@rid{i}").ToList();
            var inClause = string.Join(",", routeIdParams);
            var selectSql = $"SELECT route_ID, ISNULL(route_DriverCharges, 0) FROM tbl_deliveryRoute WHERE route_isChargePaid = 0 AND route_ID IN ({inClause})";

            var routeAmounts = new List<(long RouteId, decimal Amount)>();
            await using (var selectCmd = new SqlCommand(selectSql, conn, tx))
            {
                for (int i = 0; i < request.RouteIds.Count; i++)
                    selectCmd.Parameters.AddWithValue($"@rid{i}", request.RouteIds[i]);

                await using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    routeAmounts.Add((Convert.ToInt64(reader.GetValue(0)), reader.GetDecimal(1)));
                }
            }

            if (routeAmounts.Count == 0)
            {
                tx.Rollback();
                return Json(new { success = false, message = "No unpaid routes found for the selected IDs." });
            }

            decimal totalAmt = routeAmounts.Sum(r => r.Amount);
            string txRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? $"PAID-{DateTime.Now:yyyyMMddHHmmss}" : request.Remarks.Trim();

            // 2. INSERT tbl_RoutePayment (header)
            var insertPaymentSql = @"INSERT INTO tbl_RoutePayment 
                (RoutePayment_paidBy, RoutePayment_createdOn, RoutePayment_totalAmt, RoutePayment_remarks)
                VALUES (@paidBy, GETDATE(), @totalAmt, @remarks);
                SELECT SCOPE_IDENTITY();";

            long paymentId;
            await using (var insertCmd = new SqlCommand(insertPaymentSql, conn, tx))
            {
                insertCmd.Parameters.AddWithValue("@paidBy", (long)userId);
                insertCmd.Parameters.AddWithValue("@totalAmt", totalAmt);
                insertCmd.Parameters.AddWithValue("@remarks", txRemarks);
                paymentId = Convert.ToInt64(await insertCmd.ExecuteScalarAsync());
            }

            // 3. INSERT tbl_RoutePaymentDet for each route
            foreach (var (routeId, amount) in routeAmounts)
            {
                var insertDetSql = @"INSERT INTO tbl_RoutePaymentDet 
                    (RoutePaymentDet_routeID, RoutePaymentDet_paymentID, RoutePaymentDet_createdOn, RoutePaymentDet_amt)
                    VALUES (@routeId, @paymentId, GETDATE(), @amt)";
                await using var detCmd = new SqlCommand(insertDetSql, conn, tx);
                detCmd.Parameters.AddWithValue("@routeId", routeId);
                detCmd.Parameters.AddWithValue("@paymentId", paymentId);
                detCmd.Parameters.AddWithValue("@amt", amount);
                await detCmd.ExecuteNonQueryAsync();
            }

            // 4. UPDATE tbl_deliveryRoute: mark paid
            var paidRouteIds = routeAmounts.Select((r, i) => $"@prid{i}").ToList();
            var updateSql = $@"UPDATE tbl_deliveryRoute 
                SET route_isChargePaid = 1, route_PaidRemarks = @remarks 
                WHERE route_ID IN ({string.Join(",", paidRouteIds)})";
            await using (var updateCmd = new SqlCommand(updateSql, conn, tx))
            {
                updateCmd.Parameters.AddWithValue("@remarks", txRemarks);
                for (int i = 0; i < routeAmounts.Count; i++)
                    updateCmd.Parameters.AddWithValue($"@prid{i}", routeAmounts[i].RouteId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Json(new { success = true, message = $"Marked {routeAmounts.Count} route(s) as paid. Payment ID: {paymentId}, Total: £{totalAmt:0.00}" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to mark routes paid: {ex.Message}" });
        }
    }

    // ─── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the user has access to a specific module via tbl_moduleAssignment.
    /// </summary>
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
