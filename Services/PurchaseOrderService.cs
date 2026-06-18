using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class POListItem
{
    public long PO_ID { get; set; }
    public string PO_SysNo { get; set; } = "";
    public DateTime PO_Date { get; set; }
    public string SupplierName { get; set; } = "";
    public decimal PO_TotalAmt { get; set; }
    public int PO_Status { get; set; }
    public int PO_PurDep_Status { get; set; }
    public int PO_Manager_Status { get; set; }
}

public class POListResult
{
    public List<POListItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int SentToSupplierCount { get; set; }
    public int CompletedCount { get; set; }
    public int DeclinedCount { get; set; }
}

public class PORemarkItem
{
    public long PORemarks_ID { get; set; }
    public string PORemarks_Name { get; set; } = "";
    public string PORemarks_message { get; set; } = "";
    public DateTime PORemarks_modifiedOn { get; set; }
}

public class PODetailModel
{
    public long PO_ID { get; set; }
    public string PO_SysNo { get; set; } = "";
    public DateTime PO_Date { get; set; }
    public long PO_SupplierID { get; set; }
    public List<PODetItem> LineItems { get; set; } = new();
}

public class PODetItem
{
    public long PrdStockRequest_Id { get; set; }
    public long POdet_PrdID { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int POdet_Qty { get; set; }
    public decimal POdet_RatePerItem { get; set; }
    public decimal POdet_Amount { get; set; }
    public decimal POdet_disc { get; set; }
    public decimal POdet_Subtotal { get; set; }
    public decimal POdet_VatPer { get; set; }
    public decimal POdet_Vat { get; set; }
    public decimal POdet_NetTotal { get; set; }
}

public class POSaveModel
{
    public long PO_ID { get; set; }
    public long PO_SupplierID { get; set; }
    public DateTime PO_Date { get; set; }
    public List<PODetSaveItem> lstPOdet { get; set; } = new();
}

public class PODetSaveItem
{
    public long PrdStockRequest_Id { get; set; }
    public long POdet_PrdID { get; set; }
    public int POdet_Qty { get; set; }
    public decimal POdet_RatePerItem { get; set; }
    public decimal POdet_Amount { get; set; }
    public decimal POdet_disc { get; set; }
    public decimal POdet_Subtotal { get; set; }
    public decimal POdet_VatPer { get; set; }
    public decimal POdet_Vat { get; set; }
    public decimal POdet_NetTotal { get; set; }
}

public class SupplierListItem
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
}

public class SupplierDetailModel
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string Supplier_AddressDetail { get; set; } = "";
    public string Supplier_Remarks { get; set; } = "";
    public bool Supplier_IsTopper { get; set; }
    public bool Supplier_IsAccessory { get; set; }
}

// ─── Item Received Models ───────────────────────────────────────────────────────

public class ItemReceivedSaveModel
{
    public long PO_ID { get; set; }
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime ReceivedDate { get; set; }
    public long ReceivedBy { get; set; }
    public string Remarks { get; set; } = "";
    public List<ItemReceivedDetailModel> Items { get; set; } = new();
}

public class ItemReceivedDetailModel
{
    public long POdet_PrdID { get; set; }
    public long PODet_LocationID { get; set; }
    public long PrdStockRequest_Id { get; set; }
    public int POdet_Qty { get; set; }
    public decimal POdet_RatePerItem { get; set; }
    public decimal POdet_Amount { get; set; }
    public decimal POdet_disc { get; set; }
    public decimal POdet_Subtotal { get; set; }
    public decimal POdet_VatPer { get; set; }
    public decimal POdet_Vat { get; set; }
    public decimal POdet_NetTotal { get; set; }
}

public class StockLocationItem
{
    public long LocationId { get; set; }
    public string FullLocation { get; set; } = "";
}

public class StaffListItem
{
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
}

public class POItemReceivedViewModel
{
    public long PO_ID { get; set; }
    public string PO_SysNo { get; set; } = "";
    public DateTime PO_Date { get; set; }
    public string SupplierName { get; set; } = "";
    public List<PODetItem> LineItems { get; set; } = new();
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Purchase Order management.
/// Migrated from managepurchaseorder.aspx / addpurchaseorder.aspx.
/// Uses BusinessConnection for tbl_PO, tbl_POdet, tbl_PORemarks.
/// Uses DefaultConnection for tbl_ProductSupplier, tbl_products.
/// </summary>
public class PurchaseOrderService
{
    private readonly string _businessConnection;
    private readonly string _defaultConnection;

