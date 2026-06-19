using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays and manages Customer Requirement Forms (CRF) and bakery quotations.
/// Bakeries can view CRFs, submit quotes, accept/decline/counter-offer, and edit quotes.
/// Migrated from legacy crflist_forsBakery.aspx / crflist_forsBakery.aspx.cs.
/// Routes: /business-quotation, /business-quotation/{id}
/// </summary>
public class CrfListBakeryController : Controller
{
    private readonly IConfiguration _config;
    private readonly BakeryMenuService _menuService;

    public CrfListBakeryController(IConfiguration config, BakeryMenuService menuService)
    {
        _config = config;
        _menuService = menuService;
    }

    private string ConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

    private async Task PopulateLayoutAsync()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
    }

    [HttpGet("business-quotation")]
    [HttpGet("business-quotation/{id?}")]
    [HttpGet("crflist_forsBakery.aspx")]
    public async Task<IActionResult> Index(long? id = null, [FromQuery] string? sortid = null)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;

        if (webshopId == 0)
            return Redirect($"/?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutAsync();

        string orderFilter = " order by CRF_modifiedOn desc";
        string crfFilter = "";
        if (id.HasValue)
        {
            crfFilter = $" and CRF_ID={id.Value}";
        }
        else if (sortid == "2")
        {
            orderFilter = " order by CRF_datetime desc";
        }

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        // Load CRF list
        var dtCrf = new DataTable();
        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        var sql = @"SELECT *,
            isnull((select top 1 order_ID from tbl_order inner join tbl_orderDetail on order_ID=orderDetail_orderID
             inner join tbl_crfQuote on crfQuote_prdID=orderDetail_productID
             where crfQuote_CRFID=CRF_ID and order_isPurchased=1),0) orderid,
            isnull((select customer_name+' '+customer_surname from tbl_customers where customer_ID=CRF_linkedto),'-') crf_person
            FROM tbl_CRF
            INNER JOIN tbl_lnkCRFtoBakery ON CRF_ID=lnkCRFtoBakery_CRFID
            LEFT JOIN tbl_CakeShape ON CRF_ShapeID=CakeShapeID
            LEFT JOIN tbl_CakeType ON CRF_typeID=CakeTypeID
            LEFT JOIN tbl_category ON CRF_OccasionID=category_ID
            WHERE lnkCRFtoBakery_isdeleted=0 AND lnkCRFtoBakery_bakeryID=@wid" + crfFilter + " AND CRF_datetime>getdate() " + orderFilter;

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@wid", webshopId);
            dtCrf.Load(await cmd.ExecuteReaderAsync());
        }

        // Load sizes
        var sizes = new List<SizeItem>();
        var sqlSize = "SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE custid=@wid AND IsActive=1 ORDER BY DisplayOrder";
        await using (var cmd = new SqlCommand(sqlSize, conn))
        {
            cmd.Parameters.AddWithValue("@wid", webshopId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                sizes.Add(new SizeItem { SizeId = rdr.GetInt64(0), SizeTitle = rdr.GetString(1) });
        }

        // For each CRF, load quotes and images
        var crfQuotes = new Dictionary<long, DataTable>();
        var crfImages = new Dictionary<long, List<ImageItem>>();
        var crfAttrs = new Dictionary<long, string>();

        foreach (DataRow row in dtCrf.Rows)
        {
            long crfId = Convert.ToInt64(row["CRF_ID"]);

            // Load quotes for this CRF
            var dtQuotes = new DataTable();
            var sqlQuotes = @"SELECT q.crfQuote_ID, q.crfQuote_modifiedOn, q.crfQuote_image1,
                ws.webstore_businessName, ws.webstore_address + ', ' + ws.webstore_city + ', ' +
                CASE WHEN ws.webstore_State != '' THEN ws.webstore_State + ', ' ELSE '' END + ws.webstore_postcode as businessAddress,
                ws.webstore_logo, q.crfQuote_Remarks, q.crfQuote_Deliverycharges, q.crfQuote_QuotePrice,
                q.crfQuote_isdelivery, q.crfQuote_deliverymode,
                sz.SizeTitle, q.crfQuote_bakeryID, q.crfQuote_isdeclined,
                q.crfQuote_isbakeryConfirmed, q.crfQuote_isbakerydeclined,
                q.crfQuote_iscounteroffer, q.crfQuote_counterofferrefquoteid,
                ISNULL(d.crfQuoteDecline_Remarks,'') as declineremarks,
                ISNULL(d.crfQuoteDecline_reason,'') as declineReason,
                ws.webstore_ID,
                @orderid as order_id
                FROM tbl_crfQuote q
                INNER JOIN tbl_webstore ws ON q.crfQuote_bakeryID = ws.webstore_ID
                INNER JOIN tbl_CakeSize sz ON q.crfQuote_SizeID = sz.SizeID
                LEFT JOIN tbl_crfQuoteDecline d ON q.crfQuote_ID = d.crfQuoteDecline_quoteID AND q.crfQuote_isdeclined = 1 AND d.crfQuoteDecline_mode = 1
                WHERE q.crfQuote_CRFID = @cid AND q.crfQuote_isdelete = 0 AND ws.webstore_ID = @myWid
                ORDER BY q.crfQuote_modifiedOn DESC";
            await using (var cmd = new SqlCommand(sqlQuotes, conn))
            {
                cmd.Parameters.AddWithValue("@cid", crfId);
                cmd.Parameters.AddWithValue("@myWid", webshopId);
                cmd.Parameters.AddWithValue("@orderid", Convert.ToInt32(row["orderid"]));
                dtQuotes.Load(await cmd.ExecuteReaderAsync());
            }
            crfQuotes[crfId] = dtQuotes;

            // Build images list
            var images = new List<ImageItem>();
            for (int i = 1; i <= 4; i++)
            {
                string imgCol = $"CRF_image{i}";
                if (row.Table.Columns.Contains(imgCol) && row[imgCol] != DBNull.Value && row[imgCol].ToString() != "")
                {
                    images.Add(new ImageItem
                    {
                        ImgNo = crfId.ToString(),
                        SmallImg = cdnBase + "upload/Product_images/resized_500_500/" + row[imgCol],
                        LargeImg = cdnBase + "upload/Product_images/resized_800_800/" + row[imgCol]
                    });
                }
            }
            if (images.Count == 0)
            {
                images.Add(new ImageItem
                {
                    ImgNo = crfId.ToString(),
                    SmallImg = cdnBase + "img/commingsoon_cake.jpg",
                    LargeImg = cdnBase + "img/commingsoon_cake.jpg"
                });
            }
            crfImages[crfId] = images;

            // Load attributes
            crfAttrs[crfId] = await GetCrfAttributesAsync(conn, crfId);
        }

        ViewBag.CrfList = dtCrf;
        ViewBag.Sizes = sizes;
        ViewBag.CrfQuotes = crfQuotes;
        ViewBag.CrfImages = crfImages;
        ViewBag.CrfAttrs = crfAttrs;
        ViewBag.WebshopId = webshopIdStr;
        ViewBag.SortId = sortid ?? "1";
        ViewBag.NoRecords = dtCrf.Rows.Count == 0;

        return View("~/Views/CrfListBakery/Index.cshtml");
    }

    [HttpPost("business-quotation/submit-quote")]
    public async Task<IActionResult> SubmitQuote(
        [FromForm] long crfId,
        [FromForm] long sizeId,
        [FromForm] decimal quotePrice,
        [FromForm] string? remarks,
        [FromForm] bool isDelivery,
        [FromForm] decimal deliveryCharges,
        [FromForm] string validDate,
        [FromForm] string validHour,
        [FromForm] string validMin,
        [FromForm] long quoteId,
        [FromForm] long counterQuoteId)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        if (webshopId == 0) return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        // Parse valid-til date
        DateTime validTill = DateTime.Now.AddDays(7);
        if (!string.IsNullOrEmpty(validDate))
        {
            var parts = validDate.Split('/');
            if (parts.Length == 3)
            {
                int.TryParse(validHour, out var hr);
                int.TryParse(validMin, out var mn);
                validTill = new DateTime(int.Parse(parts[2]), int.Parse(parts[1]), int.Parse(parts[0]), hr, mn, 0);
            }
        }

        if (quoteId > 0)
        {
            // Update existing quote
            var sqlUpd = @"UPDATE tbl_crfQuote SET crfQuote_QuotePrice=@price, crfQuote_Remarks=@remarks,
                crfQuote_isdelivery=@isDel, crfQuote_Deliverycharges=@delChg, crfQuote_deliverymode=@delMode,
                crfQuote_SizeID=@sizeId, crfQuote_modifiedOn=@now, crfQuote_validtill=@validTill,
                crfQuote_isbakeryConfirmed=1, crfQuote_isbakerydeclined=0
                WHERE crfQuote_ID=@qid AND crfQuote_CRFID=@cid";
            await using var cmd = new SqlCommand(sqlUpd, conn);
            cmd.Parameters.AddWithValue("@price", quotePrice);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmd.Parameters.AddWithValue("@isDel", isDelivery);
            cmd.Parameters.AddWithValue("@delChg", isDelivery ? deliveryCharges : 0);
            cmd.Parameters.AddWithValue("@delMode", isDelivery ? 2 : 1);
            cmd.Parameters.AddWithValue("@sizeId", sizeId);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@validTill", validTill);
            cmd.Parameters.AddWithValue("@qid", quoteId);
            cmd.Parameters.AddWithValue("@cid", crfId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Get CRF data for new quote
            string crfImage = "";
            DateTime? deliveryDate = null;
            int shapeId = 0;
            var sqlCrf = "SELECT CRF_image1, CRF_datetime, CRF_ShapeID FROM tbl_CRF WHERE CRF_ID=@cid";
            await using (var cmdCrf = new SqlCommand(sqlCrf, conn))
            {
                cmdCrf.Parameters.AddWithValue("@cid", crfId);
                await using var rdr = await cmdCrf.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    crfImage = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                    deliveryDate = rdr.IsDBNull(1) ? null : rdr.GetDateTime(1);
                    shapeId = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                }
            }

            var sqlIns = @"INSERT INTO tbl_crfQuote
                (crfQuote_bakeryID, crfQuote_CSMargin, crfQuote_CRFID, crfQuote_isdelete, crfQuote_isdeclined,
                 crfQuote_QuotePrice, crfQuote_Remarks, crfQuote_isdelivery, crfQuote_Deliverycharges,
                 crfQuote_deliverymode, crfQuote_SizeID, crfQuote_modifiedOn, crfQuote_validtill,
                 crfQuote_CRMID, crfQuote_isread, crfQuote_isbakeryConfirmed, crfQuote_isbakerydeclined,
                 crfQuote_counterofferrefquoteid, crfQuote_iscounteroffer, crfQuote_prdID,
                 crfQuote_image1, crfQuote_isnewImage, crfQuote_deliverydate, crfQuote_ShapeID)
                VALUES (@bakeryId, 0, @cid, 0, 0, @price, @remarks, @isDel, @delChg, @delMode,
                 @sizeId, @now, @validTill, 0, 0, 1, 0, @counterRef, @isCounter, 0,
                 @img, 0, @delDate, @shapeId)";
            await using var cmdIns = new SqlCommand(sqlIns, conn);
            cmdIns.Parameters.AddWithValue("@bakeryId", webshopId);
            cmdIns.Parameters.AddWithValue("@cid", crfId);
            cmdIns.Parameters.AddWithValue("@price", quotePrice);
            cmdIns.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmdIns.Parameters.AddWithValue("@isDel", isDelivery);
            cmdIns.Parameters.AddWithValue("@delChg", isDelivery ? deliveryCharges : 0);
            cmdIns.Parameters.AddWithValue("@delMode", isDelivery ? 2 : 1);
            cmdIns.Parameters.AddWithValue("@sizeId", sizeId);
            cmdIns.Parameters.AddWithValue("@now", DateTime.Now);
            cmdIns.Parameters.AddWithValue("@validTill", validTill);
            cmdIns.Parameters.AddWithValue("@counterRef", counterQuoteId);
            cmdIns.Parameters.AddWithValue("@isCounter", counterQuoteId > 0);
            cmdIns.Parameters.AddWithValue("@img", crfImage);
            cmdIns.Parameters.AddWithValue("@delDate", (object?)deliveryDate ?? DBNull.Value);
            cmdIns.Parameters.AddWithValue("@shapeId", shapeId);
            await cmdIns.ExecuteNonQueryAsync();

            // If counter-offer, mark old quote as deleted
            if (counterQuoteId > 0)
            {
                var sqlDelOld = "UPDATE tbl_crfQuote SET crfQuote_isdelete=1 WHERE crfQuote_ID=@qid AND crfQuote_CRFID=@cid AND crfQuote_bakeryID=@bid AND crfQuote_isdelete=0";
                await using var cmdDelOld = new SqlCommand(sqlDelOld, conn);
                cmdDelOld.Parameters.AddWithValue("@qid", counterQuoteId);
                cmdDelOld.Parameters.AddWithValue("@cid", crfId);
                cmdDelOld.Parameters.AddWithValue("@bid", webshopId);
                await cmdDelOld.ExecuteNonQueryAsync();
            }
        }

        TempData["Message"] = "Thanks for submitting your Quote. Once this quote is approved by customer, you will be notified.";
        return Redirect("/business-quotation");
    }

    [HttpPost("business-quotation/decline")]
    public async Task<IActionResult> DeclineCrf(
        [FromForm] long crfId,
        [FromForm] string declineReason,
        [FromForm] string? declineRemarks)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        if (webshopId == 0) return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        // Mark lnkCRFtoBakery as deleted
        var sql1 = "UPDATE tbl_lnkCRFtoBakery SET lnkCRFtoBakery_isdeleted=1 WHERE lnkCRFtoBakery_CRFID=@cid AND lnkCRFtoBakery_bakeryID=@bid";
        await using (var cmd = new SqlCommand(sql1, conn))
        {
            cmd.Parameters.AddWithValue("@cid", crfId);
            cmd.Parameters.AddWithValue("@bid", webshopId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert decline record
        var sql2 = @"INSERT INTO tbl_crfQuoteDecline
            (crfQuoteDecline_CrfID, crfQuoteDecline_modifiedOn, crfQuoteDecline_quoteID,
             crfQuoteDecline_reason, crfQuoteDecline_Remarks, crfQuoteDecline_custID, crfQuoteDecline_mode)
            VALUES (@cid, @now, 0, @reason, @remarks, @bid, 2)";
        await using (var cmd = new SqlCommand(sql2, conn))
        {
            cmd.Parameters.AddWithValue("@cid", crfId);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@reason", declineReason);
            cmd.Parameters.AddWithValue("@remarks", declineRemarks ?? "");
            cmd.Parameters.AddWithValue("@bid", webshopId);
            await cmd.ExecuteNonQueryAsync();
        }

        TempData["Message"] = "Quote have been declined successfully.";
        return Redirect("/business-quotation");
    }

    [HttpPost("business-quotation/remove-quote")]
    public async Task<IActionResult> RemoveQuote([FromForm] long crfId)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        if (webshopId == 0) return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        var sql = "UPDATE tbl_crfQuote SET crfQuote_isdelete=1 WHERE crfQuote_CRFID=@cid AND crfQuote_bakeryID=@bid AND crfQuote_isdelete=0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", crfId);
        cmd.Parameters.AddWithValue("@bid", webshopId);
        await cmd.ExecuteNonQueryAsync();

        TempData["Message"] = "Your Quote has been removed.";
        return Redirect("/business-quotation");
    }

    [HttpPost("business-quotation/accept-quote")]
    public async Task<IActionResult> AcceptQuote([FromForm] long quoteId, [FromForm] long crfId)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        if (webshopId == 0) return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        var sql = "UPDATE tbl_crfQuote SET crfQuote_isbakeryConfirmed=1, crfQuote_isopenforcustomer=1 WHERE crfQuote_ID=@qid AND crfQuote_CRFID=@cid AND crfQuote_bakeryID=@bid AND crfQuote_isdelete=0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qid", quoteId);
        cmd.Parameters.AddWithValue("@cid", crfId);
        cmd.Parameters.AddWithValue("@bid", webshopId);
        await cmd.ExecuteNonQueryAsync();

        return Redirect("/business-quotation");
    }

    [HttpPost("business-quotation/decline-quote")]
    public async Task<IActionResult> DeclineQuote([FromForm] long quoteId, [FromForm] long crfId)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long webshopId = long.TryParse(webshopIdStr, out var wid) ? wid : 0;
        if (webshopId == 0) return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        var sql = "UPDATE tbl_crfQuote SET crfQuote_isbakerydeclined=1 WHERE crfQuote_ID=@qid AND crfQuote_CRFID=@cid AND crfQuote_bakeryID=@bid AND crfQuote_isdelete=0";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qid", quoteId);
        cmd.Parameters.AddWithValue("@cid", crfId);
        cmd.Parameters.AddWithValue("@bid", webshopId);
        await cmd.ExecuteNonQueryAsync();

        return Redirect("/business-quotation");
    }

    private async Task<string> GetCrfAttributesAsync(SqlConnection conn, long crfId)
    {
        string result = "";
        var sql = @"SELECT ca.CRFAtt_ParentattID, ca.CRFAtt_ViewType, ca.CRFAtt_datatext,
                    ISNULL(f.FlavourTitle,'') FlavourTitle, ISNULL(pf.FlavourTitle,'') ParentTitle
                    FROM tbl_CRFAtt ca
                    LEFT JOIN tbl_CRFflavour f ON ca.CRFAtt_AttID=f.FlavourID
                    LEFT JOIN tbl_CRFflavour pf ON ca.CRFAtt_ParentattID=pf.FlavourID
                    WHERE ca.CRFAtt_CRFID=@cid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", crfId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        string localData = "";
        while (await rdr.ReadAsync())
        {
            string parentTitle = rdr.GetString(4);
            string value = rdr.GetString(2) == "3" ? rdr.GetString(3) : rdr.GetString(3);
            if (rdr.GetString(2) == "3")
                value = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
            localData += $"<ul><li class='parentli'>{parentTitle}</li><li class='data_li'>{(rdr.GetString(2) == "3" ? rdr.GetString(3) : rdr.GetString(3))}</li></ul>";
        }
        if (localData != "")
            result = $"<div class='Flavour_outer'>{localData}</div>";
        return result;
    }

    public class SizeItem { public long SizeId { get; set; } public string SizeTitle { get; set; } = ""; }
    public class ImageItem { public string ImgNo { get; set; } = ""; public string SmallImg { get; set; } = ""; public string LargeImg { get; set; } = ""; }
}
