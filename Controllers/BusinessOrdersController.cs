using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;
using CakerStreet.Business.Models;
using CakerStreet.Business.Helpers;
using System.Globalization;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the business orders page.
/// Route: /businessorders
/// Migrated from bakeryorders.aspx - supports read + safe mutations.
/// </summary>
[Route("businessorders")]
[Route("bakeryorders")]
[Route("bakeryorders.aspx")]
public class BusinessOrdersController : Controller
{
    private readonly BusinessOrdersService _ordersService;
    private readonly BakeryMenuService _menuService;
    private readonly AssignedTasksService _assignedTasksService;
    private readonly IConfiguration _config;

    public BusinessOrdersController(
        BusinessOrdersService ordersService,
        BakeryMenuService menuService,
        AssignedTasksService assignedTasksService,
        IConfiguration config)
    {
        _ordersService = ordersService;
        _menuService = menuService;
        _assignedTasksService = assignedTasksService;
        _config = config;
    }

    [HttpGet("/assignedtasks")]
    public async Task<IActionResult> AssignedTasks(
        int ordertype = 12,
        string? sdate = null,
        string? edate = null,
        string? from = null,
        string? to = null,
        string? q = null,
        int datemode = 1,
        int dt = 0,
        int pno = 1,
        int dm = 0,
        int all = 0,
        int dayID = 0,
        int tasktype = 0,
        int topper = 0,
        int disptime = 0,
        int rt = 0,
        string? startdate = null)
    {
        return await Index(12, sdate, edate, from, to, q, datemode, dt, pno, dm, all, dayID, tasktype, topper, disptime, rt, startdate);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        int ordertype = 0,
        string? sdate = null,
        string? edate = null,
        string? from = null,
        string? to = null,
        string? q = null,
        int datemode = 1,
        int dt = 0,
        int pno = 1,
        int dm = 0,
        int all = 0,
        int dayID = 0,
        int tasktype = 0,
        int topper = 0,
        int disptime = 0,
        int rt = 0,
        string? startdate = null)
    {
        // Get auth info from HttpContext.Items (set by middleware)
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

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

        // ─── ORDERTYPE 12: Assigned Tasks (dedicated rendering path) ───────────
        if (ordertype == 12)
        {
            var effectiveDayID = dayID > 0 ? dayID : AssignedTasksNavHelper.GetDayId(DateTime.Now);

            // Parse calendar startdate param (legacy: Session["Startdate_day"])
            // Legacy default: dttaskdate = DateTime.Today.AddDays(-1)  (bakeryorders.aspx.cs line 538)
            // Legacy with CalendarExtender: Session["Startdate_day"] = exact picked date (line 2166)
            // The -1 offset only applies to the default (no user date), NOT to user-picked dates.
            DateTime weekAnchor = DateTime.Today.AddDays(-1);
            if (!string.IsNullOrEmpty(startdate))
            {
                // Support multiple date formats: dd/MM/yyyy, MM/dd/yyyy, yyyy-MM-dd
                if (DateTime.TryParseExact(startdate, new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "d/M/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    weekAnchor = parsed;
                }
            }

            var assignedRequest = new AssignedTasksRequest
            {
                WebshopId = long.Parse(webshopId),
                DayID = effectiveDayID,
                TaskType = tasktype,
                Topper = topper,
                DispTime = disptime,
                DeliveryMode = dm,
                RouteId = rt,
                StartDate = weekAnchor
            };

            var assignedModel = await _assignedTasksService.GetAssignedTasksAsync(assignedRequest);

            // Set role info for staff dropdown (source: getBakeryUserType/getBakeryUserName)
            assignedModel.LoggedInUserType = userType;
            assignedModel.LoggedInUserName = userName;
            assignedModel.LoggedInUserId = userId;

            // Get tab counts for the top status tabs
            var tabCounts12 = await _ordersService.GetTabCountsAsync(webshopId);
            ViewBag.TabCounts = tabCounts12;
            ViewBag.OrderType = 12;
            ViewBag.AssignedTasksModel = assignedModel;

            return View("Index", new BusinessOrdersResult { OrderType = 12 });
        }

        // Parse date range - support legacy 'from'/'to' params as aliases for 'sdate'/'edate'
        var effectiveSdate = sdate ?? from;
        var effectiveEdate = edate ?? to;
        // Support legacy 'dt' param as alias for 'datemode'
        var effectiveDateMode = dt > 0 ? dt : datemode;

        DateTime? startDate = null;
        DateTime? endDate = null;
        if (!string.IsNullOrEmpty(effectiveSdate) && !string.IsNullOrEmpty(effectiveEdate))
        {
            // Try dd/MM/yyyy format first (legacy format), then standard formats
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };
            if (DateTime.TryParseExact(effectiveSdate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                startDate = sd;
            if (DateTime.TryParseExact(effectiveEdate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
                endDate = ed;
        }

        // When 'all=1', show all records (up to 1000) matching legacy behavior
        var pageSize = all == 1 ? 1000 : 20;

        // Build request
        var request = new BusinessOrdersRequest
        {
            WebshopId = webshopId,
            OrderType = ordertype,
            StartDate = startDate,
            EndDate = endDate,
            DateMode = effectiveDateMode,
            SearchQuery = q,
            PageNumber = all == 1 ? 1 : (pno > 0 ? pno : 1),
            PageSize = pageSize,
            DeliveryMode = dm
        };

        // Get orders
        var result = await _ordersService.GetOrdersAsync(request);

        // Get tab counts
        var tabCounts = await _ordersService.GetTabCountsAsync(webshopId);

        // Pass data to view
        ViewBag.TabCounts = tabCounts;
        ViewBag.OrderType = ordertype;
        ViewBag.SearchQuery = q ?? "";
        ViewBag.StartDate = effectiveSdate ?? "";
        ViewBag.EndDate = effectiveEdate ?? "";
        ViewBag.DateMode = effectiveDateMode;
        ViewBag.DeliveryMode = dm;

        return View("Index", result);
    }

    // ===== MUTATION ENDPOINTS =====

    /// <summary>
    /// Updates order status. Used for: Job Assigned (5), Order Processed (2), 
    /// Under Delivery (3), Order Completed (4).
    /// </summary>
    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] OrderStatusRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid order ID." });

        // Only allow safe status values
        var allowedStatuses = new[] { 2, 3, 4, 5 };
        if (!allowedStatuses.Contains(request.NewStatus))
            return Json(new { success = false, message = "Invalid status value." });

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var result = await _ordersService.UpdateOrderStatusAsync(request.OrderId, request.NewStatus, webshopId, userId);
        if (!result)
            return Json(new { success = false, message = "Order not found or access denied." });

        return Json(new { success = true });
    }

    /// <summary>
    /// Confirms an order (Pending → Confirmed → Job Assigned).
    /// Sets status=1, logs, then status=5, logs (matching legacy).
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmOrder([FromBody] OrderConfirmRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid order ID." });

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var result = await _ordersService.ConfirmOrderAsync(request.OrderId, webshopId, userId);
        if (!result)
            return Json(new { success = false, message = "Order not found or access denied." });

        return Json(new { success = true });
    }

    /// <summary>
    /// Soft-deletes an order (sets order_isdeleted = 1).
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteOrder([FromBody] OrderDeleteRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";

        if (request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid order ID." });

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var result = await _ordersService.DeleteOrderAsync(request.OrderId, webshopId);
        if (!result)
            return Json(new { success = false, message = "Order not found or access denied." });

        return Json(new { success = true });
    }

    /// <summary>
    /// Saves topper type for an order detail (Has Topper + Edible/Non-Edible).
    /// Matching legacy SaveOrderTopperType AJAX call.
    /// </summary>
    [HttpPost("savetopper")]
    public async Task<IActionResult> SaveTopperType([FromBody] SaveTopperRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false });

        await _ordersService.SaveTopperTypeAsync(request.OrderId, request.OrderDetailId, request.HasTopper, request.TopperTypeId);
        return Json(new { success = true });
    }

    /// <summary>
    /// Saves order review (stars + remarks). Matching legacy OrderReview command.
    /// </summary>
    [HttpPost("savereview")]
    public async Task<IActionResult> SaveReview([FromBody] SaveReviewRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        await _ordersService.SaveReviewAsync(request.OrderId, request.Stars, request.Remarks);
        return Json(new { success = true });
    }

    /// <summary>
    /// Marks/unmarks an order detail for image change. Matching legacy MarkChangeOrderImage.
    /// </summary>
    [HttpPost("markchangeimage")]
    public async Task<IActionResult> MarkChangeImage([FromBody] MarkChangeImageRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false });

