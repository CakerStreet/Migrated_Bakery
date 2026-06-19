using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the franchise bakery orders page.
/// Route: /franchiseorders
/// Migrated from FranchiseBakeryorders.aspx — supports order listing by status,
/// assigned tasks view (ordertype=12), batch status updates, cancel, reviews, and task management.
/// </summary>
[Route("franchiseorders")]
[Route("FranchiseBakeryorders.aspx")]
public class FranchiseBakeryOrdersController : Controller
{
    private readonly IConfiguration _config;

    public FranchiseBakeryOrdersController(IConfiguration config)
    {
        _config = config;
    }

    // ─── HELPER: Status Text ────────────────────────────────────────────────────
    public static string GetStatusText(int orderStatus)
    {
        return orderStatus switch
        {
            0 => "Pending",
            1 => "Confirmed",
            2 => "Processed",
            3 => "Under Delivery",
            4 => "Completed",
            5 => "Job Assigned",
            11 => "Cancelled",
            _ => ""
        };
    }

    public static string GetStatusText_task(string orderStatus)
    {
        return orderStatus switch
        {
            "" => "Not Started Yet",
            "11" => "Filling",
            "12" => "Icing",
            "22" => "Decoration",
            "33" => "Finishing",
            "44" => "Under Delivery",
            _ => ""
        };
    }

    public static string GetStatusText_taskBtn(string productType, string orderTaskStatus, string isCompleted, string customerName)
    {
        bool completed = !string.IsNullOrEmpty(isCompleted) && bool.TryParse(isCompleted, out var c) && c;
        if (productType == "2")
        {
            return orderTaskStatus switch
            {
                "" => "Start Finishing",
                "33" => completed ? "Under Delivery" : $"Finishing ({customerName})",
                "44" => "Under Delivery",
                _ => ""
            };
        }
        else
        {
            return orderTaskStatus switch
            {
                "" => "Not Started Yet",
                "11" => completed ? "Start Icing" : $"Filling ({customerName})",
                "12" => completed ? "Start Decoration" : $"Icing ({customerName})",
                "22" => completed ? "Start Finishing" : $"Decoration ({customerName})",
                "33" => completed ? "Under Delivery" : $"Finishing ({customerName})",
                "44" => "Under Delivery",
                _ => ""
            };
        }
    }

    // ─── HELPER: Get day ID by day name ─────────────────────────────────────────
    private static int GetDayIdByName(string dayName)
    {
        return dayName.ToLower() switch
        {
            "monday" => 1,
            "tuesday" => 2,
            "wednesday" => 3,
            "thursday" => 4,
            "friday" => 5,
            "saturday" => 6,
            "sunday" => 7,
            _ => 0
        };
    }

    private static string GetDayNameById(int dayId)
    {
        return dayId switch
        {
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            7 => "Sunday",
            _ => ""
        };
    }

