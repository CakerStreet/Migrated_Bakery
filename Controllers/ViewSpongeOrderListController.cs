using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for viewing and managing sponge order details.
/// Route: /viewspongeorderlist
/// Migrated from viewspongeorderlist.aspx.
/// Interactive page allowing status updates on individual sponge order items.
/// Access: authenticated bakery users.
/// </summary>
[Route("viewspongeorderlist")]
[Route("viewspongeorderlist.aspx")]
public class ViewSpongeOrderListController : Controller
{
    private readonly IConfiguration _config;

    public ViewSpongeOrderListController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? spongelistID)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        if (!spongelistID.HasValue)
            return Redirect("/managespongeorderlist");

        var connectionString = _config.GetConnectionString("aboraboraboraaboraaborab");
        var model = new ViewSpongeOrderModel();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Load sponge order header
        var headerSql = @"SELECT spongeOrder_ID, spongeOrder_remarks, spongeOrder_TotalQty, 
                                 spongeOrder_ReqDate, spongeOrder_FromDate, spongeOrder_ToDate, spongeOrder_Status
                          FROM tbl_spongeOrder WHERE spongeOrder_ID = @id";
        await using (var cmd = new SqlCommand(headerSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", spongelistID.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                model.RequestId = Convert.ToInt64(reader["spongeOrder_ID"]);
                model.Remarks = reader["spongeOrder_remarks"]?.ToString() ?? "-";
                if (string.IsNullOrEmpty(model.Remarks)) model.Remarks = "-";
                model.TotalSponges = Convert.ToInt32(reader["spongeOrder_TotalQty"]);
                model.DeliveryDate = Convert.ToDateTime(reader["spongeOrder_ReqDate"]).ToLongDateString();
                model.RequestedDate = DateTime.Now.ToLongDateString();
                var fromDate = Convert.ToDateTime(reader["spongeOrder_FromDate"]).ToLongDateString();
                var toDate = Convert.ToDateTime(reader["spongeOrder_ToDate"]).ToLongDateString();
                model.OrderFromTo = $"{fromDate} - {toDate}";
                model.Status = Convert.ToInt32(reader["spongeOrder_Status"]);
            }
            else
            {
                return Redirect("/managespongeorderlist");
            }
        }

        // Load sponge order items
        var itemsSql = @"SELECT spongeOrderDet_ID, spongeOrderDet_Status, spongeOrderDet_SpongeTitle,
                                spongeOrderDet_PrdType, spongeOrderDet_shape, spongeOrderDet_size,
                                spongeOrderDet_sponge, spongeOrderDet_dietery, spongeOrderDet_qty
                         FROM tbl_spongeOrderDet 
                         WHERE spongeOrderDet_fkID = @id 
                         ORDER BY spongeOrderDet_PrdType";
        await using (var cmd = new SqlCommand(itemsSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", spongelistID.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                model.Items.Add(new SpongeOrderItem
                {
                    Id = Convert.ToInt64(reader["spongeOrderDet_ID"]),
                    Status = Convert.ToInt32(reader["spongeOrderDet_Status"]),
                    Title = reader["spongeOrderDet_SpongeTitle"]?.ToString() ?? "",
                    PrdType = Convert.ToInt32(reader["spongeOrderDet_PrdType"]),
                    Shape = reader["spongeOrderDet_shape"]?.ToString() ?? "",
                    Size = reader["spongeOrderDet_size"]?.ToString() ?? "",
                    Sponge = reader["spongeOrderDet_sponge"]?.ToString() ?? "",
                    Dietary = reader["spongeOrderDet_dietery"]?.ToString() ?? "",
                    Qty = Convert.ToInt32(reader["spongeOrderDet_qty"])
                });
            }
        }

        ViewBag.SpongeListId = spongelistID.Value;
        return View("~/Views/ViewSpongeOrderList/Index.cshtml", model);
    }

    /// <summary>
    /// Update status of selected sponge order items.
    /// </summary>
    [HttpPost("updatestatus")]
    public async Task<IActionResult> UpdateStatus([FromForm] long spongelistID, [FromForm] string selectedIds, [FromForm] int status)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        if (string.IsNullOrEmpty(selectedIds))
            return Json(new { success = false, message = "No items selected" });

        var connectionString = _config.GetConnectionString("aboraboraboraaboraaborab");

        // Validate and sanitize IDs (must be numeric)
        var idList = selectedIds.Split(',').Where(id => long.TryParse(id.Trim(), out _)).ToList();
        if (!idList.Any())
            return Json(new { success = false, message = "Invalid IDs" });

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Update item statuses
        var updateSql = $"UPDATE tbl_spongeOrderDet SET spongeOrderDet_Status = @status WHERE spongeOrderDet_ID IN ({string.Join(",", idList)})";
        await using (var cmd = new SqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("@status", status);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update parent order status via stored procedure
        await using (var spCmd = new SqlCommand("dbo.USP_UpdateSpongeOrderStatus", conn))
        {
            spCmd.CommandType = CommandType.StoredProcedure;
            spCmd.Parameters.AddWithValue("@id", spongelistID);
            await spCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true, message = "Record(s) Updated Successfully" });
    }
}

public class ViewSpongeOrderModel
{
    public long RequestId { get; set; }
    public string Remarks { get; set; } = "-";
    public int TotalSponges { get; set; }
    public string DeliveryDate { get; set; } = "";
    public string RequestedDate { get; set; } = "";
    public string OrderFromTo { get; set; } = "";
    public int Status { get; set; }
    public List<SpongeOrderItem> Items { get; set; } = new();
}

public class SpongeOrderItem
{
    public long Id { get; set; }
    public int Status { get; set; }
    public string Title { get; set; } = "";
    public int PrdType { get; set; }
    public string Shape { get; set; } = "";
    public string Size { get; set; } = "";
    public string Sponge { get; set; } = "";
    public string Dietary { get; set; } = "";
    public int Qty { get; set; }
}
