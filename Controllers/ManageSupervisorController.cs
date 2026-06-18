using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Bakery Supervisor module.
/// Route: /managesbakeryuperviser (legacy typo preserved)
/// Migrated from managesbakeryuperviser.aspx.
/// Single record form — one supervisor per bakery.
/// No service file needed — inline SQL.
/// </summary>
[Route("managesbakeryuperviser")]
public class ManageSupervisorController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageSupervisorController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
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
            return Redirect("/businesslogin?returl=/managesbakeryuperviser");

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;

        // Load existing supervisor record
        string fullName = "", email = "", mobile = "";
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT BakerySuperviser_FullName, BakerySuperviser_EmailID, BakerySuperviser_Mobile
                    FROM tbl_BakerySuperviser
                    WHERE BakerySuperviser_bakeryID = @bakeryId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@bakeryId", wid);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            fullName = reader.IsDBNull(0) ? "" : reader.GetString(0);
            email = reader.IsDBNull(1) ? "" : reader.GetString(1);
            mobile = reader.IsDBNull(2) ? "" : reader.GetString(2);
        }

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

        ViewBag.FullName = fullName;
        ViewBag.Email = email;
        ViewBag.Mobile = mobile;

        return View("~/Views/ManageSupervisor/Index.cshtml");
    }

    // ─── Save (POST) ──────────────────────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] string fullName,
        [FromForm] string email,
        [FromForm] string mobile)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var wid = long.TryParse(webshopId, out var w) ? w : 0L;
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Check if record exists
            var checkSql = "SELECT COUNT(1) FROM tbl_BakerySuperviser WHERE BakerySuperviser_bakeryID = @bakeryId";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@bakeryId", wid);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                // UPDATE
                var updateSql = @"UPDATE tbl_BakerySuperviser SET
                                      BakerySuperviser_FullName = @fullName,
                                      BakerySuperviser_EmailID = @email,
                                      BakerySuperviser_Mobile = @mobile,
                                      BakerySuperviser_modifiedOn = @modifiedOn
                                  WHERE BakerySuperviser_bakeryID = @bakeryId";

                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@fullName", fullName ?? "");
                updateCmd.Parameters.AddWithValue("@email", email ?? "");
                updateCmd.Parameters.AddWithValue("@mobile", mobile ?? "");
                updateCmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@bakeryId", wid);

                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // INSERT
                var insertSql = @"INSERT INTO tbl_BakerySuperviser
                                      (BakerySuperviser_bakeryID, BakerySuperviser_FullName, BakerySuperviser_EmailID, BakerySuperviser_Mobile, BakerySuperviser_modifiedOn)
                                  VALUES (@bakeryId, @fullName, @email, @mobile, @modifiedOn)";

                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@bakeryId", wid);
                insertCmd.Parameters.AddWithValue("@fullName", fullName ?? "");
                insertCmd.Parameters.AddWithValue("@email", email ?? "");
                insertCmd.Parameters.AddWithValue("@mobile", mobile ?? "");
                insertCmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);

                await insertCmd.ExecuteNonQueryAsync();
            }

            return Json(new { success = true, message = "Supervisor Detail changed Successfully" });
        }
        catch
        {
            return Json(new { success = false, message = "Failed to save supervisor details." });
        }
    }
}