    // ─── MAIN INDEX ACTION ──────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int ordertype = 0,
        string? from = null,
        string? to = null,
        string? q = null,
        int dt = 1,
        int dm = 0,
        int dayID = 0,
        int tasktype = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(webshopId) || webshopId == "0")
            return Redirect("/?returl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));


        int domainID = 0;
        try
        {
        // Get franchise domain ID
        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT franchiseUser_kioskID FROM tbl_franchiseUser WHERE franchiseUser_webstoreID = @wsid", conn);
            cmd.Parameters.AddWithValue("@wsid", long.Parse(webshopId));
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                domainID = Convert.ToInt32(result);
        }

        // Early return for completed tab without filters
        if (ordertype == 4 && string.IsNullOrEmpty(from) && string.IsNullOrEmpty(q))
        {
            ViewBag.OrderType = ordertype;
            ViewBag.ActiveTab = "4";
            ViewBag.Orders = new DataTable();
            ViewBag.TotalCount = "0";
            ViewBag.HasOrders = false;
            ViewBag.UserType = userType;
            ViewBag.DrpDateValue = dt.ToString();
            ViewBag.StartDate = "";
            ViewBag.EndDate = "";
            ViewBag.SearchQuery = "";
            return View("Index");
        }

        // Build date filter
        string strFilter = "";
        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
        {
            if (dt == 1)
            {
                strFilter += $" and (dbo.DateOnly(stockBatch_modifiedOn) >= '{from}' and dbo.DateOnly(stockBatch_modifiedOn) <= '{to}')";
            }
            else
            {
                strFilter += $" and (dbo.DateOnly(stockBatch_Date) >= '{from}' and dbo.DateOnly(stockBatch_Date) <= '{to}')";
            }
        }

        // Build search filter
        if (!string.IsNullOrEmpty(q))
        {
            if (long.TryParse(q, out var searchId))
            {
                strFilter += $" and (order_Id={q} or order_followingorderid={q} or order_customerName like '%{q}%' or order_customerEmail like '%{q}%') ";
            }
            else
            {
                strFilter += $" and (order_customerName like '%{q}%' or order_customerEmail like '%{q}%' or order_ID in (select orderDetail_orderID from tbl_orderDetail inner join tbl_products on orderDetail_productID=product_ID where (product_code ='{q}' or product_name like '{q}%'))) ";
            }
        }

        // Delivery mode filter
        if (dm > 0)
        {
            strFilter += $" and (ordercollection_deliverymode={dm}) ";
        }

        ViewBag.OrderType = ordertype;
        ViewBag.UserType = userType;
        ViewBag.UserId = userId;
        ViewBag.DrpDateValue = dt.ToString();
        ViewBag.StartDate = from ?? "";
        ViewBag.EndDate = to ?? "";
        ViewBag.SearchQuery = q ?? "";
        ViewBag.DayID = dayID;
        ViewBag.TaskType = tasktype;
        ViewBag.DeliveryMode = dm;

        // ─── ORDERTYPE 12: Assigned Tasks ───────────────────────────────────────
        if (ordertype == 12)
        {
            return await HandleAssignedTasks(webshopId, userId, userType, domainID, strFilter, dayID, tasktype, q, from, to, dt);
        }

        // ─── Standard order types (0,1,2,3,4,5,10,11) ──────────────────────────
        return await HandleStandardOrders(webshopId, ordertype, strFilter, dt, q, from, to);
        }
        catch (Exception)
        {
            // Gracefully render with empty data when DB tables are unavailable
            ViewBag.OrderType = ordertype;
            ViewBag.ActiveTab = ordertype.ToString();
            ViewBag.Orders = new DataTable();
            ViewBag.TotalCount = "0";
            ViewBag.HasOrders = false;
            ViewBag.UserType = userType;
            ViewBag.DrpDateValue = dt.ToString();
            ViewBag.StartDate = from ?? "";
            ViewBag.EndDate = to ?? "";
            ViewBag.SearchQuery = q ?? "";
            ViewBag.DomainID = domainID;
            ViewBag.StaffUsers = new DataTable();
            ViewBag.DayTabs = new List<DayTab>();
            ViewBag.TaskGroups = new List<object>();
            ViewBag.TaskCounts = (object?)null;
            ViewBag.TaskType = tasktype;
            ViewBag.BakeryClosed = false;
            ViewBag.FilterStr = "";
            ViewBag.TaskDetName = "";
            ViewBag.ShowBulkActions = false;
            return View("Index");
        }
    }

    // ─── Standard Orders Handler ────────────────────────────────────────────────
    private async Task<IActionResult> HandleStandardOrders(string webshopId, int ordertype, string strFilter, int dt, string? q, string? from, string? to)
    {
        var eposConnStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        var dtOrders = new DataTable();

        string tableName, whereClause, columnNames;
        string orderTypeStr = ordertype.ToString();

        // Franchise orders use StockBatch table from the epos database
        if (ordertype == 0 || ordertype == 1 || ordertype == 2 || ordertype == 3 || ordertype == 4 || ordertype == 5)
        {
            tableName = "tbl_StockBatch inner join db_cakerstreet_franchise.dbo.tbl_domains b on stockBatch_domainID = b.domain_ID";
            whereClause = "stockBatch_InOutMode=1 " + (string.IsNullOrEmpty(q) ? $"and stockBatch_status={orderTypeStr} " : "") + strFilter + " order by stockBatch_Date desc";
            columnNames = @"stockBatch_ID order_ID,stockBatch_title,stockBatch_status order_status,stockBatch_modifiedOn order_date,domain_Name order_customerName,domain_ContactNo shipping_phone,domain_postcode shipping_zip,stockBatch_ReqQty order_quality,stockBatch_Date ordercollection_Date
,stockBatch_Date ordercollection_dispatchDate,stockBatch_Date ordercollection_OcasionDate,stockBatch_domainID order_customerID, 'UB1 3AF' webstore_postcode,
isnull((select top 1 cast(Distance as nvarchar(50))+','+cast(DistanceSeconds as nvarchar(50)) from db_cakerstreet_live.dbo.tbl_PostcodeDistance where (
(Postcode2=replace('UB1 3AF',' ','') and Postcode1=replace(domain_postcode,' ',''))
or 
(Postcode1=replace('UB1 3AF',' ','') and Postcode2=replace(domain_postcode,' ',''))
)
)
,'') webstore_postcodedet";

            await using var conn = new SqlConnection(eposConnStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT {columnNames} FROM {tableName} WHERE {whereClause}", conn);
            cmd.CommandTimeout = 60;
            using var reader = await cmd.ExecuteReaderAsync();
            dtOrders.Load(reader);
        }
        else if (ordertype == 10)
        {
            // Forwarded orders
            tableName = "tbl_order inner join tbl_ordercollection on order_ID=ordercollection_OrderID inner join tbl_shippingDetail on shipping_orderID=order_ID left join tbl_orderreviews on orderreviews_orderID=order_ID";
            whereClause = $"order_bakeryID={webshopId} and order_followingOrderid>0 and order_isPurchased=1 and order_isdeleted=0 " + (string.IsNullOrEmpty(q) ? "and order_status=0 " : "") + strFilter + $" order by {(dt == 1 ? "order_date desc" : "ordercollection_Date")} ";
            columnNames = "*,'' webstore_postcodedet";

            await using var conn = new SqlConnection(eposConnStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT {columnNames} FROM {tableName} WHERE {whereClause}", conn);
            cmd.CommandTimeout = 60;
            using var reader = await cmd.ExecuteReaderAsync();
            dtOrders.Load(reader);
        }
        else if (ordertype == 11)
        {
            // Cancelled orders
            tableName = "tbl_order inner join tbl_ordercollection on order_ID=ordercollection_OrderID inner join tbl_shippingDetail on shipping_orderID=order_ID left join tbl_orderreviews on orderreviews_orderID=order_ID inner join tbl_webstore b on order_bakeryid = b.webstore_id";
            whereClause = $"order_bakeryID={webshopId} and order_isPurchased=1 and order_isdeleted=0 " + (string.IsNullOrEmpty(q) ? "and order_status=11 " : "") + strFilter + " order by ordercollection_Date ";
            columnNames = @"top 50 order_ID, order_status, order_date, order_forwardedorderid, order_customerName, shipping_phone, shipping_zip, orderreviews_stars, orderreviews_remarks, order_quality, order_totalPrice, order_CSmargin, order_shopMargin, order_payoutRefund, order_csRefund, order_bakeryID, ordercollection_Date, ordercollection_dispatchDate, ordercollection_OcasionDate, ordercollection_deliverymode, order_saletype,'' webstore_postcodedet,webstore_postcode,
isnull((select top 1 cast(Distance as nvarchar(50))+','+cast(DistanceSeconds as nvarchar(50)) from tbl_PostcodeDistance where (
(Postcode2=replace(b.webstore_postcode,' ','') and Postcode1=replace(shipping_zip,' ',''))
or 
(Postcode1=replace(b.webstore_postcode,' ','') and Postcode2=replace(shipping_zip,' ',''))
)
)
,'') webstore_postcodedet";

            await using var conn = new SqlConnection(eposConnStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT {columnNames} FROM {tableName} WHERE {whereClause}", conn);
            cmd.CommandTimeout = 60;
            using var reader = await cmd.ExecuteReaderAsync();
            dtOrders.Load(reader);
        }

        // Get order details for the orders found
        var orderDetailDt = new DataTable();
        if (dtOrders.Rows.Count > 0)
        {
            var orderIds = new List<string>();
            foreach (DataRow dr in dtOrders.Rows)
                orderIds.Add(dr["order_ID"].ToString()!);

            orderDetailDt = await GetOrderDetails(orderIds);
        }

        // Set active tab
        string activeTab = ordertype.ToString();
        ViewBag.ActiveTab = activeTab;
        ViewBag.Orders = dtOrders;
        ViewBag.OrderDetails = orderDetailDt;
        ViewBag.TotalCount = dtOrders.Rows.Count.ToString();
        ViewBag.HasOrders = dtOrders.Rows.Count > 0;

        // Show bulk action buttons
        if (ordertype > 0 && dtOrders.Rows.Count > 0 && ordertype != 12)
        {
            ViewBag.ShowBulkActions = true;
            ViewBag.ShowJobAssigned = (ordertype == 1);
            ViewBag.ShowUnderDelivery = (ordertype <= 2 || ordertype == 5);
            ViewBag.ShowComplete = (ordertype <= 3 || ordertype == 5);
            ViewBag.ShowDelete = (ordertype == 4 || ordertype == 11);
        }
        else
        {
            ViewBag.ShowBulkActions = false;
        }

        return View("Index");
    }

    // ─── Assigned Tasks Handler (ordertype=12) ──────────────────────────────────
    private async Task<IActionResult> HandleAssignedTasks(
        string webshopId, string userId, string userType,
        int domainID, string strFilter, int dayID, int tasktype,
        string? q, string? from, string? to, int dt)
    {
        long wsId = long.Parse(webshopId);
        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

        // Build day tabs
        DateTime dttaskdate = DateTime.Today.AddDays(-1);
        var dayTabs = new List<DayTab>();
        bool bakeryclosed = false;

        for (int i = 0; i < 7; i++)
        {
            var dayDate = dttaskdate.AddDays(i);
            int dayId = GetDayIdByName(dayDate.DayOfWeek.ToString());
            string cakeCount = "0";

            // Get cake counts for this day
            await using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM tbl_StockBatch 
                    INNER JOIN db_cakerstreet_franchise.dbo.tbl_domains ON stockBatch_domainID = domain_ID
                    WHERE stockBatch_InOutMode=1 AND stockBatch_status IN (5,3)
                    AND dbo.dateonly(stockBatch_Date) = dbo.dateonly(@dayDate)
                    AND (@domainID = 0 OR stockBatch_domainID = @domainID)", conn);
                cmd.Parameters.AddWithValue("@dayDate", dayDate.ToString("dd/MM/yyyy"));
                cmd.Parameters.AddWithValue("@domainID", domainID);
                cmd.CommandTimeout = 30;
                try
                {
                    var result = await cmd.ExecuteScalarAsync();
                    cakeCount = result?.ToString() ?? "0";
                }
                catch { cakeCount = "0"; }
            }

            dayTabs.Add(new DayTab
            {
                DayDate = dayDate,
                DayID = dayId,
                DayName = dayDate.DayOfWeek.ToString(),
                CountCakes = cakeCount,
                IsClosed = false
            });
        }

        // Set the current task date
        DateTime currtaskdate = DateTime.Today.Date;
        string? manifestUrl = null;
        string? spongeUrl = null;

        if (dayID > 0)
        {
            var dayTab = dayTabs.FirstOrDefault(d => d.DayID == dayID);
            if (dayTab != null)
            {
                currtaskdate = dayTab.DayDate.Date;
                manifestUrl = $"ordermenifest?from={currtaskdate:dd/MM/yyyy}";
                spongeUrl = $"orderspongelist?from={currtaskdate:dd/MM/yyyy}";
            }
        }

        // Build the assigned task filter
        string taskFilter = strFilter;
        if (string.IsNullOrEmpty(q))
        {
            taskFilter += " and ((order_status in (5,3))) ";
            if (dayID > 0)
            {
                var dayTab = dayTabs.FirstOrDefault(d => d.DayID == dayID);
                if (dayTab != null)
                {
                    taskFilter += $" and dbo.dateonly(ordercollection_dispatchDate)=dbo.dateonly('{dayTab.DayDate:dd/MM/yyyy}')";
                }
            }
        }
        else
        {
            taskFilter += " and order_status<>11 ";
        }

        // Get staff users
        var staffUsers = new List<StaffUser>();
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT customer_ID, customer_Name FROM tbl_BakeryUser 
                WHERE customer_type=3 AND customer_stafftype=1 AND customer_webshopID=@wsid 
                AND customer_isActive=1 AND customer_isOpen=1 
                ORDER BY customer_Name", conn);
            cmd.Parameters.AddWithValue("@wsid", wsId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                staffUsers.Add(new StaffUser
                {
                    UserId = reader.GetInt64(0),
                    Username = reader.GetString(1)
                });
            }
        }

        // Sort logic
        string sortColumn;
        if (userType == "3")
        {
            sortColumn = $"case when ordertask_currUserID is null then 2 when ordertask_currUserID={userId} and ordertask_isdone=0 then 0 when ordertask_isdone=0 then 1 else ordertask_tasksts end sortID,";
        }
        else
        {
            sortColumn = "case when ordertask_currUserID is null then 2 when ordertask_isdone=0 then 1 else ordertask_tasksts end sortID,";
        }

        // Main assigned tasks query
        string assignedColumns = sortColumn + @"product_type,order_status, product_SEOURL, product_image1, product_Name, CakeShapeTitle, CakeTypeTitle, sizeID, orderTopperCharge_charges, orderCakeWorth_ID, orderCakeWorth_cakeWorth,
orderCakeWorth_topperWorth, orderCakeWorth_FillingWorth, orderCakeWorth_icingWorth, orderCakeWorth_decorationWorth, SizeTitle, orderDetail_ShapeText, CakeShapeCustomText, order_customerName, shipping_phone, shipping_zip,orderreviews_remarks, ordertask_tasksts, log_orderprint_orderId, orderreviews_stars, ordertask_isCompleted, ordertask_isdone, ordertask_Id,
customer_Name, ordertaskdet_remarks, order_bakeryID, order_ID, product_ID, order_forwardedorderid, orderDetail_ID, order_quality,
orderDetail_Quantity, orderDetail_shapeId, ordercollection_deliverymode, order_saletype, order_date, ordercollection_Date, ordercollection_dispatchDate, 
ordercollection_OcasionDate, order_totalPrice, order_prdTotal, order_CSmargin, order_shopMargin, order_payoutRefund, order_csRefund, orderDetail_orderID,
IsChangeOrderImageMarked = isnull(om.IsUpdated, 1),
isnull((select count_big(1) from tbl_OrderTaskAssign where OrderTaskAssign_OrderID=order_ID and OrderTaskAssign_OrderDetail_Id=orderDetail_ID and OrderTaskAssign_isdeleted=0),0) 
countassignedtouser, 
IsChangeOrderImageMarked = case when order_status = 0 then case when om.IsUpdated is null or om.IsUpdated = 0 then 0 else 1 end else 1 end,
IsUpdated = isnull(om.IsUpdated, -1)";

        string assignedTable = @"tbl_order o inner join tbl_ordercollection c on order_ID=ordercollection_OrderID inner join tbl_orderDetail d on d.orderDetail_orderID=order_ID inner join tbl_products p on product_ID=orderDetail_productID inner join tbl_shippingDetail s on shipping_orderID=order_ID left join tbl_orderreviews v on orderreviews_orderID=order_ID left join tbl_ordertask t on ordertask_orderID=order_ID and ordertask_orderdetailid = orderDetail_ID left join tbl_bakeryuser u on customer_ID=ordertask_currUserID left join tbl_ordersorting os on ordersorting_orderID=order_ID left join tbl_log_orderprint lo on log_orderprint_orderId = order_ID and log_orderprint_typeId = 3 left join tbl_CakeShape cs on CakeShapeID=orderDetail_shapeId and orderDetail_shapeId>0 left join tbl_CakeType ct on CakeTypeID=orderDetail_TypeID and orderDetail_TypeID>0 left join tbl_CakeSize csz on SizeID=orderDetail_SizeID and orderDetail_SizeID>0 left join tbl_orderTopperCharge otc on orderTopperCharge_orderID=order_ID left outer join tbl_orderImageUpdate om on d.orderDetail_ID = OrderImage_orderDetail_ID
left join tbl_orderCakeWorth ft on ft.orderCakeWorth_productID=d.orderDetail_productID and ft.orderCakeWorth_orderDetailID=d.orderDetail_ID";

        string assignedWhere = $"order_bakeryID={webshopId} and order_isPurchased=1 and order_isdeleted=0 {taskFilter} order by ordercollection_dispatchDate, isnull(ordersorting_displayorder,100), ordercollection_Date";

        var taskRows = new List<TaskDetailRow>();
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT {assignedColumns} FROM {assignedTable} WHERE {assignedWhere}", conn);
            cmd.CommandTimeout = 60;
            try
            {
                using var reader = await cmd.ExecuteReaderAsync();
                var dtResult = new DataTable();
                dtResult.Load(reader);

                // Track seen order IDs for order_showprint
                var seenOrderIds = new HashSet<long>();

                foreach (DataRow dr in dtResult.Rows)
                {
                    var row = new TaskDetailRow();
                    row.OrderId = Convert.ToInt64(dr["order_ID"]);
                    row.OrderDetailId = Convert.ToInt64(dr["orderDetail_ID"]);
                    row.ProductId = Convert.ToInt64(dr["product_ID"]);
                    row.ProductType = Convert.ToInt32(dr["product_type"]);
                    row.ProductName = dr["product_Name"].ToString() ?? "";
                    row.ProductSeoUrl = dr["product_SEOURL"].ToString() ?? "";
                    row.ProductImage1 = dr["product_image1"].ToString() ?? "";
                    row.OrderStatus = Convert.ToInt32(dr["order_status"]);
                    row.SortID = Convert.ToInt32(dr["sortID"]);
                    row.CakeShapeTitle = dr["CakeShapeTitle"].ToString() ?? "";
                    row.CakeTypeTitle = dr["CakeTypeTitle"].ToString() ?? "";
                    row.SizeID = dr["sizeID"].ToString() ?? "";
                    row.SizeTitle = dr["SizeTitle"].ToString() ?? "";
                    row.OrderDetailShapeText = dr["orderDetail_ShapeText"].ToString() ?? "";
                    row.CakeShapeCustomText = dr["CakeShapeCustomText"].ToString() ?? "";
                    row.OrderCustomerName = dr["order_customerName"].ToString() ?? "";
                    row.ShippingPhone = dr["shipping_phone"].ToString() ?? "";
                    row.ShippingZip = dr["shipping_zip"].ToString() ?? "";
                    row.OrderReviewsRemarks = dr["orderreviews_remarks"].ToString() ?? "";
                    row.OrderReviewsStars = dr["orderreviews_stars"].ToString() ?? "";
                    row.OrderTaskTaskSts = dr["ordertask_tasksts"].ToString() ?? "";
                    row.LogOrderPrintOrderId = dr["log_orderprint_orderId"].ToString() ?? "";
                    row.OrderTaskId = dr["ordertask_Id"].ToString() ?? "";
                    row.CustomerName = dr["customer_Name"].ToString() ?? "";
                    row.OrderTaskDetRemarks = dr["ordertaskdet_remarks"].ToString() ?? "";
                    row.OrderBakeryId = Convert.ToInt64(dr["order_bakeryID"]);
                    row.OrderForwardedOrderId = Convert.ToInt64(dr["order_forwardedorderid"]);
                    row.OrderQuality = Convert.ToInt32(dr["order_quality"]);
                    row.OrderDetailQuantity = Convert.ToInt32(dr["orderDetail_Quantity"]);
                    row.OrderDetailShapeId = Convert.ToInt32(dr["orderDetail_shapeId"]);
                    row.OrderCollectionDeliveryMode = Convert.ToInt32(dr["ordercollection_deliverymode"]);
                    row.OrderSaleType = Convert.ToInt32(dr["order_saletype"]);
                    row.OrderDate = Convert.ToDateTime(dr["order_date"]);
                    row.OrderCollectionDate = Convert.ToDateTime(dr["ordercollection_Date"]);
                    row.OrderCollectionDispatchDate = Convert.ToDateTime(dr["ordercollection_dispatchDate"]);
                    row.OrderCollectionOccasionDate = Convert.ToDateTime(dr["ordercollection_OcasionDate"]);
                    row.OrderTotalPrice = Convert.ToDecimal(dr["order_totalPrice"]);
                    row.OrderPrdTotal = Convert.ToDecimal(dr["order_prdTotal"]);
                    row.OrderCSMargin = Convert.ToDecimal(dr["order_CSmargin"]);
                    row.OrderShopMargin = Convert.ToDecimal(dr["order_shopMargin"]);
                    row.OrderPayoutRefund = Convert.ToDecimal(dr["order_payoutRefund"]);
                    row.OrderCSRefund = Convert.ToDecimal(dr["order_csRefund"]);
                    row.CountAssignedToUser = dr["countassignedtouser"].ToString() ?? "0";
                    row.DispatchDate = row.OrderCollectionDispatchDate.Date;
                    row.IsChangeOrderImageMarked = Convert.ToBoolean(dr["IsChangeOrderImageMarked"]);

                    // TopperCharges and cake worth
                    row.TopperCharges = string.IsNullOrEmpty(dr["orderTopperCharge_charges"].ToString()) ? "0" : dr["orderTopperCharge_charges"].ToString()!;
                    row.FullCakeWorth = "0";
                    row.FillingWorth = "0";
                    row.IcingWorth = "0";
                    row.DecorationWorth = "0";
                    if (!string.IsNullOrEmpty(dr["orderCakeWorth_ID"].ToString()))
                    {
                        row.FullCakeWorth = Math.Round(Convert.ToDouble(dr["orderCakeWorth_cakeWorth"]), 2).ToString();
                        row.TopperCharges = Math.Round(Convert.ToDouble(dr["orderCakeWorth_topperWorth"]), 2).ToString();
                        row.FillingWorth = Math.Round(Convert.ToDouble(dr["orderCakeWorth_FillingWorth"]), 2).ToString();
                        row.IcingWorth = Math.Round(Convert.ToDouble(dr["orderCakeWorth_icingWorth"]), 2).ToString();
                        row.DecorationWorth = Math.Round(Convert.ToDouble(dr["orderCakeWorth_decorationWorth"]), 2).ToString();
                    }

                    // OrderTaskIsCompleted + OrderTaskIsDone
                    row.OrderTaskIsCompleted = (dr["order_status"].ToString() == "3" && string.IsNullOrEmpty(dr["ordertask_isCompleted"].ToString())) ? "False" : (dr["ordertask_isCompleted"].ToString() ?? "");
                    row.OrderTaskIsDone = (dr["order_status"].ToString() == "3" && string.IsNullOrEmpty(dr["ordertask_isdone"].ToString())) ? "False" : (dr["ordertask_isdone"].ToString() ?? "");

                    // order_showprint
                    row.OrderShowPrint = seenOrderIds.Contains(row.OrderId) ? 0 : 1;
                    seenOrderIds.Add(row.OrderId);

                    taskRows.Add(row);
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.Message;
            }
        }

        // Apply task type filtering (client-side in legacy, now server-side)
        var filteredRows = taskRows.ToList();
        if (string.IsNullOrEmpty(q))
        {
            if (tasktype == 0)
            {
                filteredRows = taskRows.Where(w => w.OrderStatus == 5 || w.OrderStatus == 3).ToList();
            }
            else if (tasktype == 11)
            {
                // Filling
                filteredRows = taskRows.Where(w => w.ProductType != 2 && (w.OrderStatus == 5 && (string.IsNullOrEmpty(w.OrderTaskTaskSts) || (w.OrderTaskTaskSts == "11" && !ParseBool(w.OrderTaskIsCompleted))))).ToList();
            }
            else if (tasktype == 12)
            {
                // Icing
                filteredRows = taskRows.Where(w => w.ProductType != 2 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "12" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "11" && ParseBool(w.OrderTaskIsCompleted)))).ToList();
            }
            else if (tasktype == 22)
            {
                // Decoration
                filteredRows = taskRows.Where(w => w.ProductType != 2 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "22" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "12" && ParseBool(w.OrderTaskIsCompleted)))).ToList();
            }
            else if (tasktype == 33)
            {
                // Finishing
                filteredRows = taskRows.Where(w =>
                    (w.ProductType == 2 && (string.IsNullOrEmpty(w.OrderTaskTaskSts) || !ParseBool(w.OrderTaskIsCompleted))) ||
                    (w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "33" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "22" && ParseBool(w.OrderTaskIsCompleted))))
                ).ToList();
            }
            else if (tasktype == 44)
            {
                // Under Delivery
                filteredRows = taskRows.Where(w =>
                    (w.OrderStatus == 3) || (w.OrderStatus == 5 && w.OrderTaskTaskSts == "44" && ParseBool(w.OrderTaskIsCompleted))
                ).ToList();
            }
        }

        // Build task type counts
        var taskCounts = new TaskTypeCounts();
        var sourceForCounts = taskRows;
        if (dayID > 0)
        {
            string dayName = GetDayNameById(dayID);
            sourceForCounts = taskRows.Where(w => w.OrderCollectionDispatchDate.DayOfWeek.ToString() == dayName).ToList();
        }

        taskCounts.FillingCakes = sourceForCounts.Count(w => w.ProductType == 1 && w.OrderStatus == 5 && (string.IsNullOrEmpty(w.OrderTaskTaskSts) || (w.OrderTaskTaskSts == "11" && !ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.FillingCupcakes = sourceForCounts.Count(w => w.ProductType == 6 && w.OrderStatus == 5 && (string.IsNullOrEmpty(w.OrderTaskTaskSts) || (w.OrderTaskTaskSts == "11" && !ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.IcingCakes = sourceForCounts.Count(w => w.ProductType == 1 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "12" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "11" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.IcingCupcakes = sourceForCounts.Count(w => w.ProductType == 6 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "12" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "11" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.DecorationCakes = sourceForCounts.Count(w => w.ProductType == 1 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "22" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "12" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.DecorationCupcakes = sourceForCounts.Count(w => w.ProductType == 6 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "22" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "12" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.FinishingCakes = sourceForCounts.Count(w => w.ProductType == 1 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "33" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "22" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.FinishingCupcakes = sourceForCounts.Count(w => w.ProductType == 6 && w.OrderStatus == 5 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderTaskTaskSts == "33" && !ParseBool(w.OrderTaskIsCompleted)) || (w.OrderTaskTaskSts == "22" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.UnderDeliveryCakes = sourceForCounts.Count(w => w.ProductType == 1 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderStatus == 3 && ParseBool(w.OrderTaskIsCompleted)) || (w.OrderStatus == 5 && w.OrderTaskTaskSts == "44" && ParseBool(w.OrderTaskIsCompleted))));
        taskCounts.UnderDeliveryCupcakes = sourceForCounts.Count(w => w.ProductType == 6 && !string.IsNullOrEmpty(w.OrderTaskTaskSts) && ((w.OrderStatus == 3 && ParseBool(w.OrderTaskIsCompleted)) || (w.OrderStatus == 5 && w.OrderTaskTaskSts == "44" && ParseBool(w.OrderTaskIsCompleted))));

        // Group by dispatch date
        var grouped = filteredRows.GroupBy(g => g.DispatchDate)
            .Select(n => new TaskGroup { GroupDate = n.Key, Items = n.OrderBy(o => o.SortID).ThenBy(o => o.OrderCollectionDeliveryMode == 2 ? 0 : o.OrderCollectionDeliveryMode).ToList() })
            .OrderBy(o => o.GroupDate).ToList();

        // Totals
        int totalCakes = filteredRows.Count(w => w.ProductType == 1);
        int totalCupcakes = filteredRows.Count(w => w.ProductType == 6);
        int totalAccessories = taskRows.Count(w => w.ProductType == 2);
        string totalText = $"{totalCakes} Cakes | {totalCupcakes} Cupcakes | {totalAccessories} Party Accessories";

        // Build filter string for links
        string filterStr = "";
        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
            filterStr = $"&from={from}&to={to}&dt={dt}";

        ViewBag.ActiveTab = "5"; // Job Assigned tab active
        ViewBag.AssignedTaskTab = true;
        ViewBag.TaskGroups = grouped;
        ViewBag.TaskCounts = taskCounts;
        ViewBag.TotalCount = totalText;
        ViewBag.HasOrders = filteredRows.Count > 0;
        ViewBag.DayTabs = dayTabs;
        ViewBag.CurrentTaskDate = currtaskdate;
        ViewBag.ManifestUrl = manifestUrl;
        ViewBag.SpongeUrl = spongeUrl;
        ViewBag.StaffUsers = staffUsers;
        ViewBag.BakeryClosed = bakeryclosed;
        ViewBag.FilterStr = filterStr;

        // Task name for floating panel
        string taskDetName = tasktype switch
        {
            11 => "Filling",
            12 => "Icing",
            22 => "Decoration",
            33 => "Finishing",
            _ => "Filling"
        };
        ViewBag.TaskDetName = taskDetName;

        return View("Index");
    }

    // ─── MUTATION: Update Order Status (batch) ──────────────────────────────────
    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] FranchiseOrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        if (string.IsNullOrEmpty(request.OrderIds))
            return Json(new { success = false, message = "No orders selected." });

        var allowedStatuses = new[] { 1, 2, 3, 4, 5, 11 };
        if (!allowedStatuses.Contains(request.NewStatus))
            return Json(new { success = false, message = "Invalid status value." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Log each order
        foreach (var ordId in request.OrderIds.Split(',').Where(s => long.TryParse(s, out _)))
        {
            using var logCmd = new SqlCommand(@"
                INSERT INTO tbl_Franchiseorderlog (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn)
                VALUES (@userId, @status, @orderId, GETDATE())", conn);
            logCmd.Parameters.AddWithValue("@userId", int.Parse(userId));
            logCmd.Parameters.AddWithValue("@status", request.NewStatus);
            logCmd.Parameters.AddWithValue("@orderId", long.Parse(ordId));
            await logCmd.ExecuteNonQueryAsync();
        }

        // Update StockBatch status
        using var cmd = new SqlCommand($@"
            UPDATE tbl_StockBatch SET stockBatch_status=@status 
            WHERE stockBatch_ID IN ({request.OrderIds})", conn);
        cmd.Parameters.AddWithValue("@status", request.NewStatus);
        await cmd.ExecuteNonQueryAsync();

        return Json(new { success = true });
    }

    // ─── MUTATION: Confirm Order (sets status 1 then 5) ─────────────────────────
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmOrder([FromBody] FranchiseOrderConfirmRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Check exists
        using (var chkCmd = new SqlCommand("SELECT COUNT(*) FROM tbl_StockBatch WHERE stockBatch_ID=@id", conn))
        {
            chkCmd.Parameters.AddWithValue("@id", request.OrderId);
            var cnt = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
            if (cnt == 0) return Json(new { success = false, message = "Order not found." });
        }

        // Set status=1, log
        using (var cmd1 = new SqlCommand("UPDATE tbl_StockBatch SET stockBatch_status=1 WHERE stockBatch_ID=@id", conn))
        {
            cmd1.Parameters.AddWithValue("@id", request.OrderId);
            await cmd1.ExecuteNonQueryAsync();
        }
        await InsertOrderLog(conn, int.Parse(userId), 1, request.OrderId);

        // Set status=5, log
        using (var cmd2 = new SqlCommand("UPDATE tbl_StockBatch SET stockBatch_status=5 WHERE stockBatch_ID=@id", conn))
        {
            cmd2.Parameters.AddWithValue("@id", request.OrderId);
            await cmd2.ExecuteNonQueryAsync();
        }
        await InsertOrderLog(conn, int.Parse(userId), 5, request.OrderId);

        return Json(new { success = true });
    }

    // ─── MUTATION: Cancel Order ─────────────────────────────────────────────────
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelOrder([FromBody] FranchiseOrderCancelRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        if (string.IsNullOrEmpty(request.Reason) || request.Reason == "0")
            return Json(new { success = false, message = "Please select a cancel reason." });

        if (string.IsNullOrEmpty(request.Comments?.Trim()))
            return Json(new { success = false, message = "Please provide cancel description." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(@"
            UPDATE tbl_order SET order_status=11, order_CancelRemarks=@comments, order_CancelReason=@reason
            WHERE order_ID=@orderId AND order_bakeryID=@bakeryId", conn);
        cmd.Parameters.AddWithValue("@comments", request.Comments!.Trim());
        cmd.Parameters.AddWithValue("@reason", request.Reason);
        cmd.Parameters.AddWithValue("@orderId", request.OrderId);
        cmd.Parameters.AddWithValue("@bakeryId", long.Parse(webshopId));
        await cmd.ExecuteNonQueryAsync();

        await InsertOrderLog(conn, int.Parse(userId), 11, request.OrderId);

        return Json(new { success = true });
    }

    // ─── MUTATION: Delete Order (soft) ──────────────────────────────────────────
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteOrder([FromBody] FranchiseOrderDeleteRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(@"
            UPDATE tbl_order SET order_isdeleted=1
            WHERE order_ID IN (SELECT value FROM STRING_SPLIT(@orderIds, ',')) AND order_bakeryID=@bakeryId", conn);
        cmd.Parameters.AddWithValue("@orderIds", request.OrderIds);
        cmd.Parameters.AddWithValue("@bakeryId", long.Parse(webshopId));
        await cmd.ExecuteNonQueryAsync();

        return Json(new { success = true });
    }

    // ─── MUTATION: Save Review ──────────────────────────────────────────────────
    [HttpPost("savereview")]
    public async Task<IActionResult> SaveReview([FromBody] FranchiseOrderReviewRequest request)
    {
        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Check if review exists
        using var chkCmd = new SqlCommand("SELECT orderreviews_ID FROM tbl_orderreviews WHERE orderreviews_orderID=@orderId", conn);
        chkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
        var existingId = await chkCmd.ExecuteScalarAsync();

        if (existingId != null && existingId != DBNull.Value)
        {
            using var updCmd = new SqlCommand(@"
                UPDATE tbl_orderreviews SET orderreviews_stars=@stars, orderreviews_remarks=@remarks, orderreviews_modifiedOn=GETDATE()
                WHERE orderreviews_orderID=@orderId", conn);
            updCmd.Parameters.AddWithValue("@stars", request.Stars);
            updCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            updCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            await updCmd.ExecuteNonQueryAsync();
        }
        else
        {
            using var insCmd = new SqlCommand(@"
                INSERT INTO tbl_orderreviews (orderreviews_orderID, orderreviews_stars, orderreviews_remarks, orderreviews_modifiedby, orderreviews_createdOn, orderreviews_modifiedOn)
                VALUES (@orderId, @stars, @remarks, 0, GETDATE(), GETDATE())", conn);
            insCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            insCmd.Parameters.AddWithValue("@stars", request.Stars);
            insCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            await insCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true });
    }

    // ─── P0: Update Sorting (drag-and-drop order sorting) ─────────────────────
    [HttpPost("updatesorting")]
    public async Task<IActionResult> UpdateSorting([FromBody] UpdateSortingRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return Json(new { success = false, message = "No items." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        foreach (var item in request.Items)
        {
            // Check if sorting record exists
            using var chkCmd = new SqlCommand("SELECT COUNT(*) FROM tbl_ordersorting WHERE ordersorting_orderID=@orderId", conn);
            chkCmd.Parameters.AddWithValue("@orderId", long.Parse(item.OrderID));
            var cnt = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());

            if (cnt > 0)
            {
                using var updCmd = new SqlCommand(@"
                    UPDATE tbl_ordersorting SET ordersorting_displayorder=@displayOrder
                    WHERE ordersorting_orderID=@orderId", conn);
                updCmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
                updCmd.Parameters.AddWithValue("@orderId", long.Parse(item.OrderID));
                await updCmd.ExecuteNonQueryAsync();
            }
            else
            {
                using var insCmd = new SqlCommand(@"
                    INSERT INTO tbl_ordersorting (ordersorting_orderID, ordersorting_displayorder)
                    VALUES (@orderId, @displayOrder)", conn);
                insCmd.Parameters.AddWithValue("@orderId", long.Parse(item.OrderID));
                insCmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
                await insCmd.ExecuteNonQueryAsync();
            }
        }

        return Json(new { success = true });
    }

    // ─── P0: Assign Task User ───────────────────────────────────────────────────
    [HttpPost("assign-task-user")]
    public async Task<IActionResult> AssignTaskUser([FromBody] AssignTaskUserRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Check if task exists for this order+detail
        long? existingTaskId = null;
        int? existingTaskSts = null;
        bool existingIsCompleted = false;
        int? existingCurrUserId = null;

        using (var chkCmd = new SqlCommand(@"
            SELECT ordertask_Id, ordertask_tasksts, ordertask_isCompleted, ordertask_currUserID
            FROM tbl_ordertask WHERE ordertask_orderID=@orderId AND ordertask_orderdetailid=@detailId", conn))
        {
            chkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            chkCmd.Parameters.AddWithValue("@detailId", request.OrderDetailId);
            using var reader = await chkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                existingTaskId = reader.GetInt64(0);
                existingTaskSts = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                existingIsCompleted = !reader.IsDBNull(2) && reader.GetBoolean(2);
                existingCurrUserId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            }
        }

        if (existingTaskId.HasValue)
        {
            // Update existing task - advance stage if completed
            int newTaskSts = existingTaskSts ?? 11;
            if (existingIsCompleted)
            {
                newTaskSts = existingTaskSts switch
                {
                    11 => 12,
                    12 => 22,
                    _ => 33
                };
            }

            using var updCmd = new SqlCommand(@"
                UPDATE tbl_ordertask SET ordertask_currUserID=@userId, ordertask_lastUserID=@lastUserId,
                ordertask_isCompleted=0, ordertask_isDone=0, ordertask_modifiedOn=GETDATE(),
                ordertask_tasksts=@taskSts
                WHERE ordertask_Id=@taskId", conn);
            updCmd.Parameters.AddWithValue("@userId", request.UserId);
            updCmd.Parameters.AddWithValue("@lastUserId", existingCurrUserId ?? 0);
            updCmd.Parameters.AddWithValue("@taskSts", newTaskSts);
            updCmd.Parameters.AddWithValue("@taskId", existingTaskId.Value);
            await updCmd.ExecuteNonQueryAsync();

            // Insert task detail record
            using var detCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                VALUES (GETDATE(), '', 0, 0, 0, GETDATE(), GETDATE(), @taskId, @taskSts, @userId)", conn);
            detCmd.Parameters.AddWithValue("@taskId", existingTaskId.Value);
            detCmd.Parameters.AddWithValue("@taskSts", newTaskSts);
            detCmd.Parameters.AddWithValue("@userId", request.UserId);
            await detCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Create new task
            using var insCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertask (ordertask_orderID, ordertask_orderdetailid, ordertask_currUserID, ordertask_lastUserID,
                ordertask_ishold, ordertask_isCompleted, ordertask_isDone, ordertaskdet_remarks,
                ordertask_tasksts, ordertask_createdOn, ordertask_modifiedOn)
                OUTPUT INSERTED.ordertask_Id
                VALUES (@orderId, @detailId, @userId, 0, 0, 0, 0, '', 11, GETDATE(), GETDATE())", conn);
            insCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            insCmd.Parameters.AddWithValue("@detailId", request.OrderDetailId);
            insCmd.Parameters.AddWithValue("@userId", request.UserId);
            var newTaskId = Convert.ToInt64(await insCmd.ExecuteScalarAsync());

            // Insert task detail record
            using var detCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                VALUES (GETDATE(), '', 0, 0, 0, GETDATE(), GETDATE(), @taskId, 11, @userId)", conn);
            detCmd.Parameters.AddWithValue("@taskId", newTaskId);
            detCmd.Parameters.AddWithValue("@userId", request.UserId);
            await detCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true });
    }

    // ─── P1: Bulk Task Start/Complete (btnSubmittaskselected_OnClick) ────────────
    [HttpPost("submit-task-selected")]
    public async Task<IActionResult> SubmitTaskSelected([FromBody] SubmitTaskSelectedRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        if (string.IsNullOrEmpty(request.OrderIds))
            return Json(new { success = false, message = "No orders selected." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        foreach (var ordIdStr in request.OrderIds.Split(',').Where(s => long.TryParse(s, out _)))
        {
            long orderId = long.Parse(ordIdStr);

            // Get existing task
            long? taskId = null;
            int? taskSts = null;
            bool isCompleted = false;
            int? currUserId = null;

            using (var chkCmd = new SqlCommand(@"
                SELECT ordertask_Id, ordertask_tasksts, ordertask_isCompleted, ordertask_currUserID
                FROM tbl_ordertask WHERE ordertask_orderID=@orderId", conn))
            {
                chkCmd.Parameters.AddWithValue("@orderId", orderId);
                using var reader = await chkCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    taskId = reader.GetInt64(0);
                    taskSts = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                    isCompleted = !reader.IsDBNull(2) && reader.GetBoolean(2);
                    currUserId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                }
            }

            if (request.Action == 0)
            {
                // Starting task
                if (taskId.HasValue)
                {
                    // Skip if wrong stage for this tasktype
                    if (request.TaskType == "11" ||
                        (request.TaskType == "12" && taskSts != 11) ||
                        (request.TaskType == "22" && taskSts != 12) ||
                        (request.TaskType == "33" && taskSts != 22))
                    {
                        continue;
                    }

                    int newSts = taskSts switch
                    {
                        11 => 12,
                        12 => 22,
                        22 => 33,
                        _ => 11
                    };

                    using var updCmd = new SqlCommand(@"
                        UPDATE tbl_ordertask SET ordertask_currUserID=@userId, ordertask_lastUserID=0,
                        ordertask_isCompleted=0, ordertask_modifiedOn=GETDATE(), ordertask_tasksts=@taskSts
                        WHERE ordertask_Id=@taskId", conn);
                    updCmd.Parameters.AddWithValue("@userId", request.UserId);
                    updCmd.Parameters.AddWithValue("@taskSts", newSts);
                    updCmd.Parameters.AddWithValue("@taskId", taskId.Value);
                    await updCmd.ExecuteNonQueryAsync();

                    // Insert task detail
                    using var detCmd = new SqlCommand(@"
                        INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                        ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                        ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                        VALUES (GETDATE(), '', 0, 0, 0, GETDATE(), GETDATE(), @taskId, @taskSts, @userId)", conn);
                    detCmd.Parameters.AddWithValue("@taskId", taskId.Value);
                    detCmd.Parameters.AddWithValue("@taskSts", newSts);
                    detCmd.Parameters.AddWithValue("@userId", request.UserId);
                    await detCmd.ExecuteNonQueryAsync();
                }
                else if (request.TaskType == "11")
                {
                    // Create new task at filling stage
                    using var insCmd = new SqlCommand(@"
                        INSERT INTO tbl_ordertask (ordertask_orderID, ordertask_currUserID, ordertask_lastUserID,
                        ordertask_ishold, ordertask_isCompleted, ordertask_isDone, ordertaskdet_remarks,
                        ordertask_tasksts, ordertask_createdOn, ordertask_modifiedOn)
                        OUTPUT INSERTED.ordertask_Id
                        VALUES (@orderId, @userId, 0, 0, 0, 0, '', 11, GETDATE(), GETDATE())", conn);
                    insCmd.Parameters.AddWithValue("@orderId", orderId);
                    insCmd.Parameters.AddWithValue("@userId", request.UserId);
                    var newTaskId = Convert.ToInt64(await insCmd.ExecuteScalarAsync());

                    using var detCmd = new SqlCommand(@"
                        INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                        ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                        ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                        VALUES (GETDATE(), '', 0, 0, 0, GETDATE(), GETDATE(), @taskId, 11, @userId)", conn);
                    detCmd.Parameters.AddWithValue("@taskId", newTaskId);
                    detCmd.Parameters.AddWithValue("@userId", request.UserId);
                    await detCmd.ExecuteNonQueryAsync();
                }
            }
            else if (request.Action == 1)
            {
                // Completing task
                if (!taskId.HasValue || isCompleted) continue;

                using var updCmd = new SqlCommand(@"
                    UPDATE tbl_ordertask SET ordertask_isCompleted=1, ordertask_modifiedOn=GETDATE()
                    WHERE ordertask_Id=@taskId", conn);
                updCmd.Parameters.AddWithValue("@taskId", taskId.Value);
                await updCmd.ExecuteNonQueryAsync();

                // Update task detail
                using var detCmd = new SqlCommand(@"
                    UPDATE TOP(1) tbl_ordertaskdet SET ordertaskdet_isCompleted=1, ordertaskdet_isDone=1,
                    ordertaskdet_endDate=GETDATE(), ordertaskdet_modifiedOn=GETDATE()
                    WHERE ordertaskdet_taskId=@taskId AND ordertaskdet_taskSts=@taskSts
                    ORDER BY ordertaskdet_Id DESC", conn);
                detCmd.Parameters.AddWithValue("@taskId", taskId.Value);
                detCmd.Parameters.AddWithValue("@taskSts", taskSts ?? 0);
                await detCmd.ExecuteNonQueryAsync();
            }
        }

        return Json(new { success = true });
    }

    // ─── P1: Individual Task Select (btnSelectTask_Click) ───────────────────────
    [HttpPost("select-task")]
    public async Task<IActionResult> SelectTask([FromBody] SelectTaskRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Check existing task
        long? taskId = null;
        int? taskSts = null;
        bool existingIsCompleted = false;
        int? existingCurrUserId = null;

        using (var chkCmd = new SqlCommand(@"
            SELECT ordertask_Id, ordertask_tasksts, ordertask_isCompleted, ordertask_currUserID
            FROM tbl_ordertask WHERE ordertask_orderID=@orderId", conn))
        {
            chkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            using var reader = await chkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                taskId = reader.GetInt64(0);
                taskSts = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                existingIsCompleted = !reader.IsDBNull(2) && reader.GetBoolean(2);
                existingCurrUserId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            }
        }

        if (taskId.HasValue)
        {
            int newTaskSts = taskSts ?? 11;
            if (existingIsCompleted)
            {
                newTaskSts = taskSts switch { 11 => 12, 12 => 22, _ => 33 };
            }

            using var updCmd = new SqlCommand(@"
                UPDATE tbl_ordertask SET ordertask_currUserID=@userId, ordertask_lastUserID=@lastUserId,
                ordertask_isCompleted=0, ordertask_isDone=0, ordertaskdet_remarks=@remarks,
                ordertask_modifiedOn=GETDATE(), ordertask_tasksts=@taskSts
                WHERE ordertask_Id=@taskId", conn);
            updCmd.Parameters.AddWithValue("@userId", request.UserId);
            updCmd.Parameters.AddWithValue("@lastUserId", existingCurrUserId ?? 0);
            updCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            updCmd.Parameters.AddWithValue("@taskSts", newTaskSts);
            updCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            await updCmd.ExecuteNonQueryAsync();

            // Insert task detail
            using var detCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                VALUES (GETDATE(), @remarks, 0, 0, 0, GETDATE(), GETDATE(), @taskId, @taskSts, @userId)", conn);
            detCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            detCmd.Parameters.AddWithValue("@taskSts", newTaskSts);
            detCmd.Parameters.AddWithValue("@userId", request.UserId);
            detCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            await detCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Create new task
            using var insCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertask (ordertask_orderID, ordertask_currUserID, ordertask_lastUserID,
                ordertask_ishold, ordertask_isCompleted, ordertask_isDone, ordertaskdet_remarks,
                ordertask_tasksts, ordertask_createdOn, ordertask_modifiedOn)
                OUTPUT INSERTED.ordertask_Id
                VALUES (@orderId, @userId, 0, 0, 0, 0, '', 11, GETDATE(), GETDATE())", conn);
            insCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            insCmd.Parameters.AddWithValue("@userId", request.UserId);
            var newTaskId = Convert.ToInt64(await insCmd.ExecuteScalarAsync());

            using var detCmd = new SqlCommand(@"
                INSERT INTO tbl_ordertaskdet (ordertaskdet_modifiedOn, ordertaskdet_remarks, ordertaskdet_isreply,
                ordertaskdet_isCompleted, ordertaskdet_isDone, ordertaskdet_staDate, ordertaskdet_endDate,
                ordertaskdet_taskId, ordertaskdet_taskSts, ordertaskdet_userID)
                VALUES (GETDATE(), '', 0, 0, 0, GETDATE(), GETDATE(), @taskId, 11, @userId)", conn);
            detCmd.Parameters.AddWithValue("@taskId", newTaskId);
            detCmd.Parameters.AddWithValue("@userId", request.UserId);
            await detCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true });
    }

    // ─── P1: Submit Task / Complete (btnSubmitTask_Click) ────────────────────────
    [HttpPost("submit-task")]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Get existing task
        long? taskId = null;
        int? taskSts = null;

        using (var chkCmd = new SqlCommand(@"
            SELECT ordertask_Id, ordertask_tasksts
            FROM tbl_ordertask WHERE ordertask_orderID=@orderId", conn))
        {
            chkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            using var reader = await chkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                taskId = reader.GetInt64(0);
                taskSts = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            }
        }

        if (!taskId.HasValue)
            return Json(new { success = false, message = "Task not found." });

        // Mark task as completed
        using (var updCmd = new SqlCommand(@"
            UPDATE tbl_ordertask SET ordertask_isCompleted=1, ordertask_isDone=1,
            ordertaskdet_remarks=@remarks, ordertask_modifiedOn=GETDATE()
            WHERE ordertask_Id=@taskId", conn))
        {
            updCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            updCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            await updCmd.ExecuteNonQueryAsync();
        }

        // Update task detail
        using (var detCmd = new SqlCommand(@"
            UPDATE TOP(1) tbl_ordertaskdet SET ordertaskdet_isCompleted=1, ordertaskdet_isDone=1,
            ordertaskdet_remarks=@remarks, ordertaskdet_endDate=GETDATE(), ordertaskdet_modifiedOn=GETDATE()
            WHERE ordertaskdet_taskId=@taskId AND ordertaskdet_taskSts=@taskSts
            ORDER BY ordertaskdet_Id DESC", conn))
        {
            detCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            detCmd.Parameters.AddWithValue("@taskSts", taskSts ?? 0);
            detCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            await detCmd.ExecuteNonQueryAsync();
        }

        // If finishing stage (33), update order status to processed (2)
        if (taskSts == 33)
        {
            using var ordCmd = new SqlCommand(@"
                UPDATE tbl_order SET order_status=2
                WHERE order_ID=@orderId AND order_bakeryID=@bakeryId", conn);
            ordCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            ordCmd.Parameters.AddWithValue("@bakeryId", long.Parse(webshopId));
            await ordCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true });
    }

    // ─── P1: Save Remarks (btnSaveRemarks_Click) ────────────────────────────────
    [HttpPost("save-remarks")]
    public async Task<IActionResult> SaveRemarks([FromBody] SaveRemarksRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Get existing task
        long? taskId = null;
        int? taskSts = null;

        using (var chkCmd = new SqlCommand(@"
            SELECT ordertask_Id, ordertask_tasksts
            FROM tbl_ordertask WHERE ordertask_orderID=@orderId", conn))
        {
            chkCmd.Parameters.AddWithValue("@orderId", request.OrderId);
            using var reader = await chkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                taskId = reader.GetInt64(0);
                taskSts = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            }
        }

        if (!taskId.HasValue)
            return Json(new { success = false, message = "Task not found." });

        // Update task with remarks and mark isDone
        using (var updCmd = new SqlCommand(@"
            UPDATE tbl_ordertask SET ordertask_isDone=1, ordertaskdet_remarks=@remarks,
            ordertask_modifiedOn=GETDATE()
            WHERE ordertask_Id=@taskId", conn))
        {
            updCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            updCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            await updCmd.ExecuteNonQueryAsync();
        }

        // Update task detail
        using (var detCmd = new SqlCommand(@"
            UPDATE TOP(1) tbl_ordertaskdet SET ordertaskdet_isDone=1,
            ordertaskdet_remarks=@remarks, ordertaskdet_endDate=GETDATE(), ordertaskdet_modifiedOn=GETDATE()
            WHERE ordertaskdet_taskId=@taskId AND ordertaskdet_taskSts=@taskSts
            ORDER BY ordertaskdet_Id DESC", conn))
        {
            detCmd.Parameters.AddWithValue("@taskId", taskId.Value);
            detCmd.Parameters.AddWithValue("@taskSts", taskSts ?? 0);
            detCmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
            await detCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true });
    }

    // ─── P1: Reply Remarks (lnkreplyRemarks_OnClick + btnAddReply_Click) ────────
    [HttpPost("reply-remarks")]
    public async Task<IActionResult> ReplyRemarks([FromBody] ReplyRemarksRequest request)
    {
        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Get the task detail to find the parent taskId
        long? parentTaskId = null;
        using (var chkCmd = new SqlCommand(@"
            SELECT ordertaskdet_taskId FROM tbl_ordertaskdet WHERE ordertaskdet_Id=@detId", conn))
        {
            chkCmd.Parameters.AddWithValue("@detId", request.TaskDetId);
            var result = await chkCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                parentTaskId = Convert.ToInt64(result);
        }

        if (!parentTaskId.HasValue)
            return Json(new { success = false, message = "Task detail not found." });

        // Insert reply
        using var insCmd = new SqlCommand(@"
            INSERT INTO tbl_orderTaskRemarksReply (orderTaskRemarksReply_message, orderTaskRemarksReply_Name,
            orderTaskRemarksReply_modifiedOn, orderTaskRemarksReply_taskdetID, orderTaskRemarksReply_taskID)
            VALUES (@message, @name, GETDATE(), @taskDetId, @taskId)", conn);
        insCmd.Parameters.AddWithValue("@message", request.Message ?? "");
        insCmd.Parameters.AddWithValue("@name", request.Name ?? "");
        insCmd.Parameters.AddWithValue("@taskDetId", request.TaskDetId);
        insCmd.Parameters.AddWithValue("@taskId", parentTaskId.Value);
        await insCmd.ExecuteNonQueryAsync();

        // Mark task detail as having a reply
        using var updCmd = new SqlCommand(@"
            UPDATE tbl_ordertaskdet SET ordertaskdet_isreply=1
            WHERE ordertaskdet_Id=@detId", conn);
        updCmd.Parameters.AddWithValue("@detId", request.TaskDetId);
        await updCmd.ExecuteNonQueryAsync();

        return Json(new { success = true });
    }

    // ─── Helper: Get Order Details ──────────────────────────────────────────────
    private async Task<DataTable> GetOrderDetails(List<string> orderIds)
    {
        var dt = new DataTable();
        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        string ids = string.Join(",", orderIds);
        using var cmd = new SqlCommand($@"
            SELECT p.product_SEOURL, p.product_ID, product_image1, product_Name, product_type, 
                orderDetail_orderID as order_ID, orderDetail_ID, Sub_orderID, od.orderDetail_prdType, 
                orderDetail_Quantity, 
                IsChangeOrderImageMarked = case when order_status = 0 then case when om.IsUpdated is null or om.IsUpdated = 0 then 0 else 1 end else 1 end, 
                IsUpdated = isnull(om.IsUpdated, -1), 
                COUNT(*) OVER (PARTITION BY orderDetail_orderID) AS TotalRecords
            FROM tbl_orderDetail od 
            INNER JOIN tbl_order o ON od.orderDetail_orderID = o.order_ID 
            INNER JOIN tbl_products p ON p.product_ID = od.orderDetail_productID 
            LEFT OUTER JOIN tbl_orderImageUpdate om ON od.orderDetail_ID = OrderImage_orderDetail_ID
            WHERE orderDetail_orderID IN ({ids}) 
            ORDER BY orderDetail_ID DESC", conn);
        cmd.CommandTimeout = 60;
        using var reader = await cmd.ExecuteReaderAsync();
        dt.Load(reader);
        return dt;
    }

    // ─── Helper: Insert Order Log ───────────────────────────────────────────────
    private static async Task InsertOrderLog(SqlConnection conn, int userId, int status, long orderId)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO tbl_Franchiseorderlog (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn)
            VALUES (@userId, @status, @orderId, GETDATE())", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Helper: Parse bool safely ──────────────────────────────────────────────
    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return bool.TryParse(value, out var result) && result;
    }

    // ─── Model Classes ──────────────────────────────────────────────────────────
    public class DayTab
    {
        public DateTime DayDate { get; set; }
        public int DayID { get; set; }
        public string DayName { get; set; } = "";
        public string CountCakes { get; set; } = "0";
        public bool IsClosed { get; set; }
    }

    public class StaffUser
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
    }

    public class TaskDetailRow
    {
        public long OrderId { get; set; }
        public long OrderDetailId { get; set; }
        public long ProductId { get; set; }
        public int ProductType { get; set; }
        public string ProductName { get; set; } = "";
        public string ProductSeoUrl { get; set; } = "";
        public string ProductImage1 { get; set; } = "";
        public int OrderStatus { get; set; }
        public int SortID { get; set; }
        public string CakeShapeTitle { get; set; } = "";
        public string CakeTypeTitle { get; set; } = "";
        public string SizeID { get; set; } = "";
        public string SizeTitle { get; set; } = "";
        public string OrderDetailShapeText { get; set; } = "";
        public string CakeShapeCustomText { get; set; } = "";
        public string OrderCustomerName { get; set; } = "";
        public string ShippingPhone { get; set; } = "";
        public string ShippingZip { get; set; } = "";
        public string OrderReviewsRemarks { get; set; } = "";
        public string OrderReviewsStars { get; set; } = "";
        public string OrderTaskTaskSts { get; set; } = "";
        public string LogOrderPrintOrderId { get; set; } = "";
        public string OrderTaskIsCompleted { get; set; } = "";
        public string OrderTaskIsDone { get; set; } = "";
        public string OrderTaskId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string OrderTaskDetRemarks { get; set; } = "";
        public long OrderBakeryId { get; set; }
        public long OrderForwardedOrderId { get; set; }
        public int OrderQuality { get; set; }
        public int OrderDetailQuantity { get; set; }
        public int OrderDetailShapeId { get; set; }
        public int OrderCollectionDeliveryMode { get; set; }
        public int OrderSaleType { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime OrderCollectionDate { get; set; }
        public DateTime OrderCollectionDispatchDate { get; set; }
        public DateTime OrderCollectionOccasionDate { get; set; }
        public DateTime DispatchDate { get; set; }
        public decimal OrderTotalPrice { get; set; }
        public decimal OrderPrdTotal { get; set; }
        public decimal OrderCSMargin { get; set; }
        public decimal OrderShopMargin { get; set; }
        public decimal OrderPayoutRefund { get; set; }
        public decimal OrderCSRefund { get; set; }
        public string TopperCharges { get; set; } = "0";
        public string FullCakeWorth { get; set; } = "0";
        public string FillingWorth { get; set; } = "0";
        public string IcingWorth { get; set; } = "0";
        public string DecorationWorth { get; set; } = "0";
        public int OrderShowPrint { get; set; }
        public bool IsChangeOrderImageMarked { get; set; }
        public string CountAssignedToUser { get; set; } = "0";
    }

    public class TaskGroup
    {
        public DateTime GroupDate { get; set; }
        public List<TaskDetailRow> Items { get; set; } = new();
    }

    public class TaskTypeCounts
    {
        public int FillingCakes { get; set; }
        public int FillingCupcakes { get; set; }
        public int IcingCakes { get; set; }
        public int IcingCupcakes { get; set; }
        public int DecorationCakes { get; set; }
        public int DecorationCupcakes { get; set; }
        public int FinishingCakes { get; set; }
        public int FinishingCupcakes { get; set; }
        public int UnderDeliveryCakes { get; set; }
        public int UnderDeliveryCupcakes { get; set; }
    }

    // ─── Request Models ─────────────────────────────────────────────────────────
    public class FranchiseOrderStatusRequest
    {
        public string OrderIds { get; set; } = "";
        public int NewStatus { get; set; }
    }

    public class FranchiseOrderConfirmRequest
    {
        public long OrderId { get; set; }
    }

    public class FranchiseOrderCancelRequest
    {
        public long OrderId { get; set; }
        public string? Reason { get; set; }
        public string? Comments { get; set; }
        public bool NotifyCustomer { get; set; }
    }

    public class FranchiseOrderDeleteRequest
    {
        public string OrderIds { get; set; } = "";
    }

    public class FranchiseOrderReviewRequest
    {
        public long OrderId { get; set; }
        public int Stars { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateSortingRequest
    {
        public List<SortingItem> Items { get; set; } = new();
    }

    public class SortingItem
    {
        public string OrderID { get; set; } = "";
        public int Ordersts { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AssignTaskUserRequest
    {
        public long OrderId { get; set; }
        public long OrderDetailId { get; set; }
        public int UserId { get; set; }
    }

    public class SubmitTaskSelectedRequest
    {
        public string OrderIds { get; set; } = "";
        public int Action { get; set; } // 0=Start, 1=Complete
        public int UserId { get; set; }
        public string TaskType { get; set; } = "";
    }

    public class SelectTaskRequest
    {
        public long OrderId { get; set; }
        public int UserId { get; set; }
        public string? Remarks { get; set; }
    }

    public class SubmitTaskRequest
    {
        public long OrderId { get; set; }
        public string? Remarks { get; set; }
    }

    public class SaveRemarksRequest
    {
        public long OrderId { get; set; }
        public string? Remarks { get; set; }
    }

    public class ReplyRemarksRequest
    {
        public long TaskDetId { get; set; }
        public string? Message { get; set; }
        public string? Name { get; set; }
    }
}