    public PurchaseOrderService(IConfiguration config)
    {
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated PO list with status counts.
    /// </summary>
    public async Task<POListResult> GetPurchaseOrderListAsync(int page, int pageSize, string search, int status)
    {
        var result = new POListResult();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Get status counts
        var countSql = @"
            SELECT 
                SUM(CASE WHEN PO_Status = 0 AND PO_isdeleted = 0 THEN 1 ELSE 0 END) AS PendingCount,
                SUM(CASE WHEN PO_Status IN (1,2) AND PO_isdeleted = 0 THEN 1 ELSE 0 END) AS ApprovedCount,
                SUM(CASE WHEN PO_Status = 3 AND PO_isdeleted = 0 THEN 1 ELSE 0 END) AS SentToSupplierCount,
                SUM(CASE WHEN PO_Status = 4 AND PO_isdeleted = 0 THEN 1 ELSE 0 END) AS CompletedCount,
                SUM(CASE WHEN PO_isdeleted = 1 THEN 1 ELSE 0 END) AS DeclinedCount
            FROM tbl_PO";

        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            await using var reader = await countCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.PendingCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                result.ApprovedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                result.SentToSupplierCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                result.CompletedCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                result.DeclinedCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            }
        }

        // Build WHERE clause
        var where = "";
        if (status == 0)
            where = "WHERE p.PO_Status = 0 AND p.PO_isdeleted = 0";
        else if (status == 1 || status == 2)
            where = "WHERE p.PO_Status IN (1,2) AND p.PO_isdeleted = 0";
        else if (status == 3)
            where = "WHERE p.PO_Status = 3 AND p.PO_isdeleted = 0";
        else if (status == 4)
            where = "WHERE p.PO_Status = 4 AND p.PO_isdeleted = 0";
        else if (status == 5) // declined
            where = "WHERE p.PO_isdeleted = 1";
        else
            where = "WHERE p.PO_isdeleted = 0";

        if (!string.IsNullOrWhiteSpace(search))
            where += " AND (p.PO_SysNo LIKE @search)";

        // Get total count for pagination
        var totalSql = $"SELECT COUNT(1) FROM tbl_PO p {where}";
        int totalCount = 0;
        await using (var totalCmd = new SqlCommand(totalSql, conn))
        {
            if (!string.IsNullOrWhiteSpace(search))
                totalCmd.Parameters.AddWithValue("@search", "%" + search + "%");
            totalCount = Convert.ToInt32(await totalCmd.ExecuteScalarAsync());
        }

        result.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Get page data - join with DefaultConnection supplier via subquery
        var offset = (page - 1) * pageSize;
        var listSql = $@"
            SELECT p.PO_ID, p.PO_SysNo, p.PO_Date, p.PO_TotalAmt, 
                   p.PO_Status, p.PO_PurDep_Status, p.PO_Manager_Status, p.PO_SupplierID
            FROM tbl_PO p
            {where}
            ORDER BY p.PO_ID DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var items = new List<POListItem>();
        var supplierIds = new List<long>();

        await using (var listCmd = new SqlCommand(listSql, conn))
        {
            if (!string.IsNullOrWhiteSpace(search))
                listCmd.Parameters.AddWithValue("@search", "%" + search + "%");
            listCmd.Parameters.AddWithValue("@offset", offset);
            listCmd.Parameters.AddWithValue("@pageSize", pageSize);

            await using var reader = await listCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new POListItem
                {
                    PO_ID = reader.GetInt64(0),
                    PO_SysNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PO_Date = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                    PO_TotalAmt = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    PO_Status = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    PO_PurDep_Status = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    PO_Manager_Status = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                };
                var supplierId = reader.IsDBNull(7) ? 0L : reader.GetInt64(7);
                supplierIds.Add(supplierId);
                items.Add(item);
            }
        }

