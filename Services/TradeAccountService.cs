using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class TradeAccountDayItem
{
    public DateTime? BakerWorkCostDate { get; set; }
    public decimal BakerWorkCostAmount { get; set; }
    public DateTime TimelineFrom { get; set; }
    public DateTime TimelineTo { get; set; }
    public int TotalTimeMinutes { get; set; }
    public decimal TotalWorkCost { get; set; }
}

public class TradeAccountDetailItem
{
    public long BakerWorkCostId { get; set; }
    public int AmountInOut { get; set; }
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public long ReqId { get; set; }
    public int ReqType { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CostDate { get; set; }
    public int TimeTaken { get; set; }
    public decimal TotalAmountForDay { get; set; }
    public int TotalTimeTakenForDay { get; set; }
    public decimal AmountLeft { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for My Trade Account module.
/// Migrated from mytradeaccount.aspx.
/// Uses DefaultConnection with tbl_bakerTimeline, tbl_BakerWorkCost, tbl_orderDetail tables.
/// READ-ONLY — no mutations.
/// </summary>
public class TradeAccountService
{
    private readonly string _defaultConnection;

    public TradeAccountService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets the daily work cost/timeline summary for a baker within a date range.
    /// Joins tbl_bakerTimeline with aggregated tbl_BakerWorkCost data.
    /// </summary>
    public async Task<List<TradeAccountDayItem>> GetTradeAccountAsync(int bakerId, DateTime startDate, DateTime endDate)
    {
        var items = new List<TradeAccountDayItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT bc.BakerWorkCost_date, bc.BakerWorkCost_amount, 
                           bt.bakerTimeline_dateFrom, bt.bakerTimeline_dateTo, 
                           bt.bakerTimeline_Totaltime_min, 
                           bt.bakerTimeline_totalWorkCost
                    FROM tbl_bakerTimeline bt 
                    LEFT JOIN (
                        SELECT bakerid = @bakerid, 
                               BakerWorkCost_date = CAST(BakerWorkCost_date AS date), 
                               BakerWorkCost_amount = SUM(BakerWorkCost_amount) 
                        FROM tbl_BakerWorkCost 
                        WHERE (BakerWorkCost_BakerID = @bakerid) 
                          AND (CAST(BakerWorkCost_date AS date) >= @startdate 
                               AND CAST(BakerWorkCost_date AS date) <= @enddate)
                        GROUP BY CAST(BakerWorkCost_date AS date)
                    ) AS bc ON bt.bakerTimeline_bakerID = bc.bakerid 
                           AND CAST(bt.bakerTimeline_dateFrom AS date) = bc.BakerWorkCost_date 
                           AND CAST(bt.bakerTimeline_dateTo AS date) = bc.BakerWorkCost_date
                    WHERE bakerTimeline_bakerID = @bakerid 
                    ORDER BY bt.bakerTimeline_dateFrom DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@bakerid", bakerId);
        cmd.Parameters.AddWithValue("@startdate", startDate.Date);
        cmd.Parameters.AddWithValue("@enddate", endDate.Date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TradeAccountDayItem
            {
                BakerWorkCostDate = reader.IsDBNull(0) ? null : reader.GetDateTime(0),
                BakerWorkCostAmount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                TimelineFrom = reader.GetDateTime(2),
                TimelineTo = reader.GetDateTime(3),
                TotalTimeMinutes = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                TotalWorkCost = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets the detail breakdown for a specific day — individual order costs.
    /// Joins tbl_BakerWorkCost with tbl_orderDetail.
    /// </summary>
    public async Task<List<TradeAccountDetailItem>> GetDayDetailAsync(int bakerId, DateTime workCostDate)
    {
        var items = new List<TradeAccountDetailItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT BakerWorkCost_ID, BakerWorkCost_amountInOut, orderDetail_orderID, BakerWorkCost_amount,
                           BakerWorkCost_ReqID, BakerWorkCost_reqType, BakerWorkCost_ispaid, BakerWorkCost_date,
                           BakerWorkCost_timetaken, BakerWorkCost_totalamountforday, BakerWorkCost_totaltimetakenforday, 
                           BakerWorkCost_amountleft 
                    FROM tbl_BakerWorkCost
                    INNER JOIN tbl_orderDetail ON orderDetail_ID = BakerWorkCost_ReqID 
                    WHERE (BakerWorkCost_BakerID = @bakerid) AND (CAST(BakerWorkCost_date AS date) = @WorkCost_date)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@bakerid", bakerId);
        cmd.Parameters.AddWithValue("@WorkCost_date", workCostDate.Date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TradeAccountDetailItem
            {
                BakerWorkCostId = reader.GetInt64(0),
                AmountInOut = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                OrderId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                Amount = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                ReqId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                ReqType = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                IsPaid = !reader.IsDBNull(6) && reader.GetBoolean(6),
                CostDate = reader.GetDateTime(7),
                TimeTaken = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                TotalAmountForDay = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                TotalTimeTakenForDay = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                AmountLeft = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11)
            });
        }

        return items;
    }
}
