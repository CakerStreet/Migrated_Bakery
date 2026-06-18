using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services
{
    public class PrintDocumentService
    {
        private readonly string _defaultConnection;
        private readonly string _businessConnection;
        private readonly string _eposConnection;
        private readonly string _eposAdminConnection;

        public PrintDocumentService(IConfiguration config)
        {
            _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
            _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
            _eposConnection = config.GetConnectionString("EposConnection") ?? "";
            _eposAdminConnection = config.GetConnectionString("EposAdminConnection") ?? "";
        }

        // ─── Purchase Order Printing ──────────────────────────────────────────────

        public async Task<POPrintResult?> GetPurchaseOrderPrintAsync(long poId)
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var poSql = "SELECT PO_SysNo, PO_Date, PO_SupplierID, PO_Status, PO_WebstoreID FROM tbl_PO WHERE PO_ID = @poId AND PO_isdeleted = 0";
            long supplierId = 0;
            long webstoreId = 0;
            POPrintResult? result = null;

            await using (var cmd = new SqlCommand(poSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", poId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result = new POPrintResult
                    {
                        PO_ID = poId,
                        PO_SysNo = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        PO_Date = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                        PO_Status = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3))
                    };
                    supplierId = reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2));
                    webstoreId = reader.IsDBNull(4) ? 0L : Convert.ToInt64(reader.GetValue(4));
                }
            }

            if (result == null) return null;

            // Load Supplier Detail from DefaultConnection
            if (supplierId > 0)
            {
                await using var defConn = new SqlConnection(_defaultConnection);
                await defConn.OpenAsync();

                var supSql = "SELECT SupplierName, Supplier_AddressDetail, Supplier_Remarks, Supplier_IsTopper, Supplier_IsAccessory FROM tbl_ProductSupplier WHERE SupplierId = @sid";
                await using var cmd = new SqlCommand(supSql, defConn);
                cmd.Parameters.AddWithValue("@sid", supplierId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.SupplierName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    result.SupplierAddress = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    result.SupplierRemarks = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    result.SupplierIsTopper = !reader.IsDBNull(3) && reader.GetBoolean(3);
                    result.SupplierIsAccessory = !reader.IsDBNull(4) && reader.GetBoolean(4);
                }
            }

            // Load items
            var itemsSql = @";WITH RCTE AS
            (
                SELECT LocationID, LocationTitle, CAST(LocationTitle as varchar(2000)) as FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
                FROM db_cakerstreet_live.dbo.tbl_location WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid  
                UNION ALL
                SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle  as varchar(2000)) as FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
                FROM db_cakerstreet_live.dbo.tbl_location rh
                INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
            ) SELECT LocationID, FullLocation INTO #t FROM RCTE WHERE Lvl = 3 ORDER BY DisplayOrder;
                    
            SELECT di.PODet_BatchID, #t.FullLocation, p.product_Name, p.product_code, d.PrdStockRequest_Id, d.POdet_PrdID, d.POdet_Qty, d.POdet_RatePerItem, d.POdet_Amount, d.POdet_disc, d.POdet_Subtotal, d.POdet_VatPer, d.POdet_Vat, d.POdet_NetTotal 
            FROM db_cakerstreet_live.dbo.tbl_products p 
            INNER JOIN tbl_POdet d ON p.product_ID = d.POdet_PrdID
            LEFT OUTER JOIN tbl_POdet_ItemsRec di ON p.product_ID = di.POdet_PrdID AND di.POdet_POID = d.POdet_POID
            LEFT OUTER JOIN #t ON di.PODet_LocationID = #t.LocationID
            WHERE d.POdet_POID = @poid ORDER BY d.POdet_displayOrder;
            DROP TABLE #t;";

            var items = new List<POPrintItem>();
            await using (var cmd = new SqlCommand(itemsSql, conn))
            {
                cmd.Parameters.AddWithValue("@wid", webstoreId);
                cmd.Parameters.AddWithValue("@poid", poId.ToString());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new POPrintItem
                    {
                        PODet_BatchID = reader.IsDBNull(0) ? "" : reader.GetValue(0).ToString() ?? "",
                        FullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        ProductName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ProductCode = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PrdStockRequest_Id = reader.IsDBNull(4) ? 0L : Convert.ToInt64(reader.GetValue(4)),
                        POdet_PrdID = reader.IsDBNull(5) ? 0L : Convert.ToInt64(reader.GetValue(5)),
                        POdet_Qty = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                        POdet_RatePerItem = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                        POdet_Amount = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                        POdet_disc = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                        POdet_Subtotal = reader.IsDBNull(10) ? 0m : reader.GetDecimal(10),
                        POdet_VatPer = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11),
                        POdet_Vat = reader.IsDBNull(12) ? 0m : reader.GetDecimal(12),
                        POdet_NetTotal = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13)
                    });
                }
            }

            result.LineItems = items;
            return result;
        }

        // ─── Purchase Order Item Received Printing ─────────────────────────────────

        public async Task<POItemReceivedPrintResult?> GetPurchaseOrderItemReceivedPrintAsync(long itemsRecId)
        {
            await using var conn = new SqlConnection(_businessConnection);
            await conn.OpenAsync();

            var itemsRecSql = "SELECT PO_ID, PO_InvoiceNo, PO_InvoiceDate, PO_ReceivedDate, PO_InvoiceFile, PO_ReceivedBy, PO_Remarks, PO_ItemsRec_ID FROM tbl_PO_ItemsRec WHERE PO_ItemsRec_ID = @id";
            long poId = 0;
            long receivedBy = 0;
            POItemReceivedPrintResult? result = null;

            await using (var cmd = new SqlCommand(itemsRecSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", itemsRecId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result = new POItemReceivedPrintResult
                    {
                        PO_ItemsRec_ID = itemsRecId,
                        PO_InvoiceNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        PO_InvoiceDate = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                        PO_ReceivedDate = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                        PO_InvoiceFile = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        PO_Remarks = reader.IsDBNull(6) ? "" : reader.GetString(6)
                    };
                    poId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0));
                    receivedBy = reader.IsDBNull(5) ? 0L : Convert.ToInt64(reader.GetValue(5));
                }
            }

            if (result == null) return null;

            // Load Received By staff name from DefaultConnection
            if (receivedBy > 0)
            {
                await using var defConn = new SqlConnection(_defaultConnection);
                await defConn.OpenAsync();

                var staffSql = "SELECT customer_Name FROM tbl_bakeryuser WHERE customer_ID = @uid";
                await using var cmd = new SqlCommand(staffSql, defConn);
                cmd.Parameters.AddWithValue("@uid", receivedBy);
                var res = await cmd.ExecuteScalarAsync();
                result.ReceivedByName = res?.ToString() ?? "";
            }

            // Load PO header details (like webstore ID, supplier name)
            long supplierId = 0;
            long webstoreId = 0;
            var poSql = "SELECT PO_SupplierID, PO_WebstoreID FROM tbl_PO WHERE PO_ID = @poId";
            await using (var cmd = new SqlCommand(poSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", poId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    supplierId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0));
                    webstoreId = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1));
                }
            }

            // Load Supplier Name
            if (supplierId > 0)
            {
                await using var defConn = new SqlConnection(_defaultConnection);
                await defConn.OpenAsync();

                var supSql = "SELECT SupplierName FROM tbl_ProductSupplier WHERE SupplierId = @sid";
                await using var cmd = new SqlCommand(supSql, defConn);
                cmd.Parameters.AddWithValue("@sid", supplierId);
                var res = await cmd.ExecuteScalarAsync();
                result.SupplierName = res?.ToString() ?? "";
            }

            // Load items received details
            var itemsSql = @";WITH RCTE AS
            (
                SELECT LocationID, LocationTitle, CAST(LocationTitle as varchar(2000)) as FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
                FROM db_cakerstreet_live.dbo.tbl_location WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid  
                UNION ALL
                SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle  as varchar(2000)) as FullLocation, rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
                FROM db_cakerstreet_live.dbo.tbl_location rh
                INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
            ) SELECT LocationID, FullLocation INTO #t FROM RCTE WHERE Lvl = 3 ORDER BY DisplayOrder;
                    
            SELECT d.PODet_BatchID, ps.SupplierName, #t.FullLocation, p.product_Name, p.product_code, PrdStockRequest_Id, POdet_PrdID, POdet_Qty, POdet_RatePerItem, POdet_Amount, POdet_disc, POdet_Subtotal, POdet_VatPer, POdet_Vat, POdet_NetTotal 
            FROM db_cakerstreet_live.dbo.tbl_products p 
            INNER JOIN tbl_POdet_ItemsRec d ON p.product_ID = d.POdet_PrdID 
            INNER JOIN tbl_PO_ItemsRec ie ON ie.PO_ItemsRec_ID = d.POdet_POID 
            INNER JOIN tbl_PO po ON po.PO_ID = ie.PO_ID 
            INNER JOIN db_cakerstreet_live.dbo.tbl_ProductSupplier ps ON ps.SupplierId = po.PO_SupplierID 
            LEFT OUTER JOIN #t ON d.PODet_LocationID = #t.LocationID
            WHERE d.POdet_POID = @poid ORDER BY d.POdet_displayOrder;
            DROP TABLE #t;";

            var items = new List<POPrintItem>();
            await using (var cmd = new SqlCommand(itemsSql, conn))
            {
                cmd.Parameters.AddWithValue("@wid", webstoreId);
                cmd.Parameters.AddWithValue("@poid", itemsRecId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new POPrintItem
                    {
                        PODet_BatchID = reader.IsDBNull(0) ? "" : reader.GetValue(0).ToString() ?? "",
                        SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        FullLocation = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ProductName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ProductCode = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        PrdStockRequest_Id = reader.IsDBNull(5) ? 0L : Convert.ToInt64(reader.GetValue(5)),
                        POdet_PrdID = reader.IsDBNull(6) ? 0L : Convert.ToInt64(reader.GetValue(6)),
                        POdet_Qty = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                        POdet_RatePerItem = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                        POdet_Amount = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                        POdet_disc = reader.IsDBNull(10) ? 0m : reader.GetDecimal(10),
                        POdet_Subtotal = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11),
                        POdet_VatPer = reader.IsDBNull(12) ? 0m : reader.GetDecimal(12),
                        POdet_Vat = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13),
                        POdet_NetTotal = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14)
                    });
                }
            }

            result.LineItems = items;
            return result;
        }

        // ─── Credit Note Printing ──────────────────────────────────────────────────

        public async Task<CreditNotePrintResult?> GetCreditNotePrintAsync(string creditNoteNo)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Check if tbl_order contains CreditNoteNo column
            var hasCreditNoteNo = false;

            string query;
            if (hasCreditNoteNo)
            {
                query = @"
                    SELECT 
                        O.order_ID,
                        O.order_date,
                        O.order_customerName,
                        O.order_customerEmail,
                        O.CreditNoteNo AS CreditNoteNo,
                        O.order_date AS Credit_date,
                        'System' AS Credit_generatedby,
                        O.order_payoutRefund + O.order_csRefund AS Credit_Amount,
                        S.shipping_address, S.shipping_city, S.shipping_county, S.shipping_zip, S.shipping_country, S.shipping_phone,
                        B.billing_fName, B.billing_lName, B.billing_address, B.billing_city, B.billing_county, B.billing_zip, B.billing_country, B.billing_phone
                    FROM tbl_order O
                    INNER JOIN tbl_shippingDetail S ON O.order_ID = S.shipping_orderID
                    INNER JOIN tbl_billingDetail B ON O.order_ID = B.billing_orderID
                    WHERE CAST(O.CreditNoteNo AS VARCHAR(50)) = @keyword OR CAST(O.order_ID AS VARCHAR(50)) = @keyword";
            }
            else
            {
                // Fallback join with tbl_orderRefund
                query = @"
                    SELECT 
                        O.order_ID,
                        O.order_date,
                        O.order_customerName,
                        O.order_customerEmail,
                        CAST(R.orderRefund_ID AS VARCHAR(50)) AS CreditNoteNo,
                        R.orderRefund_createdOn AS Credit_date,
                        'CRM Admin (' + CAST(R.orderRefund_CRMID AS VARCHAR(50)) + ')' AS Credit_generatedby,
                        R.orderRefund_bakeryrefund + R.orderRefund_csRefund AS Credit_Amount,
                        S.shipping_address, S.shipping_city, S.shipping_county, S.shipping_zip, S.shipping_country, S.shipping_phone,
                        B.billing_fName, B.billing_lName, B.billing_address, B.billing_city, B.billing_county, B.billing_zip, B.billing_country, B.billing_phone
                    FROM tbl_orderRefund R
                    INNER JOIN tbl_order O ON R.orderRefund_order_ID = O.order_ID
                    INNER JOIN tbl_shippingDetail S ON O.order_ID = S.shipping_orderID
                    INNER JOIN tbl_billingDetail B ON O.order_ID = B.billing_orderID
                    WHERE CAST(R.orderRefund_ID AS VARCHAR(50)) = @keyword OR CAST(O.order_ID AS VARCHAR(50)) = @keyword";
            }

            CreditNotePrintResult? result = null;
            await using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@keyword", creditNoteNo);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result = new CreditNotePrintResult
                    {
                        OrderId = Convert.ToInt64(reader.GetValue(0)),
                        OrderDate = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                        CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        CustomerEmail = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CreditNoteNo = reader.IsDBNull(4) ? "" : reader.GetValue(4).ToString() ?? "",
                        CreditDate = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                        CreditGeneratedBy = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        CreditAmount = reader.IsDBNull(7) ? 0m : Convert.ToDecimal(reader.GetValue(7)),
                        
                        ShippingAddress = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        ShippingCity = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        ShippingCounty = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        ShippingZip = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        ShippingCountry = reader.IsDBNull(12) ? "" : reader.GetString(12),
                        ShippingPhone = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        
                        BillingFName = reader.IsDBNull(14) ? "" : reader.GetString(14),
                        BillingLName = reader.IsDBNull(15) ? "" : reader.GetString(15),
                        BillingAddress = reader.IsDBNull(16) ? "" : reader.GetString(16),
                        BillingCity = reader.IsDBNull(17) ? "" : reader.GetString(17),
                        BillingCounty = reader.IsDBNull(18) ? "" : reader.GetString(18),
                        BillingZip = reader.IsDBNull(19) ? "" : reader.GetString(19),
                        BillingCountry = reader.IsDBNull(20) ? "" : reader.GetString(20),
                        BillingPhone = reader.IsDBNull(21) ? "" : reader.GetString(21),
                    };
                }
            }

            if (result == null) return null;

            // Load line items: check if qty_returned exists
            var hasQtyReturned = false;
            var qtyCol = hasQtyReturned ? "od.qty_returned" : "od.orderDetail_Quantity";

            var itemsSql = $@"
                SELECT 
                    od.orderDetail_productID,
                    p.product_code,
                    p.product_Name,
                    od.orderDetail_price,
                    {qtyCol} AS qty_returned,
                    od.orderDetail_totalPrice,
                    rod.RefundAmount,
                    rod.RefundRemarks,
                    od.orderDetail_ShapeText,
                    ISNULL(cs.CakeShapeTitle, '') AS ShapeTitle,
                    ISNULL(ct.CakeTypeTitle, '') AS TypeTitle,
                    ISNULL(sz.SizeTitle, '') AS SizeTitle
                FROM tbl_orderDetail od
                INNER JOIN tbl_products p ON od.orderDetail_productID = p.product_ID
                LEFT JOIN tbl_RefundOrderDetail rod ON rod.OrderID = od.orderDetail_orderID AND rod.ProductID = od.orderDetail_productID
                LEFT JOIN tbl_CakeShape cs ON od.orderDetail_shapeId = cs.CakeShapeID
                LEFT JOIN tbl_CakeType ct ON od.orderDetail_TypeID = ct.CakeTypeID
                LEFT JOIN tbl_CakeSize sz ON od.orderDetail_SizeID = sz.SizeID
                WHERE od.orderDetail_orderID = @orderId AND (rod.RefundAmount > 0 OR {qtyCol} > 0)";

            var items = new List<CreditNotePrintItem>();
            await using (var cmd = new SqlCommand(itemsSql, conn))
            {
                cmd.Parameters.AddWithValue("@orderId", result.OrderId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new CreditNotePrintItem
                    {
                        ProductID = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0)),
                        ProductCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        ProductName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        UnitPrice = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                        QtyReturned = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                        TotalPrice = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                        RefundAmount = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6)),
                        RefundRemarks = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        ShapeText = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        ShapeTitle = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        TypeTitle = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        SizeTitle = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    });
                }
            }

            result.LineItems = items;
            return result;
        }

        // ─── Franchise Checklist Printing ──────────────────────────────────────────

        public async Task<FranchiseChecklistPrintResult?> GetFranchiseChecklistPrintAsync(long checklistId)
        {
            await using var conn = new SqlConnection(_eposConnection);
            await conn.OpenAsync();

            long domainId = 0;
            string domainName = "";
            FranchiseChecklistPrintResult? result = null;

            var batchSql = "SELECT stockBatch_domainID, stockBatch_title, stockBatch_ReqQty, stockBatch_Date, stockBatch_Name, stockBatch_Remarks FROM tbl_stockBatch WHERE stockBatch_ID = @id";
            await using (var cmd = new SqlCommand(batchSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", checklistId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result = new FranchiseChecklistPrintResult
                    {
                        ChecklistId = checklistId,
                        BatchTitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        ReqQty = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                        BatchDate = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                        BatchName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Remarks = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    };
                    domainId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0));
                }
            }

            if (result == null) return null;

            // Load Domain Name from EposAdminConnection (db_cakerstreet_franchise)
            if (domainId > 0)
            {
                await using var adminConn = new SqlConnection(_eposAdminConnection);
                await adminConn.OpenAsync();
                var domSql = "SELECT domain_Name FROM tbl_domains WHERE domain_ID = @did";
                await using var cmd = new SqlCommand(domSql, adminConn);
                cmd.Parameters.AddWithValue("@did", domainId);
                var res = await cmd.ExecuteScalarAsync();
                domainName = res?.ToString() ?? "";
            }
            result.DomainName = domainName;

            // Load checklist items
            var itemsSql = @"
                SELECT s.stockPrd_prdID, s.stockPrd_reqqty, s.stockPrd_qty, p.product_image1,
                       p.product_Name + (CASE WHEN cs.SizeTitle IS NULL THEN '' ELSE ' - ' + cs.SizeTitle END) AS product_Name,
                       d.product_displayorder, c.category_ShowSEOURL
                FROM tbl_stockPrd s
                INNER JOIN tbl_lnk_prd_domain d ON s.stockPrd_prdID = d.product_ID AND s.stockPrd_sizeID = d.SizeID
                INNER JOIN tbl_products p ON s.stockPrd_prdID = p.product_ID
                INNER JOIN tbl_category c ON p.product_catID = c.category_ID
                LEFT JOIN tbl_CakeSize cs ON s.stockPrd_sizeID = cs.SizeID
                WHERE s.stockPrd_batchID = @checklistId 
                  AND s.stockPrd_reqqty > 0 
                  AND d.domain_ID = @domainId 
                  AND d.Is_Active = 1
                ORDER BY c.category_ShowSEOURL, product_Name";

            var items = new List<FranchiseChecklistPrintItem>();
            await using (var cmd = new SqlCommand(itemsSql, conn))
            {
                cmd.Parameters.AddWithValue("@checklistId", checklistId);
                cmd.Parameters.AddWithValue("@domainId", domainId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new FranchiseChecklistPrintItem
                    {
                        PrdId = reader["stockPrd_prdID"] == DBNull.Value ? 0L : Convert.ToInt64(reader["stockPrd_prdID"]),
                        ReqQty = reader["stockPrd_reqqty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["stockPrd_reqqty"]),
                        InStockQty = reader["stockPrd_qty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["stockPrd_qty"]),
                        ProductImage = reader["product_image1"] == DBNull.Value ? "" : reader["product_image1"].ToString() ?? "",
                        ProductName = reader["product_name"] == DBNull.Value ? "" : reader["product_name"].ToString() ?? "",
                        DisplayOrder = reader["product_displayorder"] == DBNull.Value ? 0 : Convert.ToInt32(reader["product_displayorder"]),
                        CategoryShowSeoUrl = reader["category_ShowSEOURL"] == DBNull.Value ? "" : reader["category_ShowSEOURL"].ToString() ?? ""
                    });
                }
            }

            result.LineItems = items;
            return result;
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private async Task<bool> CheckColumnExistsAsync(string tableName, string columnName, string connStr)
        {
            try
            {
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                var sql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@t", tableName);
                cmd.Parameters.AddWithValue("@c", columnName);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            catch { return false; }
        }
    }

    #region Models

    public class POPrintResult
    {
        public long PO_ID { get; set; }
        public string PO_SysNo { get; set; } = "";
        public DateTime PO_Date { get; set; }
        public int PO_Status { get; set; }
        public string SupplierName { get; set; } = "";
        public string SupplierAddress { get; set; } = "";
        public string SupplierRemarks { get; set; } = "";
        public bool SupplierIsTopper { get; set; }
        public bool SupplierIsAccessory { get; set; }
        public List<POPrintItem> LineItems { get; set; } = new();
    }

    public class POPrintItem
    {
        public string PODet_BatchID { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string FullLocation { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ProductCode { get; set; } = "";
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

    public class POItemReceivedPrintResult
    {
        public long PO_ItemsRec_ID { get; set; }
        public string PO_InvoiceNo { get; set; } = "";
        public DateTime PO_InvoiceDate { get; set; }
        public DateTime PO_ReceivedDate { get; set; }
        public string PO_InvoiceFile { get; set; } = "";
        public string ReceivedByName { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string PO_Remarks { get; set; } = "";
        public List<POPrintItem> LineItems { get; set; } = new();
    }

    public class CreditNotePrintResult
    {
        public long OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string CreditNoteNo { get; set; } = "";
        public DateTime CreditDate { get; set; }
        public string CreditGeneratedBy { get; set; } = "";
        public decimal CreditAmount { get; set; }
        
        public string ShippingAddress { get; set; } = "";
        public string ShippingCity { get; set; } = "";
        public string ShippingCounty { get; set; } = "";
        public string ShippingZip { get; set; } = "";
        public string ShippingCountry { get; set; } = "";
        public string ShippingPhone { get; set; } = "";

        public string BillingFName { get; set; } = "";
        public string BillingLName { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string BillingCity { get; set; } = "";
        public string BillingCounty { get; set; } = "";
        public string BillingZip { get; set; } = "";
        public string BillingCountry { get; set; } = "";
        public string BillingPhone { get; set; } = "";

        public List<CreditNotePrintItem> LineItems { get; set; } = new();
    }

    public class CreditNotePrintItem
    {
        public long ProductID { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int QtyReturned { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal RefundAmount { get; set; }
        public string RefundRemarks { get; set; } = "";
        public string ShapeText { get; set; } = "";
        public string ShapeTitle { get; set; } = "";
        public string TypeTitle { get; set; } = "";
        public string SizeTitle { get; set; } = "";
    }

    public class FranchiseChecklistPrintResult
    {
        public long ChecklistId { get; set; }
        public string BatchTitle { get; set; } = "";
        public int ReqQty { get; set; }
        public DateTime BatchDate { get; set; }
        public string BatchName { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string DomainName { get; set; } = "";
        public List<FranchiseChecklistPrintItem> LineItems { get; set; } = new();
    }

    public class FranchiseChecklistPrintItem
    {
        public long PrdId { get; set; }
        public int ReqQty { get; set; }
        public int InStockQty { get; set; }
        public string ProductImage { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int DisplayOrder { get; set; }
        public string CategoryShowSeoUrl { get; set; } = "";
    }

    #endregion
}
