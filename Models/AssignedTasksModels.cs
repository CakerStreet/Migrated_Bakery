namespace CakerStreet.Business.Models;

/// <summary>
/// Request parameters for the ordertype=12 Assigned Tasks query.
/// Maps to query string parameters: dayID, tasktype, topper, disptime, dm, rt.
/// Source: bakeryorders.aspx.cs bindOrders() — Request["dayID"], Request["tasktype"], etc.
/// </summary>
public class AssignedTasksRequest
{
    public long WebshopId { get; set; }
    public int DayID { get; set; }           // 1-7 (Mon-Sun), effective after defaulting
    public int TaskType { get; set; }        // 0=all, 11=Filling, 12=Icing, 22=Decoration, 33=Finishing, 44=Under Delivery
    public int Topper { get; set; }          // 0=no filter, 1=topper only
    public int DispTime { get; set; }        // 0=all, else TimeID (10, 12, 14, 16)
    public int DeliveryMode { get; set; }    // 0=all, 1=Collection, 2=Delivery By Hand, 4=Delivery By Post
    public int RouteId { get; set; }         // 0=all routes, else route ID (only when dm=2)
    public DateTime StartDate { get; set; }  // Start of 7-day window (default: today)
    public int DomainID { get; set; }        // franchiseUser_kioskID for epos cross-reference
}

/// <summary>
/// View model for the _AssignedTasks.cshtml partial view.
/// Contains all navigation layer data and task rows.
/// Source: bakeryorders.aspx.cs ordertype=12 section — lstday, lstDispatchTime, repRoutes, repAssignedTask_outer.
/// </summary>
public class AssignedTasksViewModel
{
    // Active filter state (for URL generation and active-state CSS detection)
    public int ActiveDayID { get; set; }
    public int ActiveTaskType { get; set; }
    public int ActiveTopper { get; set; }
    public int ActiveDispTime { get; set; }
    public int ActiveDeliveryMode { get; set; }
    public int ActiveRouteId { get; set; }
    public DateTime StartDate { get; set; }

    // Layer 2: Day tabs — source: lstday (clsglobaltext.taskDay list)
    public List<DayTabItem> DayTabs { get; set; } = new();

    // Layer 3: Dispatch time slots — source: lstDispatchTime (dispatchTime list)
    public List<DispatchTimeSlotItem> TimeSlots { get; set; } = new();

    // Layer 5/6: Delivery routes — source: repRoutes (deliveryRoute query, only when dm=2)
    public List<DeliveryRouteItem> DeliveryRoutes { get; set; } = new();

    // Task rows — source: repAssignedTask_outer (ordertaskdetail list grouped by dispatchDate)
    public List<AssignedTaskRow> TaskRows { get; set; } = new();

    // Staff list for assignment dropdown — source: varusers (BakeryUser query)
    public List<TaskStaffItem> StaffList { get; set; } = new();

    // Checklist definitions (active items from tbl_orderProcessingChecklist)
    public List<ChecklistDefinition> ChecklistDefinitions { get; set; } = new();

    // Per-order-detail checklist states (from tbl_lnkOrderChecklist2Order)
    public Dictionary<long, List<ChecklistItemState>> ChecklistStates { get; set; } = new();

    // Per-task processing history (from tbl_ordertaskdet)
    // Key: ordertask_Id → list of history entries ordered by modifiedOn
    public Dictionary<long, List<TaskHistoryEntry>> TaskHistory { get; set; } = new();

    // Task type counts for navigation labels (e.g. "Filling (8/1)")
    // Source: bakeryorders.aspx.cs lines 1115-1140 — countcake_filling, countcake_icing, etc.
    public string FillingCount { get; set; } = "";
    public string IcingCount { get; set; } = "";
    public string DecorationCount { get; set; } = "";
    public string FinishingCount { get; set; } = "";
    public string UnderDeliveryCount { get; set; } = "";
    public string TopperCount { get; set; } = "0";

    // Summary counts displayed in littotal
    public string TotalCakeCount { get; set; } = "0";
    public string TotalCupcakeCount { get; set; } = "0";
    public string TotalAccessoryCount { get; set; } = "0";

    // Error state — source: Requirement 12.4
    public string? ErrorMessage { get; set; }

    // Logged-in user info for role-aware staff dropdown
    // Source: bakeryorders.aspx line 832 — getBakeryUserType() == "3" ? getBakeryUserName() : "--Select Staff--"
    public string LoggedInUserType { get; set; } = "";
    public string LoggedInUserName { get; set; } = "";
    public int LoggedInUserId { get; set; }

    // Manifest/sponge URLs depend on selected date
    public string ManifestUrl { get; set; } = "";
    public string OrderSpongeUrl { get; set; } = "";
    public string StaffRotaUrl { get; set; } = "";

    // Whether the bakery is closed today (affects day tab rendering)
    public bool IsBakeryClosed { get; set; }

