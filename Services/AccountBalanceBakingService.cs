using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class BakingAccountOverviewModel
{
    public string PendingAmount { get; set; } = "0";
    public string CancelledAmount { get; set; } = "0";
    public string ConfirmedAmount { get; set; } = "0";
    public string AvailableAmount { get; set; } = "0";
    public string TotalTrade { get; set; } = "0";
    public string FeeCharged { get; set; } = "0";
    public string PayoutAmount { get; set; } = "0";
}

public class BakingFranchiseOrderItem
{
    public long OrderId { get; set; }
    public bool IsRepeat { get; set; }
    public string OrderGuid { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime RefundOrderDate { get; set; }
    public string WebstoreCode { get; set; } = "";
    public string ForwardedOrderId { get; set; } = "";
    public int PaidType { get; set; }
    public double TotalPrice { get; set; }
    public double CSMargin { get; set; }
    public double CouponValue { get; set; }
    public double PayoutRefund { get; set; }
    public double CsRefund { get; set; }
    public double PaypalFee { get; set; }
    public bool IsPayout { get; set; }
    public int Status { get; set; }
    public double PayoutAmount { get; set; }
    public string CustWithdrawalId { get; set; } = "";
    public double CustWithdrawalAmount { get; set; }
    public string BillingFName { get; set; } = "";
    public string BillingLName { get; set; } = "";
    public string BillingAddress { get; set; } = "";
    public string BillingCity { get; set; } = "";
    public string BillingZip { get; set; } = "";
    public string BillingEmailId { get; set; } = "";
    public double RefundAmount { get; set; }
    public double Withdrew { get; set; }
    public double PayoutTotal { get; set; }
    public double ReverseAmount { get; set; }
    public string RefundRemarks { get; set; } = "";
    public int IsReverseRefundRow { get; set; }
    public int ReversalStatus { get; set; }
    public int CustWithdrawalMode { get; set; }
    public string OrderWorthBakingCost { get; set; } = "0";
}

public class BakingBaseCostItem
{
    public string OrderDetailOrderId { get; set; } = "";
    public string CakeBaseCost { get; set; } = "0";
    public string ProfitMargin { get; set; } = "0";
}

public class BakingMiscPaymentItem
{
    public long StripepaymentId { get; set; }
    public DateTime ModifiedOn { get; set; }
    public double Amount { get; set; }
    public string Remarks { get; set; } = "";
    public bool IsPaid { get; set; }
    public DateTime PayoutOn { get; set; }
    public string PaymentVia { get; set; } = "";
    public string TxId { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for My Account Balance For Baking module.
/// Migrated from myaccountbalanceforbaking.aspx.
/// Contains all SQL queries from the legacy code-behind.
/// </summary>
public class AccountBalanceBakingService
{
    private readonly string _connectionString;
    private readonly IConfiguration _config;

    public AccountBalanceBakingService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("aboraboraboraaboraaborab") ?? "";
        _config = config;
    }

    /// <summary>
    /// Check if baking is enabled for this webstore branch.
    /// Legacy: checkisbakingoff() method.
    /// </summary>
    public async Task<bool> GetIsBakingOffAsync(long webstoreId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = "SELECT WebstoreBranch_isBaking FROM tbl_WebstoreBranch WHERE WebstoreBranch_BranchID = @webstoreId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        var result = await cmd.ExecuteScalarAsync();
        if (result != null && result != DBNull.Value)
        {
            return Convert.ToBoolean(result);
        }
        return false;
    }

    /// <summary>
    /// Gets account overview summary.
    /// Legacy: objOrder.getbakery_Account_Overview(webstoreid)
    /// </summary>
    public async Task<BakingAccountOverviewModel> GetAccountOverviewAsync(long webstoreId)
    {
        var model = new BakingAccountOverviewModel();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // This calls the stored procedure / function that the legacy clsOrder.getbakery_Account_Overview used.
        // We replicate the pattern from AccountBalanceService but adapted for the baking variant.
        var sql = @"SELECT 
                        Pending_Amount = ISNULL(SUM(CASE WHEN order_status = 0 THEN order_payoutAmount ELSE 0 END), 0),
                        cancelled_Amount = ISNULL(SUM(CASE WHEN order_status = 11 THEN order_payoutAmount ELSE 0 END), 0),
                        confirmed_Amount = ISNULL(SUM(CASE WHEN order_status IN (1,2,3,5) THEN order_payoutAmount ELSE 0 END), 0),
                        avaialable_Amount = ISNULL(SUM(CASE WHEN order_status = 4 AND order_ispayout = 0 THEN order_payoutAmount - order_payoutRefund ELSE 0 END), 0),
                        Total_Trade = ISNULL(SUM(order_payoutAmount), 0),
                        fee_charged = ISNULL(SUM(order_CSmargin + order_paypalfee), 0),
                        Payout_Amount = ISNULL(SUM(CASE WHEN order_ispayout = 1 THEN order_payoutAmount ELSE 0 END), 0)
                    FROM tbl_order 
                    WHERE order_branchID = @webstoreId
                      AND order_followingOrderid = 0 AND order_saletype = 1 
                      AND order_isdeleted = 0 AND order_isPurchased = 1";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            model.PendingAmount = reader["Pending_Amount"]?.ToString() ?? "0";
            model.CancelledAmount = reader["cancelled_Amount"]?.ToString() ?? "0";
            model.ConfirmedAmount = reader["confirmed_Amount"]?.ToString() ?? "0";
            model.AvailableAmount = reader["avaialable_Amount"]?.ToString() ?? "0";
            model.TotalTrade = reader["Total_Trade"]?.ToString() ?? "0";
            model.FeeCharged = reader["fee_charged"]?.ToString() ?? "0";
            model.PayoutAmount = reader["Payout_Amount"]?.ToString() ?? "0";
        }

        return model;
    }

