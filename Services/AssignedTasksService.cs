using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Models;
using CakerStreet.Business.Helpers;

namespace CakerStreet.Business.Services;

/// <summary>
/// Service for querying ordertype=12 Assigned Tasks data.
/// Migrated from bakeryorders.aspx.cs bindOrders() ordertype=12 section.
/// </summary>
public class AssignedTasksService
{
    private readonly string _connectionString;
    private readonly string _businessConnectionString;

    public AssignedTasksService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Main entry point: builds the full AssignedTasksViewModel from the request parameters.
    /// Source: bakeryorders.aspx.cs bindOrders() when litorderType.Text == "12"
    /// </summary>
    public async Task<AssignedTasksViewModel> GetAssignedTasksAsync(AssignedTasksRequest request)
    {
        var model = new AssignedTasksViewModel
        {
            ActiveDayID = request.DayID,
            ActiveTaskType = request.TaskType,
            ActiveTopper = request.Topper,
            ActiveDispTime = request.DispTime,
            ActiveDeliveryMode = request.DeliveryMode,
            ActiveRouteId = request.RouteId,
            StartDate = request.StartDate,
            StaffRotaUrl = "/staffrota"
        };

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Staff users (varusers)
            model.StaffList = await GetStaffListAsync(conn, request.WebshopId);

            // 2. Day tabs (lstday)
            model.DayTabs = await GetDayTabsAsync(conn, request);

            // 3. Check bakery closed state + IsBaking flag
            model.IsBakeryClosed = await IsBakeryClosedTodayAsync(conn);

            // Query IsBaking from tbl_WebstoreBranch (source: bakeryorders.aspx.cs lines 356-362)
            var isBakingSql = "SELECT WebstoreBranch_isBaking FROM tbl_WebstoreBranch WHERE WebstoreBranch_BranchID = @wid";
            await using (var cmd = new SqlCommand(isBakingSql, conn))
            {
                cmd.Parameters.AddWithValue("@wid", request.WebshopId);
                var val = await cmd.ExecuteScalarAsync();
                model.IsBaking = val != null && val != DBNull.Value && Convert.ToBoolean(val);
            }

            // Set ManifestUrl based on selected date
            var manifestDate = model.DayTabs.FirstOrDefault(d => d.DayID == request.DayID)?.Date ?? DateTime.Today;
            model.ManifestUrl = "/manageordermenifest?from=" + manifestDate.ToString("dd/MM/yyyy");

            // 4. Main task rows
            model.TaskRows = await GetTaskRowsAsync(conn, request);

            // 4b. Compute task type counts from task rows (source: lines 1115-1140)
            ComputeTaskTypeCounts(model);

            // 4c. Load checklist definitions and per-order-detail states
            model.ChecklistDefinitions = await GetChecklistDefinitionsAsync(conn);
            if (model.TaskRows.Count > 0)
            {
                var orderDetailIds = model.TaskRows.Select(r => r.OrderDetailId).Distinct().ToList();
                model.ChecklistStates = await GetChecklistStatesAsync(conn, orderDetailIds);

                // 4d. Load task processing history (source: lines 1370-1396)
                var taskIds = model.TaskRows
                    .Where(r => !string.IsNullOrEmpty(r.TaskId) && r.TaskId != "0")
                    .Select(r => long.Parse(r.TaskId)).Distinct().ToList();
                if (taskIds.Count > 0)
                {
                    model.TaskHistory = await GetTaskHistoryAsync(conn, taskIds, request.WebshopId);
                }
            }

            // 5. Dispatch time slots (only when dayID is specified)
            if (request.DayID > 0)
            {
                var selectedDate = model.DayTabs
                    .FirstOrDefault(d => d.DayID == request.DayID)?.Date ?? DateTime.Today;
                model.TimeSlots = await GetDispatchTimeSlotsAsync(
                    conn, selectedDate, request);
            }

            // 6. Delivery routes (only when dm=2)
            if (request.DeliveryMode == 2 && request.DayID > 0)
            {
                var selectedDate = model.DayTabs
                    .FirstOrDefault(d => d.DayID == request.DayID)?.Date ?? DateTime.Today;
                model.DeliveryRoutes = await GetDeliveryRoutesAsync(selectedDate);
            }
        }
        catch (Exception ex)
        {
            model.ErrorMessage = "Data could not be loaded. Please try again.";
            // Log ex in production
        }