    // Whether the close-day button should be visible
    public bool ShowCloseDayButton { get; set; }

    // Whether baking-related buttons (manifest, sponge) should be visible
    public bool IsBaking { get; set; }
}

/// <summary>
/// Day tab navigation item.
/// Source: bakeryorders.aspx.cs class taskDay + GetCakecountsForBakers_byDate result.
/// Fields: DayID, DayName, DayDate, countCakes, isclosed.
/// </summary>
public class DayTabItem
{
    public int DayID { get; set; }              // 1-7 (Mon=1, Sun=7)
    public string DayName { get; set; } = "";   // "Monday", "Tuesday", etc.
    public DateTime Date { get; set; }
    public string CakeCount { get; set; } = "0"; // Legacy uses string "countCakes" (can be "5" or "0")
    public bool IsClosed { get; set; }
}

/// <summary>
/// Dispatch time slot navigation item.
/// Source: bakeryorders.aspx.cs class dispatchTime.
/// Fields: TimeID, DayName (display name), cakecount, cupcakecount, totalcount, totaldone, isdone.
/// </summary>
public class DispatchTimeSlotItem
{
    public int TimeID { get; set; }                // 10, 12, 14, 16
    public string TimeSlotName { get; set; } = ""; // "10:00 AM", "12:00 PM", "02:00 PM", "04:00 PM"
    public int CakeCount { get; set; }
    public int CupcakeCount { get; set; }
    public int TotalCount { get; set; }
    public int TotalDone { get; set; }
}

