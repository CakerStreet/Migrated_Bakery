using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Franchise Product module.
/// Route: /manageproductwithfranchise
/// Migrated from manageproductwithfranchise.aspx.
/// Simplified read-only list with franchise edit/remove.
/// No service file — inline SQL.
/// </summary>
[Route("manageproductwithfranchise")]
public class FranchiseProductController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public FranchiseProductController(
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    // ─── Index (GET) ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long franchiseid = 0, [FromQuery] string? msg = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect("/businesslogin?returl=/manageproductwithfranchise");

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

        // Load franchise dropdown
        var franchises = new List<FranchiseDropdownItem>();
        var businessConn = _config.GetConnectionString("BusinessConnection") ?? "";

        await using (var conn = new SqlConnection(businessConn))
        {
            await conn.OpenAsync();

            var dropdownSql = @"SELECT ID, Title + ' (' + CASE [Status] WHEN 1 THEN 'Under Proposal' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Disapproved' ELSE '' END + ')' AS DisplayTitle
                FROM tbl_tempFranchise WHERE IsDeleted = 0 ORDER BY Title";

            await using var cmd = new SqlCommand(dropdownSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                franchises.Add(new FranchiseDropdownItem
                {
                    Id = reader.GetInt64(0),
                    DisplayTitle = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        ViewBag.Franchises = franchises;
        ViewBag.SelectedFranchiseId = franchiseid;
        ViewBag.Message = msg;

        // If franchise selected, load details and products
        if (franchiseid > 0)
        {
            // Load franchise details for edit form
            await using (var conn = new SqlConnection(businessConn))
            {
                await conn.OpenAsync();

                var detailSql = @"SELECT Title, [Status], isActive FROM tbl_tempFranchise WHERE ID = @fid AND IsDeleted = 0";
                await using var cmd = new SqlCommand(detailSql, conn);
                cmd.Parameters.AddWithValue("@fid", franchiseid);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    ViewBag.FranchiseTitle = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    ViewBag.FranchiseStatus = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    ViewBag.FranchiseIsActive = !reader.IsDBNull(2) && reader.GetBoolean(2);
                }
            }

            // Load linked products from BusinessConnection
            var linkedProducts = new List<FranchiseProductItem>();
            await using (var conn = new SqlConnection(businessConn))
            {
                await conn.OpenAsync();

                var productSql = @"SELECT l.ProductID, l.Price, l.Min_StockReq, l.Total_Investment,
                       CASE l.Ordered WHEN 1 THEN 'Yes' ELSE 'No' END AS Ordered,
                       CASE l.Delivered WHEN 1 THEN 'Yes' ELSE 'No' END AS Delivered
                FROM tbl_lnkItem2tempfranchise l
                WHERE l.tempFranchise_Id = @franchiseId
                ORDER BY l.ID DESC";

                await using var cmd = new SqlCommand(productSql, conn);
                cmd.Parameters.AddWithValue("@franchiseId", franchiseid);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    linkedProducts.Add(new FranchiseProductItem
                    {
                        ProductId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                        Price = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                        MinStockReq = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        TotalInvestment = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                        Ordered = reader.IsDBNull(4) ? "No" : reader.GetString(4),
                        Delivered = reader.IsDBNull(5) ? "No" : reader.GetString(5)
                    });
                }
            }

            // Resolve product names from DefaultConnection
            if (linkedProducts.Count > 0)
            {
                var defaultConn = _config.GetConnectionString("DefaultConnection") ?? "";
                var productIds = linkedProducts.Select(p => p.ProductId).Where(id => id > 0).Distinct().ToList();

                if (productIds.Count > 0)
                {
                    await using var conn = new SqlConnection(defaultConn);
                    await conn.OpenAsync();

                    // Build parameterized IN clause
                    var paramNames = productIds.Select((id, idx) => $"@pid{idx}").ToList();
                    var nameSql = $"SELECT product_ID, product_name FROM tbl_products WHERE product_ID IN ({string.Join(",", paramNames)})";

                    await using var cmd = new SqlCommand(nameSql, conn);
                    for (int i = 0; i < productIds.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@pid{i}", productIds[i]);
                    }

                    var nameMap = new Dictionary<long, string>();
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var pid = reader.GetInt64(0);
                        var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        nameMap[pid] = name;
                    }

                    foreach (var p in linkedProducts)
                    {
                        p.ProductName = nameMap.GetValueOrDefault(p.ProductId, "(Unknown Product)");
                    }
                }
            }

            ViewBag.Products = linkedProducts;
        }

        return View("~/Views/FranchiseProduct/Index.cshtml");
    }

    // ─── Save Franchise Details (POST) ─────────────────────────────────────────

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] long franchiseId,
        [FromForm] string title,
        [FromForm] int status,
        [FromForm] int isActive)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var businessConn = _config.GetConnectionString("BusinessConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(businessConn);
            await conn.OpenAsync();

            // Check for duplicate title
            var checkSql = @"SELECT COUNT(1) FROM tbl_tempFranchise WHERE LOWER(Title) = LOWER(@title) AND ID != @fid AND IsDeleted = 0";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@title", title?.Trim() ?? "");
            checkCmd.Parameters.AddWithValue("@fid", franchiseId);
            var duplicateCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (duplicateCount > 0)
                return Json(new { success = false, message = "This franchise already exists" });

            // Update franchise
            var updateSql = @"UPDATE tbl_tempFranchise SET
                                Title = @title,
                                [Status] = @status,
                                isActive = @isActive,
                                ModifiedOn = @modifiedOn
                              WHERE ID = @fid";

            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@title", title?.Trim() ?? "");
            updateCmd.Parameters.AddWithValue("@status", status);
            updateCmd.Parameters.AddWithValue("@isActive", isActive == 1);
            updateCmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
            updateCmd.Parameters.AddWithValue("@fid", franchiseId);

            await updateCmd.ExecuteNonQueryAsync();

            return Json(new { success = true, message = "Franchise details updated successfully" });
        }
        catch
        {
            return Json(new { success = false, message = "Failed to save franchise details." });
        }
    }

    // ─── Remove Franchise (POST) ───────────────────────────────────────────────

    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromForm] long franchiseId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Json(new { success = false, message = "Unauthorized" });

        var businessConn = _config.GetConnectionString("BusinessConnection") ?? "";

        try
        {
            await using var conn = new SqlConnection(businessConn);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_tempFranchise SET IsDeleted = 1 WHERE ID = @fid";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@fid", franchiseId);

            await cmd.ExecuteNonQueryAsync();

            return Json(new { success = true, message = "Franchise has been removed successfully" });
        }
        catch
        {
            return Json(new { success = false, message = "Failed to remove franchise." });
        }
    }
}

// ─── View Models ───────────────────────────────────────────────────────────────

public class FranchiseDropdownItem
{
    public long Id { get; set; }
    public string DisplayTitle { get; set; } = "";
}

public class FranchiseProductItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Price { get; set; }
    public int MinStockReq { get; set; }
    public decimal TotalInvestment { get; set; }
    public string Ordered { get; set; } = "No";
    public string Delivered { get; set; } = "No";
}