    /// <summary>
    /// Checks if the webstore is a franchise user.
    /// Legacy: db.franchiseUser.Where(w => w.franchiseUser_webstoreID == webstoreid).Any()
    /// </summary>
    public async Task<bool> IsFranchiseAsync(long webstoreId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = "SELECT COUNT(1) FROM tbl_franchiseUser WHERE franchiseUser_webstoreID = @webstoreId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// Gets franchise orders using the exact complex temp-table SQL from legacy code.
    /// Legacy: bindgrid() franchise query block.
    /// </summary>
    public async Task<List<BakingFranchiseOrderItem>> GetFranchiseOrdersAsync(long webstoreId)
    {
        var items = new List<BakingFranchiseOrderItem>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = string.Format(@"select o.order_ID, o.order_isrepeat, o.order_guid, o.order_date, ordercollection_Date = oc.ordercollection_Date,ro.RefundOrder_Date, w.webstore_code,
o.order_forwardedorderid, o.order_paidType, o.order_totalPrice,
o.order_CSmargin,order_couponvalue,o.order_payoutRefund, o.order_csRefund, o.order_paypalfee, o.order_ispayout, o.order_status,
 o.order_payoutAmount, custwithdrawal_ID = isnull(cw.custwithdrawal_ID, 0),
 custwithdrawal_amount = isnull(cw.custwithdrawal_amount, 0), b.billing_fName, b.billing_lName, b.billing_address, b.billing_city, b.billing_zip, b.billing_emailID,
 case when order_payoutRefund>0 then order_payoutRefund when RefundAmount>0 then RefundAmount else ro.CustomerRefundAmount end Refund_Amount
,case when cw.custwithdrawal_amount>0 then custwithdrawal_amount else 0 end Withdrew
--,o.order_payoutAmount,o.order_payoutRefund
,
case when order_payoutRefund>0 then o.order_payoutAmount-o.order_payoutRefund
when RefundAmount>0 then o.order_payoutAmount-ro.RefundAmount 
else  o.order_payoutAmount-ro.CustomerRefundAmount end Payout_Total
,case when cw.custwithdrawal_amount is not null then cw.custwithdrawal_amount-(case when order_payoutRefund>0 then o.order_payoutAmount-o.order_payoutRefund
when RefundAmount>0 then o.order_payoutAmount-ro.RefundAmount 
else  o.order_payoutAmount-ro.CustomerRefundAmount end ) else 0-(case when order_payoutRefund>0 then o.order_payoutAmount-o.order_payoutRefund
when RefundAmount>0 then o.order_payoutAmount-ro.RefundAmount 
else  o.order_payoutAmount-ro.CustomerRefundAmount end) end ReverseAmount,
isnull(n.crmRCNotes_Remarks,'') Refund_remarks, 1 as IsReverseRefundRow, isnull(rr.status, 0) as reversal_status,isnull(custwithdrawal_Mode,0) custwithdrawal_Mode
, ow.OrderWorth_BakingCost
into #t
from tbl_order o inner join tbl_webstore w on o.order_branchID=w.webstore_ID
inner join tbl_ordercollection oc on o.order_ID=oc.ordercollection_OrderID
inner join tbl_RefundOrderDetail ro on o.order_ID=ro.OrderID
left join tbl_custwithdrawal cw on o.order_ID=cw.custwithdrawal_orderID and cw.custwithdrawal_Mode = 0
left join tbl_RefundReverse rr on rr.order_id = o.order_ID
inner join tbl_billingDetail b on o.order_ID = b.billing_orderID
inner join tbl_OrderWorth ow on o.order_ID = ow.OrderWorth_OrderID
left join (
select * from
(
   SELECT *,
         ROW_NUMBER() OVER (PARTITION BY crmNotes_RefundOrderDetailID ORDER BY crmrcNotes_modifiedon) AS rn
   FROM tbl_crmRCNotes
) tbl
WHERE rn = 1) n on n.crmNotes_RefundOrderDetailID= o.order_ID
where order_branchID={0} 
and 
(
order_payoutRefund>0
or 

RefundAmount>0
or 
ro.CustomerRefundAmount>0 
or ro.OrderID>0 
)
and order_status<=4

/* end temp table query */

select * from
(
select o.order_ID, o.order_isrepeat, o.order_guid, o.order_date, c.ordercollection_Date,c.ordercollection_Date RefundOrder_Date, w.webstore_code, o.order_forwardedorderid, o.order_paidType, o.order_totalPrice,
o.order_CSmargin,order_couponvalue,o.order_payoutRefund, o.order_csRefund, o.order_paypalfee, o.order_ispayout, o.order_status, o.order_payoutAmount, cw.custwithdrawal_ID,
cw.custwithdrawal_amount, b.billing_fName, b.billing_lName, b.billing_address, b.billing_city, b.billing_zip, b.billing_emailID
,0 as Refund_Amount, 0 as Payout_Total, 0 as Withdrew, 0 as ReverseAmount, '' as Refund_remarks, 0 as IsReverseRefundRow, reversal_status = 2,0 custwithdrawal_Mode
, ow.OrderWorth_BakingCost
from tbl_order o inner join tbl_ordercollection c on o.order_ID=c.ordercollection_OrderID inner join tbl_webstore w on o.order_branchID=w.webstore_ID 
left join tbl_custwithdrawal cw on cw.custwithdrawal_orderID= o.order_ID and cw.custwithdrawal_bakeryID=w.webstore_ID and cw.custwithdrawal_Mode = 0
inner join tbl_billingDetail b on o.order_ID = b.billing_orderID
inner join tbl_OrderWorth ow on o.order_ID = ow.OrderWorth_OrderID
where (
(order_branchID={0})
or
 order_ID in (select ordertask_orderID from tbl_ordertask OTin inner join tbl_order Oin on Oin.order_ID=OTin.ordertask_orderID 
 inner join tbl_bakeryuser bu1 on OTin.ordertask_currUserID=bu1.customer_ID where  bu1.customer_webshopID={0}))
            and order_followingOrderid=0  and order_saletype=1  and  order_isdeleted=0 and order_isPurchased=1

union all
select order_ID, order_isrepeat, order_guid, order_date, ordercollection_Date,RefundOrder_Date, webstore_code, order_forwardedorderid, order_paidType, order_totalPrice,
order_CSmargin,order_couponvalue, order_payoutRefund, order_csRefund, order_paypalfee, order_ispayout, order_status, order_payoutAmount, custwithdrawal_ID,
custwithdrawal_amount, billing_fName, billing_lName, billing_address, billing_city, billing_zip, billing_emailID,
Refund_Amount, Payout_Total,Withdrew, ReverseAmount, Refund_remarks, IsReverseRefundRow,reversal_status,custwithdrawal_Mode, OrderWorth_BakingCost from #t where ReverseAmount>0
) as t order by reversal_status, order_date desc

drop table #t", webstoreId);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 60;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BakingFranchiseOrderItem
            {
                OrderId = Convert.ToInt64(reader["order_ID"]),
                IsRepeat = reader["order_isrepeat"] != DBNull.Value && Convert.ToBoolean(reader["order_isrepeat"]),
                OrderGuid = reader["order_guid"]?.ToString() ?? "",
                OrderDate = Convert.ToDateTime(reader["order_date"]),
                CollectionDate = Convert.ToDateTime(reader["ordercollection_Date"]),
                RefundOrderDate = Convert.ToDateTime(reader["RefundOrder_Date"]),
                WebstoreCode = reader["webstore_code"]?.ToString() ?? "",
                ForwardedOrderId = reader["order_forwardedorderid"]?.ToString() ?? "",
                PaidType = Convert.ToInt32(reader["order_paidType"]),
                TotalPrice = Convert.ToDouble(reader["order_totalPrice"]),
                CSMargin = Convert.ToDouble(reader["order_CSmargin"]),
                CouponValue = reader["order_couponvalue"] != DBNull.Value ? Convert.ToDouble(reader["order_couponvalue"]) : 0,
                PayoutRefund = Convert.ToDouble(reader["order_payoutRefund"]),
                CsRefund = Convert.ToDouble(reader["order_csRefund"]),
                PaypalFee = Convert.ToDouble(reader["order_paypalfee"]),
                IsPayout = reader["order_ispayout"] != DBNull.Value && Convert.ToBoolean(reader["order_ispayout"]),
                Status = Convert.ToInt32(reader["order_status"]),
                PayoutAmount = Convert.ToDouble(reader["order_payoutAmount"]),
                CustWithdrawalId = reader["custwithdrawal_ID"]?.ToString() ?? "",
                CustWithdrawalAmount = reader["custwithdrawal_amount"] != DBNull.Value ? Convert.ToDouble(reader["custwithdrawal_amount"]) : 0,
                BillingFName = reader["billing_fName"]?.ToString() ?? "",
                BillingLName = reader["billing_lName"]?.ToString() ?? "",
                BillingAddress = reader["billing_address"]?.ToString() ?? "",
                BillingCity = reader["billing_city"]?.ToString() ?? "",
                BillingZip = reader["billing_zip"]?.ToString() ?? "",
                BillingEmailId = reader["billing_emailID"]?.ToString() ?? "",
                RefundAmount = reader["Refund_Amount"] != DBNull.Value ? Convert.ToDouble(reader["Refund_Amount"]) : 0,
                Withdrew = reader["Withdrew"] != DBNull.Value ? Convert.ToDouble(reader["Withdrew"]) : 0,
                PayoutTotal = reader["Payout_Total"] != DBNull.Value ? Convert.ToDouble(reader["Payout_Total"]) : 0,
                ReverseAmount = reader["ReverseAmount"] != DBNull.Value ? Convert.ToDouble(reader["ReverseAmount"]) : 0,
                RefundRemarks = reader["Refund_remarks"]?.ToString() ?? "",
                IsReverseRefundRow = Convert.ToInt32(reader["IsReverseRefundRow"]),
                ReversalStatus = Convert.ToInt32(reader["reversal_status"]),
                CustWithdrawalMode = reader["custwithdrawal_Mode"] != DBNull.Value ? Convert.ToInt32(reader["custwithdrawal_Mode"]) : 0,
                OrderWorthBakingCost = reader["OrderWorth_BakingCost"]?.ToString() ?? "0"
            });
        }

        return items;
    }

