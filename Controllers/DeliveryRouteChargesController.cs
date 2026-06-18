using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Manage Delivery Route Charges module.
/// Route: /managedeliveryroutecharges (legacy URL preserved)
/// Migrated from manageDeliveryRouteCharges.aspx.
/// Admin-only (userType 1 or 2). Manages distance-based pricing tiers.
/// Tables: tbl_deliveryRouteChargeTemplate, tbl_deliveryRouteChargesCalc (BusinessConnection).
/// </summary>
[Route("managedeliveryroutecharges")]
public class DeliveryRouteChargesController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public DeliveryRouteChargesController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long ID = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/managedeliveryroutecharges");

        // Admin only
        if (userType != "1" && userType != "2")
            return Redirect("/mywebstore");

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        long wid = long.Parse(webshopId);

        // Load templates
        var templates = new List<ChargeTemplate>();
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT deliveryRouteChargeTemplate_ID, deliveryRouteChargeTemplate_title, deliveryRouteChargeTemplate_displayOrder
                        FROM tbl_deliveryRouteChargeTemplate 
                        WHERE deliveryRouteChargeTemplate_isActive = 1 AND deliveryRouteChargeTemplate_webstoreID = @wid
                        ORDER BY deliveryRouteChargeTemplate_displayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                templates.Add(new ChargeTemplate
                {
                    Id = Convert.ToInt64(reader.GetValue(0)),
                    Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    DisplayOrder = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
                });
            }
        }

        // Load charge bands for selected template
        var bands = new List<ChargeBand>();
        if (ID > 0)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var sql = @"SELECT routeChargesCalc_ID, routeChargesCalc_minDistance, routeChargesCalc_maxDistance,
                               routeChargesCalc_amount, routeChargesCalc_priceType
                        FROM tbl_deliveryRouteChargesCalc 
                        WHERE routeChargesCalc_webstoreID = @wid AND routeChargesCalc_templateID = @tid
                        ORDER BY routeChargesCalc_maxDistance";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            cmd.Parameters.AddWithValue("@tid", ID);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                bands.Add(new ChargeBand
                {
                    Id = Convert.ToInt64(reader.GetValue(0)),
                    MinDistance = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)),
                    MaxDistance = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2)),
                    Amount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    PriceType = reader.IsDBNull(4) ? 1 : Convert.ToInt32(reader.GetValue(4))
                });
            }
        }

        // ViewBag
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        ViewBag.Templates = templates;
        ViewBag.SelectedTemplateId = ID;
        ViewBag.Bands = bands;

        return View("~/Views/DeliveryRouteCharges/Index.cshtml");
    }

    // ─── Save Template (POST) ─────────────────────────────────────────────────

    [HttpPost("savetemplate")]
    public async Task<IActionResult> SaveTemplate([FromForm] long id, [FromForm] string title, [FromForm] int displayOrder)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";

        if (userId == 0 || userType != "1" && userType != "2")
            return Json(new { success = false, message = "Access denied" });

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        long wid = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            long templateId;
            if (id > 0)
            {
                var sql = @"UPDATE tbl_deliveryRouteChargeTemplate SET
                    deliveryRouteChargeTemplate_title = @title,
                    deliveryRouteChargeTemplate_displayOrder = @order,
                    deliveryRouteChargeTemplate_modifiedOn = GETDATE(),
                    deliveryRouteChargeTemplate_modifiedBy = @userId
                    WHERE deliveryRouteChargeTemplate_ID = @id AND deliveryRouteChargeTemplate_webstoreID = @wid";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@title", title?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@order", displayOrder);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@wid", wid);
                await cmd.ExecuteNonQueryAsync();
                templateId = id;
            }
            else
            {
                var sql = @"INSERT INTO tbl_deliveryRouteChargeTemplate 
                    (deliveryRouteChargeTemplate_title, deliveryRouteChargeTemplate_displayOrder,
                     deliveryRouteChargeTemplate_webstoreID, deliveryRouteChargeTemplate_isActive,
                     deliveryRouteChargeTemplate_modifiedOn, deliveryRouteChargeTemplate_modifiedBy)
                    VALUES (@title, @order, @wid, 1, GETDATE(), @userId);
                    SELECT SCOPE_IDENTITY();";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@title", title?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@order", displayOrder);
                cmd.Parameters.AddWithValue("@wid", wid);
                cmd.Parameters.AddWithValue("@userId", userId);
                templateId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            return Json(new { success = true, templateId, message = "Template saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── Save Bands (POST) ────────────────────────────────────────────────────

    [HttpPost("savebands")]
    public async Task<IActionResult> SaveBands([FromBody] SaveBandsRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";

        if (userId == 0 || userType != "1" && userType != "2")
            return Json(new { success = false, message = "Access denied" });

        if (request.TemplateId <= 0 || request.Bands == null || request.Bands.Count == 0)
            return Json(new { success = false, message = "No data to save." });

        var connectionString = _config.GetConnectionString("BusinessConnection") ?? "";
        long wid = long.Parse(webshopId);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            // Collect IDs to keep
            var keepIds = request.Bands.Where(b => b.Id > 0).Select(b => b.Id).ToList();

            // Delete bands not in the keep list
            if (keepIds.Count > 0)
            {
                var keepParams = keepIds.Select((id, i) => $"@kid{i}").ToList();
                var delSql = $"DELETE FROM tbl_deliveryRouteChargesCalc WHERE routeChargesCalc_webstoreID = @wid AND routeChargesCalc_templateID = @tid AND routeChargesCalc_ID NOT IN ({string.Join(",", keepParams)})";
                await using var delCmd = new SqlCommand(delSql, conn, tx);
                delCmd.Parameters.AddWithValue("@wid", wid);
                delCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                for (int i = 0; i < keepIds.Count; i++)
                    delCmd.Parameters.AddWithValue($"@kid{i}", keepIds[i]);
                await delCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var delSql = "DELETE FROM tbl_deliveryRouteChargesCalc WHERE routeChargesCalc_webstoreID = @wid AND routeChargesCalc_templateID = @tid";
                await using var delCmd = new SqlCommand(delSql, conn, tx);
                delCmd.Parameters.AddWithValue("@wid", wid);
                delCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                await delCmd.ExecuteNonQueryAsync();
            }

            // Upsert each band
            foreach (var band in request.Bands)
            {
                if (band.Id > 0)
                {
                    var updSql = @"UPDATE tbl_deliveryRouteChargesCalc SET
                        routeChargesCalc_minDistance = @min, routeChargesCalc_maxDistance = @max,
                        routeChargesCalc_amount = @amount, routeChargesCalc_priceType = @priceType,
                        routeChargesCalc_modifiedOn = GETDATE(), routeChargesCalc_modifiedBy = @userId
                        WHERE routeChargesCalc_ID = @id";
                    await using var updCmd = new SqlCommand(updSql, conn, tx);
                    updCmd.Parameters.AddWithValue("@min", band.MinDistance);
                    updCmd.Parameters.AddWithValue("@max", band.MaxDistance);
                    updCmd.Parameters.AddWithValue("@amount", band.Amount);
                    updCmd.Parameters.AddWithValue("@priceType", band.PriceType);
                    updCmd.Parameters.AddWithValue("@userId", userId);
                    updCmd.Parameters.AddWithValue("@id", band.Id);
                    await updCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var insSql = @"INSERT INTO tbl_deliveryRouteChargesCalc
                        (routeChargesCalc_webstoreID, routeChargesCalc_templateID, routeChargesCalc_minDistance,
                         routeChargesCalc_maxDistance, routeChargesCalc_amount, routeChargesCalc_priceType,
                         routeChargesCalc_modifiedOn, routeChargesCalc_modifiedBy)
                        VALUES (@wid, @tid, @min, @max, @amount, @priceType, GETDATE(), @userId)";
                    await using var insCmd = new SqlCommand(insSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@wid", wid);
                    insCmd.Parameters.AddWithValue("@tid", request.TemplateId);
                    insCmd.Parameters.AddWithValue("@min", band.MinDistance);
                    insCmd.Parameters.AddWithValue("@max", band.MaxDistance);
                    insCmd.Parameters.AddWithValue("@amount", band.Amount);
                    insCmd.Parameters.AddWithValue("@priceType", band.PriceType);
                    insCmd.Parameters.AddWithValue("@userId", userId);
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            return Json(new { success = true, message = "Delivery Route Charges saved successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed: " + ex.Message });
        }
    }

    // ─── Models ───────────────────────────────────────────────────────────────

    public class ChargeTemplate { public long Id { get; set; } public string Title { get; set; } = ""; public int DisplayOrder { get; set; } }
    public class ChargeBand { public long Id { get; set; } public double MinDistance { get; set; } public double MaxDistance { get; set; } public decimal Amount { get; set; } public int PriceType { get; set; } }
    public class SaveBandsRequest { public long TemplateId { get; set; } public List<ChargeBand> Bands { get; set; } = new(); }
}