/// <summary>
/// Delivery route navigation item.
/// Source: bakeryorders.aspx.cs repRoutes datasource — deliveryRoute entity.
/// Fields: route_ID, route_title, route_date, route_displayOrder, countCakes.
/// </summary>
public class DeliveryRouteItem
{
    public long RouteId { get; set; }
    public string RouteName { get; set; } = "";
    public string OrderCount { get; set; } = "0"; // Legacy uses string from getcakecountsforbakers_byrouteID
    public DateTime RouteDate { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Task row for the assigned tasks table.
/// Source: bakeryorders.aspx.cs class ordertaskdetail — all fields mapped from SQL columns.
/// Grouped by dispatchDate in the view (repAssignedTask_outer groups by dispatchDate).
/// </summary>
public class AssignedTaskRow
{
    // Order-level — source: tbl_order
    public long OrderId { get; set; }
    public int OrderStatus { get; set; }
    public bool IsRepeat { get; set; }
    public long ForwardedOrderId { get; set; }
    public long FollowingOrderId { get; set; }
    public long BakeryId { get; set; }
    public long BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int OrderQuality { get; set; }
    public int SaleType { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal PrdTotal { get; set; }
    public decimal CSMargin { get; set; }
    public decimal ShopMargin { get; set; }
    public decimal PayoutRefund { get; set; }
    public decimal CsRefund { get; set; }

    // Order detail-level — source: tbl_orderDetail + tbl_products
    public long OrderDetailId { get; set; }
    public long ProductId { get; set; }
    public int ProductType { get; set; }           // 1=cake, 2=accessory, 6=cupcake
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
    public int Quantity { get; set; }
    public int ShapeId { get; set; }

    // Item detail — source: tbl_CakeShape, tbl_CakeType, tbl_CakeSize, orderDetail columns
    public string CakeShapeTitle { get; set; } = "";
    public string CakeTypeTitle { get; set; } = "";
    public string SizeId { get; set; } = "";
    public string SizeTitle { get; set; } = "";
    public string ShapeText { get; set; } = "";        // orderDetail_ShapeText
    public string CakeShapeCustomText { get; set; } = "";

    // Collection/delivery — source: tbl_ordercollection
    public int DeliveryMode { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime DispatchDate { get; set; }
    public DateTime OccasionDate { get; set; }
    public DateTime ReadyByDate { get; set; }          // Computed: dispatchDate +/- hours based on delivery mode
    public string CollectionRemarks { get; set; } = "";

    // Customer — source: tbl_shippingDetail
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string PostcodeDistance { get; set; } = "";  // "distance,seconds" from tbl_PostcodeDistance
    public string BranchPostcode { get; set; } = "";

    // Task status — source: tbl_ordertask
    public string TaskId { get; set; } = "";           // ordertask_Id (can be empty if no task assigned)
    public string TaskStatus { get; set; } = "";       // ordertask_tasksts: "11","12","22","33","44" or empty
    public string IsCompleted { get; set; } = "";      // ordertask_isCompleted: "True"/"False" or empty
    public string IsDone { get; set; } = "";           // ordertask_isdone: "True"/"False" or empty
    public string AssignedUserName { get; set; } = ""; // customer_Name from tbl_bakeryuser
    public string TaskRemarks { get; set; } = "";      // ordertaskdet_remarks

    // Hold status — source: tbl_ordertaskhold
    public bool IsOnHold { get; set; }                 // ordertask_ishold (derived from ordertaskhold_ishold)

    // Delivery route — source: tbl_deliveryRouteOrder + tbl_deliveryRoute
    public long RouteId { get; set; }
    public string RouteTitle { get; set; } = "";

    // Print status — source: tbl_log_orderprint
    public string PrintOrderId { get; set; } = "";     // log_orderprint_orderId (non-empty = printed)

    // Reviews — source: tbl_orderreviews
    public string ReviewStars { get; set; } = "";
    public string ReviewRemarks { get; set; } = "";

    // Image update status — source: tbl_orderImageUpdate
    public bool IsChangeOrderImageMarked { get; set; }

    // Other order images (other products in same order)
    public List<OrderImageItem> OrderImages { get; set; } = new();

    // Assigned user count — source: tbl_OrderTaskAssign count
    public string CountAssignedToUser { get; set; } = "0";

    // Sort fields — source: computed sortID and ordersorting_displayorder
    public int SortId { get; set; }
    public int SortId2 { get; set; }

    // Baking flags
    public bool IsBaking { get; set; }                 // WebstoreBranch_isBaking

    // Sponge status — source: tbl_spongeOrderDet
    public int SpongeStatus { get; set; }

    // Whether to show print button (first occurrence of order_ID in list)
    public int ShowPrint { get; set; }

    // Grouping key for the outer repeater
    public DateTime GroupDate { get; set; }            // dispatchDate.Date
}

/// <summary>
/// Other product images in the same order (shown in Picture column).
/// Source: bakeryorders.aspx.cs class orderimage + GetOrderImages method.
/// </summary>
public class OrderImageItem
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public bool IsChangeOrderImageMarked { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
}

/// <summary>
/// Staff member for the task assignment dropdown.
/// Source: bakeryorders.aspx.cs class taskUser — BakeryUser query (customer_type=3, stafftype=1, active, open).
/// </summary>
public class TaskStaffItem
{
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
}

/// <summary>
/// Request model for assigning a staff user to a task.
/// </summary>
public class AssignTaskUserRequest
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public long TaskId { get; set; }
    public int UserId { get; set; }
}

/// <summary>
/// Request model for task actions (start/pause/complete/rewind/remarks/assign).
/// </summary>
public class TaskActionRequest
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public string Action { get; set; } = ""; // start, pause, complete, rewind, remarks, assign
    public int UserId { get; set; }
    public string Remarks { get; set; } = "";
}

/// <summary>
/// Request model for checklist item update.
/// </summary>
public class UpdateChecklistRequest
{
    public long OrderDetailId { get; set; }
    public int ChecklistId { get; set; }
    public bool IsDone { get; set; }
}

/// <summary>
/// Task processing history entry from tbl_ordertaskdet.
/// Source: bakeryorders.aspx.cs lines 1370-1396 (vartaskhistory)
/// Rendered in: bakeryorders.aspx lines 1010-1040 (repTaskhistory_bot)
/// </summary>
public class TaskHistoryEntry
{
    public long TaskDetId { get; set; }          // ordertaskdet_Id
    public long TaskId { get; set; }             // ordertaskdet_taskId
    public int TaskSts { get; set; }             // ordertaskdet_taskSts (11,12,22,33)
    public int UserID { get; set; }              // ordertaskdet_userID
    public string UserName { get; set; } = "";   // customer_Name from tbl_bakeryuser
    public string Remarks { get; set; } = "";    // ordertaskdet_remarks
    public bool IsCompleted { get; set; }        // ordertaskdet_isCompleted
    public bool IsDone { get; set; }             // ordertaskdet_isDone
    public bool IsReply { get; set; }            // ordertaskdet_isreply
    public DateTime StartDate { get; set; }      // ordertaskdet_staDate
    public DateTime EndDate { get; set; }        // ordertaskdet_endDate
    public DateTime ModifiedOn { get; set; }     // ordertaskdet_modifiedOn

    /// <summary>
    /// Gets the stage display name matching legacy GetStatusText_task.
    /// </summary>
    public string StageName => TaskSts switch
    {
        11 => "Filling",
        12 => "Icing",
        22 => "Decoration",
        33 => "Finishing",
        _ => "Not Started"
    };
}

/// <summary>
/// Checklist definition item from tbl_orderProcessingChecklist.
/// Source: bakeryorders.aspx.cs line 1400-1402
/// </summary>
public class ChecklistDefinition
{
    public int ChecklistId { get; set; }
    public string Title { get; set; } = "";
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Per-order-detail checklist state from tbl_lnkOrderChecklist2Order.
/// Source: bakeryorders.aspx.cs line 1407-1409
/// </summary>
public class ChecklistItemState
{
    public int ChecklistId { get; set; }
    public long OrderDetailId { get; set; }
    public bool IsDone { get; set; }
    public bool IsExcluded { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string Remarks { get; set; } = "";
}