        return model;
    }

    // ─── Staff Users ───────────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs line 835:
    //   db.BakeryUser.Where(w => w.customer_type == 3 && w.customer_stafftype == 1
    //     && w.customer_webshopID == webstoreid && w.customer_isActive == true
    //     && w.customer_isOpen == true)

    private async Task<List<TaskStaffItem>> GetStaffListAsync(SqlConnection conn, long webshopId)
    {
        var list = new List<TaskStaffItem>();
        var sql = @"SELECT customer_ID, customer_Name 
            FROM tbl_bakeryuser 
            WHERE customer_type = 3 
              AND customer_stafftype = 1 
              AND customer_webshopID = @webshopId 
              AND customer_isActive = 1 
              AND customer_isOpen = 1 
            ORDER BY customer_Name";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TaskStaffItem
            {
                UserId = reader.GetInt64(0),
                UserName = reader.GetString(1)
            });
        }
        return list;
    }

    // ─── Day Tabs ──────────────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs line 547:
    //   lstday = order.GetCakecountsForBakers_byDate(dttaskdate, webshopId)
    // The implementation is not available but the commented fallback (line 566) shows:
    //   For each of 7 days: DayDate, countCakes (count of orders for that date),
    //   DayID (getdayIdbyName), DayName, isclosed
    // We replicate this with a direct count query per day + closed day check.

    private async Task<List<DayTabItem>> GetDayTabsAsync(SqlConnection conn, AssignedTasksRequest request)
    {
        var list = new List<DayTabItem>();
        var startDate = request.StartDate.Date;

        // Get closed dates for the 7-day window
        var closedDates = new HashSet<DateTime>();
        var closedSql = @"SELECT dayClosed_date FROM tbl_dayClosed 
            WHERE dayClosed_date >= @startDate AND dayClosed_date < @endDate";
        await using (var cmd = new SqlCommand(closedSql, conn))
        {
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", startDate.AddDays(7));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                closedDates.Add(reader.GetDateTime(0).Date);
            }
        }

        // Get cake counts for each day in the 7-day window
        // Source: legacy counts orders with dispatch date on that day
        var countSql = @"SELECT CAST(ordercollection_dispatchDate AS DATE) AS dispDate, 
                COUNT(*) AS cnt
            FROM tbl_order o
            INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID
            INNER JOIN tbl_orderDetail d ON d.orderDetail_orderID = o.order_ID
            WHERE order_branchID IN (
                SELECT WebstoreBranch_BranchID FROM tbl_WebstoreBranch 
                WHERE (WebstoreBranch_WebstoreID = @webshopId AND WebstoreBranch_isBaking = 0) 
                   OR WebstoreBranch_BranchID = @webshopId
            )
            AND order_isPurchased = 1 AND order_isdeleted = 0
            AND order_status IN (0, 1, 3, 5)
            AND ordercollection_dispatchDate >= @startDate 
            AND ordercollection_dispatchDate < @endDate
            GROUP BY CAST(ordercollection_dispatchDate AS DATE)";

        var countsByDate = new Dictionary<DateTime, int>();
        await using (var cmd = new SqlCommand(countSql, conn))
        {
            cmd.Parameters.AddWithValue("@webshopId", request.WebshopId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", startDate.AddDays(7));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = reader.GetDateTime(0).Date;
                var count = reader.GetInt32(1);
                countsByDate[date] = count;
            }
        }

        for (int i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);
            var dayId = AssignedTasksNavHelper.GetDayId(date);
            countsByDate.TryGetValue(date, out var count);

            list.Add(new DayTabItem
            {
                DayID = dayId,
                DayName = date.DayOfWeek.ToString(),
                Date = date,
                CakeCount = count.ToString(),
                IsClosed = closedDates.Contains(date)
            });
        }

        return list;
    }

    // ─── Bakery Closed Today ───────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs line 550:
    //   bakeryclosed = db.dayClosed.Any(w => w.dayClosed_date == DateTime.Today.Date)

    private async Task<bool> IsBakeryClosedTodayAsync(SqlConnection conn)
    {
        var sql = "SELECT COUNT(1) FROM tbl_dayClosed WHERE dayClosed_date = @today";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", DateTime.Today);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    // ─── Checklist Definitions ─────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1400-1404:
    //   tbl_orderProcessingChecklist WHERE orderProcessingChecklist_isActive=1
    //   ORDER BY orderProcessingChecklist_displayorder

    private async Task<List<ChecklistDefinition>> GetChecklistDefinitionsAsync(SqlConnection conn)
    {
        var list = new List<ChecklistDefinition>();
        var sql = @"SELECT orderProcessingChecklist_ID, orderProcessingChecklist_title, orderProcessingChecklist_displayorder
            FROM tbl_orderProcessingChecklist
            WHERE orderProcessingChecklist_isActive = 1
            ORDER BY orderProcessingChecklist_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ChecklistDefinition
            {
                ChecklistId = reader.GetInt32(0),
                Title = reader.GetString(1),
                DisplayOrder = reader.GetInt32(2)
            });
        }
        return list;
    }

    // ─── Checklist States Per Order Detail ─────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1407-1411:
    //   tbl_orderProcessingChecklist INNER JOIN tbl_lnkOrderChecklist2Order
    //   ON orderProcessingChecklist_ID = lnkOrderChecklist2Order_checklistID
    //   AND lnkOrderChecklist2Order_orderDetailID IN (...)

    private async Task<Dictionary<long, List<ChecklistItemState>>> GetChecklistStatesAsync(
        SqlConnection conn, List<long> orderDetailIds)
    {
        var result = new Dictionary<long, List<ChecklistItemState>>();
        if (orderDetailIds.Count == 0) return result;

        var idList = string.Join(",", orderDetailIds);
        var sql = $@"SELECT lnkOrderChecklist2Order_checklistID, lnkOrderChecklist2Order_orderDetailID,
                lnkOrderChecklist2Order_isDone, isnull(lnkOrderChecklist2Order_isexcluded, 0) AS isexcluded,
                lnkOrderChecklist2Order_modifiedOn, isnull(lnkOrderChecklist2Order_remarks, '') AS remarks
            FROM tbl_lnkOrderChecklist2Order
            WHERE lnkOrderChecklist2Order_orderDetailID IN ({idList})";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var state = new ChecklistItemState
            {
                ChecklistId = reader.GetInt32(0),
                OrderDetailId = reader.GetInt64(1),
                IsDone = reader.GetBoolean(2),
                IsExcluded = reader.GetBoolean(3),
                ModifiedOn = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                Remarks = reader.GetString(5)
            };

            if (!result.ContainsKey(state.OrderDetailId))
                result[state.OrderDetailId] = new List<ChecklistItemState>();
            result[state.OrderDetailId].Add(state);
        }
        return result;
    }

    // ─── Checklist Toggle Persistence ──────────────────────────────────────────
    // Source: bakeryorders.aspx.cs — checkuncheck_Checklist JS → server update
    // Table: tbl_lnkOrderChecklist2Order
    // If row exists: UPDATE lnkOrderChecklist2Order_isDone + modifiedOn
    // If row does not exist: INSERT new row

    public async Task ToggleChecklistItemAsync(long orderDetailId, int checklistId, bool isDone)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if row exists
        var checkSql = @"SELECT COUNT(1) FROM tbl_lnkOrderChecklist2Order 
            WHERE lnkOrderChecklist2Order_checklistID = @checklistId 
            AND lnkOrderChecklist2Order_orderDetailID = @orderDetailId";

        int exists = 0;
        await using (var cmd = new SqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("@checklistId", checklistId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        if (exists > 0)
        {
            var updateSql = @"UPDATE tbl_lnkOrderChecklist2Order 
                SET lnkOrderChecklist2Order_isDone = @isDone, lnkOrderChecklist2Order_modifiedOn = GETDATE()
                WHERE lnkOrderChecklist2Order_checklistID = @checklistId 
                AND lnkOrderChecklist2Order_orderDetailID = @orderDetailId";
            await using var cmd = new SqlCommand(updateSql, conn);
            cmd.Parameters.AddWithValue("@isDone", isDone);
            cmd.Parameters.AddWithValue("@checklistId", checklistId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertSql = @"INSERT INTO tbl_lnkOrderChecklist2Order 
                (lnkOrderChecklist2Order_checklistID, lnkOrderChecklist2Order_orderDetailID, 
                 lnkOrderChecklist2Order_isDone, lnkOrderChecklist2Order_isexcluded, 
                 lnkOrderChecklist2Order_modifiedOn, lnkOrderChecklist2Order_remarks)
                VALUES (@checklistId, @orderDetailId, @isDone, 0, GETDATE(), '')";
            await using var cmd = new SqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@checklistId", checklistId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            cmd.Parameters.AddWithValue("@isDone", isDone);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ─── Task Processing History ───────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1370-1396:
    //   from tsk in lstTaskdet
    //   join att in db.ordertaskdet.Where(w => lsttaskIds.Contains(w.ordertaskdet_taskId) && w.ordertaskdet_taskSts <= 33)
    //   join lnk in db.BakeryUser.Where(w => w.customer_isOpen && w.customer_isActive && w.customer_type == 3)
    //   on { att.ordertaskdet_userID, tsk.orderbaking_webstoreid } equals { lnk.customer_ID, lnk.customer_webshopID }
    //   OrderBy(o => o.ordertaskdet_modifiedOn)

    private async Task<Dictionary<long, List<TaskHistoryEntry>>> GetTaskHistoryAsync(
        SqlConnection conn, List<long> taskIds, long webshopId)
    {
        var result = new Dictionary<long, List<TaskHistoryEntry>>();
        if (taskIds.Count == 0) return result;

        var idList = string.Join(",", taskIds);
        var sql = $@"SELECT td.ordertaskdet_Id, td.ordertaskdet_taskId, td.ordertaskdet_taskSts,
                td.ordertaskdet_userID, u.customer_Name, td.ordertaskdet_remarks,
                td.ordertaskdet_isCompleted, td.ordertaskdet_isDone, td.ordertaskdet_isreply,
                td.ordertaskdet_staDate, td.ordertaskdet_endDate, td.ordertaskdet_modifiedOn
            FROM tbl_ordertaskdet td
            INNER JOIN tbl_bakeryuser u ON td.ordertaskdet_userID = u.customer_ID
                AND u.customer_isOpen = 1 AND u.customer_isActive = 1 AND u.customer_type = 3
                AND u.customer_webshopID = @webshopId
            WHERE td.ordertaskdet_taskId IN ({idList})
                AND td.ordertaskdet_taskSts <= 33
            ORDER BY td.ordertaskdet_modifiedOn";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var entry = new TaskHistoryEntry
            {
                TaskDetId = reader.GetInt64(0),
                TaskId = reader.GetInt64(1),
                TaskSts = reader.GetInt32(2),
                UserID = reader.GetInt32(3),
                UserName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Remarks = reader.IsDBNull(5) ? "" : reader.GetString(5),
                IsCompleted = !reader.IsDBNull(6) && reader.GetBoolean(6),
                IsDone = !reader.IsDBNull(7) && reader.GetBoolean(7),
                IsReply = !reader.IsDBNull(8) && reader.GetBoolean(8),
                StartDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                EndDate = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                ModifiedOn = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11)
            };

            if (!result.ContainsKey(entry.TaskId))
                result[entry.TaskId] = new List<TaskHistoryEntry>();
            result[entry.TaskId].Add(entry);
        }
        return result;
    }

    // ─── Task Type Counts ──────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1115-1140 (countcake_filling, etc.)
    // Computed from the full task row list, matching legacy LINQ logic.
    // Format: "cakecount/cupcakecount" — only set if not "0/0"

    private void ComputeTaskTypeCounts(AssignedTasksViewModel model)
    {
        var rows = model.TaskRows;

        // Filling: product_type!=2, order_status in (0,1,5), tasksts is null or (tasksts=11 and !completed)
        var fillingCakes = rows.Count(r => r.ProductType == 1 && (r.OrderStatus == 0 || r.OrderStatus == 1 || r.OrderStatus == 5) && (string.IsNullOrEmpty(r.TaskStatus) || (r.TaskStatus == "11" && r.IsCompleted != "True")));
        var fillingCupcakes = rows.Count(r => r.ProductType == 6 && (r.OrderStatus == 0 || r.OrderStatus == 1 || r.OrderStatus == 5) && (string.IsNullOrEmpty(r.TaskStatus) || (r.TaskStatus == "11" && r.IsCompleted != "True")));
        var filling = $"{fillingCakes}/{fillingCupcakes}";
        model.FillingCount = filling != "0/0" ? filling : "";

        // Icing: product_type!=2, order_status=5, (tasksts=12 and !completed) or (tasksts=11 and completed)
        var icingCakes = rows.Count(r => r.ProductType == 1 && r.OrderStatus == 5 && ((r.TaskStatus == "12" && r.IsCompleted != "True") || (r.TaskStatus == "11" && r.IsCompleted == "True")));
        var icingCupcakes = rows.Count(r => r.ProductType == 6 && r.OrderStatus == 5 && ((r.TaskStatus == "12" && r.IsCompleted != "True") || (r.TaskStatus == "11" && r.IsCompleted == "True")));
        var icing = $"{icingCakes}/{icingCupcakes}";
        model.IcingCount = icing != "0/0" ? icing : "";

        // Decoration: product_type!=2, order_status=5, (tasksts=22 and !completed) or (tasksts=12 and completed)
        var decCakes = rows.Count(r => r.ProductType == 1 && r.OrderStatus == 5 && ((r.TaskStatus == "22" && r.IsCompleted != "True") || (r.TaskStatus == "12" && r.IsCompleted == "True")));
        var decCupcakes = rows.Count(r => r.ProductType == 6 && r.OrderStatus == 5 && ((r.TaskStatus == "22" && r.IsCompleted != "True") || (r.TaskStatus == "12" && r.IsCompleted == "True")));
        var dec = $"{decCakes}/{decCupcakes}";
        model.DecorationCount = dec != "0/0" ? dec : "";

        // Finishing: order_status=5, (tasksts=33 and !completed) or (tasksts=22 and completed)
        var finCakes = rows.Count(r => r.ProductType == 1 && r.OrderStatus == 5 && ((r.TaskStatus == "33" && r.IsCompleted != "True") || (r.TaskStatus == "22" && r.IsCompleted == "True")));
        var finCupcakes = rows.Count(r => r.ProductType == 6 && r.OrderStatus == 5 && ((r.TaskStatus == "33" && r.IsCompleted != "True") || (r.TaskStatus == "22" && r.IsCompleted == "True")));
        var fin = $"{finCakes}/{finCupcakes}";
        model.FinishingCount = fin != "0/0" ? fin : "";

        // Under Delivery: (order_status=3 and completed) or (order_status=5 and tasksts=44 and completed)
        var udCakes = rows.Count(r => r.ProductType == 1 && ((r.OrderStatus == 3 && r.IsCompleted == "True") || (r.OrderStatus == 5 && r.TaskStatus == "44" && r.IsCompleted == "True")));
        var udCupcakes = rows.Count(r => r.ProductType == 6 && ((r.OrderStatus == 3 && r.IsCompleted == "True") || (r.OrderStatus == 5 && r.TaskStatus == "44" && r.IsCompleted == "True")));
        var ud = $"{udCakes}/{udCupcakes}";
        model.UnderDeliveryCount = ud != "0/0" ? ud : "";

        // Topper count (legacy uses sum of topper_count which is always 0 in current code)
        model.TopperCount = "0";
    }

    // ─── Dispatch Time Slots ───────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1072-1110:
    //   dscount = objOrder.getcakecount_bydayID_ordertaskpage(selectedDate, domainID, webstoreid, topperreq, disptimeid)
    //   Returns columns: countcake_DATEPART_10am, countcake_DATEPART_12pm, etc.
    //   Each slot has format "cakecount/cupcakecount" and a faded status "totalcount/totaldone"
    //   TimeIDs: 10 (10:00 AM), 12 (12:00 PM), 14 (02:00 PM), 16 (04:00 PM)
    // Since the stored proc is not available, we replicate the logic:
    //   Group by DATEPART(hour, ordercollection_dispatchDate) bucketed into 10/12/14/16

    private async Task<List<DispatchTimeSlotItem>> GetDispatchTimeSlotsAsync(
        SqlConnection conn, DateTime selectedDate, AssignedTasksRequest request)
    {
        var slots = new List<DispatchTimeSlotItem>();

        // Query: for the selected date, group order details by dispatch hour bucket
        // and count cakes (product_type=1) and cupcakes (product_type=6)
        // Also count "done" items for faded status
        var sql = @"
SELECT 
    TimeSlot,
    SUM(CASE WHEN product_type = 1 THEN 1 ELSE 0 END) AS cakecount,
    SUM(CASE WHEN product_type = 6 THEN 1 ELSE 0 END) AS cupcakecount,
    COUNT(*) AS totalcount,
    SUM(CASE WHEN (order_status = 3) 
             OR (order_status = 5 AND ordertask_tasksts IN (33, 44) AND ordertask_isCompleted = 1)
             OR (order_status = 5 AND ordertask_tasksts = 22 AND ordertask_isCompleted = 1)
        THEN 1 ELSE 0 END) AS totaldone
FROM (
    SELECT 
        CASE 
            WHEN ordercollection_deliverymode IN (3,4) THEN 16
            WHEN ordercollection_deliverymode = 2 THEN 
                CASE WHEN DATEPART(hour, ordercollection_dispatchDate) - 2 <= 10 THEN 10
                     WHEN DATEPART(hour, ordercollection_dispatchDate) - 2 <= 12 THEN 12
                     WHEN DATEPART(hour, ordercollection_dispatchDate) - 2 <= 14 THEN 14
                     ELSE 16 END
            ELSE 
                CASE WHEN DATEPART(hour, ordercollection_dispatchDate) <= 10 THEN 10
                     WHEN DATEPART(hour, ordercollection_dispatchDate) <= 12 THEN 12
                     WHEN DATEPART(hour, ordercollection_dispatchDate) <= 14 THEN 14
                     ELSE 16 END
        END AS TimeSlot,
        p.product_type, o.order_status, t.ordertask_tasksts, t.ordertask_isCompleted
    FROM tbl_order o
    INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID
    INNER JOIN tbl_orderDetail d ON d.orderDetail_orderID = o.order_ID
    INNER JOIN tbl_products p ON p.product_ID = d.orderDetail_productID
    LEFT JOIN tbl_ordertask t ON t.ordertask_orderID = o.order_ID 
        AND t.ordertask_orderdetailid = d.orderDetail_ID
    WHERE order_branchID IN (
        SELECT WebstoreBranch_BranchID FROM tbl_WebstoreBranch 
        WHERE (WebstoreBranch_WebstoreID = @webshopId AND WebstoreBranch_isBaking = 0) 
           OR WebstoreBranch_BranchID = @webshopId
    )
    AND order_isPurchased = 1 AND order_isdeleted = 0
    AND order_status IN (0, 1, 3, 5)
    AND ordercollection_dispatchDate >= @dateStart 
    AND ordercollection_dispatchDate < @dateEnd
) sub
GROUP BY TimeSlot
ORDER BY TimeSlot";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", request.WebshopId);
        cmd.Parameters.AddWithValue("@dateStart", selectedDate.Date);
        cmd.Parameters.AddWithValue("@dateEnd", selectedDate.Date.AddDays(1));
        await using var reader = await cmd.ExecuteReaderAsync();

        var timeSlotNames = new Dictionary<int, string>
        {
            { 10, "10:00 AM" }, { 12, "12:00 PM" }, { 14, "02:00 PM" }, { 16, "04:00 PM" }
        };

        while (await reader.ReadAsync())
        {
            var timeId = reader.GetInt32(0);
            var cakeCount = reader.GetInt32(1);
            var cupcakeCount = reader.GetInt32(2);
            var totalCount = reader.GetInt32(3);
            var totalDone = reader.GetInt32(4);

            // Source: line 1082 — only include if cakecount or cupcakecount > 0
            if (cakeCount > 0 || cupcakeCount > 0)
            {
                slots.Add(new DispatchTimeSlotItem
                {
                    TimeID = timeId,
                    TimeSlotName = timeSlotNames.GetValueOrDefault(timeId, timeId + ":00"),
                    CakeCount = cakeCount,
                    CupcakeCount = cupcakeCount,
                    TotalCount = totalCount,
                    TotalDone = totalDone
                });
            }
        }

        return slots;
    }

    // ─── Delivery Routes ───────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs line 751-753:
    //   db_route.deliveryRoute.Where(w => w.route_date == dtdate_route)
    //     .OrderBy(o => o.route_displayOrder)
    //   with countCakes = clsglobaltext.getcakecountsforbakers_byrouteID(route_ID, domainID)
    // Routes are in db_cakerstreet_business.dbo.tbl_deliveryRoute
    // Route order counts from db_cakerstreet_business.dbo.tbl_deliveryRouteOrder

    private async Task<List<DeliveryRouteItem>> GetDeliveryRoutesAsync(DateTime selectedDate)
    {
        var list = new List<DeliveryRouteItem>();

        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        var sql = @"SELECT r.route_ID, r.route_title, r.route_date, r.route_displayOrder,
                (SELECT COUNT(*) FROM tbl_deliveryRouteOrder ro 
                 WHERE ro.routeOrder_routeID = r.route_ID) AS orderCount
            FROM tbl_deliveryRoute r
            WHERE r.route_date = @routeDate
            ORDER BY r.route_displayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@routeDate", selectedDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DeliveryRouteItem
            {
                RouteId = reader.GetInt64(0),
                RouteName = reader.GetString(1),
                RouteDate = reader.GetDateTime(2),
                DisplayOrder = reader.GetInt32(3),
                OrderCount = reader.GetInt32(4).ToString()
            });
        }

        return list;
    }

    // ─── Main Task Rows ────────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 857-887 (the big SQL query)
    // Joins: tbl_order, tbl_ordercollection, tbl_orderDetail, tbl_products,
    //   tbl_shippingDetail, tbl_ordertask, tbl_ordertaskhold, tbl_bakeryuser,
    //   tbl_ordersorting, tbl_log_orderprint, tbl_CakeShape, tbl_CakeType,
    //   tbl_CakeSize, tbl_orderImageUpdate, tbl_webstore, tbl_WebstoreBranch,
    //   tbl_PrdTopperType, tbl_deliveryRouteOrder, tbl_deliveryRoute,
    //   tbl_spongeOrderDet, tbl_orderreviews

    private async Task<List<AssignedTaskRow>> GetTaskRowsAsync(
        SqlConnection conn, AssignedTasksRequest request)
    {
        var rows = new List<AssignedTaskRow>();
        var parameters = new List<SqlParameter>();

        // Build dynamic WHERE filters matching legacy strFilter construction
        var filters = BuildTaskFilters(request, parameters);

        var sql = $@"SET NOCOUNT ON;
SELECT 
    case when (ordertask_currUserID is null or ordertask_isdone=1) 
         and ordercollection_deliverymode in (3,4) then 3 
         when (ordertask_currUserID is null or ordertask_isdone=1) then 2 
         when ordertask_isdone=0 then 1 
         else ordertask_tasksts end AS sortID,
    isnull(ordersorting_displayorder,0) AS sortID2,
    isnull(route_ID,0) AS route_ID,
    isnull(route_title,'') AS route_title,
    product_type, product_Code, order_isrepeat, order_status,
    product_SEOURL, product_image1, product_Name,
    isnull(CakeShapeTitle,'') AS CakeShapeTitle,
    isnull(CakeTypeTitle,'') AS CakeTypeTitle,
    isnull(cast(sizeID as nvarchar(10)),'') AS sizeID,
    isnull(SizeTitle,'') AS SizeTitle,
    isnull(orderDetail_ShapeText,'') AS orderDetail_ShapeText,
    isnull(CakeShapeCustomText,'') AS CakeShapeCustomText,
    order_customerName, shipping_phone, shipping_zip,
    isnull(orderreviews_remarks,'') AS orderreviews_remarks,
    isnull(ordercollection_Remarks,'') AS ordercollection_Remarks,
    ordertask_tasksts, log_orderprint_orderId,
    isnull(orderreviews_stars,'') AS orderreviews_stars,
    ordertask_isCompleted, ordertask_isdone, ordertask_Id,
    ordertaskhold_ID, ordertaskhold_ishold, ordertask_ishold,
    isnull(u.customer_Name,'') AS customer_Name,
    isnull(ordertaskdet_remarks,'') AS ordertaskdet_remarks,
    order_bakeryID, order_branchID, o.order_ID, p.product_ID,
    order_forwardedorderid, order_followingorderid,
    d.orderDetail_ID, order_quality,
    orderDetail_Quantity, orderDetail_shapeId,
    ordercollection_deliverymode, order_saletype,
    order_date, ordercollection_Date, ordercollection_dispatchDate,
    ordercollection_OcasionDate, order_totalPrice, order_prdTotal,
    order_CSmargin, order_shopMargin, order_payoutRefund, order_csRefund,
    d.orderDetail_orderID,
    case when order_status = 0 then 
        case when om.IsUpdated is null or om.IsUpdated = 0 then 0 else 1 end 
    else 1 end AS IsChangeOrderImageMarked,
    isnull((select count_big(1) from tbl_OrderTaskAssign 
        where OrderTaskAssign_OrderID=o.order_ID 
        and OrderTaskAssign_OrderDetail_Id=d.orderDetail_ID 
        and OrderTaskAssign_isdeleted=0),0) AS countassignedtouser,
    br.webstore_businessName AS branchName,
    wb.WebstoreBranch_isBaking,
    isnull((select top 1 cast(Distance as nvarchar(50))+','+cast(DistanceSeconds as nvarchar(50)) 
        from tbl_PostcodeDistance where (
        (Postcode2=replace(br.webstore_postcode,' ','') and Postcode1=replace(shipping_zip,' ',''))
        or (Postcode1=replace(br.webstore_postcode,' ','') and Postcode2=replace(shipping_zip,' ',''))
    )),'') AS webstore_postcodedet,
    br.webstore_postcode,
    isnull(so.spongeOrderDet_Status, 0) AS spongeOrderDet_Status
FROM tbl_order o
INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID
INNER JOIN tbl_orderDetail d ON d.orderDetail_orderID = o.order_ID
INNER JOIN tbl_products p ON p.product_ID = d.orderDetail_productID
INNER JOIN tbl_shippingDetail s ON s.shipping_orderID = o.order_ID
LEFT OUTER JOIN tbl_spongeOrderDet so ON o.order_ID = so.spongeOrderDet_OrderID
LEFT JOIN tbl_orderreviews v ON v.orderreviews_orderID = o.order_ID
LEFT JOIN tbl_ordertask t ON t.ordertask_orderID = o.order_ID 
    AND t.ordertask_orderdetailid = d.orderDetail_ID
LEFT JOIN tbl_ordertaskhold th ON th.ordertaskhold_orderdetID = d.orderDetail_ID
LEFT JOIN tbl_bakeryuser u ON u.customer_ID = t.ordertask_currUserID
LEFT JOIN tbl_ordersorting os ON os.ordersorting_orderID = o.order_ID
LEFT JOIN tbl_log_orderprint lo ON lo.log_orderprint_orderId = o.order_ID 
    AND lo.log_orderprint_typeId = 3
LEFT JOIN tbl_CakeShape cs ON cs.CakeShapeID = d.orderDetail_shapeId 
    AND d.orderDetail_shapeId > 0
LEFT JOIN tbl_CakeType ct ON ct.CakeTypeID = d.orderDetail_TypeID 
    AND d.orderDetail_TypeID > 0
LEFT JOIN tbl_CakeSize csz ON csz.SizeID = d.orderDetail_SizeID 
    AND d.orderDetail_SizeID > 0
LEFT OUTER JOIN tbl_orderImageUpdate om ON d.orderDetail_ID = om.OrderImage_orderDetail_ID
LEFT JOIN tbl_webstore br ON o.order_branchid = br.webstore_id
INNER JOIN tbl_WebstoreBranch wb ON @webshopId = wb.WebstoreBranch_BranchID
LEFT OUTER JOIN tbl_PrdTopperType ot ON ot.product_ID = d.orderDetail_productID 
    AND ot.IsDeleted = 0
LEFT OUTER JOIN db_cakerstreet_business.dbo.tbl_deliveryRouteOrder drt 
    ON drt.routeOrder_orderID = o.order_ID
LEFT JOIN db_cakerstreet_business.dbo.tbl_deliveryRoute dr 
    ON drt.routeOrder_routeID = dr.route_ID
WHERE (
    order_branchID IN (
        SELECT WebstoreBranch_BranchID FROM tbl_WebstoreBranch 
        WHERE (WebstoreBranch_WebstoreID = @webshopId AND WebstoreBranch_isBaking = 0) 
           OR WebstoreBranch_BranchID = @webshopId
    )
    OR o.order_ID IN (
        SELECT ordertask_orderID FROM tbl_ordertask OTin 
        INNER JOIN tbl_order Oin ON Oin.order_ID = OTin.ordertask_orderID
        INNER JOIN tbl_bakeryuser bu1 ON OTin.ordertask_currUserID = bu1.customer_ID 
        WHERE Oin.order_status IN (5) AND bu1.customer_webshopID = @webshopId
    )
)
AND order_isPurchased = 1 AND order_isdeleted = 0
{filters}
ORDER BY ordercollection_dispatchDate, isnull(ordersorting_displayorder,100), ordercollection_Date";

        parameters.Add(new SqlParameter("@webshopId", request.WebshopId));

        await using var cmd = new SqlCommand(sql, conn);
        foreach (var p in parameters)
            cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();
        var seenOrderIds = new HashSet<long>();

        while (await reader.ReadAsync())
        {
            var orderId = GetInt64Safe(reader, "order_ID");
            var row = new AssignedTaskRow
            {
                SortId = GetInt32Safe(reader, "sortID"),
                SortId2 = GetInt32Safe(reader, "sortID2"),
                RouteId = GetInt64Safe(reader, "route_ID"),
                RouteTitle = GetStringSafe(reader, "route_title"),
                ProductType = GetInt32Safe(reader, "product_type"),
                ProductCode = GetStringSafe(reader, "product_Code"),
                IsRepeat = GetBoolSafe(reader, "order_isrepeat"),
                OrderStatus = GetInt32Safe(reader, "order_status"),
                ProductSeoUrl = GetStringSafe(reader, "product_SEOURL"),
                ProductImage = GetStringSafe(reader, "product_image1"),
                ProductName = GetStringSafe(reader, "product_Name"),
                CakeShapeTitle = GetStringSafe(reader, "CakeShapeTitle"),
                CakeTypeTitle = GetStringSafe(reader, "CakeTypeTitle"),
                SizeId = GetStringSafe(reader, "sizeID"),
                SizeTitle = GetStringSafe(reader, "SizeTitle"),
                ShapeText = GetStringSafe(reader, "orderDetail_ShapeText"),
                CakeShapeCustomText = GetStringSafe(reader, "CakeShapeCustomText"),
                CustomerName = GetStringSafe(reader, "order_customerName"),
                Phone = GetStringSafe(reader, "shipping_phone"),
                Postcode = GetStringSafe(reader, "shipping_zip"),
                ReviewRemarks = GetStringSafe(reader, "orderreviews_remarks"),
                CollectionRemarks = GetStringSafe(reader, "ordercollection_Remarks"),
                TaskStatus = GetStringSafe(reader, "ordertask_tasksts"),
                PrintOrderId = GetStringSafe(reader, "log_orderprint_orderId"),
                ReviewStars = GetStringSafe(reader, "orderreviews_stars"),
                IsCompleted = GetStringSafe(reader, "ordertask_isCompleted"),
                IsDone = GetStringSafe(reader, "ordertask_isdone"),
                TaskId = GetStringSafe(reader, "ordertask_Id"),
                IsOnHold = GetBoolSafe(reader, "ordertask_ishold"),
                AssignedUserName = GetStringSafe(reader, "customer_Name"),
                TaskRemarks = GetStringSafe(reader, "ordertaskdet_remarks"),
                BakeryId = GetInt64Safe(reader, "order_bakeryID"),
                BranchId = GetInt64Safe(reader, "order_branchID"),
                OrderId = orderId,
                ProductId = GetInt64Safe(reader, "product_ID"),
                ForwardedOrderId = GetInt64Safe(reader, "order_forwardedorderid"),
                FollowingOrderId = GetInt64Safe(reader, "order_followingorderid"),
                OrderDetailId = GetInt64Safe(reader, "orderDetail_ID"),
                OrderQuality = GetInt32Safe(reader, "order_quality"),
                Quantity = GetInt32Safe(reader, "orderDetail_Quantity"),
                ShapeId = GetInt32Safe(reader, "orderDetail_shapeId"),
                DeliveryMode = GetInt32Safe(reader, "ordercollection_deliverymode"),
                SaleType = GetInt32Safe(reader, "order_saletype"),
                OrderDate = GetDateTimeSafe(reader, "order_date"),
                CollectionDate = GetDateTimeSafe(reader, "ordercollection_Date"),
                DispatchDate = GetDateTimeSafe(reader, "ordercollection_dispatchDate"),
                OccasionDate = GetDateTimeSafe(reader, "ordercollection_OcasionDate"),
                TotalPrice = GetDecimalSafe(reader, "order_totalPrice"),
                PrdTotal = GetDecimalSafe(reader, "order_prdTotal"),
                CSMargin = GetDecimalSafe(reader, "order_CSmargin"),
                ShopMargin = GetDecimalSafe(reader, "order_shopMargin"),
                PayoutRefund = GetDecimalSafe(reader, "order_payoutRefund"),
                CsRefund = GetDecimalSafe(reader, "order_csRefund"),
                IsChangeOrderImageMarked = GetBoolSafe(reader, "IsChangeOrderImageMarked"),
                CountAssignedToUser = GetStringSafe(reader, "countassignedtouser"),
                BranchName = GetStringSafe(reader, "branchName"),
                IsBaking = GetBoolSafe(reader, "WebstoreBranch_isBaking"),
                PostcodeDistance = GetStringSafe(reader, "webstore_postcodedet"),
                BranchPostcode = GetStringSafe(reader, "webstore_postcode"),
                SpongeStatus = GetInt32Safe(reader, "spongeOrderDet_Status"),
            };

            // Source: bakeryorders.aspx.cs line 975:
            //   ordercollection_readybyDate = dispatchDate.AddHours(dm==1 ? 0 : dm==2 ? -2 : 16)
            row.ReadyByDate = row.DispatchDate.AddHours(
                row.DeliveryMode == 1 ? 0 : row.DeliveryMode == 2 ? -2 : 16);

            // Source: line 980 — dispatchDate = ordercollection_dispatchDate.Date
            row.GroupDate = row.DispatchDate.Date;

            // Source: line 982 — order_showprint: 1 if first occurrence of order_ID
            row.ShowPrint = seenOrderIds.Contains(orderId) ? 0 : 1;
            seenOrderIds.Add(orderId);

            // Source: lines 937-938 — special handling for order_status=3 with null task fields
            if (row.OrderStatus == 3 && string.IsNullOrEmpty(row.IsCompleted))
                row.IsCompleted = "False";
            if (row.OrderStatus == 3 && string.IsNullOrEmpty(row.IsDone))
                row.IsDone = "False";

            rows.Add(row);
        }

        // Apply route filter in-memory (source: lines 1340-1348)
        if (request.RouteId > 0 && request.DeliveryMode == 2)
        {
            var routeOrderIds = await GetRouteOrderIdsAsync(request.RouteId);
            rows = rows.Where(r => routeOrderIds.Contains(r.OrderId)).ToList();
        }

        return rows;
    }

    // ─── Route Order IDs ───────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 1345-1346:
    //   dbB.deliveryRouteOrder.Where(w => w.routeOrder_routeID == routeID)
    //     .Select(s => s.routeOrder_orderID).ToList()

    private async Task<HashSet<long>> GetRouteOrderIdsAsync(int routeId)
    {
        var ids = new HashSet<long>();
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        var sql = "SELECT routeOrder_orderID FROM tbl_deliveryRouteOrder WHERE routeOrder_routeID = @routeId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@routeId", routeId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt64(0));
        }
        return ids;
    }

    // ─── Filter Builder ────────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 730-831 (strFilter construction)
    // Builds the dynamic WHERE clause additions based on active filters.

    private string BuildTaskFilters(AssignedTasksRequest request, List<SqlParameter> parameters)
    {
        var filters = "";

        // Base status filter (source: line 730)
        // "and ((order_status in (0,1,5,3)))"
        filters += " AND order_status IN (0, 1, 3, 5)";

        // dayID filter (source: lines 741-745)
        // "and (ordercollection_dispatchDate >= 'yyyy-MM-dd' and ordercollection_dispatchDate < 'yyyy-MM-dd+1')"
        // Note: we don't have the actual date here, we need to compute it from DayTabs
        // But since DayTabs aren't loaded yet when this runs, we pass the date range directly
        // The caller should set StartDate appropriately
        if (request.DayID > 0)
        {
            // Find the date for this dayID within the 7-day window
            var startDate = request.StartDate.Date;
            DateTime? targetDate = null;
            for (int i = 0; i < 7; i++)
            {
                var d = startDate.AddDays(i);
                if (AssignedTasksNavHelper.GetDayId(d) == request.DayID)
                {
                    targetDate = d;
                    break;
                }
            }
            if (targetDate.HasValue)
            {
                filters += " AND ordercollection_dispatchDate >= @dayStart AND ordercollection_dispatchDate < @dayEnd";
                parameters.Add(new SqlParameter("@dayStart", targetDate.Value));
                parameters.Add(new SqlParameter("@dayEnd", targetDate.Value.AddDays(1)));
            }
        }

        // tasktype filter (source: lines 795-831)
        if (request.TaskType == 11)
        {
            // Filling: product_type<>2 and order_status in (0,1,5) and (ordertask_tasksts is null or (ordertask_tasksts=11 and ordertask_isCompleted=0))
            filters += " AND product_type <> 2 AND order_status IN (0,1,5) AND (ordertask_tasksts IS NULL OR (ordertask_tasksts = 11 AND ordertask_isCompleted = 0))";
        }
        else if (request.TaskType == 12)
        {
            // Icing: product_type<>2 and order_status in (5) and ((ordertask_tasksts=11 and ordertask_isCompleted=1) or (ordertask_tasksts=12 and ordertask_isCompleted=0))
            filters += " AND product_type <> 2 AND order_status IN (5) AND ((ordertask_tasksts = 11 AND ordertask_isCompleted = 1) OR (ordertask_tasksts = 12 AND ordertask_isCompleted = 0))";
        }
        else if (request.TaskType == 22)
        {
            // Decoration: product_type<>2 and order_status in (5) and ((ordertask_tasksts=12 and ordertask_isCompleted=1) or (ordertask_tasksts=22 and ordertask_isCompleted=0))
            filters += " AND product_type <> 2 AND order_status IN (5) AND ((ordertask_tasksts = 12 AND ordertask_isCompleted = 1) OR (ordertask_tasksts = 22 AND ordertask_isCompleted = 0))";
        }
        else if (request.TaskType == 33)
        {
            // Finishing: (product_type=2 and (ordertask_tasksts is null or ordertask_isCompleted=0)) or (order_status=5 and ((ordertask_tasksts=33 and ordertask_isCompleted=0) or (ordertask_tasksts=22 and ordertask_isCompleted=1)))
            filters += " AND ((product_type = 2 AND (ordertask_tasksts IS NULL OR ordertask_isCompleted = 0)) OR (order_status = 5 AND ((ordertask_tasksts = 33 AND ordertask_isCompleted = 0) OR (ordertask_tasksts = 22 AND ordertask_isCompleted = 1))))";
        }
        else if (request.TaskType == 44)
        {
            // Under Delivery: (order_status=3) or (order_status=5 and ordertask_tasksts=44 and ordertask_isCompleted=1)
            filters += " AND ((order_status = 3) OR (order_status = 5 AND ordertask_tasksts = 44 AND ordertask_isCompleted = 1))";
        }

        // topper filter (source: uses tbl_PrdTopperType join — already in FROM)
        // When topper=1, filter to only rows where topper type exists
        if (request.Topper == 1)
        {
            filters += " AND ot.product_ID IS NOT NULL";
        }

        // delivery mode filter (source: lines 467-480)
        if (request.DeliveryMode == 1)
        {
            // Collection: ordercollection_deliverymode=1 and order_bakeryID=order_branchID
            filters += " AND ordercollection_deliverymode = 1 AND order_bakeryID = order_branchID";
        }
        else if (request.DeliveryMode == 2)
        {
            // Delivery By Hand: ordercollection_deliverymode=2 or (deliverymode=1 and bakeryID!=branchID)
            filters += " AND (ordercollection_deliverymode = 2 OR (ordercollection_deliverymode = 1 AND order_bakeryID != order_branchID))";
        }
        else if (request.DeliveryMode == 4)
        {
            // Delivery By Post: ordercollection_deliverymode=4
            filters += " AND ordercollection_deliverymode = 4";
        }

        // disptime filter — handled by the stored proc in legacy
        // Source: getcakecount_bydayID_ordertaskpage passes disptimeid
        // The dispatch time filtering is done at the SP level in legacy
        // We apply it here as a WHERE clause on dispatch hour
        if (request.DispTime > 0)
        {
            if (request.DispTime == 16)
            {
                // 4PM slot includes postal (dm=3,4) + collection/hand at 16:00
                filters += @" AND ((ordercollection_deliverymode IN (3,4)) 
                    OR (ordercollection_deliverymode = 1 AND DATEPART(hour, ordercollection_dispatchDate) >= 16)
                    OR (ordercollection_deliverymode = 2 AND DATEPART(hour, ordercollection_dispatchDate) - 2 >= 16))";
            }
            else
            {
                filters += @" AND ((ordercollection_deliverymode = 1 
                        AND DATEPART(hour, ordercollection_dispatchDate) >= @dispTimeStart 
                        AND DATEPART(hour, ordercollection_dispatchDate) < @dispTimeEnd)
                    OR (ordercollection_deliverymode = 2 
                        AND DATEPART(hour, ordercollection_dispatchDate) - 2 >= @dispTimeStart 
                        AND DATEPART(hour, ordercollection_dispatchDate) - 2 < @dispTimeEnd))";
                parameters.Add(new SqlParameter("@dispTimeStart", request.DispTime));
                parameters.Add(new SqlParameter("@dispTimeEnd", request.DispTime + 2));
            }
        }

        return filters;
    }

    // ─── Safe Reader Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Assigns a staff user to a task (creates or updates tbl_ordertask record).
    /// Source: bakeryorders.aspx.cs btnSelectTask_Click (lines 2740-2800)
    /// Legacy behaviour: if task exists and isCompleted, advances stage (11→12→22→33).
    /// If task doesn't exist, creates new with tasksts=11.
    /// Also creates tbl_ordertaskdet audit record.
    /// </summary>
    public async Task AssignUserToTaskAsync(long orderId, long orderDetailId, int userId, long webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if task record exists
        var checkSql = @"SELECT ordertask_Id, ordertask_tasksts, ordertask_isCompleted, ordertask_currUserID 
            FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId";
        
        long taskId = 0;
        int? currentSts = null;
        bool isCompleted = false;
        int prevUserId = 0;

        await using (var cmd = new SqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                taskId = reader.GetInt64(0);
                currentSts = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1);
                isCompleted = !reader.IsDBNull(2) && reader.GetBoolean(2);
                prevUserId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            }
        }

        if (taskId > 0)
        {
            // Task exists — assign user
            // Source: lines 2758-2768 — if isCompleted, advance stage
            int newSts = currentSts ?? 11;
            if (isCompleted)
            {
                // Advance stage: 11→12, 12→22, 22→33
                newSts = currentSts switch { 11 => 12, 12 => 22, 22 => 33, _ => currentSts ?? 11 };
            }

            var updateSql = @"UPDATE tbl_ordertask 
                SET ordertask_currUserID = @userId, ordertask_lastUserID = @prevUserId,
                    ordertask_tasksts = @newSts, ordertask_isCompleted = 0, ordertask_isDone = 0,
                    ordertask_modifiedOn = GETDATE()
                WHERE ordertask_Id = @taskId";
            await using (var cmd = new SqlCommand(updateSql, conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@prevUserId", prevUserId);
                cmd.Parameters.AddWithValue("@newSts", newSts);
                cmd.Parameters.AddWithValue("@taskId", taskId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Create audit record
            var auditSql = @"INSERT INTO tbl_ordertaskdet 
                (ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID, 
                 ordertaskdet_remarks, ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_isreply,
                 ordertaskdet_staDate, ordertaskdet_endDate, ordertaskdet_modifiedOn)
                VALUES (@taskId, @taskSts, @userId, '', 0, 0, 0, GETDATE(), GETDATE(), GETDATE())";
            await using (var cmd = new SqlCommand(auditSql, conn))
            {
                cmd.Parameters.AddWithValue("@taskId", taskId);
                cmd.Parameters.AddWithValue("@taskSts", newSts);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        else
        {
            // No task exists — create new with tasksts=11
            // Source: lines 2774-2784
            var insertSql = @"INSERT INTO tbl_ordertask 
                (ordertask_orderID, ordertask_orderdetailid, ordertask_currUserID, ordertask_lastUserID,
                 ordertask_tasksts, ordertask_isCompleted, ordertask_isDone, ordertask_ishold,
                 ordertaskdet_remarks, ordertask_createdOn, ordertask_modifiedOn)
                OUTPUT INSERTED.ordertask_Id
                VALUES (@orderId, @orderDetailId, @userId, 0, 11, 0, 0, 0, '', GETDATE(), GETDATE())";
            await using (var cmd = new SqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@orderId", orderId);
                cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
                cmd.Parameters.AddWithValue("@userId", userId);
                taskId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            // Create audit record
            var auditSql = @"INSERT INTO tbl_ordertaskdet 
                (ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID, 
                 ordertaskdet_remarks, ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_isreply,
                 ordertaskdet_staDate, ordertaskdet_endDate, ordertaskdet_modifiedOn)
                VALUES (@taskId, 11, @userId, '', 0, 0, 0, GETDATE(), GETDATE(), GETDATE())";
            await using (var cmd = new SqlCommand(auditSql, conn))
            {
                cmd.Parameters.AddWithValue("@taskId", taskId);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    // ─── Safe Reader Helpers (original) ────────────────────────────────────────

    /// <summary>
    /// Executes a task action (start/pause/complete/rewind/remarks/assign).
    /// Source: bakeryorders.aspx.cs lines 2600-2900 — task state machine.
    /// 
    /// Legacy state machine:
    /// - Start (Play): Advances stage NULL→11, 11→12, 12→22, 22→33. Creates tbl_ordertaskdet audit record.
    /// - Pause (Remarks/isDone): Sets isDone=1. Updates existing tbl_ordertaskdet endDate.
    /// - Stop (Complete): Sets isCompleted=isDone=1. Updates tbl_ordertaskdet isCompleted+isDone+endDate.
    ///   If tasksts=33, also sets order_status=2 (Processed).
    /// - Rewind: Moves back one stage. Resets isCompleted+isDone.
    /// </summary>
    public async Task ExecuteTaskActionAsync(TaskActionRequest request, long webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        switch (request.Action)
        {
            case "assign":
                await AssignUserToTaskAsync(request.OrderId, request.OrderDetailId, request.UserId, webshopId);
                break;

            case "start":
                await StartTaskAsync(conn, request.OrderId, request.OrderDetailId, webshopId);
                break;

            case "pause":
                await PauseTaskAsync(conn, request.OrderId, request.OrderDetailId, request.Remarks);
                break;

            case "complete":
                await CompleteTaskAsync(conn, request.OrderId, request.OrderDetailId, request.Remarks, webshopId);
                break;

            case "rewind":
                await RewindTaskAsync(conn, request.OrderId, request.OrderDetailId);
                break;

            case "remarks":
                await PauseTaskAsync(conn, request.OrderId, request.OrderDetailId, request.Remarks);
                break;
        }
    }

    /// <summary>
    /// Start/Play: Advances task stage and creates audit record.
    /// Source: bakeryorders.aspx.cs lines 2628-2645
    /// Stage progression: NULL→11, 11→12, 12→22, 22→33
    /// SEQUENTIAL ENFORCEMENT: Only advances if current stage isCompleted=true.
    /// If isCompleted=false, the task is still in progress — reject advancement.
    /// Source: lines 2622-2627 — "continue;" skips task if not ready for next stage.
    /// </summary>
    private async Task StartTaskAsync(SqlConnection conn, long orderId, long orderDetailId, long webshopId)
    {
        // Get current task state
        var getSql = @"SELECT ordertask_Id, ordertask_tasksts, ordertask_isCompleted, ordertask_isDone, ordertask_currUserID 
            FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId";
        
        long taskId = 0;
        int? currentSts = null;
        bool isCompleted = false;
        int currentUserId = 0;

        await using (var cmd = new SqlCommand(getSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                taskId = reader.GetInt64(0);
                currentSts = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1);
                isCompleted = !reader.IsDBNull(2) && reader.GetBoolean(2);
                currentUserId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            }
        }

        // SEQUENTIAL ENFORCEMENT (source: bakeryorders.aspx.cs lines 2622-2627)
        // Rule: Can only advance to next stage if current stage isCompleted=true
        // Exception: First start (no task exists or tasksts is NULL) is always allowed
        if (taskId > 0 && currentSts != null && !isCompleted)
        {
            // Current stage is NOT completed — cannot advance to next stage.
            // Legacy behaviour: "continue;" in the loop — silently skips.
            return;
        }

        // Also reject if already at stage 33 completed (Under Delivery is not started via Play)
        if (currentSts == 33 && isCompleted)
        {
            return;
        }

        // Compute next stage
        int nextSts;
        if (taskId == 0 || currentSts == null)
        {
            nextSts = 11; // First start — always Filling
        }
        else
        {
            // isCompleted=true — advance to next stage
            nextSts = currentSts switch
            {
                11 => 12,
                12 => 22,
                22 => 33,
                _ => currentSts.Value
            };
        }

        if (taskId > 0)
        {
            // Update existing task — advance stage, reset completion
            var updateSql = @"UPDATE tbl_ordertask 
                SET ordertask_tasksts = @nextSts, ordertask_isCompleted = 0, ordertask_isDone = 0, 
                    ordertask_modifiedOn = GETDATE()
                WHERE ordertask_Id = @taskId";
            await using (var cmd = new SqlCommand(updateSql, conn))
            {
                cmd.Parameters.AddWithValue("@nextSts", nextSts);
                cmd.Parameters.AddWithValue("@taskId", taskId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        else
        {
            // Create new task record (first time starting)
            var insertSql = @"INSERT INTO tbl_ordertask 
                (ordertask_orderID, ordertask_orderdetailid, ordertask_currUserID, ordertask_lastUserID,
                 ordertask_tasksts, ordertask_isCompleted, ordertask_isDone, ordertask_ishold,
                 ordertaskdet_remarks, ordertask_createdOn, ordertask_modifiedOn)
                OUTPUT INSERTED.ordertask_Id
                VALUES (@orderId, @orderDetailId, 0, 0, @nextSts, 0, 0, 0, '', GETDATE(), GETDATE())";
            await using (var cmd = new SqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@orderId", orderId);
                cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
                cmd.Parameters.AddWithValue("@nextSts", nextSts);
                taskId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
        }

        // Create audit record in tbl_ordertaskdet
        // Source: bakeryorders.aspx.cs lines 2633-2643
        var auditSql = @"INSERT INTO tbl_ordertaskdet 
            (ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID, 
             ordertaskdet_remarks, ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_isreply,
             ordertaskdet_staDate, ordertaskdet_endDate, ordertaskdet_modifiedOn)
            VALUES (@taskId, @taskSts, @userId, '', 0, 0, 0, GETDATE(), GETDATE(), GETDATE())";
        await using (var cmd = new SqlCommand(auditSql, conn))
        {
            cmd.Parameters.AddWithValue("@taskId", taskId);
            cmd.Parameters.AddWithValue("@taskSts", nextSts);
            cmd.Parameters.AddWithValue("@userId", currentUserId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Pause/Remarks: Sets isDone=1, saves remarks, updates audit record endDate.
    /// Source: bakeryorders.aspx.cs btnSaveRemarks_Click (lines 2850-2870)
    /// </summary>
    private async Task PauseTaskAsync(SqlConnection conn, long orderId, long orderDetailId, string remarks)
    {
        // Update main task record
        var updateSql = @"UPDATE tbl_ordertask 
            SET ordertask_isDone = 1, ordertaskdet_remarks = @remarks, ordertask_modifiedOn = GETDATE()
            WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId";
        await using (var cmd = new SqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        // Update latest audit record — set isDone=1, endDate, remarks
        // Source: vardatadet = db.ordertaskdet.Where(taskId && taskSts).OrderByDescending(Id).Take(1)
        var auditSql = @"UPDATE TOP(1) tbl_ordertaskdet 
            SET ordertaskdet_isDone = 1, ordertaskdet_endDate = GETDATE(), 
                ordertaskdet_modifiedOn = GETDATE(), ordertaskdet_remarks = @remarks
            WHERE ordertaskdet_taskId = (SELECT ordertask_Id FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId)
            AND ordertaskdet_taskSts = (SELECT ordertask_tasksts FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId)";
        await using (var cmd = new SqlCommand(auditSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Stop/Complete: Sets isCompleted=isDone=1, updates audit record, advances order status if tasksts=33.
    /// Source: bakeryorders.aspx.cs btnSubmitTask_Click (lines 2802-2840)
    /// </summary>
    private async Task CompleteTaskAsync(SqlConnection conn, long orderId, long orderDetailId, string remarks, long webshopId)
    {
        // Update main task record
        var updateSql = @"UPDATE tbl_ordertask 
            SET ordertask_isCompleted = 1, ordertask_isDone = 1, 
                ordertaskdet_remarks = @remarks, ordertask_modifiedOn = GETDATE()
            WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId";
        await using (var cmd = new SqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        // Update latest audit record — set isCompleted=isDone=1, endDate
        var auditSql = @"UPDATE TOP(1) tbl_ordertaskdet 
            SET ordertaskdet_isCompleted = 1, ordertaskdet_isDone = 1, 
                ordertaskdet_endDate = GETDATE(), ordertaskdet_modifiedOn = GETDATE(),
                ordertaskdet_remarks = @remarks
            WHERE ordertaskdet_taskId = (SELECT ordertask_Id FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId)
            AND ordertaskdet_taskSts = (SELECT ordertask_tasksts FROM tbl_ordertask WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId)";
        await using (var cmd = new SqlCommand(auditSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        // If tasksts=33 (Finishing complete), set order_status=2 (Processed)
        // Source: bakeryorders.aspx.cs line 2836
        var statusSql = @"UPDATE tbl_order SET order_status = 2 
            WHERE order_ID = @orderId 
            AND (order_bakeryID = @webshopId OR order_branchID = @webshopId)
            AND order_ID IN (SELECT ordertask_orderID FROM tbl_ordertask 
                WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId 
                AND ordertask_tasksts = 33)";
        await using (var cmd = new SqlCommand(statusSql, conn))
        {
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);
            cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Rewind: Moves task back one stage, resets completion flags.
    /// </summary>
    private async Task RewindTaskAsync(SqlConnection conn, long orderId, long orderDetailId)
    {
        var rewindSql = @"UPDATE tbl_ordertask 
            SET ordertask_tasksts = CASE 
                WHEN ordertask_tasksts = 12 THEN 11
                WHEN ordertask_tasksts = 22 THEN 12
                WHEN ordertask_tasksts = 33 THEN 22
                ELSE ordertask_tasksts END,
            ordertask_isCompleted = 0, ordertask_isDone = 0, ordertask_modifiedOn = GETDATE()
            WHERE ordertask_orderID = @orderId AND ordertask_orderdetailid = @orderDetailId";
        await using var cmd = new SqlCommand(rewindSql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Safe Reader Helpers ───────────────────────────────────────────────────

    private static string GetStringSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? "" : reader.GetValue(ordinal).ToString() ?? "";
    }

    private static long GetInt64Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var val = reader.GetValue(ordinal);
        return Convert.ToInt64(val);
    }

    private static int GetInt32Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var val = reader.GetValue(ordinal);
        return Convert.ToInt32(val);
    }

    private static decimal GetDecimalSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var val = reader.GetValue(ordinal);
        return Convert.ToDecimal(val);
    }

    private static bool GetBoolSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return false;
        var val = reader.GetValue(ordinal);
        if (val is bool b) return b;
        return Convert.ToBoolean(val);
    }

    private static DateTime GetDateTimeSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
        return reader.GetDateTime(ordinal);
    }
}
