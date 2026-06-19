using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Handles PayPal IPN (Instant Payment Notification) callback.
/// Verifies payment with PayPal API, updates order payout status, and logs the transaction.
/// No view — POST handler only, redirects to success/failure message page.
/// Migrated from legacy paypalipn.aspx / paypalipn.aspx.cs.
/// Route: /paypalipn
/// </summary>
public class PaypalIpnController : Controller
{
    private readonly IConfiguration _config;

    public PaypalIpnController(IConfiguration config)
    {
        _config = config;
    }

    private string ConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

    [HttpGet("paypalipn")]
    [HttpGet("paypalipn.aspx")]
    public async Task<IActionResult> Index(
        [FromQuery] string? paymentId = null)
    {
        if (string.IsNullOrEmpty(paymentId))
            return Redirect("/mailmessage/CheckFailed");

        try
        {
            string mode = _config["PayPal:Mode"] ?? "sandbox";
            string clientId = _config["PayPal:ClientId"] ?? "";
            string clientSecret = _config["PayPal:ClientSecret"] ?? "";

            // Verify payment with PayPal REST API
            string baseUrl = mode == "live"
                ? "https://api.paypal.com"
                : "https://api.sandbox.paypal.com";

            // Get access token
            string accessToken = "";
            using (var http = new System.Net.Http.HttpClient())
            {
                var authBytes = System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var tokenContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var tokenResp = await http.PostAsync($"{baseUrl}/v1/oauth2/token", tokenContent);
                if (tokenResp.IsSuccessStatusCode)
                {
                    var tokenJson = await tokenResp.Content.ReadAsStringAsync();
                    // Simple JSON parsing for access_token
                    int idx = tokenJson.IndexOf("\"access_token\"");
                    if (idx >= 0)
                    {
                        int start = tokenJson.IndexOf("\"", idx + 15) + 1;
                        int end = tokenJson.IndexOf("\"", start);
                        accessToken = tokenJson.Substring(start, end - start);
                    }
                }
            }

            if (string.IsNullOrEmpty(accessToken))
                return Redirect("/mailmessage/CheckFailed");

            // Get payment details
            string paymentState = "";
            string customField = "";

            using (var http = new System.Net.Http.HttpClient())
            {
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var paymentResp = await http.GetAsync($"{baseUrl}/v1/payments/payment/{paymentId}");

                if (paymentResp.IsSuccessStatusCode)
                {
                    var paymentJson = await paymentResp.Content.ReadAsStringAsync();

                    // Parse state
                    int stateIdx = paymentJson.IndexOf("\"state\"");
                    if (stateIdx >= 0)
                    {
                        int start = paymentJson.IndexOf("\"", stateIdx + 8) + 1;
                        int end = paymentJson.IndexOf("\"", start);
                        paymentState = paymentJson.Substring(start, end - start);
                    }

                    // Parse custom field from transactions
                    int customIdx = paymentJson.IndexOf("\"custom\"");
                    if (customIdx >= 0)
                    {
                        int start = paymentJson.IndexOf("\"", customIdx + 9) + 1;
                        int end = paymentJson.IndexOf("\"", start);
                        customField = paymentJson.Substring(start, end - start);
                    }
                }
            }

            if (paymentState.ToLower() == "approved")
            {
                return await UpdateOrderStatus(customField, paymentId, false);
            }
            else
            {
                return Redirect("/mailmessage/CheckFailed");
            }
        }
        catch
        {
            return Redirect("/mailmessage/CheckFailed");
        }
    }

    private async Task<IActionResult> UpdateOrderStatus(string orderGuid, string txnId, bool hasError)
    {
        if (hasError || string.IsNullOrEmpty(orderGuid))
            return Redirect("/mailmessage/CheckFailed");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        // Get order details
        var sql = @"SELECT order_ID, isnull(order_couponID, 0) as order_couponID, order_isPurchased,
            order_shippingCost, order_customerEmail, ws.webstore_OrderEmail as seller_email,
            b.webstore_OrderEmail as branch_email, order_bakeryID, order_branchID,
            order_totalPrice, order_paypalfee, order_CSmargin
            FROM tbl_order o
            INNER JOIN tbl_webstore ws ON order_bakeryID=ws.webstore_ID
            LEFT OUTER JOIN tbl_webstore b ON o.order_branchID = b.webstore_ID
            WHERE order_guid = @guid";

        var dt = new DataTable();
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@guid", orderGuid);
            dt.Load(await cmd.ExecuteReaderAsync());
        }