        await _ordersService.MarkChangeImageAsync(request.OrderDetailId, request.MarkForChange);
        return Json(new { success = true });
    }

    /// <summary>
    /// Cancels an order with reason and comments.
    /// Sets status=11, logs cancel reason/remarks, inserts order log.
    /// GUARDED: Wallet refund and email notification are logged but not executed.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelOrder([FromBody] OrderCancelRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid order ID." });

        if (string.IsNullOrEmpty(request.Reason) || request.Reason == "0")
            return Json(new { success = false, message = "Please select a cancel reason." });

        if (string.IsNullOrEmpty(request.Comments?.Trim()))
            return Json(new { success = false, message = "Please provide cancel description." });

        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var result = await _ordersService.CancelOrderAsync(
            request.OrderId, webshopId, userId,
            request.Reason, request.Comments.Trim(), request.NotifyCustomer);

        return Json(new { success = result.Success, message = result.Message, warnings = result.Warnings });
    }

    // ─── ASSIGN TASK USER ENDPOINT ─────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs line 2751 — ordertask_currUserID assignment
    [HttpPost("assign-task-user")]
    public async Task<IActionResult> AssignTaskUser([FromBody] AssignTaskUserRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            await _assignedTasksService.AssignUserToTaskAsync(
                request.OrderId, request.OrderDetailId, request.UserId, long.Parse(webshopId));
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ─── TASK ACTION ENDPOINT (play/pause/stop/rewind/remarks/assign) ──────────
    [HttpPost("task-action")]
    public async Task<IActionResult> TaskAction([FromBody] TaskActionRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            await _assignedTasksService.ExecuteTaskActionAsync(request, long.Parse(webshopId));
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, stack = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0)) });
        }
    }

    // ─── CHECKLIST UPDATE ENDPOINT ─────────────────────────────────────────────
    [HttpPost("update-checklist")]
    public async Task<IActionResult> UpdateChecklist([FromBody] UpdateChecklistRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated" });

        try
        {
            await _assignedTasksService.ToggleChecklistItemAsync(
                request.OrderDetailId, request.ChecklistId, request.IsDone);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ─── UPDATE SORTING ENDPOINT ──────────────────────────────────────────────
    // Source: bakeryorders.aspx lines 1137-1169 — updatesorting() JS function
    // Calls webservices.aspx/updateordersorting in legacy
    [HttpPost("updatesorting")]
    public async Task<IActionResult> UpdateSorting([FromBody] UpdateSortingRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        await _ordersService.UpdateOrderSortingAsync(request.Items);
        return Json(new { success = true });
    }

    // ─── INGREDIENT ENDPOINTS ────────────────────────────────────────────────
    // Source: bakeryorders.aspx lines 1595-1692 — ViewIngredients_popup, AddIng_popup_Click, remIng_popup_Click

    [HttpPost("ingredients")]
    public async Task<IActionResult> GetIngredients([FromBody] GetIngredientsRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        var items = await _ordersService.GetIngredientListAsync(request.OrderDetailId);
        return Json(new { success = true, items });
    }

    [HttpPost("addingredient")]
    public async Task<IActionResult> AddIngredient([FromBody] AddIngredientRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        if (string.IsNullOrWhiteSpace(request.BatchId))
            return Json(new { success = false, message = "Please enter Batch No." });

        var result = await _ordersService.AddIngredientAsync(
            request.OrderDetailId, request.OrderId, request.BatchId, request.SectionId);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost("removeingredient")]
    public async Task<IActionResult> RemoveIngredient([FromBody] RemoveIngredientRequest request)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { success = false, message = "Not authenticated." });

        await _ordersService.RemoveIngredientAsync(request.IngredientId);
        return Json(new { success = true });
    }

    // ─── TEMPORARY DEBUG ENDPOINT ──────────────────────────────────────────────
    // Returns JSON counts for data verification against legacy (port 27201).
    // Remove after visual parity is confirmed.

    [HttpGet("debug-assigned-tasks")]
    public async Task<IActionResult> DebugAssignedTasks(
        int dayID = 0, int tasktype = 0, int topper = 0,
        int disptime = 0, int dm = 0, int rt = 0)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Json(new { error = "Not authenticated" });

        var effectiveDayID = dayID > 0 ? dayID : AssignedTasksNavHelper.GetDayId(DateTime.Now);

        var request = new AssignedTasksRequest
        {
            WebshopId = long.Parse(webshopId),
            DayID = effectiveDayID,
            TaskType = tasktype,
            Topper = topper,
            DispTime = disptime,
            DeliveryMode = dm,
            RouteId = rt,
            StartDate = DateTime.Today.AddDays(-1)
        };

        var model = await _assignedTasksService.GetAssignedTasksAsync(request);

        return Json(new
        {
            error = model.ErrorMessage,
            counts = new
            {
                dayTabs = model.DayTabs.Count,
                dayTabDetails = model.DayTabs.Select(d => new { d.DayID, d.DayName, d.CakeCount, d.IsClosed, date = d.Date.ToString("dd/MM/yyyy") }),
                timeSlots = model.TimeSlots.Count,
                timeSlotDetails = model.TimeSlots.Select(t => new { t.TimeID, t.TimeSlotName, t.CakeCount, t.CupcakeCount, t.TotalCount, t.TotalDone }),
                taskRows = model.TaskRows.Count,
                taskRowsByProductType = new
                {
                    cakes = model.TaskRows.Count(r => r.ProductType == 1),
                    cupcakes = model.TaskRows.Count(r => r.ProductType == 6),
                    accessories = model.TaskRows.Count(r => r.ProductType == 2)
                },
                deliveryRoutes = model.DeliveryRoutes.Count,
                routeDetails = model.DeliveryRoutes.Select(r => new { r.RouteId, r.RouteName, r.OrderCount, date = r.RouteDate.ToString("dd/MM/yyyy") }),
                staffUsers = model.StaffList.Count,
                isBakeryClosed = model.IsBakeryClosed
            },
            filters = new { dayID = effectiveDayID, tasktype, topper, disptime, dm, rt }
        });
    }
}