    /// <summary>
    /// Gets base cost data for franchise orders.
    /// Legacy: GetBaseCost() with ViewState["vs_franchise"] query.
    /// </summary>
    public async Task<List<BakingBaseCostItem>> GetBaseCostsAsync(string orderIds)
    {
        var items = new List<BakingBaseCostItem>();
        if (string.IsNullOrEmpty(orderIds)) return items;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = string.Format(@"select od.orderDetail_orderID, CakeBaseCost = isnull(sum(f.CakeBaseCost), isnull(sum(cp.CakePrice), sum(p.product_startingtPrice))),
ProfitMargin = isnull(sum(f.ProfitMargin), 0) from tbl_orderDetail od 
left outer join tbl_lnkprdtemplate pt on od.orderDetail_productID = pt.lnkprdtemplate_prdId inner join tbl_TemplatePriceFormula f
on pt.lnkprdtemplate_templateID = f.TemplateID and f.SizeID = od.orderDetail_SizeID
left outer join tbl_CakePrice cp on cp.product_ID = od.orderDetail_productID and cp.SizeID = od.orderDetail_SizeID
inner join tbl_products p on p.product_ID = od.orderDetail_productID where od.orderDetail_orderID in ({0})
group by od.orderDetail_orderID", orderIds);

        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BakingBaseCostItem
            {
                OrderDetailOrderId = reader["orderDetail_orderID"]?.ToString() ?? "",
                CakeBaseCost = reader["CakeBaseCost"]?.ToString() ?? "0",
                ProfitMargin = reader["ProfitMargin"]?.ToString() ?? "0"
            });
        }