        if (dt.Rows.Count > 0)
        {
            string orderId = Convert.ToString(dt.Rows[0]["order_ID"]);
            int orderBranchId = Convert.ToInt32(dt.Rows[0]["order_branchID"]);
            decimal paypalFee = Convert.ToDecimal(dt.Rows[0]["order_paypalfee"]);

            // Update order payout status
            var sqlUpd = "UPDATE tbl_order SET order_ispayout=1 WHERE order_ID = @oid";
            await using (var cmd = new SqlCommand(sqlUpd, conn))
            {
                cmd.Parameters.AddWithValue("@oid", orderId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Update account overview
            var sqlAcct = "SELECT orderAcntOverview_ID, orderAcntOverview_widthdrawPrc, orderAcntOverview_PaidPrc FROM tbl_orderAcntOverview WHERE orderAcntOverview_bakeryId = @bid";
            await using (var cmdAcct = new SqlCommand(sqlAcct, conn))
            {
                cmdAcct.Parameters.AddWithValue("@bid", orderBranchId);
                await using var rdr = await cmdAcct.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    long acctId = rdr.GetInt64(0);
                    decimal withdrawPrc = rdr.GetDecimal(1) - paypalFee;
                    decimal paidPrc = rdr.GetDecimal(2) + paypalFee;
                    await rdr.CloseAsync();

                    var sqlAcctUpd = "UPDATE tbl_orderAcntOverview SET orderAcntOverview_widthdrawPrc=@wp, orderAcntOverview_PaidPrc=@pp WHERE orderAcntOverview_ID=@id";
                    await using var cmdAcctUpd = new SqlCommand(sqlAcctUpd, conn);
                    cmdAcctUpd.Parameters.AddWithValue("@wp", withdrawPrc);
                    cmdAcctUpd.Parameters.AddWithValue("@pp", paidPrc);
                    cmdAcctUpd.Parameters.AddWithValue("@id", acctId);
                    await cmdAcctUpd.ExecuteNonQueryAsync();

                    // Insert account log
                    var sqlLog = @"INSERT INTO tbl_BakeryAccountLog
                        (BakeryAccountLog_bakeryID, BakeryAccountLog_paymentmode, BakeryAccountLog_paymentInOuttype,
                         BakeryAccountLog_Guid, BakeryAccountLog_AccountType, BakeryAccountLog_currencyID,
                         BakeryAccountLog_balance, BakeryAccountLog_availablebalance, BakeryAccountLog_isPaid,
                         BakeryAccountLog_OrderGuid, BakeryAccountLog_TxID, BakeryAccountLog_createdOn)
                        VALUES (@bid, 103, 1, @guid, 4, 1, @fee, @pp, 1, @txn, @oid, @now)";
                    await using var cmdLog = new SqlCommand(sqlLog, conn);
                    cmdLog.Parameters.AddWithValue("@bid", orderBranchId);
                    cmdLog.Parameters.AddWithValue("@guid", Guid.NewGuid().ToString());
                    cmdLog.Parameters.AddWithValue("@fee", paypalFee);
                    cmdLog.Parameters.AddWithValue("@pp", paidPrc);
                    cmdLog.Parameters.AddWithValue("@txn", txnId);
                    cmdLog.Parameters.AddWithValue("@oid", orderId);
                    cmdLog.Parameters.AddWithValue("@now", DateTime.Now);
                    await cmdLog.ExecuteNonQueryAsync();
                }
            }
        }

        return Redirect($"/mailmessage/checksuccess/{orderGuid}");
    }
}
