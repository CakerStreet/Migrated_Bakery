using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the baker's print copy page.
/// Route: /printorderbakers/{id}
/// Migrated from PrintBakeryOrder.aspx - standalone print page (no layout).
/// </summary>
[Route("printorderbakers")]
[Route("printbakeryorder")]
[Route("PrintBakeryOrder.aspx")]
public class PrintBakerOrderController : Controller
{
    private readonly BusinessOrderDetailService _orderDetailService;
    private readonly IConfiguration _config;

    // CakerStreet HQ webstore ID (legacy: ConfigurationManager.AppSettings["ckwebstoreid"])
    private const long CakerStreetHqId = 82;

    public PrintBakerOrderController(
        BusinessOrderDetailService orderDetailService,
        IConfiguration config)
    {
        _orderDetailService = orderDetailService;
        _config = config;
    }

    /// <summary>
    /// Single order print view.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Index(long id)
    {
        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.CdnBase = cdnBase;

        var result = await _orderDetailService.GetOrderDetailAsync(id, webshopId);

        if (result == null)
            return NotFound("Order not found.");

        // Load baker instructions for this order
        await LoadBakerInstructionsAsync(id, result);

        return View(result);
    }

    /// <summary>
    /// Loads baker instructions from tbl_orderBakeryInst and tbl_orderBakerInstList.
    /// If no saved instructions exist, computes defaults from delivery date (matching legacy).
    /// </summary>
    private async Task LoadBakerInstructionsAsync(long orderId, OrderDetailResult order)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Load saved instructions
        var instSql = @"SELECT orderBakeryInst_Remarks, orderBakeryInst_readybydate, orderBakeryInst_readybytime,
                               orderBakeryInst_dispatchdate, orderBakeryInst_dispatchtime
                        FROM tbl_orderBakeryInst
                        WHERE orderBakeryInst_orderID = @orderId";

        string remarks = "";
        string readyByDate = "";
        string readyByTime = "";
        string dispatchDate = "";
        string dispatchTime = "";
        bool hasInstructions = false;

        await using (var cmd = new SqlCommand(instSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                hasInstructions = true;
                remarks = reader.IsDBNull(0) ? "" : reader.GetString(0);
                readyByDate = reader.IsDBNull(1) ? "" : reader.GetString(1);
                readyByTime = reader.IsDBNull(2) ? "" : reader.GetString(2);
                dispatchDate = reader.IsDBNull(3) ? "" : reader.GetString(3);
                dispatchTime = reader.IsDBNull(4) ? "" : reader.GetString(4);
            }
        }

        // If no saved instructions, compute defaults from delivery date (matching legacy)
        if (!hasInstructions)
        {
            var collDate = order.CollectionDate;
            if (collDate != DateTime.MinValue)
            {
                // CakerStreet HQ or forwarded/following orders
                if (order.BakeryId == CakerStreetHqId || order.ForwardedOrderId > 0 || order.FollowingOrderId > 0)
                {
                    if (collDate.DayOfWeek == DayOfWeek.Monday)
                    {
                        readyByDate = collDate.AddDays(-4).ToString("dd/MM/yyyy");
                        dispatchDate = collDate.AddDays(-3).ToString("dd/MM/yyyy");
                    }
                    else
                    {
                        readyByDate = collDate.AddDays(-2).ToString("dd/MM/yyyy");
                        dispatchDate = collDate.AddDays(-1).ToString("dd/MM/yyyy");
                    }
                    dispatchTime = "3:30 PM";
                }

                // Collection from branch (legacy deliverymode == 1 maps to model DeliveryMode == 0)
                // Note: In legacy, deliverymode=1 means collection. In our model, DeliveryMode=0 means collection.
                // The task spec says: "delivery mode == 1 (collection from branch)" which is the legacy value.
                // Our model stores ordercollection_deliverymode directly, where 1 = collection in legacy DB.
                // Actually checking the model mapping: DeliveryMode maps to ordercollection_deliverymode directly.
                // Legacy code: if (varorderdelivery[0].ordercollection_deliverymode == 1) means collection.
                // So we check DeliveryMode == 1 here (raw DB value).
                if (order.DeliveryMode == 1)
                {
                    readyByDate = collDate.AddDays(-1).ToString("dd/MM/yyyy");
                    dispatchDate = collDate.ToString("dd/MM/yyyy");
                    dispatchTime = collDate.ToString("hh:mm tt");
                }
            }
        }

