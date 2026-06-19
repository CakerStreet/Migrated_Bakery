using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the My Account Balance / Withdraw Amount page.
/// Displays available balance, withdrawal form, and transaction log.
/// Migrated from legacy withdrawamount.aspx / withdrawamount.aspx.cs.
/// </summary>
[Route("withdrawamount")]
public class WithdrawAmountController : Controller
{
    private readonly IConfiguration _config;

    public WithdrawAmountController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    [Route("~/withdrawamount.aspx")]
    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";
        if (userId == "0")
            return Redirect("/?returl=" + Request.Path);

        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (!long.TryParse(webshopId, out var webstoreId))
            return Redirect("/?returl=" + Request.Path);

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        var model = new WithdrawAmountViewModel();

        // Get available balance
        using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT * FROM tbl_webstore LEFT JOIN tbl_orderAcntOverview ON orderAcntOverview_bakeryID=webstore_ID WHERE webstore_ID=@wid", conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var paidPrc = reader["orderAcntOverview_PaidPrc"]?.ToString();
                if (!string.IsNullOrEmpty(paidPrc))
                {
                    model.AvailableBalance = Math.Round(decimal.Parse(paidPrc), 2);
                    model.WithdrawEnabled = model.AvailableBalance >= 0;
                    model.DefaultWithdrawAmount = paidPrc;
                }
            }
        }

        // Get transaction log
        using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT * FROM tbl_BakeryAccountLog b 
                INNER JOIN tbl_webstore w ON b.BakeryAccountLog_bakeryID=w.webstore_ID 
                LEFT JOIN tbl_order o ON Cast(order_ID as nvarchar(50)) = b.BakeryAccountLog_TxID
                WHERE BakeryAccountLog_paymentmode IN (1,2,4,17,18,101,102,103,104,202,203,1004) 
                AND BakeryAccountLog_isPaid=1 
                AND BakeryAccountLog_bakeryID=@wid 
                ORDER BY BakeryAccountLog_createdOn DESC", conn);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                model.Transactions.Add(new BalanceTransaction
                {
                    CreatedOn = reader["BakeryAccountLog_createdOn"] != DBNull.Value
                        ? Convert.ToDateTime(reader["BakeryAccountLog_createdOn"]) : DateTime.MinValue,
                    PaymentInOutType = reader["BakeryAccountLog_paymentInOuttype"]?.ToString() ?? "",
                    PaymentMode = reader["BakeryAccountLog_paymentmode"]?.ToString() ?? "",
                    Balance = reader["BakeryAccountLog_balance"] != DBNull.Value
                        ? Convert.ToDouble(reader["BakeryAccountLog_balance"]) : 0,
                    AvailableBalance = reader["BakeryAccountLog_availablebalance"] != DBNull.Value
                        ? Convert.ToDouble(reader["BakeryAccountLog_availablebalance"]) : 0,
                    WebstoreCode = reader["webstore_code"]?.ToString() ?? "",
                    TxID = reader["BakeryAccountLog_TxID"]?.ToString() ?? "",
                    OrderForwardedId = reader["order_forwardedorderid"]?.ToString() ?? "0",
                    OrderIsRepeat = reader["order_isrepeat"]?.ToString() ?? ""
                });
            }
        }

        ViewBag.SiteUrl = _config["SiteUrl"] ?? "/";
        return View("~/Views/WithdrawAmount/Index.cshtml", model);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromForm] string txtWidthDrawPrice)
    {
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";
        if (userId == "0")
            return Redirect("/?returl=" + Request.Path);

        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (!long.TryParse(webshopId, out var webstoreId))
            return Redirect("/?returl=" + Request.Path);

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT * FROM tbl_webstore LEFT JOIN tbl_orderAcntOverview ON orderAcntOverview_bakeryID=webstore_ID WHERE webstore_ID=@wid", conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var paidPrc = reader["orderAcntOverview_PaidPrc"]?.ToString();
            if (!string.IsNullOrEmpty(paidPrc))
            {
                var availBalance = Math.Round(decimal.Parse(paidPrc), 2);
                var requestedAmt = Math.Round(decimal.Parse(txtWidthDrawPrice), 2);
                if (availBalance >= requestedAmt && requestedAmt >= 0)
                {
                    reader.Close();
                    // Call the WithdrawAmount stored procedure
                    using var cmdWithdraw = new SqlCommand("WithdrawAmount", conn);
                    cmdWithdraw.CommandType = CommandType.StoredProcedure;
                    cmdWithdraw.Parameters.AddWithValue("@bakeryID", (int)webstoreId);
                    cmdWithdraw.Parameters.AddWithValue("@amount", requestedAmt);
                    cmdWithdraw.Parameters.AddWithValue("@txID", Guid.NewGuid().ToString());
                    await cmdWithdraw.ExecuteNonQueryAsync();

                    TempData["Message"] = "Your widthdrawal progress is successfully done.";
                }
            }
        }

        return RedirectToAction("Index");
    }

    public static string GetBalanceType(string strType, string strPaymentMode, string strWcCode,
        string strTxID, string strForwardOrderID, string orderIsRepeat, string siteUrl)
    {
        string GetOrderIdDisplay(string txId, string fwdId, string isRepeat)
        {
            if (isRepeat == "1" || isRepeat == "True")
                return fwdId + "/ReOrd";
            if (long.TryParse(fwdId, out var fid) && fid > 0)
                return fwdId + "/FWD";
            return txId;
        }

        if (strType == "1")
        {
            return "Paid In - Order Number : <a class='ancbottomLink' href='" + siteUrl + "printorder/" +
                   strTxID + "?fccode=" + strWcCode + "' target='_blank'>" +
                   GetOrderIdDisplay(strTxID, strForwardOrderID, orderIsRepeat) + "</a>";
        }
        else if (strType == "2")
        {
            try
            {
                long orderID = long.Parse(strTxID);
                if (strPaymentMode == "102")
                {
                    return "Paid at Counter - Order Number : <a class='ancbottomLink' href='" + siteUrl +
                           "printorder/" + strTxID + "?fccode=" + strWcCode + "' target='_blank'>" +
                           GetOrderIdDisplay(strTxID, strForwardOrderID, orderIsRepeat) + "</a>";
                }
                else if (strPaymentMode == "203")
                {
                    return "Deducted from account balance - Order Number : <a class='ancbottomLink' href='" +
                           siteUrl + "printorder/" + orderID + "?fccode=" + strWcCode + "' target='_blank'>" +
                           GetOrderIdDisplay(strTxID, strForwardOrderID, orderIsRepeat) + "</a>";
                }

                var strOrderText = " - Order Number : <a class='ancbottomLink' href='" + siteUrl +
                                   "printorder/" + orderID + "?fccode=" + strWcCode + "' target='_blank'>" +
                                   GetOrderIdDisplay(strTxID, strForwardOrderID, orderIsRepeat) + "</a>";
                return "Withdrawal from account balance" + strOrderText;
            }
            catch
            {
                return "Withdrawal from account balance";
            }
        }
        else
        {
            return "---";
        }
    }
}

public class WithdrawAmountViewModel
{
    public decimal AvailableBalance { get; set; }
    public bool WithdrawEnabled { get; set; }
    public string DefaultWithdrawAmount { get; set; } = "0";
    public List<BalanceTransaction> Transactions { get; set; } = new();
}

public class BalanceTransaction
{
    public DateTime CreatedOn { get; set; }
    public string PaymentInOutType { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public double Balance { get; set; }
    public double AvailableBalance { get; set; }
    public string WebstoreCode { get; set; } = "";
    public string TxID { get; set; } = "";
    public string OrderForwardedId { get; set; } = "0";
    public string OrderIsRepeat { get; set; } = "";
}