        return items;
    }

    /// <summary>
    /// Gets miscellaneous (Stripe) payment records.
    /// Legacy: bindgrid() rpOtherPayments query.
    /// </summary>
    public async Task<List<BakingMiscPaymentItem>> GetMiscPaymentsAsync(long webstoreId)
    {
        var items = new List<BakingMiscPaymentItem>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT * FROM tbl_Stripepayment 
                    INNER JOIN tbl_customers ON Stripepayment_crmId = tbl_customers.customer_ID 
                    INNER JOIN tbl_webstore ON webstore_ID = Stripepayment_BakeryID 
                    WHERE Stripepayment_BakeryID = @webstoreId AND Stripepayment_isdeleted = 0 
                    ORDER BY Stripepayment_ModifiedOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BakingMiscPaymentItem
            {
                StripepaymentId = Convert.ToInt64(reader["Stripepayment_ID"]),
                ModifiedOn = Convert.ToDateTime(reader["Stripepayment_ModifiedOn"]),
                Amount = Convert.ToDouble(reader["Stripepayment_Amount"]),
                Remarks = reader["Stripepayment_Remarks"]?.ToString() ?? "",
                IsPaid = reader["Stripepayment_isPaid"] != DBNull.Value && Convert.ToBoolean(reader["Stripepayment_isPaid"]),
                PayoutOn = reader["Stripepayment_payoutOn"] != DBNull.Value ? Convert.ToDateTime(reader["Stripepayment_payoutOn"]) : DateTime.MinValue,
                PaymentVia = reader["Stripepayment_paymentvia"]?.ToString() ?? "",
                TxId = reader["Stripepayment_txID"]?.ToString() ?? ""
            });
        }

        return items;
    }

    /// <summary>
    /// Withdraw order (manual payout request).
    /// Legacy: lnkwithdrawOrder_OnClick -> withdrawalrequest_manually
    /// Inserts into tbl_custwithdrawal with Mode=0.
    /// </summary>
    public async Task<(bool success, string message)> WithdrawOrderAsync(long orderId, long webstoreId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check daily withdrawal limit
        var limitSql = @"SELECT ISNULL(SUM(custwithdrawal_amount), 0) 
                         FROM tbl_custwithdrawal 
                         WHERE custwithdrawal_bakeryID = @bakeryId 
                           AND custwithdrawal_modifiedOn >= CAST(GETDATE() AS DATE) 
                           AND custwithdrawal_modifiedOn < CAST(DATEADD(DAY, 1, GETDATE()) AS DATE)";
        await using (var limitCmd = new SqlCommand(limitSql, conn))
        {
            limitCmd.Parameters.AddWithValue("@bakeryId", webstoreId);
            var dailyTotal = Convert.ToDecimal(await limitCmd.ExecuteScalarAsync());
            if (dailyTotal >= 6000)
            {
                return (false, "Sorry for inconvenience!\nWidthdrawal request can be sent upto £300 per day.\nPlease try tomorrow.");
            }
        }

        // Get order details
        var orderSql = @"SELECT * FROM tbl_order 
                         INNER JOIN tbl_webstore ON order_branchID = webstore_ID 
                         LEFT JOIN tbl_webstorebank ON webstorebank_webstoreID = webstore_ID 
                         WHERE order_status = 4 AND order_ispayout = 0 AND order_id = @orderId";
        await using var orderCmd = new SqlCommand(orderSql, conn);
        orderCmd.Parameters.AddWithValue("@orderId", orderId);

        await using var reader = await orderCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (false, "Order not found or not eligible for withdrawal.");
        }

        var webstoreBankId = reader["webstorebank_webstoreID"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webstoreBankId))
        {
            await reader.CloseAsync();
            return (false, "REDIRECT:seller-payment-settings");
        }

        var orderTotalPrice = Convert.ToDecimal(reader["order_totalPrice"]);
        var orderCSMargin = Convert.ToDecimal(reader["order_CSmargin"]);
        var orderPaypalFee = Convert.ToDecimal(reader["order_paypalfee"]);
        var orderPayoutRefund = Convert.ToDecimal(reader["order_payoutRefund"]);
        var orderCouponValue = Convert.ToDecimal(reader["order_couponvalue"]);
        var orderBranchId = Convert.ToInt64(reader["order_branchID"]);
        var businessName = reader["webstore_businessName"]?.ToString() ?? "";

        await reader.CloseAsync();

        var dcpendingpayment = Math.Round(orderTotalPrice - orderCSMargin - orderPaypalFee - orderPayoutRefund - orderCouponValue, 2);

        // Check if withdrawal already exists
        var existsSql = "SELECT COUNT(1) FROM tbl_custwithdrawal WHERE custwithdrawal_orderID = @orderId AND custwithdrawal_Mode = 0";
        await using (var existsCmd = new SqlCommand(existsSql, conn))
        {
            existsCmd.Parameters.AddWithValue("@orderId", orderId);
            var exists = Convert.ToInt32(await existsCmd.ExecuteScalarAsync()) > 0;
            if (exists)
            {
                return (false, "Withdrawal request already exists for this order.");
            }
        }

        // Insert withdrawal request
        var insertSql = @"INSERT INTO tbl_custwithdrawal 
                          (custwithdrawal_orderID, custwithdrawal_modifiedOn, custwithdrawal_isWithdrawalled, 
                           custwithdrawal_bakeryID, custwithdrawal_amount, custwithdrawal_Mode) 
                          VALUES (@orderId, GETDATE(), 0, @bakeryId, @amount, 0)";
        await using (var insertCmd = new SqlCommand(insertSql, conn))
        {
            insertCmd.Parameters.AddWithValue("@orderId", orderId);
            insertCmd.Parameters.AddWithValue("@bakeryId", orderBranchId);
            insertCmd.Parameters.AddWithValue("@amount", dcpendingpayment);
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Legacy: clsMail.WidthrawalrequestToadmin() email notification to admin
        // Sends email to accountMail from fromMail with withdrawal request details
        try
        {
            var accountMail = _config["accountMail"] ?? "";
            var fromMail = _config["fromMail"] ?? "";
            var websiteNameWithExt = _config["websiteNamewithExt"] ?? "";
            var siteUrlCrm = _config["CrmSiteUrl"] ?? "";
            var siteUrl = _config["SiteUrl"] ?? "";

            if (!string.IsNullOrEmpty(accountMail) && !string.IsNullOrEmpty(fromMail))
            {
                // Legacy: clsMail.WidthrawalrequestToadmin(webstore_businessName)
                var emailInnerBody = $@"
<table cellspacing='0' cellpadding='0' border='0' width='100%'>
    <tbody>
    <tr>
    <td style='padding:10px;font-family:arial;font-size:12px;color:#555;text-align:left;'>
    Dear admin,<br /><br />
New Widthrawal request has been placed by {businessName} at {websiteNameWithExt.ToLower()}.
<br/><br/><b><a href='{siteUrlCrm}crmwidthrawalrequests'>Click here</a></b> to View detail.<br/><br/>
    </td></tr>
</table>";

                // Legacy: clsMail.strMainEmailBody("", innerBody, "", "0", "650")
                var emailBody = $@"
<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html><head><meta http-equiv='Content-Type' content='text/html; charset=iso-8859-1' />
<title>Withdrawal Request</title></head>
<body style='margin:auto;font-family:Arial;font-size:12px;color:#000;'>
<table width='100%' cellpadding='0' align='center' cellspacing='0'>
<tr><td valign='top' align='center'>
<table width='100%' cellpadding='0' align='center' cellspacing='0'>
<tr><td align='center'>
<table width='650' cellpadding='0' cellspacing='0' border='0' style='border:solid 1px #555;'>
<tr><td>
<table width='100%' cellpadding='0' cellspacing='0'>
<tr><td style='background-color: #ffffff; border-bottom: 1px solid rgb(204, 204, 204); padding: 5px 10px;'>
<a href='{siteUrl}login'><img border='0' style='padding: 10px 5px;max-width: 114px;' src='{siteUrl}images/logo.png' /></a>
</td></tr></table>
</td></tr>
<tr><td>{emailInnerBody}</td></tr>
</table></td></tr></table></td></tr></table>
</body></html>";

                var subject = $"New Widthrawal request has been placed by {businessName}";

                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(fromMail);
                mailMessage.To.Add(accountMail);
                mailMessage.Subject = subject;
                mailMessage.Body = emailBody;
                mailMessage.IsBodyHtml = true;

                var smtpHost = _config["smtpClient"] ?? "";
                var smtpEmail = _config["smtpEmail"] ?? "";
                var smtpPwd = _config["smtpPwd"] ?? "";
                var smtpPort = int.TryParse(_config["smtpport"], out var port) ? port : 587;
                var smtpSsl = _config["smtpisSSL"] == "1";

                using var smtpClient = new SmtpClient(smtpHost)
                {
                    Credentials = new NetworkCredential(smtpEmail, smtpPwd),
                    Port = smtpPort,
                    EnableSsl = smtpSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };
                await smtpClient.SendMailAsync(mailMessage);
            }
        }
        catch (Exception)
        {
            // Legacy: silently swallows email sending exceptions
        }

        return (true, "Widthrawal Request Sent successfully!\nOnce payment is transferred to you, you will be notified.");
    }

    /// <summary>
    /// Reverse refund for a franchise order.
    /// Legacy: lnkReverseRefund_OnClick
    /// Calls the WithdrawAmount_Refund stored procedure equivalent.
    /// </summary>
    public async Task<(bool success, string message)> ReverseRefundAsync(long orderId, int mode, long webstoreId, decimal reverseAmount)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        decimal deciAmount = -1 * reverseAmount;

        // Legacy: clsOrder.WithdrawAmount_Refund(webstoreid, deciAmount, OrderID, mode)
        // This inserts into BakeryAccountLog or updates the account overview
        var sql = @"INSERT INTO tbl_custwithdrawal 
                    (custwithdrawal_orderID, custwithdrawal_modifiedOn, custwithdrawal_isWithdrawalled, 
                     custwithdrawal_bakeryID, custwithdrawal_amount, custwithdrawal_Mode) 
                    VALUES (@orderId, GETDATE(), 0, @bakeryId, @amount, @mode)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@bakeryId", webstoreId);
        cmd.Parameters.AddWithValue("@amount", deciAmount);
        cmd.Parameters.AddWithValue("@mode", mode);

        await cmd.ExecuteNonQueryAsync();

        return (true, "Reversal Request Sent successfully!");
    }

    /// <summary>
    /// Withdraw miscellaneous payout.
    /// Legacy: lnkwithdrawPayout_OnClick
    /// Executes Stripe transfer and updates tbl_Stripepayment with payment details.
    /// </summary>
    public async Task<(bool success, string message)> WithdrawPayoutAsync(long paymentId, long webstoreId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT * FROM tbl_Stripepayment 
                    INNER JOIN tbl_customers ON Stripepayment_crmId = tbl_customers.customer_ID 
                    INNER JOIN tbl_webstore ON webstore_ID = Stripepayment_BakeryID 
                    WHERE Stripepayment_isdeleted = 0 
                      AND Stripepayment_BakeryID = @webstoreId 
                      AND Stripepayment_isPaid = 0 
                      AND Stripepayment_ID = @paymentId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
        cmd.Parameters.AddWithValue("@paymentId", paymentId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (false, "Payment record not found or already paid.");
        }

        var paypalEmail = reader["PaypalEmail"]?.ToString() ?? "";
        var isVerified = reader["IsPaypalVerified"] != DBNull.Value && Convert.ToBoolean(reader["IsPaypalVerified"]);
        var stripepaymentAmount = Convert.ToDecimal(reader["Stripepayment_Amount"]);
        var stripepaymentBakeryId = reader["Stripepayment_BakeryID"]?.ToString() ?? "";

        await reader.CloseAsync();

        if (string.IsNullOrEmpty(paypalEmail) || !isVerified)
        {
            return (false, "REDIRECT:seller-payment-settings");
        }

        // Legacy: Calculate amount in pence (pennies) for Stripe
        var dcpendingpayment = Math.Round(stripepaymentAmount, 2);
        var strdcpendingpayment_100 = double.Parse(Convert.ToString(dcpendingpayment * 100)).ToString();
        var pendingamt = int.Parse(strdcpendingpayment_100);

        // Legacy: StripeConfiguration.SetApiKey(ConfigurationManager.AppSettings["StipeSecretkey"])
        // Legacy: Check Stripe balance before transfer
        // var balanceService = new StripeBalanceService();
        // StripeBalance balanceresponse = balanceService.Get();
        // Canwithdraw = balanceresponse.Available[0].Amount > pendingamt;
        //
        // Legacy: Create Stripe transfer
        // var transferOptions = new StripeTransferCreateOptions()
        // {
        //     Amount = pendingamt,                    // amount in pence
        //     Currency = "gbp",
        //     Destination = paypalEmail,               // Stripe connected account ID (stored in PaypalEmail)
        //     TransferGroup = paymentId + "_" + stripepaymentBakeryId
        // };
        // var transferService = new StripeTransferService();
        // StripeTransfer stripeTransfer = transferService.Create(transferOptions);
        // string strtxID = stripeTransfer.Id;

        // TODO: Replace this block with actual Stripe.net SDK call when Stripe NuGet package is added.
        // Legacy Stripe API parameters:
        //   API Key: config["StipeSecretkey"]
        //   Transfer Amount: pendingamt (int, in pence)
        //   Currency: "gbp"
        //   Destination: paypalEmail (Stripe connected account ID)
        //   TransferGroup: "{paymentId}_{stripepaymentBakeryId}"
        //   Balance check: StripeBalanceService.Get().Available[0].Amount > pendingamt
        string strtxID;
        try
        {
            strtxID = await ExecuteStripeTransferAsync(pendingamt, "gbp", paypalEmail, $"{paymentId}_{stripepaymentBakeryId}");
        }
        catch (Exception)
        {
            return (false, "Sorry for inconvenience!\nAt this moment you can not widthdraw. Please Try later.");
        }

        if (string.IsNullOrEmpty(strtxID))
        {
            return (false, "Sorry for inconvenience!\nAt this moment you can not widthdraw. Please Try later.");
        }

        // Legacy: Update tbl_Stripepayment after successful transfer
        // Sets Stripepayment_isPaid=1, Stripepayment_payoutOn=getdate(), Stripepayment_txID=strtxID
        var updateSql = @"UPDATE tbl_Stripepayment 
                          SET Stripepayment_isPaid = 1, 
                              Stripepayment_payoutOn = GETDATE(), 
                              Stripepayment_txID = @txId 
                          WHERE Stripepayment_ID = @paymentId";
        await using (var updateCmd = new SqlCommand(updateSql, conn))
        {
            updateCmd.Parameters.AddWithValue("@txId", strtxID);
            updateCmd.Parameters.AddWithValue("@paymentId", paymentId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        return (true, "Your widthdrawal progress is successfully done.");
    }

    /// <summary>
    /// Execute a Stripe transfer.
    /// TODO: Implement with Stripe.net NuGet package (Stripe.TransferService.CreateAsync).
    /// Legacy used: StripeTransferService.Create(transferOptions) with StripeTransferCreateOptions.
    /// Legacy parameters:
    ///   - API Key from config["StipeSecretkey"]
    ///   - StripeBalanceService.Get() to check Available balance > amount
    ///   - StripeTransferCreateOptions { Amount, Currency, Destination, TransferGroup }
    /// Returns the Stripe Transfer ID (e.g. "tr_xxx") or empty string if balance insufficient.
    /// </summary>
    private async Task<string> ExecuteStripeTransferAsync(int amountInPence, string currency, string destination, string transferGroup)
    {
        // TODO: Add Stripe.net NuGet package and implement:
        //
        // StripeConfiguration.ApiKey = _config["StipeSecretkey"];
        //
        // // Check balance first (legacy pattern)
        // var balanceService = new Stripe.BalanceService();
        // var balance = await balanceService.GetAsync();
        // if (!balance.Available.Any() || balance.Available[0].Amount <= amountInPence)
        //     return ""; // Insufficient balance
        //
        // // Create transfer
        // var transferOptions = new Stripe.TransferCreateOptions
        // {
        //     Amount = amountInPence,
        //     Currency = currency,
        //     Destination = destination,
        //     TransferGroup = transferGroup
        // };
        // var transferService = new Stripe.TransferService();
        // var transfer = await transferService.CreateAsync(transferOptions);
        // return transfer.Id;

        // Placeholder: throw to indicate not yet implemented
        await Task.CompletedTask;
        throw new NotImplementedException(
            $"Stripe transfer not yet implemented. Parameters: Amount={amountInPence}, Currency={currency}, Destination={destination}, TransferGroup={transferGroup}. " +
            "Add Stripe.net NuGet package and implement BalanceService.GetAsync() + TransferService.CreateAsync().");
    }
}
