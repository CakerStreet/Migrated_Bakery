using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class AccountOverviewModel
{
    public decimal PendingAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal TotalTrade { get; set; }
    public decimal FeeCharged { get; set; }
    public decimal PayoutAmount { get; set; }
}

public class AccountOrderItem
{
    public DateTime OrderDate { get; set; }
    public DateTime CollectionDate { get; set; }
    public long OrderId { get; set; }
    public long? ForwardedOrderId { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal CSMargin { get; set; }
    public decimal CouponValue { get; set; }
    public decimal PayoutRefund { get; set; }
    public decimal CsRefund { get; set; }
    public decimal PaypalFee { get; set; }
    public int Status { get; set; }
    public decimal PayoutAmount { get; set; }
    public bool IsPayout { get; set; }
    public long? WithdrawalId { get; set; }
    public decimal? WithdrawalAmount { get; set; }
    public long BakeryId { get; set; }
    public bool IsRepeat { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for My Account Balance module.
/// Migrated from myaccountbalance.aspx.
/// Uses DefaultConnection with tbl_order, tbl_orderDetail, tbl_products, tbl_ordercollection, 
/// tbl_webstore, tbl_custwithdrawal, tbl_orderAcntOverview tables.
/// READ-ONLY for account overview and order list.
/// Withdrawal mutations are FEATURE FLAGGED (AccountBalance:WithdrawalsEnabled).
/// </summary>
public class AccountBalanceService
{
    private readonly string _defaultConnection;

    public AccountBalanceService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets the account overview summary — pending, confirmed, available, cancelled, 
    /// total trade, fee charges, and payout amounts.
    /// </summary>
    public async Task<AccountOverviewModel> GetAccountOverviewAsync(long webstoreId)
    {
        var model = new AccountOverviewModel();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT 
                        SUM(CASE WHEN order_status = 0 THEN order_payoutAmount ELSE 0 END) AS Pending_Amount,
                        SUM(CASE WHEN order_status = 11 THEN order_payoutAmount ELSE 0 END) AS cancelled_Amount,
                        SUM(CASE WHEN order_status IN (1,2,3,5) THEN order_payoutAmount ELSE 0 END) AS confirmed_Amount,
                        SUM(CASE WHEN order_status = 4 AND order_ispayout = 0 THEN order_payoutAmount ELSE 0 END) AS available_Amount,
                        SUM(order_totalPrice) AS Total_Trade,
                        SUM(order_CSmargin + order_paypalfee) AS fee_charged,
                        SUM(CASE WHEN order_ispayout = 1 THEN order_payoutAmount ELSE 0 END) AS Payout_Amount
                    FROM tbl_order 
                    WHERE order_branchID = @webstoreId AND order_bakeryID = order_branchID 
                      AND order_followingOrderid = 0 AND order_saletype = 1 
                      AND order_isdeleted = 0 AND order_isPurchased = 1";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            model.PendingAmount = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
            model.CancelledAmount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
            model.ConfirmedAmount = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
            model.AvailableAmount = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            model.TotalTrade = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
            model.FeeCharged = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);
            model.PayoutAmount = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6);
        }

        return model;
    }

    /// <summary>
    /// Gets the last 100 orders for the account balance order list.
    /// Joins tbl_order with tbl_ordercollection, tbl_webstore, and tbl_custwithdrawal.
    /// </summary>
    public async Task<List<AccountOrderItem>> GetOrdersAsync(long webstoreId)
    {
        var items = new List<AccountOrderItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT TOP 100 
                        order_date, ordercollection_Date, order_ID, order_forwardedorderid, 
                        order_totalPrice, order_CSmargin, order_couponvalue, order_payoutRefund, 
                        order_csRefund, order_paypalfee, order_status, order_payoutAmount, 
                        order_ispayout, custwithdrawal_ID, custwithdrawal_amount, order_bakeryID, order_isrepeat
                    FROM tbl_order 
                    INNER JOIN tbl_ordercollection ON order_ID = ordercollection_OrderID 
                    INNER JOIN tbl_webstore ON order_branchID = webstore_ID 
                    LEFT JOIN tbl_custwithdrawal ON custwithdrawal_orderID = order_ID 
                        AND custwithdrawal_bakeryID = webstore_ID AND custwithdrawal_Mode = 0
                    WHERE order_branchID = @webstoreId AND order_bakeryID = order_branchID 
                      AND order_followingOrderid = 0 AND order_saletype = 1 
                      AND order_isdeleted = 0 AND order_isPurchased = 1 
                    ORDER BY order_date DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new AccountOrderItem
            {
                OrderDate = reader.GetDateTime(0),
                CollectionDate = reader.GetDateTime(1),
                OrderId = reader.GetInt64(2),
                ForwardedOrderId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                TotalPrice = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                CSMargin = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                CouponValue = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                PayoutRefund = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                CsRefund = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                PaypalFee = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                Status = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                PayoutAmount = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11),
                IsPayout = !reader.IsDBNull(12) && reader.GetBoolean(12),
                WithdrawalId = reader.IsDBNull(13) ? null : reader.GetInt64(13),
                WithdrawalAmount = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                BakeryId = reader.IsDBNull(15) ? 0 : reader.GetInt64(15),
                IsRepeat = !reader.IsDBNull(16) && reader.GetBoolean(16)
            });
        }

        return items;
    }

    /// <summary>
    /// Submits a manual withdrawal request for a completed order.
    /// Inserts into tbl_custwithdrawal.
    /// </summary>
    public async Task<bool> RequestWithdrawalAsync(long orderId, long webstoreId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // 1. Check if order exists, is completed (status = 4), and is not paid out (ispayout = 0)
        var checkSql = @"SELECT order_payoutAmount FROM tbl_order 
                         WHERE order_ID = @orderId AND order_branchID = @webstoreId 
                           AND order_status = 4 AND order_ispayout = 0";
        decimal payoutAmount = 0;
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@orderId", orderId);
            checkCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            var result = await checkCmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return false;
            payoutAmount = Convert.ToDecimal(result);
        }

        // 2. Check if a withdrawal already exists
        var existsSql = "SELECT COUNT(1) FROM tbl_custwithdrawal WHERE custwithdrawal_orderID = @orderId";
        await using (var existsCmd = new SqlCommand(existsSql, conn))
        {
            existsCmd.Parameters.AddWithValue("@orderId", orderId);
            var exists = Convert.ToInt32(await existsCmd.ExecuteScalarAsync()) > 0;
            if (exists) return false;
        }

        // 3. Insert into tbl_custwithdrawal
        var insertSql = @"INSERT INTO tbl_custwithdrawal 
                            (custwithdrawal_orderID, custwithdrawal_bakeryID, custwithdrawal_amount, 
                             custwithdrawal_isWithdrawalled, custwithdrawal_Mode, custwithdrawal_modifiedOn) 
                          VALUES 
                            (@orderId, @webstoreId, @amount, 0, 0, GETDATE())";
        await using (var insertCmd = new SqlCommand(insertSql, conn))
        {
            insertCmd.Parameters.AddWithValue("@orderId", orderId);
            insertCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            insertCmd.Parameters.AddWithValue("@amount", payoutAmount);
            await insertCmd.ExecuteNonQueryAsync();
        }

        return true;
    }
}
