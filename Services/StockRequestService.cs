using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace CakerStreet.Business.Services;

// ─── Stock Request Models ──────────────────────────────────────────────────────

public class StockRequestListResult
{
    public List<StockRequestItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int PendingCount { get; set; }
    public int POPendingCount { get; set; }
    public int POApprovedCount { get; set; }
    public int SentToSupplierCount { get; set; }
    public int CompletedCount { get; set; }
    public int DeclinedCount { get; set; }
}

public class StockRequestItem
{
    public long PrdStockRequest_Id { get; set; }
    public long Product_Id { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public int Required_Qty { get; set; }
    public DateTime Required_Date { get; set; }
    public long Req_UserID { get; set; }
    public string? ReqUserName { get; set; }
    public string? Remarks { get; set; }
    public int Status { get; set; }
    public int PO_Status { get; set; }
    public long? PO_ID { get; set; }
    public string? PO_SysNo { get; set; }
    public DateTime Created_On { get; set; }
}

public class StockRequestSaveModel
{
    public long PrdStockRequest_Id { get; set; }
    public long Product_Id { get; set; }
    public int Required_Qty { get; set; }
    public string Required_Date { get; set; } = "";
    public long Req_UserID { get; set; }
    public string? Remarks { get; set; }
}

public class StockRequestDetail
{
    public long PrdStockRequest_Id { get; set; }
    public long Product_Id { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductType { get; set; } = "";
    public int Required_Qty { get; set; }
    public string Required_Date { get; set; } = "";
    public long Req_UserID { get; set; }
    public string? Remarks { get; set; }
    public string? StockLocationHtml { get; set; }
}

public class RemarkReplyItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ModifiedOn { get; set; }
}

public class ProductSearchItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Stock Request management.
/// Migrated from managestockrequest.aspx.
/// Uses BusinessConnection for tbl_PrdStockRequest, tbl_PrdStockRequestRemarksReply, tbl_PO, tbl_POdet.
/// Uses DefaultConnection for tbl_products, tbl_bakeryuser, tbl_location, tbl_StockLocation.
/// Module 20 permission + HQ-only (webshopId == 82).
/// </summary>
public class StockRequestService
{
    private readonly string _businessConnection;
    private readonly string _defaultConnection;

    public StockRequestService(IConfiguration config)
    {
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated stock requests with status counts.
    /// Status tab mapping:
    ///   0 = Pending (Status=0)
    ///   1 = Approved/PO Pending (Status=1 AND PO_Status=-1)
    ///   2 = PO Approved (Status=1 AND PO_Status IN (0,1,2))
    ///   3 = PO Sent to Supplier (Status=1 AND PO_Status=3)
    ///   4 = PO Completed (Status=1 AND PO_Status=4)
    ///   5 = Declined (Status=3)
    /// </summary>
    public async Task<StockRequestListResult> GetStockRequestsAsync(
        int page, int pageSize, string? search, int status)
    {
        var result = new StockRequestListResult();
        await using var bizConn = new SqlConnection(_businessConnection);
        await bizConn.OpenAsync();

        // Get all status counts in a single query
        // NOTE: PO_Status column may not exist in all DB environments.
        // Detect column existence first, then build query accordingly.
        bool hasPOStatus = false;
        var colCheckSql = "SELECT COL_LENGTH('tbl_PrdStockRequest','PO_Status')";
        await using (var colCmd = new SqlCommand(colCheckSql, bizConn))
        {
            var colResult = await colCmd.ExecuteScalarAsync();
            hasPOStatus = colResult != null && colResult != DBNull.Value;
        }

        string countSql;
        if (hasPOStatus)
        {
            countSql = @"
            SELECT 
                SUM(CASE WHEN Status = 0 AND IsDeleted = 0 THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 1 AND IsDeleted = 0 AND ISNULL(PO_Status, -1) = -1 THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 1 AND IsDeleted = 0 AND ISNULL(PO_Status, -1) IN (0,1,2) THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 1 AND IsDeleted = 0 AND ISNULL(PO_Status, -1) = 5 THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 1 AND IsDeleted = 0 AND ISNULL(PO_Status, -1) = 6 THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 3 AND IsDeleted = 0 THEN 1 ELSE 0 END)
            FROM tbl_PrdStockRequest";
        }
        else
        {
            // Fallback: no PO_Status column — all Status=1 goes to "Approved" bucket
            countSql = @"
            SELECT 
                SUM(CASE WHEN Status = 0 AND IsDeleted = 0 THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status = 1 AND IsDeleted = 0 THEN 1 ELSE 0 END),
                0, 0, 0,
                SUM(CASE WHEN Status = 3 AND IsDeleted = 0 THEN 1 ELSE 0 END)
            FROM tbl_PrdStockRequest";
        }

        await using (var countCmd = new SqlCommand(countSql, bizConn))
        {
            await using var reader = await countCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.PendingCount = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                result.POPendingCount = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                result.POApprovedCount = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                result.SentToSupplierCount = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
                result.CompletedCount = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
                result.DeclinedCount = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
            }
        }

        // Build WHERE clause based on status tab
        var where = "WHERE r.IsDeleted = 0";
        switch (status)
        {
            case 0: where += " AND r.Status = 0"; break;
            case 1: where += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = -1" : " AND r.Status = 1"; break;
            case 2: where += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) IN (0,1,2)" : " AND 1=0"; break;
            case 3: where += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = 5" : " AND 1=0"; break; // PO_Status=5 (legacy Sent to Supplier)
            case 4: where += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = 6" : " AND 1=0"; break; // PO_Status=6 (legacy Completed)
            case 5: where += " AND r.Status = 3"; break;
            case -1: break; // All — no additional status filter
            default: where += " AND r.Status = 0"; break; // default to pending
        }

