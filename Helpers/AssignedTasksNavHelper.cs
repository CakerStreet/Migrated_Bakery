namespace CakerStreet.Business.Helpers;

/// <summary>
/// Pure helper methods for the ordertype=12 Assigned Tasks navigation layers.
/// All methods are static and side-effect-free for easy unit/property testing.
/// Source: bakeryorders.aspx + bakeryorders.aspx.cs ordertype=12 section.
/// </summary>
public static class AssignedTasksNavHelper
{
    // ─── Day-of-week mapping ───────────────────────────────────────────────────
    // Source: clsglobaltext.getdayIdbyName / getdaybyID
    // Monday=1, Tuesday=2, Wednesday=3, Thursday=4, Friday=5, Saturday=6, Sunday=7

    /// <summary>
    /// Converts a DateTime to the custom dayID (Monday=1 through Sunday=7).
    /// Source: clsglobaltext.getdayIdbyName(date.DayOfWeek.ToString())
    /// </summary>
    public static int GetDayId(DateTime date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 1
        };
    }

    /// <summary>
    /// Converts a dayID (1-7) to the day name string.
    /// Source: clsglobaltext.getdaybyID(strdayid)
    /// </summary>
    public static string GetDayName(int dayId)
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
            _ => "Monday"
        };
    }

    // ─── Day Tab CSS class ─────────────────────────────────────────────────────
    // Source: bakeryorders.aspx line 166 (repDayType ItemTemplate):
    //   class='<%#(((Convert.ToDateTime(Eval("DayDate").ToString()) < DateTime.Today) || bool.Parse(Eval("isclosed").ToString()))?"faded":"norm")
    //          +((Request["dayID"] != null)?((Request["dayID"].ToString() == Eval("DayID").ToString())?" stt":""):"") %>'

    /// <summary>
    /// Returns the CSS class for a day tab anchor element.
    /// Logic: if past or closed → "faded"; else → "norm". If active dayID matches → append " stt".
    /// </summary>
    public static string GetDayTabCssClass(DateTime tabDate, bool isClosed, int tabDayId, int activeDayId, bool hasDayIdParam)
    {
        // Base class: faded if past or closed, otherwise norm
        string baseClass = (tabDate.Date < DateTime.Today || isClosed) ? "faded" : "norm";

        // Active suffix: only appended if dayID querystring param is present and matches
        if (hasDayIdParam && tabDayId == activeDayId)
        {
            return baseClass + " stt";
        }

        return baseClass;
    }

    // ─── Dispatch Time Slot CSS class ──────────────────────────────────────────
    // Source: bakeryorders.aspx line 190 (repDispatchTimeType ItemTemplate):
    //   class='<%#((Convert.ToInt32(Eval("totalcount").ToString()) == Convert.ToInt32(Eval("totaldone").ToString()))?"faded"
    //          :((Request["disptime"] != null)?((Request["disptime"].ToString() == Eval("TimeID").ToString())?" stt":""):""))%>'

    /// <summary>
    /// Returns the CSS class for a dispatch time slot link.
    /// Logic: if totalcount == totaldone → "faded"; else if active → " stt"; else → "".
    /// </summary>
    public static string GetTimeSlotCssClass(int timeId, int activeDispTime, int totalCount, int totalDone, bool hasDispTimeParam)
    {
        if (totalCount == totalDone)
        {
            return "faded";
        }

        if (hasDispTimeParam && timeId == activeDispTime)
        {
            return " stt";
        }

        return "";
    }

    // ─── Task Type active class ────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 648-676:
    //   if littasktype == "0" && topper == 0 → ancAlltask gets "stt"
    //   if littasktype == "0" && topper == 1 → ancPreparation_topper gets "stt"
    //   if littasktype == "11" → ancPreparation_filling gets "stt"
    //   if littasktype == "12" → ancPreparation_icing gets "stt"
    //   if littasktype == "22" → ancDecoration gets "stt"
    //   if littasktype == "33" → ancCompletion gets "stt"
    //   if littasktype == "44" → anctaskprocessed gets "stt"

    /// <summary>
    /// Returns "stt" if the given task type link is the active one, otherwise "".
    /// </summary>
    /// <param name="linkTaskType">The tasktype value this link represents (0=All, 11, 12, 22, 33, 44)</param>
    /// <param name="linkIsTopper">True if this is the Topper link</param>
    /// <param name="activeTaskType">Current tasktype from querystring (0 if not present)</param>
    /// <param name="activeTopper">Current topper from querystring (0 or 1)</param>
    public static string GetTaskTypeCssClass(int linkTaskType, bool linkIsTopper, int activeTaskType, int activeTopper)
    {
        if (linkIsTopper)
        {
            // Topper link is active when topper=1 and tasktype=0
            return (activeTopper == 1 && activeTaskType == 0) ? "stt" : "";
        }

        if (linkTaskType == 0)
        {
            // "All" link is active when tasktype=0 and topper=0
            return (activeTaskType == 0 && activeTopper == 0) ? "stt" : "";
        }

        // Specific task type link is active when its value matches
        return (activeTaskType == linkTaskType) ? "stt" : "";
    }

    // ─── Delivery Mode active class ────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 706-726:
    //   if litdeliverymode == "0" → ancDeliveryAll gets "stt"
    //   if litdeliverymode == "1" → ancDelivery_collection gets "stt"
    //   if litdeliverymode == "2" → ancDelivery_Byhand gets "stt"
    //   if litdeliverymode == "4" → ancDelivery_ByPost gets "stt"

    /// <summary>
    /// Returns "stt" if the given delivery mode link is the active one, otherwise "".
    /// </summary>
    /// <param name="linkDm">The dm value this link represents (0=All, 1, 2, 4)</param>
    /// <param name="activeDm">Current dm from querystring (0 if not present)</param>
    public static string GetDeliveryModeCssClass(int linkDm, int activeDm)
    {
        return (linkDm == activeDm) ? "stt" : "";
    }

    // ─── Delivery Route CSS class ──────────────────────────────────────────────
    // Source: bakeryorders.aspx line 251 (repRoutes ItemTemplate):
    //   class='<%#"norm"+((Request["rt"] != null)?((Request["rt"].ToString() == Eval("route_ID").ToString())?" stt":""):"") %>'
    // And for "All Routes" link (ancDeliveryrouteAll):
    //   if Request["rt"] == "0" → gets "stt"

    /// <summary>
    /// Returns the CSS class for a delivery route link.
    /// Logic: base is always "norm", append " stt" if this route matches active rt.
    /// </summary>
    public static string GetRouteCssClass(long routeId, int activeRouteId, bool hasRtParam)
    {
        if (hasRtParam && routeId == activeRouteId)
        {
            return "norm stt";
        }
        return "norm";
    }

    /// <summary>
    /// Returns the CSS class for the "All Routes" link.
    /// Source: bakeryorders.aspx.cs line 722 — if Request["rt"].ToString() == "0" → "stt"
    /// </summary>
    public static string GetAllRoutesCssClass(int activeRouteId, bool hasRtParam)
    {
        if (hasRtParam && activeRouteId == 0)
        {
            return "stt";
        }
        return "";
    }

    // ─── Task Status CSS class ─────────────────────────────────────────────────
    // Source: bakeryorders.aspx line 855 area — task status label uses btn-* classes:
    //   Not Started (no tasksts) → "btn-default"
    //   Filling (11) or Icing (12) → "btn-danger"
    //   Decoration (22) → "btn-info"
    //   Finishing (33) → "btn-success"

    /// <summary>
    /// Returns the CSS class for a task status label button.
    /// </summary>
    public static string GetTaskStatusCssClass(string taskStatus)
    {
        return taskStatus switch
        {
            "11" => "btn-danger",
            "12" => "btn-danger",
            "22" => "btn-info",
            "33" => "btn-success",
            _ => "btn-default"
        };
    }

    /// <summary>
    /// Returns the exact legacy task status button text.
    /// Source: bakeryorders.aspx.cs GetStatusText_taskBtn (lines 3512-3548)
    /// </summary>
    /// <param name="productType">1=cake, 2=accessory, 6=cupcake</param>
    /// <param name="taskStatus">ordertask_tasksts: "", "11", "12", "22", "33", "44"</param>
    /// <param name="isCompleted">ordertask_isCompleted as string "True"/"False"/""</param>
    /// <param name="customerName">customer_Name from tbl_bakeryuser (assigned user)</param>
    public static string GetTaskStatusButtonText(int productType, string taskStatus, string isCompleted, string customerName)
    {
        bool completed = string.Equals(isCompleted, "True", StringComparison.OrdinalIgnoreCase);

        if (productType == 2)
        {
            // Accessories skip to Finishing
            return taskStatus switch
            {
                "" or null => "Start Finishing",
                "33" => completed ? "Under Delivery" : $"Finishing ({customerName})",
                "44" => "Under Delivery",
                _ => ""
            };
        }
        else
        {
            // Cakes/cupcakes — full stage chain
            return taskStatus switch
            {
                "" or null => "Not Started Yet",
                "11" => completed ? "Start Icing" : $"Filling ({customerName})",
                "12" => completed ? "Start Decoration" : $"Icing ({customerName})",
                "22" => completed ? "Start Finishing" : $"Decoration ({customerName})",
                "33" => completed ? "Under Delivery" : $"Finishing ({customerName})",
                "44" => "Under Delivery",
                _ => ""
            };
        }
    }

    // ─── Filter parameter validation ──────────────────────────────────────────
    // Source: bakeryorders.aspx.cs — valid values observed in querystring handling

    /// <summary>
    /// Validates a filter parameter value against its allowed set.
    /// Returns true if the value is valid for the given parameter.
    /// </summary>
    public static bool IsValidFilterValue(string paramName, int value)
    {
        return paramName switch
        {
            "tasktype" => value is 11 or 12 or 22 or 33 or 44,
            "dm" => value is 1 or 2 or 4,
            "topper" => value is 0 or 1,
            "dayID" => value is >= 1 and <= 7,
            _ => true // Unknown params are not validated
        };
    }

    // ─── URL generation ────────────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 686-704 — task type link URL patterns:
    //   ancAlltask.HRef = "~/businessorders?ordertype=12" + strfindfilter + ((litDayType.Text == "0") ? "" : "&dayID=" + litDayType.Text);
    //   ancPreparation_topper.HRef = "~/businessorders?ordertype=12&topper=1" + strfindfilter + ((litDayType.Text == "0") ? "" : "&dayID=" + litDayType.Text);
    //   ancPreparation_filling.HRef = "~/businessorders?ordertype=12" + strfindfilter + ((litDayType.Text == "0") ? "" : "&dayID=" + litDayType.Text) + "&tasktype=11";
    //
    // Delivery mode link URL patterns (lines 700-703):
    //   ancDeliveryAll.HRef = "~/businessorders?ordertype=12" + ((topper==1)?"&topper=1":"") + ((littasktype.Text=="0")?"":"&tasktype="+littasktype.Text) + strfindfilter + ((litDayType.Text=="0")?"":"&dayID="+litDayType.Text);
    //   ancDelivery_collection.HRef = same + "&dm=1"
    //   ancDelivery_Byhand.HRef = same + "&dm=2&rt=0"
    //   ancDelivery_ByPost.HRef = same + "&dm=4"

    /// <summary>
    /// Builds a task type filter link URL.
    /// Preserves: ordertype=12, strfindfilter (from/to/dt, disptime), dayID if present.
    /// Source: bakeryorders.aspx.cs lines 686-693.
    /// </summary>
    /// <param name="tasktype">The tasktype value for this link (0 for All, 11, 12, 22, 33, 44)</param>
    /// <param name="isTopper">True if this is the Topper link (uses topper=1 instead of tasktype)</param>
    /// <param name="dayId">Active dayID (0 = not present)</param>
    /// <param name="strFindFilter">Additional filter params string (from/to/dt, disptime) — already formatted with leading &amp;</param>
    public static string BuildTaskTypeUrl(int tasktype, bool isTopper, int dayId, string strFindFilter)
    {
        string url = "/businessorders?ordertype=12";

        if (isTopper)
        {
            url += "&topper=1";
        }

        url += strFindFilter;

        if (dayId > 0)
        {
            url += "&dayID=" + dayId;
        }

        if (!isTopper && tasktype > 0)
        {
            url += "&tasktype=" + tasktype;
        }

        return url;
    }

    /// <summary>
    /// Builds a delivery mode filter link URL.
    /// Preserves: ordertype=12, topper, tasktype, strfindfilter, dayID.
    /// Source: bakeryorders.aspx.cs lines 700-703.
    /// </summary>
    /// <param name="dm">The dm value for this link (0 for All, 1, 2, 4)</param>
    /// <param name="activeTopper">Current topper value (0 or 1)</param>
    /// <param name="activeTaskType">Current tasktype value (0 = not present)</param>
    /// <param name="dayId">Active dayID (0 = not present)</param>
    /// <param name="strFindFilter">Additional filter params string</param>
    public static string BuildDeliveryModeUrl(int dm, int activeTopper, int activeTaskType, int dayId, string strFindFilter)
    {
        string url = "/businessorders?ordertype=12";

        if (activeTopper == 1)
        {
            url += "&topper=1";
        }

        if (activeTaskType > 0)
        {
            url += "&tasktype=" + activeTaskType;
        }

        url += strFindFilter;

        if (dayId > 0)
        {
            url += "&dayID=" + dayId;
        }

        if (dm == 2)
        {
            url += "&dm=2&rt=0";
        }
        else if (dm > 0)
        {
            url += "&dm=" + dm;
        }

        return url;
    }

    /// <summary>
    /// Builds the "All Routes" link URL.
    /// Source: bakeryorders.aspx.cs line 716:
    ///   ancDeliveryrouteAll.HRef = "~/businessorders?ordertype=12" + topper + tasktype + strfindfilter + dayID + "&dm=2&rt=0"
    /// </summary>
    public static string BuildAllRoutesUrl(int activeTopper, int activeTaskType, int dayId, string strFindFilter)
    {
        return BuildDeliveryModeUrl(2, activeTopper, activeTaskType, dayId, strFindFilter);
    }

    /// <summary>
    /// Builds a delivery route link URL.
    /// Source: bakeryorders.aspx line 252:
    ///   href='<%#"~/businessorders?ordertype=12&dayID="+((int)DateTime.Parse(Eval("route_date").ToString()).DayOfWeek).ToString()+"&dm=2&rt="+Eval("route_ID").ToString() %>'
    /// NOTE: Legacy uses (int)DayOfWeek (Sunday=0, Monday=1..Saturday=6) for the dayID in route links.
    /// This appears to be a legacy inconsistency with the custom mapping (Monday=1..Sunday=7).
    /// We replicate the legacy behaviour exactly.
    /// </summary>
    public static string BuildRouteUrl(DateTime routeDate, long routeId)
    {
        int dayOfWeekInt = (int)routeDate.DayOfWeek;
        return $"/businessorders?ordertype=12&dayID={dayOfWeekInt}&dm=2&rt={routeId}";
    }

    /// <summary>
    /// Builds a day tab link URL.
    /// Source: bakeryorders.aspx line 167:
    ///   href='<%#"~/businessorders?ordertype=12&dayID="+Eval("DayID").ToString() %>'
    /// </summary>
    public static string BuildDayTabUrl(int dayId, string? startdate = null)
    {
        var url = $"/businessorders?ordertype=12&dayID={dayId}";
        if (!string.IsNullOrEmpty(startdate)) url += $"&startdate={Uri.EscapeDataString(startdate)}";
        return url;
    }

    /// <summary>
    /// Builds a dispatch time slot link URL.
    /// Source: bakeryorders.aspx line 192:
    ///   href='<%#"~/businessorders?ordertype=12&dayID="+Request["DayID"].ToString()+"&disptime="+Eval("TimeID").ToString() %>'
    /// </summary>
    public static string BuildDispatchTimeUrl(int dayId, int timeId, string? startdate = null)
    {
        var url = $"/businessorders?ordertype=12&dayID={dayId}&disptime={timeId}";
        if (!string.IsNullOrEmpty(startdate)) url += $"&startdate={Uri.EscapeDataString(startdate)}";
        return url;
    }

    /// <summary>
    /// Builds the "All" dispatch time link URL (no disptime param).
    /// Source: bakeryorders.aspx.cs line 1196:
    ///   ancAllDispatchTime.HRef = "~/businessorders?ordertype=12&dayID=" + dayId.ToString();
    /// </summary>
    public static string BuildAllDispatchTimeUrl(int dayId, string? startdate = null)
    {
        var url = $"/businessorders?ordertype=12&dayID={dayId}";
        if (!string.IsNullOrEmpty(startdate)) url += $"&startdate={Uri.EscapeDataString(startdate)}";
        return url;
    }

    // ─── strFindFilter builder ─────────────────────────────────────────────────
    // Source: bakeryorders.aspx.cs lines 678-684:
    //   if (Request["from"] != null && Request["to"] != null && Request["dt"] != null)
    //       strfindfilter += "&from=" + Request["from"] + "&to=" + Request["to"] + "&dt=" + Request["dt"];
    //   if (Request["disptime"] != null)
    //       strfindfilter += "&disptime=" + Request["disptime"];

    /// <summary>
    /// Builds the strfindfilter string from active date range and disptime parameters.
    /// Returns a string with leading &amp; for each param, or empty string if none.
    /// </summary>
    public static string BuildFindFilter(string? from, string? to, string? dt, int disptime)
    {
        string filter = "";

        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to) && !string.IsNullOrEmpty(dt))
        {
            filter += "&from=" + from + "&to=" + to + "&dt=" + dt;
        }

        if (disptime > 0)
        {
            filter += "&disptime=" + disptime;
        }

        return filter;
    }
}
