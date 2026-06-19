using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the iframe order manifest page (standalone/print view).
/// Route: /iframeordermenfest
/// Migrated from iframeordermenfest.aspx.
/// 
/// This page is designed to be loaded inside an iframe or printed directly.
/// It calls the same SP as the main order manifest but renders the results
/// inside an email-style template wrapper, grouped by bakery then by date.
/// </summary>
[Route("iframeordermenfest")]
[Route("iframeordermenfest.aspx")]
public class IframeOrderManifestController : Controller
{
    private readonly IConfiguration _config;

    public IframeOrderManifestController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? bakery = null,
        string? from = null,
        string? to = null,
        string? showprint = null,
        string? chkattr = null)
    {
        var bakeryID = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";

        if ((bakery != null || bakeryID != "0") && from != null)
        {
            var showPrintButtons = false;

            // Determine bakery ID
            if (bakery != null)
            {
                bakeryID = bakery;
            }
            else
            {
                showPrintButtons = true;
            }

            if (showprint != null)
            {
                showPrintButtons = true;
            }

            var endDate = to ?? from;
            var siteUrl = _config["SiteUrl"] ?? "/";
            var ckWebStoreId = _config["ckwebstoreid"] ?? "0";
            var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

            // Get manifest data
            var orders = await GetManifestDataAsync(
                Convert.ToInt32(bakeryID), from, endDate);

            if (orders.Any())
            {
                // Group by bakery
                var bakeryGroups = orders
                    .GroupBy(g => g.OrderBakeryId)
                    .Select(n => new { GroupId = n.Key, Items = n.ToList() })
                    .ToList();

                var contentHtml = new StringBuilder();

                foreach (var bakeryGroup in bakeryGroups)
                {
                    var sb = new StringBuilder(
                        "<table border='0' cellpadding='0' cellspacing='5' width='100%'>");
                    int counterint = 0;

                    // Group by dispatch date
                    var dateGroups = bakeryGroup.Items
                        .GroupBy(g => g.DispatchDate.Date)
                        .Select(n => new { DateId = n.Key, DateItems = n.ToList() })
                        .OrderBy(o => o.DateId)
                        .ToList();

                    foreach (var dateGroup in dateGroups)
                    {
                        counterint += 1;

                        // Date header
                        string dateHeader;
                        if (dateGroup.DateItems[0].DispatchDate.Date == DateTime.Today.Date)
                        {
                            dateHeader = "<font style='font-size: 16px; font-weight: bold; text-transform: uppercase;'>Today - "
                                + dateGroup.DateItems[0].DispatchDate.ToString("dddd")
                                + "</font> "
                                + dateGroup.DateItems[0].DispatchDate.ToString("(dd/MM/yyyy)");
                        }
                        else
                        {
                            dateHeader = "<font style='font-size: 16px; font-weight: bold; text-transform: uppercase;'>"
                                + dateGroup.DateItems[0].DispatchDate.ToString("dddd")
                                + "</font> "
                                + dateGroup.DateItems[0].DispatchDate.ToString("(dd/MM/yyyy)");
                        }

                        // Count by product type
                        var cakeCount = dateGroup.DateItems.Count(w => w.ProductType == 1);
                        var cupcakeCount = dateGroup.DateItems.Count(w => w.ProductType == 6);
                        var accessoryCount = dateGroup.DateItems.Count(w => w.ProductType == 2);

                        sb.AppendFormat(
                            "<tr style='color:#ff0000;'><td style='line-height:22px;font-size:14px;text-align:left;{0}'>{1}<span style='float:right;color:#555;'>Total: {2} [{3} + {4} + {5}]</span></td></tr>",
                            counterint > 1 ? "padding-top: 10px;" : "",
                            dateHeader,
                            dateGroup.DateItems.Count,
                            cakeCount,
                            cupcakeCount,
                            accessoryCount);

                        // Table header
                        sb.Append("<tr><td style='border:1px solid #ccc;'><table border='0' cellpadding='5' cellspacing='0' width='100%'><tr style='background-color:#cccccc;text-align:center;'><th  width='100px;'>Order ID</th><th width='150px;'>Delivery Method</th><th>Date(s)</th><th width='180px;'>Status</th></tr>");

                        int counterint1 = 0;
                        var sortedData = dateGroup.DateItems
                            .OrderByDescending(o => o.OrderBranchId)
                            .ThenBy(o => o.DeliveryMode == 4 ? o.DispatchDate : o.CollectionDate)
                            .ToList();

                        foreach (var order in sortedData)
                        {
                            counterint1 += 1;

                            // Date display (objs[0])
                            string dateDisplay;
                            DateTime deliveryDate = order.CollectionDate;

                            if (order.OrderBakeryId.ToString() == ckWebStoreId && order.DeliveryMode == 4)
                            {
                                DateTime dispatchDate = order.DispatchDate;
                                DateTime readyDispatchDate = order.ReadyDispatchDate;
                                dateDisplay = "<b>Ready By:</b><br/>"
                                    + readyDispatchDate.ToString("dd-MMM-yyyy") + " (04:00 PM)<br/><br/>"
                                    + "<b>Dispatch Date:</b><br/>"
                                    + dispatchDate.ToString("dd-MMM-yyyy") + "<br/>(03:00 PM - 05:00 PM)<br/><br/>"
                                    + "<b>Delivery Date:</b><br/>"
                                    + deliveryDate.ToString("dd-MMM-yyyy") + "<br/>(10:00 AM - 05:30 PM)";
                            }
                            else if (order.DeliveryMode == 4)
                            {
                                DateTime dispatchDate = order.DispatchDate;
                                DateTime readyDispatchDate = order.ReadyDispatchDate;
                                dateDisplay = "<b>Ready By:</b><br/>"
                                    + readyDispatchDate.ToString("dd-MMM-yyyy") + " (04:00 PM)<br/><br/>"
                                    + "<b>Dispatch Date:</b><br/>"
                                    + dispatchDate.ToString("dd-MMM-yyyy") + "<br/>(03:00 PM - 05:00 PM)<br/><br/>"
                                    + "<b>Delivery Date:</b><br/>"
                                    + deliveryDate.ToString("dd-MMM-yyyy") + "<br/>(10:00 AM - 05:30 PM)";
                            }
                            else
                            {
                                var modeLabel = order.DeliveryMode == 1 ? "Collection" : "Delivery";
                                dateDisplay = "<b>" + modeLabel + " Date:</b><br/>"
                                    + (order.OrderBakeryId.ToString() == ckWebStoreId && order.DeliveryMode == 4
                                        ? deliveryDate.ToString("dd-MMM-yyyy") + "<br/>(10:00 AM - 05:30 PM)"
                                        : deliveryDate.ToString("dd-MMM-yyyy<br/>(hh:mm tt")
                                            + " - " + deliveryDate.AddHours(2).ToString("hh:mm tt)"));
                            }

                            // Delivery method (objs[4])
                            string deliveryMethod;
                            if (order.OrderBakeryId != order.OrderBranchId && order.DeliveryMode == 1)
                            {
                                deliveryMethod = "<font color='#850000'>Delivery to <b style='display:table'>"
                                    + order.BranchNameDetail + " (" + order.BranchPostcode + ")</b></font><br/>Collection from <font color='#850000'><b>"
                                    + order.BranchPostcode + "</b></font><br/>(" + order.ShippingZip + ")";
                            }
                            else if (order.OrderBakeryId.ToString() == ckWebStoreId && order.DeliveryMode == 4)
                            {
                                deliveryMethod = "Postal Delivery<br/>(" + order.ShippingZip + ")";
                            }
                            else if (order.DeliveryMode == 4)
                            {
                                deliveryMethod = "Postal Delivery<br/>(" + order.ShippingZip + ")";
                            }
                            else if (order.DeliveryMode == 3)
                            {
                                deliveryMethod = "Hand Delivery (CakerSt.)<br/>(" + order.ShippingZip + ")";
                            }
                            else if (order.DeliveryMode == 2)
                            {
                                deliveryMethod = "Hand Delivery (Bakery)<br/>(" + order.ShippingZip + ")";
                            }
                            else
                            {
                                deliveryMethod = (order.DeliveryMode == 2 ? "Hand Delivery (Bakery)" : "Collection")
                                    + "<br/>(" + order.ShippingZip + ")";
                            }

                            // Display ID (objs[6])
                            string displayId;
                            if (order.IsRepeat)
                            {
                                displayId = order.ForwardedOrderId + "/ReOrd";
                            }
                            else if (order.ForwardedOrderId > 0)
                            {
                                displayId = order.ForwardedOrderId + "/FWD";
                            }
                            else
                            {
                                displayId = order.OrderId.ToString();
                            }

                            // Status (objs[7])
                            string statusText = await GetStatusTextAsync(
                                order.OrderStatus, true, order.ForwardedOrderId, order.FollowingOrderId, siteUrl);

                            // Product type label (objs[8])
                            string productTypeLabel = order.ProductType == 1 ? "Cake"
                                : order.ProductType == 6 ? "Cupcake"
                                : "Party Accessory";

                            // Image URL
                            string imageUrl = GetProductImageUrl(order.ProductImage1, cdnBase);

                            var borderColor = counterint > 1 ? "#aaa" : "#ccc";
                            var printUrl = siteUrl + "printorder/" + order.OrderId + "?wccode=" + order.WebstoreCode;

                            sb.AppendFormat(
                                "<tr style='text-align:center;vertical-align:top;'> "
                                + "<td style='border-top:1px solid {0};'><a target='_blank' href='{1}'>#{2}</a><br/><a target='_blank' href='{1}'><img style='width:80px;border-width:0;' src='{3}' /></a><br/><br/>{4}</td>"
                                + "<td style='border-top:1px solid {0};'>{5}</td>"
                                + "<td style='border-top:1px solid {0};'>{6}</td>"
                                + "<td style='border-top:1px solid {0};'>{7}</td>"
                                + "</tr>",
                                borderColor,
                                printUrl,
                                displayId,
                                imageUrl,
                                productTypeLabel,
                                deliveryMethod,
                                dateDisplay,
                                statusText);

                            // Attributes row (if chkattr param present)
                            if (chkattr != null)
                            {
                                var attrHtml = await GetOrderAttributesHtmlAsync(order.OrderDetailId);
                                sb.AppendFormat(
                                    "<tr><td style='text-align:justify;' colspan='5'>{0}</td></tr>",
                                    attrHtml);
                            }

                            // Remarks row
                            if (!string.IsNullOrEmpty(order.Remarks))
                            {
                                sb.AppendFormat(
                                    "<tr style='background-color:#f5f5f5;'><td style='text-align:left;' colspan='5'><b>Remarks</b></td></tr>"
                                    + "<tr><td style='text-align:justify;' colspan='5'>{0}</td></tr>",
                                    order.Remarks);
                            }
                        }

                        sb.Append("</table></td></tr>");
                    }

                    sb.Append("</table>");

                    // Wrap in email template
                    var wrappedHtml = WrapInEmailTemplate(
                        "Order Dispatch Reminder",
                        sb.ToString(),
                        bakeryGroup.Items[0].WebstoreBusinessName,
                        siteUrl);

                    contentHtml.Append(wrappedHtml);
                }

                ViewBag.ContentHtml = contentHtml.ToString();
            }
            else
            {
                ViewBag.ContentHtml = "";
            }

            ViewBag.ShowPrintButtons = showPrintButtons;
            return View();
        }

        // No valid params — show empty page
        ViewBag.ContentHtml = "";
        ViewBag.ShowPrintButtons = false;
        return View();
    }

    // ─── Data Access ──────────────────────────────────────────────────────────

    private async Task<List<IframeManifestOrderItem>> GetManifestDataAsync(
        int bakeryId, string fromDate, string toDate)
    {
        var items = new List<IframeManifestOrderItem>();
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("getordermenifestlistbybakeryID_withapcdata", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 120;

        cmd.Parameters.AddWithValue("@bakeryID", bakeryId);
        cmd.Parameters.AddWithValue("@dtnow", fromDate);
        cmd.Parameters.AddWithValue("@dt", toDate);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new IframeManifestOrderItem
            {
                OrderId = GetIntSafe(reader, "order_ID"),
                OrderDetailId = GetIntSafe(reader, "orderDetail_ID"),
                OrderBakeryId = GetIntSafe(reader, "order_bakeryID"),
                OrderBranchId = GetIntSafe(reader, "order_branchID"),
                ForwardedOrderId = GetIntSafe(reader, "order_forwardedorderid"),
                FollowingOrderId = GetIntSafe(reader, "order_followingorderid"),
                OrderStatus = GetIntSafe(reader, "order_status"),
                DeliveryMode = GetIntSafe(reader, "ordercollection_deliverymode"),
                ProductType = GetIntSafe(reader, "product_type"),
                CollectionDate = GetDateTimeSafe(reader, "ordercollection_Date"),
                DispatchDate = GetDateTimeSafe(reader, "dispatchdate"),
                ReadyDispatchDate = GetDateTimeSafe(reader, "readydispatchdate"),
                Remarks = GetStringSafe(reader, "ordercollection_Remarks"),
                WebstoreBusinessName = GetStringSafe(reader, "webstore_businessName"),
                WebstoreOrderEmail = GetStringSafe(reader, "webstore_OrderEmail"),
                WebstoreCode = GetStringSafe(reader, "webstore_code"),
                CustomerEmailId = GetStringSafe(reader, "customer_EmailID"),
                ProductImage1 = GetStringSafe(reader, "Product_Image1"),
                ShippingZip = GetStringSafe(reader, "shipping_zip"),
                BranchNameDetail = GetStringSafe(reader, "branchName"),
                BranchPostcode = GetStringSafe(reader, "branchpostcode"),
                IsRepeat = GetBoolSafe(reader, "order_isrepeat")
            });
        }
        return items;
    }

    // ─── Status Text Logic ────────────────────────────────────────────────────

    private async Task<string> GetStatusTextAsync(
        int orderStatus, bool orderIsPurchased,
        int forwardedOrderId, int followingOrderId, string siteUrl)
    {
        if (followingOrderId > 0)
        {
            var statusText = "Forwarded";
            var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT o.*, b.webstore_businessName 
                        FROM tbl_order o 
                        INNER JOIN tbl_webstore b ON o.order_bakeryid = b.webstore_id 
                        WHERE o.order_ID = @orderId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@orderId", followingOrderId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var businessName = GetStringSafe(reader, "webstore_businessName");
                var fwdForwardedOrderId = GetStringSafe(reader, "order_forwardedorderid");
                var fwdIsRepeat = GetBoolSafe(reader, "order_isrepeat");

                var fwdDisplayId = GetOrderIdWithRepeatCheck(
                    followingOrderId.ToString(), fwdForwardedOrderId, fwdIsRepeat);

                statusText += " To: <br/>" + businessName
                    + "<br/><a target='_blank' href='" + siteUrl + "printorder/"
                    + followingOrderId + "'>#" + fwdDisplayId + "</a>";
            }

            return statusText;
        }
        else if (!orderIsPurchased)
        {
            return "Not Paid Yet";
        }
        else
        {
            return GetStatusTextByCode(orderStatus);
        }
    }

    private static string GetStatusTextByCode(int orderStatus)
    {
        return orderStatus switch
        {
            0 => "Pending",
            1 => "Confirmed",
            2 => "Processed",
            3 => "Under Delivery",
            4 => "Completed",
            5 => "Under Processing",
            11 => "Cancelled",
            _ => ""
        };
    }

    private static string GetOrderIdWithRepeatCheck(
        string orderId, string forwardId, bool isRepeat)
    {
        if (isRepeat)
            return forwardId + "/ReOrd";

        if (long.TryParse(string.IsNullOrEmpty(forwardId) ? "0" : forwardId, out var fwd) && fwd > 0)
            return forwardId + "/FWD";

        return orderId;
    }

    // ─── Order Attributes HTML ────────────────────────────────────────────────

    private async Task<string> GetOrderAttributesHtmlAsync(int orderDetailId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        var result = new StringBuilder();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Get order detail
        var sqlDetail = "SELECT * FROM tbl_orderDetail WHERE orderDetail_ID = @detailId";
        await using var cmdDetail = new SqlCommand(sqlDetail, conn);
        cmdDetail.Parameters.AddWithValue("@detailId", orderDetailId);

        int shapeId = 0, sizeId = 0, typeId = 0;
        string shapeText = "", quantity = "";
        int shapeCustomText = 0;

        await using (var reader = await cmdDetail.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                shapeId = GetIntSafe(reader, "orderDetail_shapeId");
                sizeId = GetIntSafe(reader, "orderDetail_SizeID");
                typeId = GetIntSafe(reader, "orderDetail_TypeID");
                shapeText = GetStringSafe(reader, "orderDetail_ShapeText");
                quantity = GetStringSafe(reader, "orderDetail_Quantity");
            }
        }

        if (shapeId <= 0) return result.ToString();

        var localData = new StringBuilder();

        // Get shape info
        var sqlShape = "SELECT * FROM tbl_CakeShape WHERE CakeShapeID = @shapeId";
        await using var cmdShape = new SqlCommand(sqlShape, conn);
        cmdShape.Parameters.AddWithValue("@shapeId", shapeId);

        await using (var reader = await cmdShape.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var shapeTitle = GetStringSafe(reader, "CakeShapeTitle");
                shapeCustomText = GetIntSafe(reader, "CakeShapeCustomText");

                localData.Append("<div class='headertext'>Shape</div><div class='valtext'>"
                    + shapeTitle + "</div>");

                if (shapeCustomText > 0)
                {
                    localData.Append("<div class='headertext'>No of Cakes</div><div class='valtext'>"
                        + quantity + "</div>");
                    if (shapeCustomText == 1)
                    {
                        localData.Append("<div class='headertext'>Letters A-Z</div><div class='valtext'>"
                            + shapeText + "</div>");
                    }
                    else
                    {
                        localData.Append("<div class='headertext'>Numbers 0-9</div><div class='valtext'>"
                            + shapeText + "</div>");
                    }
                }
            }
        }

        // Get type info
        var sqlType = "SELECT * FROM tbl_CakeType WHERE CakeTypeID = @typeId";
        await using var cmdType = new SqlCommand(sqlType, conn);
        cmdType.Parameters.AddWithValue("@typeId", typeId);

        await using (var reader = await cmdType.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var typeTitle = GetStringSafe(reader, "CakeTypeTitle");
                localData.Append("<div class='headertext'>Type</div><div class='valtext'>"
                    + typeTitle + "</div>");
            }
        }

        // Get size info
        var sqlSize = "SELECT * FROM tbl_CakeSize WHERE SizeID = @sizeId";
        await using var cmdSize = new SqlCommand(sqlSize, conn);
        cmdSize.Parameters.AddWithValue("@sizeId", sizeId);

        await using (var reader = await cmdSize.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var sizeTitle = GetStringSafe(reader, "SizeTitle");
                localData.Append("<div class='headertext'>Size</div><div class='valtext'>"
                    + sizeTitle + "</div>");
            }
        }

        if (localData.Length > 0)
        {
            result.Append("<div class='Flavour_outer'><div class='headertext'>Shape, Size & Type</div><div class='maindata'>"
                + localData + "</div></div>");
        }

        // Get flavour attributes
        var flavourData = new StringBuilder();
        var sqlAtt = "SELECT * FROM tbl_orderAttDet WHERE orderAttDet_orderdetID = @detailId AND orderAttDet_flavourType = 1";
        await using var cmdAtt = new SqlCommand(sqlAtt, conn);
        cmdAtt.Parameters.AddWithValue("@detailId", orderDetailId);

        await using (var reader = await cmdAtt.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var attIds = GetStringSafe(reader, "orderAttDet_AttIDs");
                var parentAttId = GetIntSafe(reader, "orderAttDet_ParentAttId");

                // Get flavour names
                var attNames = new StringBuilder();
                if (!string.IsNullOrEmpty(attIds))
                {
                    var sqlFlavour = "SELECT FlavourTitle FROM tbl_CustFlavour WHERE FlavourID IN (SELECT value FROM STRING_SPLIT(@ids, ','))";
                    await using var cmdFlavour = new SqlCommand(sqlFlavour, conn);
                    cmdFlavour.Parameters.AddWithValue("@ids", attIds);

                    await using var flavourReader = await cmdFlavour.ExecuteReaderAsync();
                    while (await flavourReader.ReadAsync())
                    {
                        attNames.Append(GetStringSafe(flavourReader, "FlavourTitle") + "<br />");
                    }
                }

                // Get parent flavour short name
                var parentName = "";
                var sqlParent = "SELECT FlavourShortName FROM tbl_Flavour WHERE FlavourID = @parentId";
                await using var cmdParent = new SqlCommand(sqlParent, conn);
                cmdParent.Parameters.AddWithValue("@parentId", parentAttId);

                var parentResult = await cmdParent.ExecuteScalarAsync();
                if (parentResult != null)
                {
                    parentName = parentResult.ToString() ?? "";
                }

                flavourData.Append("<div class='headertext'>" + parentName
                    + "</div><div class='valtext'>" + attNames + "</div>");
            }
        }

        if (flavourData.Length > 0)
        {
            result.Append("<div class='Flavour_outer attr'><div class='headertext'><ul><li class='red'>Flavour</li></ul></div><div class='maindata'>"
                + flavourData + "</div></div>");
        }

        return result.ToString();
    }

    // ─── Product Image URL ────────────────────────────────────────────────────

    private static string GetProductImageUrl(string productImage, string cdnBase)
    {
        if (string.IsNullOrEmpty(productImage))
        {
            return cdnBase + "img/commingsoon_cake.jpg";
        }
        return cdnBase + "resized_300_300/" + productImage;
    }

    // ─── Email Template Wrapper ───────────────────────────────────────────────
    // Replicates clsMail.strMainEmailBody with stremailtype="0" (no unsubscribe)

    private string WrapInEmailTemplate(
        string title, string innerBody, string businessName, string siteUrl)
    {
        return @"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html>
<head>
<meta http-equiv='Content-Type' content='text/html; charset=iso-8859-1' />
<title>" + title + @"</title>
</head>
<body style='margin:auto;font-family:Arial;font-size:12px;color:#000;'>
<table width='100%' cellpadding='0' align='center' cellspacing='0'>
<tr>
<td valign='top' align='center'>
<table width='100%' cellpadding='0' align='center' cellspacing='0'>
<tr>
<td align='center'>
<table width='650' cellpadding='0' cellspacing='0' border='0' style='border:solid 1px #555;'>
<tr>
<td>
<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='background-color: #ffffff; border-bottom: 1px solid rgb(204, 204, 204); padding: 5px 10px;'>
<a href='" + siteUrl + @"login'>
<img border='0' style='padding: 10px 5px;max-width: 114px;' src='" + siteUrl + @"images/logo.png' alt='CakerStreet' /></a>
</td></tr>
</table>
</td>
</tr>
<tr>
<td>
" + innerBody + @"
</td>
</tr>
</table>
</td>
</tr>
</table></td>
</tr>
</table>
</body>
</html>";
    }

    // ─── Safe Reader Helpers ──────────────────────────────────────────────────

    private static int GetIntSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }
        catch { return 0; }
    }

    private static string GetStringSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            return reader.IsDBNull(ordinal) ? "" : reader.GetValue(ordinal)?.ToString() ?? "";
        }
        catch { return ""; }
    }

    private static DateTime GetDateTimeSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
            var value = reader.GetValue(ordinal);
            if (value is DateTime dt) return dt;
            return DateTime.TryParse(value?.ToString(), out var parsed) ? parsed : DateTime.MinValue;
        }
        catch { return DateTime.MinValue; }
    }

    private static bool GetBoolSafe(SqlDataReader reader, string col)
    {
        try
        {
            var ordinal = reader.GetOrdinal(col);
            if (reader.IsDBNull(ordinal)) return false;
            var value = reader.GetValue(ordinal);
            if (value is bool b) return b;
            if (int.TryParse(value?.ToString(), out var intVal)) return intVal != 0;
            return false;
        }
        catch { return false; }
    }

    // ─── Model ────────────────────────────────────────────────────────────────

    private class IframeManifestOrderItem
    {
        public int OrderId { get; set; }
        public int OrderDetailId { get; set; }
        public int OrderBakeryId { get; set; }
        public int OrderBranchId { get; set; }
        public int ForwardedOrderId { get; set; }
        public int FollowingOrderId { get; set; }
        public int OrderStatus { get; set; }
        public int DeliveryMode { get; set; }
        public int ProductType { get; set; }
        public DateTime CollectionDate { get; set; }
        public DateTime DispatchDate { get; set; }
        public DateTime ReadyDispatchDate { get; set; }
        public string Remarks { get; set; } = "";
        public string WebstoreBusinessName { get; set; } = "";
        public string WebstoreOrderEmail { get; set; } = "";
        public string WebstoreCode { get; set; } = "";
        public string CustomerEmailId { get; set; } = "";
        public string ProductImage1 { get; set; } = "";
        public string ShippingZip { get; set; } = "";
        public string BranchNameDetail { get; set; } = "";
        public string BranchPostcode { get; set; } = "";
        public bool IsRepeat { get; set; }
    }
}
