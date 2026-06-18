using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Refund / Credit Note print page.
/// Route: /printcreditnote/{creditNoteNo}
/// Migrated from: printcreditnote.aspx / printcreditnote.aspx.cs
///
/// Displays a printable credit note with:
/// - Credit note header (number, date, generated-by, original order no.)
/// - Customer billing details and company "From" info
/// - Line items table showing returned products (SKU, name, unit price, qty returned, sub-total)
/// - Total credit amount
/// - Billing address and contact details
///
/// SQL:
///   1. SELECT * FROM tbl_order O, tbl_shippingDetail S, tbl_billingDetail B
///      WHERE O.order_ID = S.shipping_orderID AND O.order_ID = B.billing_orderID AND O.CreditNoteNo = @id
///   2. SELECT * FROM tbl_orderDetail O, tbl_products P
///      WHERE O.orderDetail_productID = P.product_ID AND qty_returned > 0 AND O.orderDetail_orderID = @orderId
/// </summary>
[Route("printcreditnote")]
public class PrintCreditNoteController : Controller
{
    private readonly IConfiguration _config;

    public PrintCreditNoteController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Renders the printable credit note page.
    /// Legacy route: /printcreditnote/{creditNoteNo}
    /// </summary>
    [HttpGet("{creditNoteNo}")]
    public async Task<IActionResult> Index(string creditNoteNo)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin?returl=" + Request.Path);

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.CdnBase = cdnBase;

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

        // ── Query 1: Order + Shipping + Billing by CreditNoteNo ──
        DataTable dtOrder;
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            var sql = @"SELECT *
                        FROM tbl_order O, tbl_shippingDetail S, tbl_billingDetail B
                        WHERE O.order_ID = S.shipping_orderID
                          AND O.order_ID = B.billing_orderID
                          AND O.CreditNoteNo = @CreditNoteNo";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CreditNoteNo", creditNoteNo);
            var adapter = new SqlDataAdapter(cmd);
            dtOrder = new DataTable();
            adapter.Fill(dtOrder);
        }

        if (dtOrder.Rows.Count == 0)
            return NotFound("Credit note not found.");

        var row = dtOrder.Rows[0];
        ViewBag.InvoiceNo = creditNoteNo;
        ViewBag.InvoiceDate = Convert.ToDateTime(row["Credit_date"].ToString()).ToString("dd-MMM-yyyy");
        ViewBag.PaymentMode = Convert.ToString(row["Credit_generatedby"]);
        ViewBag.OrderNo = Convert.ToString(row["order_ID"]);
        ViewBag.TotalCreditAmount = Convert.ToString(row["Credit_Amount"]);

        // Company info from config (legacy used AppSettings)
        ViewBag.CompanyName = _config["Company_Name"] ?? "";
        ViewBag.CompanyAddress = (_config["Company_Address"] ?? "").Replace("@#", "<").Replace("#@", "/>");
        ViewBag.CompanyNo = _config["Company_No"] ?? "";
        ViewBag.VatNumber = _config["vat_number"] ?? "";

        // Order header info for customer/billing panels
        var orderInfoList = new List<Dictionary<string, object>>();
        foreach (DataRow r in dtOrder.Rows)
        {
            var dict = new Dictionary<string, object>();
            foreach (DataColumn col in dtOrder.Columns)
                dict[col.ColumnName] = r[col];
            orderInfoList.Add(dict);
        }
        ViewBag.OrderInfo = orderInfoList;

        // ── Query 2: Returned order detail items ──
        var orderId = Convert.ToString(row["order_ID"]);
        DataTable dtItems;
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            var sql = @"SELECT *
                        FROM tbl_orderDetail O, tbl_products P
                        WHERE O.orderDetail_productID = P.product_ID
                          AND O.qty_returned > 0
                          AND O.orderDetail_orderID = @OrderID";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderID", orderId);
            var adapter = new SqlDataAdapter(cmd);
            dtItems = new DataTable();
            adapter.Fill(dtItems);
        }

        // Build items list with computed unit price and variant info
        var items = new List<Dictionary<string, object>>();
        foreach (DataRow r in dtItems.Rows)
        {
            var item = new Dictionary<string, object>();
            foreach (DataColumn col in dtItems.Columns)
                item[col.ColumnName] = r[col];

            // Compute unit price = orderDetail_price / orderDetail_Quantity
            var totalPrice = Convert.ToDecimal(r["orderDetail_price"]);
            var qty = Convert.ToDecimal(r["orderDetail_Quantity"]);
            item["UnitPrice"] = Math.Round(totalPrice / qty, 2);
            item["SubTotal"] = Math.Round(totalPrice, 2);

            // Build variant text
            var variant = "";
            var colorName = Convert.ToString(r["Colorname"]);
            var sizeName = Convert.ToString(r["Sizename"]);
            if (!string.IsNullOrEmpty(colorName))
                variant += "<br /><b>Color:</b> " + colorName;
            if (!string.IsNullOrEmpty(sizeName))
                variant += "<br /><b>Size:</b> " + sizeName;
            item["VariantHtml"] = variant;

            items.Add(item);
        }
        ViewBag.Items = items;

        return View("~/Views/PrintCreditNote/Index.cshtml");
    }
}