        if (!string.IsNullOrWhiteSpace(search))
            where += " AND r.PrdStockRequest_Id IN (SELECT sr.PrdStockRequest_Id FROM tbl_PrdStockRequest sr WHERE sr.Product_Id IN (SELECT product_id FROM @productMatches))";

        // If search is provided, we need to find matching product IDs from DefaultConnection first
        var matchingProductIds = new List<long>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            await using var defConn = new SqlConnection(_defaultConnection);
            await defConn.OpenAsync();
            var searchSql = "SELECT product_id FROM tbl_products WHERE product_name LIKE @search AND product_isdeleted = 0";
            await using var searchCmd = new SqlCommand(searchSql, defConn);
            searchCmd.Parameters.AddWithValue("@search", "%" + search + "%");
            await using var searchReader = await searchCmd.ExecuteReaderAsync();
            while (await searchReader.ReadAsync())
                matchingProductIds.Add(searchReader.GetInt64(0));
        }

        // Rebuild WHERE for actual query (use product ID list instead of subquery)
        var actualWhere = "WHERE r.IsDeleted = 0";
        switch (status)
        {
            case 0: actualWhere += " AND r.Status = 0"; break;
            case 1: actualWhere += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = -1" : " AND r.Status = 1"; break;
            case 2: actualWhere += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) IN (0,1,2)" : " AND 1=0"; break;
            case 3: actualWhere += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = 5" : " AND 1=0"; break; // PO_Status=5 (legacy Sent to Supplier)
            case 4: actualWhere += hasPOStatus ? " AND r.Status = 1 AND ISNULL(r.PO_Status, -1) = 6" : " AND 1=0"; break; // PO_Status=6 (legacy Completed)
            case 5: actualWhere += " AND r.Status = 3"; break;
            case -1: break; // All — no additional status filter
            default: actualWhere += " AND r.Status = 0"; break;
        }

        if (!string.IsNullOrWhiteSpace(search) && matchingProductIds.Count > 0)
        {
            var pidParams = matchingProductIds.Select((id, i) => $"@mpid{i}").ToList();
            actualWhere += $" AND r.Product_Id IN ({string.Join(",", pidParams)})";
        }
        else if (!string.IsNullOrWhiteSpace(search) && matchingProductIds.Count == 0)
        {
            // No matching products found — return empty result
            return result;
        }

        // Get total count
        var totalSql = $"SELECT COUNT(1) FROM tbl_PrdStockRequest r {actualWhere}";
        int totalCount = 0;
        await using (var totalCmd = new SqlCommand(totalSql, bizConn))
        {
            AddProductMatchParams(totalCmd, matchingProductIds);
            totalCount = Convert.ToInt32(await totalCmd.ExecuteScalarAsync());
        }
        result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Get page data
        var offset = (page - 1) * pageSize;
        var listSql = $@"
            SELECT r.PrdStockRequest_Id, r.Product_Id, r.Required_Qty, r.Required_Date,
                   r.Req_UserID, r.Remarks, r.Status, {(hasPOStatus ? "ISNULL(r.PO_Status, -1)" : "-1")} AS PO_Status,
                   {(hasPOStatus ? "r.PO_ID" : "NULL")} AS PO_ID, {(hasPOStatus ? "r.PO_SysNo" : "NULL")} AS PO_SysNo, r.Created_On
            FROM tbl_PrdStockRequest r
            {actualWhere}
            ORDER BY r.PrdStockRequest_Id DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var items = new List<StockRequestItem>();
        var productIds = new List<long>();
        var userIds = new List<long>();

        await using (var listCmd = new SqlCommand(listSql, bizConn))
        {
            AddProductMatchParams(listCmd, matchingProductIds);
            listCmd.Parameters.AddWithValue("@offset", offset);
            listCmd.Parameters.AddWithValue("@pageSize", pageSize);

            await using var reader = await listCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new StockRequestItem
                {
                    PrdStockRequest_Id = reader.GetInt64(0),
                    Product_Id = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    Required_Qty = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                    Required_Date = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    Req_UserID = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    Remarks = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Status = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                    PO_Status = Convert.ToInt32(reader.GetValue(7)),
                    PO_ID = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                    PO_SysNo = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Created_On = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10)
                };
                items.Add(item);
                if (item.Product_Id > 0) productIds.Add(item.Product_Id);
                if (item.Req_UserID > 0) userIds.Add(item.Req_UserID);
            }
        }

        // Fetch product names and user names from DefaultConnection
        if (items.Count > 0)
        {
            await using var defConn = new SqlConnection(_defaultConnection);
            await defConn.OpenAsync();

            // Get product info
            var distinctPids = productIds.Distinct().Where(id => id > 0).ToList();
            if (distinctPids.Count > 0)
            {
                var pParams = distinctPids.Select((id, i) => $"@pp{i}").ToList();
                var pSql = $"SELECT product_id, product_name, product_image1 FROM tbl_products WHERE product_id IN ({string.Join(",", pParams)})";
                await using var pCmd = new SqlCommand(pSql, defConn);
                for (int i = 0; i < distinctPids.Count; i++)
                    pCmd.Parameters.AddWithValue($"@pp{i}", distinctPids[i]);

                var prdMap = new Dictionary<long, (string Name, string? Image)>();
                await using var pReader = await pCmd.ExecuteReaderAsync();
                while (await pReader.ReadAsync())
                {
                    var pid = pReader.GetInt64(0);
                    var pname = pReader.IsDBNull(1) ? "" : pReader.GetString(1);
                    var pimg = pReader.IsDBNull(2) ? null : pReader.GetString(2);
                    prdMap[pid] = (pname, pimg);
                }

                foreach (var item in items)
                {
                    if (prdMap.ContainsKey(item.Product_Id))
                    {
                        item.ProductName = prdMap[item.Product_Id].Name;
                        item.ProductImage = prdMap[item.Product_Id].Image;
                    }
                }
            }

            // Get user names
            var distinctUids = userIds.Distinct().Where(id => id > 0).ToList();
            if (distinctUids.Count > 0)
            {
                var uParams = distinctUids.Select((id, i) => $"@uid{i}").ToList();
                var uSql = $"SELECT customer_ID, customer_Name FROM tbl_bakeryuser WHERE customer_ID IN ({string.Join(",", uParams)})";
                await using var uCmd = new SqlCommand(uSql, defConn);
                for (int i = 0; i < distinctUids.Count; i++)
                    uCmd.Parameters.AddWithValue($"@uid{i}", distinctUids[i]);

                var userMap = new Dictionary<long, string>();
                await using var uReader = await uCmd.ExecuteReaderAsync();
                while (await uReader.ReadAsync())
                {
                    var uid = uReader.GetInt64(0);
                    var uname = uReader.IsDBNull(1) ? "" : uReader.GetString(1);
                    userMap[uid] = uname;
                }

                foreach (var item in items)
                {
                    if (userMap.ContainsKey(item.Req_UserID))
                        item.ReqUserName = userMap[item.Req_UserID];
                }
            }
        }

        result.Items = items;
        return result;
    }

    private static void AddProductMatchParams(SqlCommand cmd, List<long> matchingProductIds)
    {
        for (int i = 0; i < matchingProductIds.Count; i++)
            cmd.Parameters.AddWithValue($"@mpid{i}", matchingProductIds[i]);
    }

    /// <summary>
    /// Gets a single stock request by ID with product name/type and stock location HTML.
    /// </summary>
    public async Task<StockRequestDetail?> GetByIdAsync(long id)
    {
        await using var bizConn = new SqlConnection(_businessConnection);
        await bizConn.OpenAsync();

        var sql = @"SELECT PrdStockRequest_Id, Product_Id, Required_Qty, Required_Date, 
                           Req_UserID, Remarks
                    FROM tbl_PrdStockRequest WHERE PrdStockRequest_Id = @id";
        await using var cmd = new SqlCommand(sql, bizConn);
        cmd.Parameters.AddWithValue("@id", id);

        StockRequestDetail? detail = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            detail = new StockRequestDetail
            {
                PrdStockRequest_Id = reader.GetInt64(0),
                Product_Id = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                Required_Qty = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                Required_Date = reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("d"),
                Req_UserID = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                Remarks = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
        }

        if (detail == null) return null;

        // Get product name and type from DefaultConnection
        await using var defConn = new SqlConnection(_defaultConnection);
        await defConn.OpenAsync();

        var prdSql = "SELECT product_name, product_type FROM tbl_products WHERE product_id = @pid";
        await using var prdCmd = new SqlCommand(prdSql, defConn);
        prdCmd.Parameters.AddWithValue("@pid", detail.Product_Id);
        await using var prdReader = await prdCmd.ExecuteReaderAsync();
        if (await prdReader.ReadAsync())
        {
            detail.ProductName = prdReader.IsDBNull(0) ? "" : prdReader.GetString(0);
            detail.ProductType = prdReader.IsDBNull(1) ? "" : prdReader.GetString(1);
        }
        await prdReader.CloseAsync();

        // Get stock location HTML
        detail.StockLocationHtml = await GetStockLocationHtmlInternalAsync(detail.Product_Id, defConn);

        return detail;
    }

    /// <summary>
    /// Saves a stock request. INSERT if id=0, else UPDATE.
    /// </summary>
    public async Task<string> SaveStockRequestAsync(StockRequestSaveModel model, int userId, long webstoreId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        if (model.PrdStockRequest_Id == 0)
        {
            var sql = @"INSERT INTO tbl_PrdStockRequest 
                (Product_Id, Required_Qty, Required_Date, Req_UserID, Remarks, 
                 Status, IsDeleted, Created_On, Created_By, Modified_On, Modified_By, Webstore_Id)
                VALUES (@productId, @qty, @reqDate, @reqUserId, @remarks, 
                        0, 0, @now, @userId, @now, @userId, @webstoreId)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@productId", model.Product_Id);
            cmd.Parameters.AddWithValue("@qty", model.Required_Qty);
            cmd.Parameters.AddWithValue("@reqDate", DateTime.Parse(model.Required_Date));
            cmd.Parameters.AddWithValue("@reqUserId", model.Req_UserID);
            cmd.Parameters.AddWithValue("@remarks", (object?)model.Remarks ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var sql = @"UPDATE tbl_PrdStockRequest SET 
                Product_Id = @productId, Required_Qty = @qty, Required_Date = @reqDate,
                Req_UserID = @reqUserId, Remarks = @remarks, 
                Modified_On = @now, Modified_By = @userId
                WHERE PrdStockRequest_Id = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@productId", model.Product_Id);
            cmd.Parameters.AddWithValue("@qty", model.Required_Qty);
            cmd.Parameters.AddWithValue("@reqDate", DateTime.Parse(model.Required_Date));
            cmd.Parameters.AddWithValue("@reqUserId", model.Req_UserID);
            cmd.Parameters.AddWithValue("@remarks", (object?)model.Remarks ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@id", model.PrdStockRequest_Id);
            await cmd.ExecuteNonQueryAsync();
        }

        return "1";
    }

    /// <summary>
    /// Approve a single stock request (Status=1).
    /// </summary>
    public async Task<bool> ApproveAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var sql = "UPDATE tbl_PrdStockRequest SET Status = 1 WHERE PrdStockRequest_Id = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Decline a single stock request (Status=3).
    /// </summary>
    public async Task<bool> DeclineAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var sql = "UPDATE tbl_PrdStockRequest SET Status = 3 WHERE PrdStockRequest_Id = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Bulk approve stock requests (Status=1).
    /// </summary>
    public async Task<bool> BulkApproveAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var paramNames = ids.Select((id, i) => $"@id{i}").ToList();
            var sql = $"UPDATE tbl_PrdStockRequest SET Status = 1 WHERE PrdStockRequest_Id IN ({string.Join(",", paramNames)})";
            await using var cmd = new SqlCommand(sql, conn);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Bulk decline stock requests (Status=3).
    /// </summary>
    public async Task<bool> BulkDeclineAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var paramNames = ids.Select((id, i) => $"@id{i}").ToList();
            var sql = $"UPDATE tbl_PrdStockRequest SET Status = 3 WHERE PrdStockRequest_Id IN ({string.Join(",", paramNames)})";
            await using var cmd = new SqlCommand(sql, conn);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Soft-delete a single stock request (IsDeleted=1).
    /// </summary>
    public async Task<bool> DeleteAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var sql = "UPDATE tbl_PrdStockRequest SET IsDeleted = 1 WHERE PrdStockRequest_Id = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Bulk soft-delete stock requests (IsDeleted=1).
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();
            var paramNames = ids.Select((id, i) => $"@id{i}").ToList();
            var sql = $"UPDATE tbl_PrdStockRequest SET IsDeleted = 1 WHERE PrdStockRequest_Id IN ({string.Join(",", paramNames)})";
            await using var cmd = new SqlCommand(sql, conn);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Saves a reply to a stock request remarks thread.
    /// </summary>
    public async Task<RemarkReplyItem?> SaveReplyAsync(long requestId, int userId, string name, string message)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var sql = @"INSERT INTO tbl_PrdStockRequestRemarksReply 
                (PrdStockRequest_Id, PrdStockRequestRemarksReply_custID, 
                 PrdStockRequestRemarksReply_Name, PrdStockRequestRemarksReply_message, 
                 PrdStockRequestRemarksReply_modifiedOn)
                OUTPUT INSERTED.PrdStockRequestRemarksReply_ID, 
                       INSERTED.PrdStockRequestRemarksReply_Name,
                       INSERTED.PrdStockRequestRemarksReply_message, 
                       INSERTED.PrdStockRequestRemarksReply_modifiedOn
                VALUES (@requestId, @userId, @name, @message, @now)";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@requestId", requestId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@message", message);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RemarkReplyItem
                {
                    Id = reader.GetInt64(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ModifiedOn = reader.GetDateTime(3)
                };
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Gets replies for a stock request ordered by date DESC.
    /// </summary>
    public async Task<List<RemarkReplyItem>> GetRepliesAsync(long requestId)
    {
        var items = new List<RemarkReplyItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"SELECT PrdStockRequestRemarksReply_ID, PrdStockRequestRemarksReply_Name, 
                           PrdStockRequestRemarksReply_message, PrdStockRequestRemarksReply_modifiedOn
                    FROM tbl_PrdStockRequestRemarksReply 
                    WHERE PrdStockRequest_Id = @requestId
                    ORDER BY PrdStockRequestRemarksReply_modifiedOn DESC";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@requestId", requestId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new RemarkReplyItem
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ModifiedOn = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3)
            });
        }
        return items;
    }

    /// <summary>
    /// Searches products by keyword for autocomplete.
    /// </summary>
    public async Task<List<ProductSearchItem>> SearchProductsAsync(string keyword, string prdType, long webstoreId)
    {
        var items = new List<ProductSearchItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT product_id, product_name, product_image1 
                    FROM tbl_products 
                    WHERE product_type = @prdtype 
                      AND product_webstoreid = @wid 
                      AND product_isdeleted = 0 
                      AND product_name LIKE '%' + @keyword + '%'";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@prdtype", prdType);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@keyword", keyword);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ProductSearchItem
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return items;
    }

    /// <summary>
    /// Gets stock location HTML table for a product (recursive CTE, same as BakeryInventory).
    /// </summary>
    public async Task<string> GetStockLocationHtmlAsync(long productId, long webstoreId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();
        return await GetStockLocationHtmlInternalAsync(productId, conn, webstoreId);
    }

    private async Task<string> GetStockLocationHtmlInternalAsync(
        long productId, SqlConnection conn, long webstoreId = 82)
    {
        var sql = @";WITH RCTE AS (
            SELECT LocationID, LocationTitle, 
                   CAST(LocationTitle AS VARCHAR(2000)) AS FullLocation, 
                   ParentLocationId, 1 AS Lvl, DisplayOrder
            FROM tbl_location 
            WHERE ParentLocationId = 0 AND location_isactive = 1 
              AND location_isdeleted = 0 AND webstoreid = @wid

            UNION ALL

            SELECT rh.LocationID, rh.LocationTitle, 
                   CAST(rc.FullLocation + ' > ' + rh.LocationTitle AS VARCHAR(2000)) AS FullLocation, 
                   rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
            FROM tbl_location rh
            INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID 
            WHERE rh.Location_IsDeleted = 0 AND rh.location_isactive = 1
        )
        SELECT RCTE.LocationID, FullLocation, Qty, Product_Id 
        FROM RCTE 
        INNER JOIN tbl_StockLocation ON tbl_StockLocation.LocationID = RCTE.LocationID 
            AND Product_Id = @pid
        WHERE Lvl = 3 
        ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);

        var sb = new StringBuilder();
        var hasRows = false;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!hasRows)
            {
                sb.Append("<div id='dvToppers_location'><div class='form-group col-sm-12'>");
                sb.Append(@"<table id='tbToppers_location' class='table'>
                    <thead><tr><th></th><th>Location Name</th><th>Qty</th><th></th></tr></thead>
                    <tbody>");
                hasRows = true;
            }

            var fullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var qty = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
            sb.Append($"<tr><td></td><td>{fullLocation}</td><td>{qty}</td><td></td></tr>");
        }

        if (hasRows)
        {
            sb.Append("</tbody></table></div></div>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if a product is in an active PO.
    /// Returns HTML warning string if found, null otherwise.
    /// </summary>
    public async Task<string?> CheckProductInActivePOAsync(long productId, long webstoreId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"SELECT TOP 1 p.PO_ID, p.PO_SysNo, d.POdet_PrdID 
                    FROM tbl_PO p 
                    INNER JOIN tbl_POdet d ON p.PO_ID = d.POdet_POID 
                    WHERE (p.PO_isdeleted = 0) 
                      AND (PO_WebstoreID = @wid) 
                      AND (p.PO_PurDep_Status <> 3) 
                      AND (p.PO_Status <> 4) 
                      AND (d.POdet_PrdID = @pid) 
                    ORDER BY PO_createdOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var poId = reader.GetInt64(0);
            var poSysNo = reader.IsDBNull(1) ? "" : reader.GetString(1);
            return $"This product already exists in PO (<a target='_blank' style='color:#CF0000;' href='/printpurchaseorder?id={poId}'>{poSysNo}</a>)";
        }

        return null;
    }

    /// <summary>
    /// Gets staff list for user assignment dropdown.
    /// customer_type IN (2,3), webshopid=82, isActive=1.
    /// </summary>
    public async Task<List<StaffListItem>> GetStaffListAsync()
    {
        var items = new List<StaffListItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT customer_ID, customer_Name FROM tbl_bakeryuser 
                    WHERE customer_type IN (2,3) AND customer_webshopid = 82 AND customer_isActive = 1
                    ORDER BY customer_type, customer_Name";
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new StaffListItem
            {
                CustomerId = reader.GetInt64(0),
                CustomerName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }
}