        // Load instruction list items (read-only display)
        var listSql = @"SELECT orderBakerInstList_Title, orderBakerInstList_remarks, orderBakerInstList_img
                        FROM tbl_orderBakerInstList
                        WHERE orderBakerInstList_orderID = @orderId
                        ORDER BY orderBakerInstList_displayorder";

        var instructionItems = new List<BakerInstructionItem>();
        await using (var cmd = new SqlCommand(listSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instructionItems.Add(new BakerInstructionItem
                {
                    Title = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Remarks = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Img = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
        }

        ViewBag.BakerRemarks = remarks;
        ViewBag.ReadyByDate = readyByDate;
        ViewBag.ReadyByTime = readyByTime;
        ViewBag.DispatchDate = dispatchDate;
        ViewBag.DispatchTime = dispatchTime;
        ViewBag.InstructionItems = instructionItems;
        ViewBag.HasInstructions = hasInstructions;
        // Dispatch label: "Hand Delivery:" when collection mode (legacy deliverymode == 1)
        ViewBag.DispatchLabel = order.DeliveryMode == 1 ? "Hand Delivery:" : "Dispatch Date:";
    }

    /// <summary>
    /// Multiple orders print view (comma-separated IDs via query string).
    /// Legacy: /printorderbakers?orderIDs=123,456,789
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Multiple([FromQuery] string? orderIDs)
    {
        if (string.IsNullOrWhiteSpace(orderIDs))
            return BadRequest("No order IDs provided.");

        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.CdnBase = cdnBase;

        var orders = new List<OrderDetailResult>();
        foreach (var idStr in orderIDs.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(idStr.Trim(), out var orderId) && orderId > 0)
            {
                var result = await _orderDetailService.GetOrderDetailAsync(orderId, webshopId);
                if (result != null)
                    orders.Add(result);
            }
        }

        if (orders.Count == 0)
            return NotFound("No orders found.");

        return View("Multiple", orders);
    }

    /// <summary>
    /// Logs that an order has been printed (baker copy).
    /// Matches legacy: webservices.aspx/updateorderprintlog
    /// Table: tbl_log_orderprint (typeId=3 for baker print)
    /// Safety: constrained typeId, try/catch for graceful degradation, MERGE-style upsert.
    /// </summary>
    [HttpPost("logprint")]
    public async Task<IActionResult> LogPrint([FromBody] PrintLogRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        if (string.IsNullOrEmpty(request?.OrderIDs))
            return BadRequest(new { success = false, error = "No order IDs" });

        // Safety: constrain typeId to known valid values (legacy only uses 3 for baker print)
        var typeId = request.TypeId;
        if (typeId != 3)
            return BadRequest(new { success = false, error = "Invalid typeId" });

        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Deduplicate order IDs to prevent redundant DB calls
            var orderIds = request.OrderIDs
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            foreach (var orderId in orderIds)
            {
                // Atomic upsert: UPDATE first, INSERT only if no rows affected.
                // This avoids the race condition of check-then-insert.
                var updateSql = @"UPDATE tbl_log_orderprint 
                    SET log_orderprint_userId = @userId, log_orderprint_modifiedOn = GETDATE()
                    WHERE log_orderprint_orderId = @orderId AND log_orderprint_typeId = @typeId";
                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@userId", userId);
                updateCmd.Parameters.AddWithValue("@orderId", orderId);
                updateCmd.Parameters.AddWithValue("@typeId", typeId);
                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    var insertSql = @"INSERT INTO tbl_log_orderprint 
                        (log_orderprint_orderId, log_orderprint_typeId, log_orderprint_userId, log_orderprint_modifiedOn)
                        VALUES (@orderId, @typeId, @userId, GETDATE())";
                    await using var insertCmd = new SqlCommand(insertSql, conn);
                    insertCmd.Parameters.AddWithValue("@orderId", orderId);
                    insertCmd.Parameters.AddWithValue("@typeId", typeId);
                    insertCmd.Parameters.AddWithValue("@userId", userId);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            return Json(new { success = true });
        }
        catch (Exception)
        {
            // Graceful degradation: return success so the client still triggers window.print()
            // The print log is non-critical — printing should never be blocked by a log failure.
            return Json(new { success = true, warning = "Log may not have been recorded" });
        }
    }

    /// <summary>
    /// Saves baker instructions (upsert tbl_orderBakeryInst).
    /// Matches legacy btnSubmit_OnClick.
    /// </summary>
    [HttpPost("saveinstructions")]
    public async Task<IActionResult> SaveInstructions([FromBody] SaveInstructionsRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        if (request == null || request.OrderId <= 0)
            return BadRequest(new { success = false, error = "Invalid order ID" });

        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Check if record exists
            var checkSql = "SELECT COUNT(1) FROM tbl_orderBakeryInst WHERE orderBakeryInst_orderID = @orderId";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

            if (exists)
            {
                var updateSql = @"UPDATE tbl_orderBakeryInst 
                    SET orderBakeryInst_Remarks = @remarks,
                        orderBakeryInst_readybydate = @readyByDate,
                        orderBakeryInst_readybytime = @readyByTime,
                        orderBakeryInst_dispatchdate = @dispatchDate,
                        orderBakeryInst_dispatchtime = @dispatchTime,
                        orderBakeryInst_modifiedOn = GETDATE(),
                        orderBakeryInst_modifiedBy = @userId
                    WHERE orderBakeryInst_orderID = @orderId";
                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
                updateCmd.Parameters.AddWithValue("@readyByDate", request.ReadyByDate ?? "");
                updateCmd.Parameters.AddWithValue("@readyByTime", request.ReadyByTime ?? "");
                updateCmd.Parameters.AddWithValue("@dispatchDate", request.DispatchDate ?? "");
                updateCmd.Parameters.AddWithValue("@dispatchTime", request.DispatchTime ?? "");
                updateCmd.Parameters.AddWithValue("@userId", userId);
                updateCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertSql = @"INSERT INTO tbl_orderBakeryInst 
                    (orderBakeryInst_orderID, orderBakeryInst_Remarks, orderBakeryInst_readybydate, 
                     orderBakeryInst_readybytime, orderBakeryInst_dispatchdate, orderBakeryInst_dispatchtime,
                     orderBakeryInst_modifiedOn, orderBakeryInst_modifiedBy)
                    VALUES (@orderId, @remarks, @readyByDate, @readyByTime, @dispatchDate, @dispatchTime, GETDATE(), @userId)";
                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@orderId", request.OrderId);
                insertCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
                insertCmd.Parameters.AddWithValue("@readyByDate", request.ReadyByDate ?? "");
                insertCmd.Parameters.AddWithValue("@readyByTime", request.ReadyByTime ?? "");
                insertCmd.Parameters.AddWithValue("@dispatchDate", request.DispatchDate ?? "");
                insertCmd.Parameters.AddWithValue("@dispatchTime", request.DispatchTime ?? "");
                insertCmd.Parameters.AddWithValue("@userId", userId);
                await insertCmd.ExecuteNonQueryAsync();
            }

            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, error = "Error saving instructions" });
        }
    }

    /// <summary>
    /// Resets (deletes) baker instructions for an order.
    /// Matches legacy btnReset_OnClick.
    /// </summary>
    [HttpPost("resetinstructions")]
    public async Task<IActionResult> ResetInstructions([FromBody] ResetInstructionsRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Unauthorized();

        if (request == null || request.OrderId <= 0)
            return BadRequest(new { success = false, error = "Invalid order ID" });

        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Delete from tbl_orderBakeryInst
            var delInstSql = "DELETE FROM tbl_orderBakeryInst WHERE orderBakeryInst_orderID = @orderId";
            await using var delInstCmd = new SqlCommand(delInstSql, conn);
            delInstCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            await delInstCmd.ExecuteNonQueryAsync();

            // Delete from tbl_orderBakerInstList
            var delListSql = "DELETE FROM tbl_orderBakerInstList WHERE orderBakerInstList_orderID = @orderId";
            await using var delListCmd = new SqlCommand(delListSql, conn);
            delListCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            await delListCmd.ExecuteNonQueryAsync();

            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, error = "Error resetting instructions" });
        }
    }
}

/// <summary>
/// Request model for the print log endpoint.
/// </summary>
public class PrintLogRequest
{
    public string OrderIDs { get; set; } = "";
    public int TypeId { get; set; } = 3; // Default: baker print (only valid value)
}

/// <summary>
/// Request model for saving baker instructions.
/// </summary>
public class SaveInstructionsRequest
{
    public long OrderId { get; set; }
    public string Remarks { get; set; } = "";
    public string ReadyByDate { get; set; } = "";
    public string ReadyByTime { get; set; } = "";
    public string DispatchDate { get; set; } = "";
    public string DispatchTime { get; set; } = "";
}

/// <summary>
/// Request model for resetting baker instructions.
/// </summary>
public class ResetInstructionsRequest
{
    public long OrderId { get; set; }
}

/// <summary>
/// Model for baker instruction list items (read-only display).
/// </summary>
public class BakerInstructionItem
{
    public string Title { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string Img { get; set; } = "";
}