        // Fetch supplier names from DefaultConnection
        if (supplierIds.Count > 0)
        {
            var distinctIds = supplierIds.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                await using var defConn = new SqlConnection(_defaultConnection);
                await defConn.OpenAsync();

                // Build parameterized IN clause
                var paramNames = distinctIds.Select((id, i) => $"@sid{i}").ToList();
                var inClause = string.Join(",", paramNames);
                var supSql = $"SELECT SupplierId, SupplierName FROM tbl_ProductSupplier WHERE SupplierId IN ({inClause})";

                await using var supCmd = new SqlCommand(supSql, defConn);
                for (int i = 0; i < distinctIds.Count; i++)
                    supCmd.Parameters.AddWithValue($"@sid{i}", distinctIds[i]);

                var supplierMap = new Dictionary<long, string>();
                await using var supReader = await supCmd.ExecuteReaderAsync();
                while (await supReader.ReadAsync())
                {
                    var sid = supReader.GetInt64(0);
                    var sname = supReader.IsDBNull(1) ? "" : supReader.GetString(1);
                    supplierMap[sid] = sname;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    if (supplierIds[i] > 0 && supplierMap.ContainsKey(supplierIds[i]))
                        items[i].SupplierName = supplierMap[supplierIds[i]];
                }
            }
        }

        result.Items = items;
        return result;
    }

    /// <summary>
    /// Approve PO by purchase dept (isPurchaseDept=true) or manager (isPurchaseDept=false).
    /// </summary>
    public async Task<bool> ApprovePOAsync(long poId, int userId, bool isPurchaseDept)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var col = isPurchaseDept ? "PO_PurDep_Status" : "PO_Manager_Status";
            var sql1 = $"UPDATE tbl_PO SET {col} = 1 WHERE PO_ID = @poId";
            await using (var cmd1 = new SqlCommand(sql1, conn))
            {
                cmd1.Parameters.AddWithValue("@poId", poId);
                await cmd1.ExecuteNonQueryAsync();
            }

            // Recalculate PO_Status
            var sql2 = @"UPDATE tbl_PO SET PO_Status = CASE 
                WHEN PO_PurDep_Status = 1 AND PO_Manager_Status = 1 THEN 2 
                ELSE 1 END WHERE PO_ID = @poId";
            await using (var cmd2 = new SqlCommand(sql2, conn))
            {
                cmd2.Parameters.AddWithValue("@poId", poId);
                await cmd2.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Decline PO: set PO_isdeleted=1, delete POdet and PORemarks.
    /// </summary>
    public async Task<bool> DeclinePOAsync(long poId)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var sql1 = "UPDATE tbl_PO SET PO_isdeleted = 1 WHERE PO_ID = @poId";
            await using (var cmd1 = new SqlCommand(sql1, conn))
            {
                cmd1.Parameters.AddWithValue("@poId", poId);
                await cmd1.ExecuteNonQueryAsync();
            }

            var sql2 = "DELETE FROM tbl_POdet WHERE POdet_POID = @poId";
            await using (var cmd2 = new SqlCommand(sql2, conn))
            {
                cmd2.Parameters.AddWithValue("@poId", poId);
                await cmd2.ExecuteNonQueryAsync();
            }

            var sql3 = "DELETE FROM tbl_PORemarks WHERE PO_ID = @poId";
            await using (var cmd3 = new SqlCommand(sql3, conn))
            {
                cmd3.Parameters.AddWithValue("@poId", poId);
                await cmd3.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Send PO to supplier: set PO_Status=3, add log.
    /// </summary>
    public async Task<bool> SendToSupplierAsync(long poId, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var sql = "UPDATE tbl_PO SET PO_Status = 3 WHERE PO_ID = @poId";
            await using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", poId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Add PO log (legacy: clsPOHelper.AddPOLog)
            var logSql = @"INSERT INTO tbl_POLog (PO_ID, POLog_UserID, POLog_Status, POLog_Date) 
                           VALUES (@poId, @userId, 3, @now)";
            await using (var logCmd = new SqlCommand(logSql, conn))
            {
                logCmd.Parameters.AddWithValue("@poId", poId);
                logCmd.Parameters.AddWithValue("@userId", userId);
                logCmd.Parameters.AddWithValue("@now", DateTime.Now);
                try { await logCmd.ExecuteNonQueryAsync(); } catch { /* log table may not exist */ }
            }

            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Save a remark to tbl_PORemarks.
    /// </summary>
    public async Task<PORemarkItem?> SaveRemarkAsync(long poId, int userId, string name, string message)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var sql = @"INSERT INTO tbl_PORemarks (PO_ID, PORemarks_custID, PORemarks_Name, PORemarks_message, PORemarks_modifiedOn)
                        OUTPUT INSERTED.PORemarks_ID, INSERTED.PORemarks_Name, INSERTED.PORemarks_message, INSERTED.PORemarks_modifiedOn
                        VALUES (@poId, @userId, @name, @message, @now)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@poId", poId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@message", message);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new PORemarkItem
                {
                    PORemarks_ID = reader.GetInt64(0),
                    PORemarks_Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PORemarks_message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    PORemarks_modifiedOn = reader.GetDateTime(3)
                };
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Get remarks for a PO.
    /// </summary>
    public async Task<List<PORemarkItem>> GetRemarksAsync(long poId)
    {
        var items = new List<PORemarkItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT PORemarks_ID, PORemarks_Name, PORemarks_message, PORemarks_modifiedOn FROM tbl_PORemarks WHERE PO_ID = @poId ORDER BY PORemarks_modifiedOn DESC";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@poId", poId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PORemarkItem
            {
                PORemarks_ID = reader.GetInt64(0),
                PORemarks_Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                PORemarks_message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                PORemarks_modifiedOn = reader.GetDateTime(3)
            });
        }
        return items;
    }

    /// <summary>
    /// Get PO detail with line items for edit.
    /// </summary>
    public async Task<PODetailModel?> GetPODetailAsync(long poId)
    {
        PODetailModel? model = null;
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT PO_ID, PO_SysNo, PO_Date, PO_SupplierID FROM tbl_PO WHERE PO_ID = @poId";
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@poId", poId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                model = new PODetailModel
                {
                    PO_ID = reader.GetInt64(0),
                    PO_SysNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PO_Date = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
                    PO_SupplierID = reader.IsDBNull(3) ? 0 : reader.GetInt64(3)
                };
            }
        }

        if (model == null) return null;

        // Get line items
        var detSql = @"SELECT d.PrdStockRequest_Id, d.POdet_PrdID, d.POdet_Qty, d.POdet_RatePerItem, 
                              d.POdet_Amount, d.POdet_disc, d.POdet_Subtotal, d.POdet_VatPer, d.POdet_Vat, d.POdet_NetTotal
                       FROM tbl_POdet d WHERE d.POdet_POID = @poId ORDER BY d.POdet_displayOrder";
        var lineItems = new List<PODetItem>();
        var productIds = new List<long>();

        await using (var detCmd = new SqlCommand(detSql, conn))
        {
            detCmd.Parameters.AddWithValue("@poId", poId);
            await using var reader = await detCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new PODetItem
                {
                    PrdStockRequest_Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    POdet_PrdID = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    POdet_Qty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    POdet_RatePerItem = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    POdet_Amount = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    POdet_disc = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    POdet_Subtotal = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    POdet_VatPer = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    POdet_Vat = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                    POdet_NetTotal = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                };
                productIds.Add(item.POdet_PrdID);
                lineItems.Add(item);
            }
        }

        // Fetch product names from DefaultConnection
        if (productIds.Count > 0)
        {
            var distinctPids = productIds.Where(id => id > 0).Distinct().ToList();
            if (distinctPids.Count > 0)
            {
                await using var defConn = new SqlConnection(_defaultConnection);
                await defConn.OpenAsync();

                var paramNames = distinctPids.Select((id, i) => $"@pid{i}").ToList();
                var inClause = string.Join(",", paramNames);
                var prdSql = $"SELECT product_ID, product_Name, product_code FROM tbl_products WHERE product_ID IN ({inClause})";

                await using var prdCmd = new SqlCommand(prdSql, defConn);
                for (int i = 0; i < distinctPids.Count; i++)
                    prdCmd.Parameters.AddWithValue($"@pid{i}", distinctPids[i]);

                var prdMap = new Dictionary<long, (string Name, string Code)>();
                await using var prdReader = await prdCmd.ExecuteReaderAsync();
                while (await prdReader.ReadAsync())
                {
                    var pid = prdReader.GetInt64(0);
                    var pname = prdReader.IsDBNull(1) ? "" : prdReader.GetString(1);
                    var pcode = prdReader.IsDBNull(2) ? "" : prdReader.GetString(2);
                    prdMap[pid] = (pname, pcode);
                }

                foreach (var item in lineItems)
                {
                    if (prdMap.ContainsKey(item.POdet_PrdID))
                    {
                        item.ProductName = prdMap[item.POdet_PrdID].Name;
                        item.ProductCode = prdMap[item.POdet_PrdID].Code;
                    }
                }
            }
        }

        model.LineItems = lineItems;
        return model;
    }

    /// <summary>
    /// Save (create or update) a PO with line items.
    /// </summary>
    public async Task<string> SavePurchaseOrderAsync(POSaveModel model, int userId, long webshopId)
    {
        try
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            long poId = model.PO_ID;
            var totalAmt = model.lstPOdet.Sum(s => s.POdet_NetTotal);
            var totalDisc = model.lstPOdet.Sum(s => s.POdet_disc);
            var totalQty = model.lstPOdet.Sum(s => s.POdet_Qty);
            var totalTax = model.lstPOdet.Sum(s => s.POdet_Vat);

            if (poId == 0)
            {
                // Create new PO
                var sysNo = await GenerateInvoiceNumberAsync();
                var insertSql = @"INSERT INTO tbl_PO 
                    (PO_SysNo, PO_Date, PO_SupplierID, PO_WebstoreID, PO_TotalAmt, PO_TotalDisc, PO_TotalQty, PO_TotalTax,
                     PO_createdOn, PO_createdBy, PO_ModifiedOn, PO_ModifiedBy, PO_isdeleted, PO_Manager_Status, PO_PurDep_Status, PO_Status)
                    OUTPUT INSERTED.PO_ID
                    VALUES (@sysNo, @date, @supplierId, @webshopId, @totalAmt, @totalDisc, @totalQty, @totalTax,
                            @now, @userId, @now, @userId, 0, 0, 0, 0)";

                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@sysNo", sysNo);
                insertCmd.Parameters.AddWithValue("@date", model.PO_Date);
                insertCmd.Parameters.AddWithValue("@supplierId", model.PO_SupplierID);
                insertCmd.Parameters.AddWithValue("@webshopId", webshopId);
                insertCmd.Parameters.AddWithValue("@totalAmt", totalAmt);
                insertCmd.Parameters.AddWithValue("@totalDisc", totalDisc);
                insertCmd.Parameters.AddWithValue("@totalQty", totalQty);
                insertCmd.Parameters.AddWithValue("@totalTax", totalTax);
                insertCmd.Parameters.AddWithValue("@now", DateTime.Now);
                insertCmd.Parameters.AddWithValue("@userId", userId);

                var result = await insertCmd.ExecuteScalarAsync();
                poId = Convert.ToInt64(result);
            }
            else
            {
                // Update existing PO
                var updateSql = @"UPDATE tbl_PO SET 
                    PO_Date = @date, PO_SupplierID = @supplierId, PO_TotalAmt = @totalAmt,
                    PO_TotalDisc = @totalDisc, PO_TotalQty = @totalQty, PO_TotalTax = @totalTax,
                    PO_ModifiedOn = @now, PO_ModifiedBy = @userId
                    WHERE PO_ID = @poId";

                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@date", model.PO_Date);
                updateCmd.Parameters.AddWithValue("@supplierId", model.PO_SupplierID);
                updateCmd.Parameters.AddWithValue("@totalAmt", totalAmt);
                updateCmd.Parameters.AddWithValue("@totalDisc", totalDisc);
                updateCmd.Parameters.AddWithValue("@totalQty", totalQty);
                updateCmd.Parameters.AddWithValue("@totalTax", totalTax);
                updateCmd.Parameters.AddWithValue("@now", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@userId", userId);
                updateCmd.Parameters.AddWithValue("@poId", poId);
                await updateCmd.ExecuteNonQueryAsync();

                // Delete existing line items
                var delSql = "DELETE FROM tbl_POdet WHERE POdet_POID = @poId";
                await using var delCmd = new SqlCommand(delSql, conn);
                delCmd.Parameters.AddWithValue("@poId", poId);
                await delCmd.ExecuteNonQueryAsync();
            }

            // Insert line items
            for (int i = 0; i < model.lstPOdet.Count; i++)
            {
                var det = model.lstPOdet[i];
                var detSql = @"INSERT INTO tbl_POdet 
                    (POdet_POID, POdet_PrdID, POdet_Qty, POdet_RatePerItem, POdet_Amount, POdet_disc, 
                     POdet_Subtotal, POdet_VatPer, POdet_Vat, POdet_NetTotal, POdet_displayOrder, POdet_CreateOn, PrdStockRequest_Id)
                    VALUES (@poId, @prdId, @qty, @rate, @amount, @disc, @subtotal, @vatPer, @vat, @netTotal, @order, @now, @reqId)";

                await using var detCmd = new SqlCommand(detSql, conn);
                detCmd.Parameters.AddWithValue("@poId", poId);
                detCmd.Parameters.AddWithValue("@prdId", det.POdet_PrdID);
                detCmd.Parameters.AddWithValue("@qty", det.POdet_Qty);
                detCmd.Parameters.AddWithValue("@rate", det.POdet_RatePerItem);
                detCmd.Parameters.AddWithValue("@amount", det.POdet_Amount);
                detCmd.Parameters.AddWithValue("@disc", det.POdet_disc);
                detCmd.Parameters.AddWithValue("@subtotal", det.POdet_Subtotal);
                detCmd.Parameters.AddWithValue("@vatPer", det.POdet_VatPer);
                detCmd.Parameters.AddWithValue("@vat", det.POdet_Vat);
                detCmd.Parameters.AddWithValue("@netTotal", det.POdet_NetTotal);
                detCmd.Parameters.AddWithValue("@order", i + 1);
                detCmd.Parameters.AddWithValue("@now", DateTime.Now);
                detCmd.Parameters.AddWithValue("@reqId", det.PrdStockRequest_Id);
                await detCmd.ExecuteNonQueryAsync();
            }

            return "1";
        }
        catch { return "0"; }
    }

    /// <summary>
    /// Autocomplete search for suppliers.
    /// </summary>
    public async Task<List<SupplierListItem>> GetSupplierListAsync(string keyword)
    {
        var items = new List<SupplierListItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT TOP 10 SupplierId, SupplierName FROM tbl_ProductSupplier 
                    WHERE SupplierName LIKE @keyword AND Suppllier_IsDeleted = 0 AND Supplier_IsActive = 1 
                    ORDER BY SupplierName";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@keyword", keyword + "%");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SupplierListItem
            {
                SupplierId = reader.GetInt64(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }

    /// <summary>
    /// Get supplier details.
    /// </summary>
    public async Task<SupplierDetailModel?> GetSupplierDetailAsync(long supplierId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT SupplierId, SupplierName, Supplier_AddressDetail, Supplier_Remarks, Supplier_IsTopper, Supplier_IsAccessory 
                    FROM tbl_ProductSupplier 
                    WHERE SupplierId = @id AND Suppllier_IsDeleted = 0 AND Supplier_IsActive = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", supplierId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SupplierDetailModel
            {
                SupplierId = reader.GetInt64(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Supplier_AddressDetail = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Supplier_Remarks = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Supplier_IsTopper = !reader.IsDBNull(4) && reader.GetBoolean(4),
                Supplier_IsAccessory = !reader.IsDBNull(5) && reader.GetBoolean(5)
            };
        }
        return null;
    }

    /// <summary>
    /// Auto-increment PO number (7-digit zero-padded).
    /// </summary>
    public async Task<string> GenerateInvoiceNumberAsync()
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT MAX(PO_ID) FROM tbl_PO WHERE PO_isdeleted = 0";
        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        long nextId = (result == null || result == DBNull.Value) ? 1 : Convert.ToInt64(result) + 1;
        return nextId.ToString("D7");
    }

    // ─── Item Received Methods ─────────────────────────────────────────────────

    /// <summary>
    /// Gets PO header + line items for Item Received page. Validates status == 3.
    /// </summary>
    public async Task<POItemReceivedViewModel?> GetPOForItemReceivedAsync(long poId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Get PO header and validate status
        var sql = "SELECT PO_ID, PO_SysNo, PO_Date, PO_SupplierID, PO_Status FROM tbl_PO WHERE PO_ID = @poId AND PO_isdeleted = 0";
        long supplierId = 0;
        POItemReceivedViewModel? model = null;

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@poId", poId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var status = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                if (status != 3) return null; // Only status 3 (Sent to Supplier) allowed

                model = new POItemReceivedViewModel
                {
                    PO_ID = reader.GetInt64(0),
                    PO_SysNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PO_Date = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2)
                };
                supplierId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
            }
        }

        if (model == null) return null;

        // Get line items
        var detSql = @"SELECT d.PrdStockRequest_Id, d.POdet_PrdID, d.POdet_Qty, d.POdet_RatePerItem, 
                              d.POdet_Amount, d.POdet_disc, d.POdet_Subtotal, d.POdet_VatPer, d.POdet_Vat, d.POdet_NetTotal
                       FROM tbl_POdet d WHERE d.POdet_POID = @poId ORDER BY d.POdet_displayOrder";
        var lineItems = new List<PODetItem>();
        var productIds = new List<long>();

        await using (var detCmd = new SqlCommand(detSql, conn))
        {
            detCmd.Parameters.AddWithValue("@poId", poId);
            await using var reader = await detCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new PODetItem
                {
                    PrdStockRequest_Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    POdet_PrdID = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    POdet_Qty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    POdet_RatePerItem = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    POdet_Amount = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    POdet_disc = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    POdet_Subtotal = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    POdet_VatPer = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    POdet_Vat = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                    POdet_NetTotal = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                };
                productIds.Add(item.POdet_PrdID);
                lineItems.Add(item);
            }
        }

        // Fetch product names + supplier name from DefaultConnection
        await using var defConn = new SqlConnection(_defaultConnection);
        await defConn.OpenAsync();

        if (productIds.Count > 0)
        {
            var distinctPids = productIds.Where(id => id > 0).Distinct().ToList();
            if (distinctPids.Count > 0)
            {
                var paramNames = distinctPids.Select((id, i) => $"@pid{i}").ToList();
                var inClause = string.Join(",", paramNames);
                var prdSql = $"SELECT product_ID, product_Name, product_code FROM tbl_products WHERE product_ID IN ({inClause})";

                await using var prdCmd = new SqlCommand(prdSql, defConn);
                for (int i = 0; i < distinctPids.Count; i++)
                    prdCmd.Parameters.AddWithValue($"@pid{i}", distinctPids[i]);

                var prdMap = new Dictionary<long, (string Name, string Code)>();
                await using var prdReader = await prdCmd.ExecuteReaderAsync();
                while (await prdReader.ReadAsync())
                {
                    var pid = prdReader.GetInt64(0);
                    var pname = prdReader.IsDBNull(1) ? "" : prdReader.GetString(1);
                    var pcode = prdReader.IsDBNull(2) ? "" : prdReader.GetString(2);
                    prdMap[pid] = (pname, pcode);
                }

                foreach (var item in lineItems)
                {
                    if (prdMap.ContainsKey(item.POdet_PrdID))
                    {
                        item.ProductName = prdMap[item.POdet_PrdID].Name;
                        item.ProductCode = prdMap[item.POdet_PrdID].Code;
                    }
                }
            }
        }

        // Get supplier name
        if (supplierId > 0)
        {
            var supSql = "SELECT SupplierName FROM tbl_ProductSupplier WHERE SupplierId = @sid";
            await using var supCmd = new SqlCommand(supSql, defConn);
            supCmd.Parameters.AddWithValue("@sid", supplierId);
            var supName = await supCmd.ExecuteScalarAsync();
            model.SupplierName = supName?.ToString() ?? "";
        }

        model.LineItems = lineItems;
        return model;
    }

    /// <summary>
    /// Gets stock locations for a product using the legacy CTE query.
    /// Uses DefaultConnection (tbl_location, tbl_StockLocation).
    /// </summary>
    public async Task<List<StockLocationItem>> GetStockLocationsForProductAsync(long productId, long webshopId)
    {
        var items = new List<StockLocationItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @";WITH RCTE AS (
            SELECT LocationID, LocationTitle, CAST(LocationTitle AS VARCHAR(2000)) AS FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
            FROM tbl_location WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid
            UNION ALL
            SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle AS VARCHAR(2000)) AS FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder
            FROM dbo.tbl_location rh INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID
            WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
        )
        SELECT l.LocationID, FullLocation FROM RCTE l
        INNER JOIN tbl_StockLocation p ON l.LocationID = p.LocationID
        WHERE Lvl = 3 AND p.Product_Id = @pid
        ORDER BY DisplayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new StockLocationItem
            {
                LocationId = reader.GetInt64(0),
                FullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return items;
    }

    /// <summary>
    /// Gets staff list for "Received By" dropdown.
    /// Uses DefaultConnection (tbl_bakeryuser).
    /// </summary>
    public async Task<List<StaffListItem>> GetStaffListForReceivedAsync(long webshopId)
    {
        var items = new List<StaffListItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT customer_ID, customer_Name FROM tbl_bakeryuser 
                    WHERE customer_type IN (2,3) AND customer_webshopid = @wid AND customer_isActive = 1
                    ORDER BY customer_type, customer_Name";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);

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

    /// <summary>
    /// Saves Item Received: inserts header + detail rows, updates batch IDs, sets PO status to 4, logs.
    /// All in a transaction. Returns the PO_ItemsRec_ID on success, 0 on failure.
    /// </summary>
    public async Task<long> SaveItemReceivedAsync(ItemReceivedSaveModel model, int userId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            var totalAmt = model.Items.Sum(s => s.POdet_NetTotal);
            var totalDisc = model.Items.Sum(s => s.POdet_disc);
            var totalQty = model.Items.Sum(s => s.POdet_Qty);
            var totalTax = model.Items.Sum(s => s.POdet_Vat);

            // 1. INSERT into tbl_PO_ItemsRec (header)
            var headerSql = @"INSERT INTO tbl_PO_ItemsRec 
                (PO_ID, PO_InvoiceNo, PO_InvoiceDate, PO_ReceivedDate, PO_ReceivedBy, PO_Remarks, PO_InvoiceFile,
                 PO_TotalAmt, PO_TotalDisc, PO_TotalQty, PO_TotalTax, PO_ItemTotal, PO_isdeleted, PO_createdOn, PO_createdBy)
                OUTPUT INSERTED.PO_ItemsRec_ID
                VALUES (@poId, @invoiceNo, @invoiceDate, @receivedDate, @receivedBy, @remarks, '',
                        @totalAmt, @totalDisc, @totalQty, @totalTax, @itemTotal, 0, @now, @userId)";

            long headerRecId = 0;
            await using (var headerCmd = new SqlCommand(headerSql, conn, transaction))
            {
                headerCmd.Parameters.AddWithValue("@poId", model.PO_ID);
                headerCmd.Parameters.AddWithValue("@invoiceNo", model.InvoiceNo ?? "");
                headerCmd.Parameters.AddWithValue("@invoiceDate", model.InvoiceDate);
                headerCmd.Parameters.AddWithValue("@receivedDate", model.ReceivedDate);
                headerCmd.Parameters.AddWithValue("@receivedBy", model.ReceivedBy);
                headerCmd.Parameters.AddWithValue("@remarks", model.Remarks ?? "");
                headerCmd.Parameters.AddWithValue("@totalAmt", totalAmt);
                headerCmd.Parameters.AddWithValue("@totalDisc", totalDisc);
                headerCmd.Parameters.AddWithValue("@totalQty", totalQty);
                headerCmd.Parameters.AddWithValue("@totalTax", totalTax);
                headerCmd.Parameters.AddWithValue("@itemTotal", totalAmt);
                headerCmd.Parameters.AddWithValue("@now", DateTime.Now);
                headerCmd.Parameters.AddWithValue("@userId", userId);

                var result = await headerCmd.ExecuteScalarAsync();
                headerRecId = Convert.ToInt64(result);
            }

            // 2. INSERT into tbl_POdet_ItemsRec (line items)
            for (int i = 0; i < model.Items.Count; i++)
            {
                var det = model.Items[i];
                var detSql = @"INSERT INTO tbl_POdet_ItemsRec 
                    (POdet_POID, POdet_mainPOID, POdet_PrdID, PODet_LocationID, POdet_Qty, POdet_RatePerItem, 
                     POdet_Amount, POdet_disc, POdet_Subtotal, POdet_VatPer, POdet_Vat, POdet_NetTotal, 
                     POdet_displayOrder, POdet_CreateOn, PrdStockRequest_Id, PODet_BatchID)
                    VALUES (@poid, @mainPoid, @prdId, @locationId, @qty, @rate, 
                            @amount, @disc, @subtotal, @vatPer, @vat, @netTotal, 
                            @order, @now, @reqId, '')";

                await using var detCmd = new SqlCommand(detSql, conn, transaction);
                detCmd.Parameters.AddWithValue("@poid", headerRecId);
                detCmd.Parameters.AddWithValue("@mainPoid", model.PO_ID);
                detCmd.Parameters.AddWithValue("@prdId", det.POdet_PrdID);
                detCmd.Parameters.AddWithValue("@locationId", det.PODet_LocationID);
                detCmd.Parameters.AddWithValue("@qty", det.POdet_Qty);
                detCmd.Parameters.AddWithValue("@rate", det.POdet_RatePerItem);
                detCmd.Parameters.AddWithValue("@amount", det.POdet_Amount);
                detCmd.Parameters.AddWithValue("@disc", det.POdet_disc);
                detCmd.Parameters.AddWithValue("@subtotal", det.POdet_Subtotal);
                detCmd.Parameters.AddWithValue("@vatPer", det.POdet_VatPer);
                detCmd.Parameters.AddWithValue("@vat", det.POdet_Vat);
                detCmd.Parameters.AddWithValue("@netTotal", det.POdet_NetTotal);
                detCmd.Parameters.AddWithValue("@order", i + 1);
                detCmd.Parameters.AddWithValue("@now", DateTime.Now);
                detCmd.Parameters.AddWithValue("@reqId", det.PrdStockRequest_Id);
                await detCmd.ExecuteNonQueryAsync();
            }

            // 3. UPDATE batch IDs
            var batchSql = @"UPDATE d SET d.PODet_BatchID = p.PO_SysNo + '-' + FORMAT(PO_InvoiceDate, 'ddMMyyyy') + '-' + CAST(d.POdet_ID AS NVARCHAR(10)) 
                             FROM tbl_PO p 
                             INNER JOIN tbl_PO_ItemsRec ie ON p.PO_ID = ie.PO_ID 
                             INNER JOIN tbl_POdet_ItemsRec d ON ie.PO_ItemsRec_ID = d.POdet_POID 
                             WHERE ie.PO_ItemsRec_ID = @id";
            await using (var batchCmd = new SqlCommand(batchSql, conn, transaction))
            {
                batchCmd.Parameters.AddWithValue("@id", headerRecId);
                await batchCmd.ExecuteNonQueryAsync();
            }

            // 4. UPDATE tbl_PO SET PO_Status = 4
            var statusSql = "UPDATE tbl_PO SET PO_Status = 4 WHERE PO_ID = @poId";
            await using (var statusCmd = new SqlCommand(statusSql, conn, transaction))
            {
                statusCmd.Parameters.AddWithValue("@poId", model.PO_ID);
                await statusCmd.ExecuteNonQueryAsync();
            }

            // 5. INSERT PO log (status=4)
            var logSql = @"INSERT INTO tbl_POLog (PO_ID, POLog_UserID, POLog_Status, POLog_Date) 
                           VALUES (@poId, @userId, 4, @now)";
            await using (var logCmd = new SqlCommand(logSql, conn, transaction))
            {
                logCmd.Parameters.AddWithValue("@poId", model.PO_ID);
                logCmd.Parameters.AddWithValue("@userId", userId);
                logCmd.Parameters.AddWithValue("@now", DateTime.Now);
                try { await logCmd.ExecuteNonQueryAsync(); } catch { /* log table may not exist */ }
            }

            await transaction.CommitAsync();
            return headerRecId;
        }
        catch
        {
            await transaction.RollbackAsync();
            return 0;
        }
    }
}
