using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

/// <summary>
/// Service for querying and mutating business orders from tbl_order with related tables.
/// Migrated from bakeryorders.aspx.cs bindOrders() logic.
/// </summary>
public class BusinessOrdersService
{
    private readonly string _connectionString;

    public BusinessOrdersService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated orders filtered by order type, date range, and search query.
    /// Returns orders with nested order details per order (matching legacy repeater).
    /// </summary>
    public async Task<BusinessOrdersResult> GetOrdersAsync(BusinessOrdersRequest request)
    {
        var result = new BusinessOrdersResult
        {
            OrderType = request.OrderType,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            DeliveryModeFilter = request.DeliveryMode
        };

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build WHERE clause and parameters
        var parameters = new List<SqlParameter>();
        var whereClause = BuildWhereClause(request, parameters);

        // Get total count
        var countSql = $@"SELECT COUNT(DISTINCT o.order_ID) 
            FROM tbl_order o 
            INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID 
            LEFT JOIN tbl_shippingDetail s ON s.shipping_orderID = o.order_ID 
            LEFT JOIN tbl_webstore br ON o.order_branchID = br.webstore_id 
            LEFT JOIN tbl_orderreviews rv ON rv.orderreviews_orderID = o.order_ID
            WHERE {whereClause}";

        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            foreach (var p in parameters)
                countCmd.Parameters.Add(CloneParameter(p));
            var countResult = await countCmd.ExecuteScalarAsync();
            result.TotalCount = countResult != null && countResult != DBNull.Value ? Convert.ToInt32(countResult) : 0;
        }

        if (result.TotalCount == 0)
        {
            result.Orders = new List<BusinessOrderItem>();
            return result;
        }

        // Get paginated orders (distinct order-level data)
        int offset = (request.PageNumber - 1) * request.PageSize;

        var dataSql = $@"SELECT 
                o.order_ID AS OrderId,
                o.order_date AS OrderDate,
                c.ordercollection_OcasionDate AS OccasionDate,
                c.ordercollection_Date AS CollectionDate,
                c.ordercollection_dispatchDate AS DispatchDate,
                o.order_customerName AS CustomerName,
                ISNULL(s.shipping_phone, '') AS Phone,
                ISNULL(s.shipping_zip, '') AS Postcode,
                o.order_quality AS Qty,
                o.order_totalPrice AS TotalPrice,
                o.order_CSmargin AS CSMargin,
                o.order_shopMargin AS ShopMargin,
                o.order_payoutRefund AS PayoutRefund,
                o.order_csRefund AS CsRefund,
                c.ordercollection_deliverymode AS DeliveryMode,
                o.order_saletype AS SaleType,
                o.order_status AS Status,
                o.order_isrepeat AS IsRepeat,
                o.order_followingorderid AS FollowingOrderId,
                o.order_forwardedorderid AS ForwardedOrderId,
                o.order_bakeryID AS BakeryId,
                o.order_branchID AS BranchId,
                o.order_customerID AS CustomerId,
                o.order_shopID AS ShopId,
                ISNULL(br.webstore_businessName, '') AS BranchName,
                ISNULL(br.webstore_postcode, '') AS BranchPostcode,
                ISNULL((SELECT TOP 1 CAST(Distance AS NVARCHAR(50))+','+CAST(DistanceSeconds AS NVARCHAR(50)) 
                    FROM tbl_PostcodeDistance WHERE 
                    (Postcode2=REPLACE(br.webstore_postcode,' ','') AND Postcode1=REPLACE(s.shipping_zip,' ',''))
                    OR (Postcode1=REPLACE(br.webstore_postcode,' ','') AND Postcode2=REPLACE(s.shipping_zip,' ',''))
                ), '') AS PostcodeDistance,
                ISNULL(rv.orderreviews_stars, '') AS ReviewStars,
                ISNULL(rv.orderreviews_remarks, '') AS ReviewRemarks
            FROM tbl_order o 
            INNER JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID 
            LEFT JOIN tbl_shippingDetail s ON s.shipping_orderID = o.order_ID 
            LEFT JOIN tbl_webstore br ON o.order_branchID = br.webstore_id 
            LEFT JOIN tbl_orderreviews rv ON rv.orderreviews_orderID = o.order_ID
            WHERE {whereClause}
            ORDER BY c.ordercollection_dispatchDate, o.order_date DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var orders = new List<BusinessOrderItem>();
        var orderIds = new List<long>();

        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            foreach (var p in parameters)
                dataCmd.Parameters.Add(CloneParameter(p));
            dataCmd.Parameters.AddWithValue("@Offset", offset);
            dataCmd.Parameters.AddWithValue("@PageSize", request.PageSize);

            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new BusinessOrderItem
                {
                    OrderId = GetInt64Safe(reader, "OrderId"),
                    OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                    OccasionDate = reader.GetDateTime(reader.GetOrdinal("OccasionDate")),
                    CollectionDate = reader.GetDateTime(reader.GetOrdinal("CollectionDate")),
                    DispatchDate = reader.GetDateTime(reader.GetOrdinal("DispatchDate")),
                    CustomerName = GetStringSafe(reader, "CustomerName"),
                    Phone = GetStringSafe(reader, "Phone"),
                    Postcode = GetStringSafe(reader, "Postcode"),
                    Qty = GetInt32Safe(reader, "Qty"),
                    TotalPrice = GetDecimalSafe(reader, "TotalPrice"),
                    CSMargin = GetDecimalSafe(reader, "CSMargin"),
                    ShopMargin = GetDecimalSafe(reader, "ShopMargin"),
                    PayoutRefund = GetDecimalSafe(reader, "PayoutRefund"),
                    CsRefund = GetDecimalSafe(reader, "CsRefund"),
                    DeliveryMode = GetInt32Safe(reader, "DeliveryMode"),
                    SaleType = GetInt32Safe(reader, "SaleType"),
                    Status = GetInt32Safe(reader, "Status"),
                    IsRepeat = GetBoolSafe(reader, "IsRepeat"),
                    FollowingOrderId = GetInt64Safe(reader, "FollowingOrderId"),
                    ForwardedOrderId = GetInt64Safe(reader, "ForwardedOrderId"),
                    BakeryId = GetInt64Safe(reader, "BakeryId"),
                    BranchId = GetInt64Safe(reader, "BranchId"),
                    CustomerId = GetInt64Safe(reader, "CustomerId"),
                    ShopId = GetInt64Safe(reader, "ShopId"),
                    BranchName = GetStringSafe(reader, "BranchName"),
                    BranchPostcode = GetStringSafe(reader, "BranchPostcode"),
                    PostcodeDistance = GetStringSafe(reader, "PostcodeDistance"),
                    ReviewStars = GetStringSafe(reader, "ReviewStars"),
                    ReviewRemarks = GetStringSafe(reader, "ReviewRemarks")
                };
                orders.Add(item);
                orderIds.Add(item.OrderId);
            }
        }

        // Get order details (nested repeater data) for all orders in this page
        if (orderIds.Count > 0)
        {
            var detailSql = @"SELECT 
                od.orderDetail_orderID AS OrderId,
                od.orderDetail_ID AS OrderDetailId,
                od.Sub_orderID AS SubOrderId,
                od.orderDetail_Quantity AS Quantity,
                od.orderDetail_prdType AS PrdType,
                p.product_ID AS ProductId,
                p.product_Name AS ProductName,
                p.product_image1 AS ProductImage,
                p.product_SEOURL AS ProductSeoUrl,
                p.product_type AS ProductType,
                p.product_isexpired AS IsExpired,
                CASE WHEN p.product_isexpired = 1 AND s.SkuMapping_newPrdID IS NULL THEN 0 ELSE 1 END AS IsCustomizedPrdMapped,
                ISNULL(ot.topper_typeId, 0) AS TopperTypeId,
                CASE WHEN o.order_status = 0 THEN CASE WHEN om.IsUpdated IS NULL OR om.IsUpdated = 0 THEN 0 ELSE 1 END ELSE 1 END AS IsChangeOrderImageMarked,
                ISNULL(om.IsUpdated, -1) AS IsUpdated
            FROM tbl_orderDetail od 
            INNER JOIN tbl_order o ON od.orderDetail_orderID = o.order_ID
            INNER JOIN tbl_products p ON p.product_ID = od.orderDetail_productID 
            LEFT OUTER JOIN tbl_orderImageUpdate om ON od.orderDetail_ID = om.OrderImage_orderDetail_ID
            LEFT OUTER JOIN tbl_PrdTopperType ot ON od.orderDetail_productID = ot.product_ID AND ot.IsDeleted = 0
            LEFT OUTER JOIN tbl_skumapping s ON p.product_ID = s.SkuMapping_newPrdID
            WHERE od.orderDetail_orderID IN (" + string.Join(",", orderIds) + @")
            ORDER BY od.orderDetail_ID DESC";

            await using var detailCmd = new SqlCommand(detailSql, conn);
            await using var detailReader = await detailCmd.ExecuteReaderAsync();

            var detailsByOrder = new Dictionary<long, List<OrderDetailItem>>();
            while (await detailReader.ReadAsync())
            {
                var orderId = GetInt64Safe(detailReader, "OrderId");
                var detail = new OrderDetailItem
                {
                    OrderId = orderId,
                    OrderDetailId = GetInt64Safe(detailReader, "OrderDetailId"),
                    SubOrderId = GetInt64Safe(detailReader, "SubOrderId"),
                    Quantity = GetInt32Safe(detailReader, "Quantity"),
                    PrdType = GetInt32Safe(detailReader, "PrdType"),
                    ProductId = GetInt64Safe(detailReader, "ProductId"),
                    ProductName = GetStringSafe(detailReader, "ProductName"),
                    ProductImage = GetStringSafe(detailReader, "ProductImage"),
                    ProductSeoUrl = GetStringSafe(detailReader, "ProductSeoUrl"),
                    ProductType = GetInt32Safe(detailReader, "ProductType"),
                    IsExpired = GetBoolSafe(detailReader, "IsExpired"),
                    IsCustomizedPrdMapped = GetInt32Safe(detailReader, "IsCustomizedPrdMapped") == 1,
                    TopperTypeId = GetInt32Safe(detailReader, "TopperTypeId"),
                    IsChangeOrderImageMarked = GetInt32Safe(detailReader, "IsChangeOrderImageMarked"),
                    IsUpdated = GetInt32Safe(detailReader, "IsUpdated")
                };
                if (!detailsByOrder.ContainsKey(orderId))
                    detailsByOrder[orderId] = new List<OrderDetailItem>();
                detailsByOrder[orderId].Add(detail);
            }

            // Assign details to orders
            foreach (var order in orders)
            {
                order.OrderDetails = detailsByOrder.ContainsKey(order.OrderId)
                    ? detailsByOrder[order.OrderId]
                    : new List<OrderDetailItem>();
            }
        }

        result.Orders = orders;
        return result;
    }

    /// <summary>
    /// Gets tab counts for all order statuses (for tab badges).
    /// </summary>
    public async Task<Dictionary<int, int>> GetTabCountsAsync(string webshopId)
    {
        var counts = new Dictionary<int, int>
        {
            { 0, 0 }, { 1, 0 }, { 5, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 11, 0 }
        };

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT order_status, COUNT(1) AS cnt 
            FROM tbl_order 
            WHERE order_branchID = @WebshopId 
              AND order_isPurchased = 1 
              AND order_isdeleted = 0 
              AND order_followingorderid = 0
              AND order_status IN (0, 1, 2, 3, 4, 5)
            GROUP BY order_status";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var status = reader.GetInt32(0);
            var count = reader.GetInt32(1);
            if (counts.ContainsKey(status))
                counts[status] = count;
        }

        // Cancelled count (status=10 OR isdeleted=1)
        await reader.CloseAsync();

        await using var cancelCmd = new SqlCommand(
            @"SELECT COUNT(1) FROM tbl_order 
              WHERE order_branchID = @WebshopId 
                AND order_isPurchased = 1 
                AND (order_status = 10 OR order_isdeleted = 1)", conn);
        cancelCmd.Parameters.AddWithValue("@WebshopId", webshopId);
        var cancelResult = await cancelCmd.ExecuteScalarAsync();
        counts[11] = cancelResult != null && cancelResult != DBNull.Value ? Convert.ToInt32(cancelResult) : 0;

        return counts;
    }

    // ===== MUTATION METHODS =====

    /// <summary>
    /// Updates order status with ownership validation and order log entry.
    /// Used for: Job Assigned (5), Order Processed (2), Under Delivery (3), Completed (4).
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(long orderId, int newStatus, string webshopId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Update status with ownership check
        var updateSql = @"UPDATE tbl_order 
            SET order_status = @NewStatus 
            WHERE order_ID = @OrderId 
              AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(updateSql, conn);
        cmd.Parameters.AddWithValue("@NewStatus", newStatus);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0) return false;

        // Insert order log
        await InsertOrderLogAsync(conn, orderId, newStatus, userId);
        return true;
    }

    /// <summary>
    /// Confirms an order: sets status to 1 (Confirmed), logs it, then sets to 5 (Job Assigned), logs it.
    /// Matches legacy ConfirmOrder behavior (minus topper quantity side effects).
    /// </summary>
    public async Task<bool> ConfirmOrderAsync(long orderId, string webshopId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Verify ownership first
        var checkSql = @"SELECT COUNT(1) FROM tbl_order 
            WHERE order_ID = @OrderId 
              AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
        checkCmd.Parameters.AddWithValue("@WebshopId", webshopId);
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
        if (!exists) return false;

        // Step 1: Set status = 1 (Confirmed)
        var update1Sql = @"UPDATE tbl_order SET order_status = 1 WHERE order_ID = @OrderId";
        await using var cmd1 = new SqlCommand(update1Sql, conn);
        cmd1.Parameters.AddWithValue("@OrderId", orderId);
        await cmd1.ExecuteNonQueryAsync();
        await InsertOrderLogAsync(conn, orderId, 1, userId);

        // Step 2: Set status = 5 (Job Assigned)
        var update2Sql = @"UPDATE tbl_order SET order_status = 5 WHERE order_ID = @OrderId";
        await using var cmd2 = new SqlCommand(update2Sql, conn);
        cmd2.Parameters.AddWithValue("@OrderId", orderId);
        await cmd2.ExecuteNonQueryAsync();
        await InsertOrderLogAsync(conn, orderId, 5, userId);

        return true;
    }

    /// <summary>
    /// Cancels an order: sets status=11, stores cancel reason/remarks, logs it.
    /// GUARDED: Wallet refund (saletype==1) and email notification are not yet implemented.
    /// Matching legacy btnCancelOrder_Click behavior.
    /// </summary>
    public async Task<CancelOrderResult> CancelOrderAsync(long orderId, string webshopId, int userId, string reason, string comments, bool notifyCustomer)
    {
        var result = new CancelOrderResult();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Step 1: UPDATE order_status=11, order_CancelRemarks, order_CancelReason with ownership check
        var updateSql = @"UPDATE tbl_order 
            SET order_status = 11, 
                order_CancelRemarks = @Comments, 
                order_CancelReason = @Reason 
            WHERE order_ID = @OrderId 
              AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var updateCmd = new SqlCommand(updateSql, conn);
        updateCmd.Parameters.AddWithValue("@Comments", comments);
        updateCmd.Parameters.AddWithValue("@Reason", reason);
        updateCmd.Parameters.AddWithValue("@OrderId", orderId);
        updateCmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            result.Success = false;
            result.Message = "Order not found or access denied.";
            return result;
        }

        // Step 2: Check saletype - if 1, wallet refund would be triggered (GUARDED)
        var saletypeSql = @"SELECT order_saletype FROM tbl_order WHERE order_ID = @OrderId";
        await using var stCmd = new SqlCommand(saletypeSql, conn);
        stCmd.Parameters.AddWithValue("@OrderId", orderId);
        var saletypeResult = await stCmd.ExecuteScalarAsync();
        var saletype = saletypeResult != null && saletypeResult != DBNull.Value ? Convert.ToInt32(saletypeResult) : 0;

        if (saletype == 1)
        {
            result.Warnings.Add("Wallet refund skipped (not yet implemented - manual refund may be needed)");
        }

        // Step 3: If notifyCustomer, email would be sent (GUARDED)
        if (notifyCustomer)
        {
            result.Warnings.Add("Customer notification email skipped (not yet implemented)");
        }

        // Step 4: INSERT order log (status=11)
        await InsertOrderLogAsync(conn, orderId, 11, userId);

        result.Success = true;
        result.Message = "Order cancelled successfully.";
        return result;
    }

    /// <summary>
    /// Soft-deletes an order (sets order_isdeleted = 1) with ownership validation.
    /// </summary>
    public async Task<bool> DeleteOrderAsync(long orderId, string webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_order 
            SET order_isdeleted = 1 
            WHERE order_ID = @OrderId 
              AND (order_bakeryID = @WebshopId OR order_branchID = @WebshopId)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <summary>
    /// Saves order review (stars + remarks). Upserts into tbl_orderreviews.
    /// Matching legacy OrderReview command handler.
    /// </summary>
    public async Task SaveReviewAsync(long orderId, int stars, string remarks)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if review exists
        var checkSql = "SELECT COUNT(1) FROM tbl_orderreviews WHERE orderreviews_orderID = @OrderId";
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
        var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

        if (exists)
        {
            var updateSql = "UPDATE tbl_orderreviews SET orderreviews_stars = @Stars, orderreviews_remarks = @Remarks WHERE orderreviews_orderID = @OrderId";
            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@Stars", stars);
            updateCmd.Parameters.AddWithValue("@Remarks", remarks ?? "");
            updateCmd.Parameters.AddWithValue("@OrderId", orderId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertSql = "INSERT INTO tbl_orderreviews (orderreviews_orderID, orderreviews_stars, orderreviews_remarks) VALUES (@OrderId, @Stars, @Remarks)";
            await using var insertCmd = new SqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@OrderId", orderId);
            insertCmd.Parameters.AddWithValue("@Stars", stars);
            insertCmd.Parameters.AddWithValue("@Remarks", remarks ?? "");
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Marks/unmarks an order detail for image change. Upserts tbl_orderImageUpdate.
    /// Matching legacy MarkChangeOrderImage AJAX call.
    /// </summary>
    public async Task MarkChangeImageAsync(long orderDetailId, bool markForChange)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if record exists
        var checkSql = "SELECT COUNT(1) FROM tbl_orderImageUpdate WHERE OrderImage_orderDetail_ID = @OrderDetailId";
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@OrderDetailId", orderDetailId);
        var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

        if (exists)
        {
            var updateSql = "UPDATE tbl_orderImageUpdate SET IsUpdated = @IsUpdated WHERE OrderImage_orderDetail_ID = @OrderDetailId";
            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@IsUpdated", markForChange ? 0 : 1);
            updateCmd.Parameters.AddWithValue("@OrderDetailId", orderDetailId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertSql = "INSERT INTO tbl_orderImageUpdate (OrderImage_orderDetail_ID, IsUpdated) VALUES (@OrderDetailId, @IsUpdated)";
            await using var insertCmd = new SqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@OrderDetailId", orderDetailId);
            insertCmd.Parameters.AddWithValue("@IsUpdated", markForChange ? 0 : 1);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Saves topper type for an order detail. Matching legacy SaveOrderTopperType.
    /// Updates tbl_PrdTopperType (upsert).
    /// </summary>
    public async Task SaveTopperTypeAsync(long orderId, long orderDetailId, bool hasTopper, int topperTypeId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Get the productID for this order detail
        var getPrdSql = "SELECT orderDetail_productID FROM tbl_orderDetail WHERE orderDetail_ID = @OrderDetailId AND orderDetail_orderID = @OrderId";
        await using var getPrdCmd = new SqlCommand(getPrdSql, conn);
        getPrdCmd.Parameters.AddWithValue("@OrderDetailId", orderDetailId);
        getPrdCmd.Parameters.AddWithValue("@OrderId", orderId);
        var prdIdObj = await getPrdCmd.ExecuteScalarAsync();
        if (prdIdObj == null) return;
        var productId = Convert.ToInt64(prdIdObj);

        if (hasTopper)
        {
            // Upsert into tbl_PrdTopperType
            var checkSql = "SELECT COUNT(1) FROM tbl_PrdTopperType WHERE product_ID = @ProductId AND IsDeleted = 0";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@ProductId", productId);
            var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

            if (exists)
            {
                var updateSql = "UPDATE tbl_PrdTopperType SET topper_typeId = @TypeId WHERE product_ID = @ProductId AND IsDeleted = 0";
                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@TypeId", topperTypeId);
                updateCmd.Parameters.AddWithValue("@ProductId", productId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertSql = "INSERT INTO tbl_PrdTopperType (product_ID, topper_typeId, IsDeleted) VALUES (@ProductId, @TypeId, 0)";
                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@ProductId", productId);
                insertCmd.Parameters.AddWithValue("@TypeId", topperTypeId);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        else
        {
            // Mark as deleted (soft delete)
            var deleteSql = "UPDATE tbl_PrdTopperType SET IsDeleted = 1 WHERE product_ID = @ProductId";
            await using var deleteCmd = new SqlCommand(deleteSql, conn);
            deleteCmd.Parameters.AddWithValue("@ProductId", productId);
            await deleteCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Inserts a record into tbl_orderlog (matching legacy updateorder_log).
    /// </summary>
    private async Task InsertOrderLogAsync(SqlConnection conn, long orderId, int status, int userId)
    {
        var logSql = @"INSERT INTO tbl_orderlog 
            (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn) 
            VALUES (@UserId, @Status, @OrderId, GETDATE())";

        await using var logCmd = new SqlCommand(logSql, conn);
        logCmd.Parameters.AddWithValue("@UserId", userId);
        logCmd.Parameters.AddWithValue("@Status", status);
        logCmd.Parameters.AddWithValue("@OrderId", orderId);
        await logCmd.ExecuteNonQueryAsync();
    }

    private string BuildWhereClause(BusinessOrdersRequest request, List<SqlParameter> parameters)
    {
        var conditions = new List<string>();

        // Base filter: branch ID
        conditions.Add("o.order_branchID = @WebshopId");
        parameters.Add(new SqlParameter("@WebshopId", request.WebshopId));

        // Always purchased
        conditions.Add("o.order_isPurchased = 1");

        // Status-based filtering
        switch (request.OrderType)
        {
            case 0: // Pending
                conditions.Add("o.order_status = 0");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 1: // Confirmed
                conditions.Add("o.order_status = 1");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 5: // Job Assigned
                conditions.Add("o.order_status = 5");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 2: // Processed
                conditions.Add("o.order_status = 2");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 3: // Under Delivery
                conditions.Add("o.order_status = 3");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 4: // Completed
                conditions.Add("o.order_status = 4");
                conditions.Add("o.order_isdeleted = 0");
                break;
            case 11: // Cancelled
                conditions.Add("(o.order_status = 10 OR o.order_isdeleted = 1)");
                break;
            case 10: // Forwarded
                conditions.Add("o.order_followingorderid > 0");
                conditions.Add("o.order_isdeleted = 0");
                break;
            default:
                conditions.Add("o.order_status = 0");
                conditions.Add("o.order_isdeleted = 0");
                break;
        }

        // Delivery mode filter
        if (request.DeliveryMode > 0)
        {
            if (request.DeliveryMode == 1)
            {
                conditions.Add("(c.ordercollection_deliverymode = 1 AND o.order_bakeryID = o.order_branchID)");
            }
            else if (request.DeliveryMode == 2)
            {
                conditions.Add("(c.ordercollection_deliverymode = 2 OR (c.ordercollection_deliverymode = 1 AND o.order_bakeryID != o.order_branchID))");
            }
            else
            {
                conditions.Add($"c.ordercollection_deliverymode = @DeliveryMode");
                parameters.Add(new SqlParameter("@DeliveryMode", request.DeliveryMode));
            }
        }

        // Date range filter
        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            if (request.DateMode == 2)
            {
                conditions.Add("c.ordercollection_Date >= @StartDate");
                conditions.Add("c.ordercollection_Date < @EndDate");
            }
            else
            {
                conditions.Add("o.order_date >= @StartDate");
                conditions.Add("o.order_date < @EndDate");
            }
            parameters.Add(new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = request.StartDate.Value.Date });
            parameters.Add(new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = request.EndDate.Value.Date.AddDays(1) });
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var searchTerm = request.SearchQuery.Trim();
            if (long.TryParse(searchTerm, out var searchOrderId))
            {
                conditions.Add("(o.order_ID = @SearchOrderId OR o.order_followingorderid = @SearchOrderId OR o.order_customerName LIKE @SearchTerm OR o.order_customerEmail LIKE @SearchTerm)");
                parameters.Add(new SqlParameter("@SearchOrderId", searchOrderId));
                parameters.Add(new SqlParameter("@SearchTerm", $"%{searchTerm}%"));
            }
            else
            {
                conditions.Add("(o.order_customerName LIKE @SearchTerm OR o.order_customerEmail LIKE @SearchTerm)");
                parameters.Add(new SqlParameter("@SearchTerm", $"%{searchTerm}%"));
            }
        }

        return string.Join(" AND ", conditions);
    }

    private static SqlParameter CloneParameter(SqlParameter source)
    {
        return new SqlParameter(source.ParameterName, source.SqlDbType)
        {
            Value = source.Value,
            Direction = source.Direction
        };
    }

    private static string GetStringSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return "";
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(string)) return reader.GetString(ordinal);
        return reader.GetValue(ordinal)?.ToString() ?? "";
    }

    private static long GetInt64Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(long)) return reader.GetInt64(ordinal);
        if (fieldType == typeof(int)) return reader.GetInt32(ordinal);
        if (fieldType == typeof(short)) return reader.GetInt16(ordinal);
        var val = reader.GetValue(ordinal)?.ToString() ?? "";
        return long.TryParse(val, out var result) ? result : 0;
    }

    private static int GetInt32Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(int)) return reader.GetInt32(ordinal);
        if (fieldType == typeof(short)) return reader.GetInt16(ordinal);
        if (fieldType == typeof(long)) return (int)reader.GetInt64(ordinal);
        if (fieldType == typeof(byte)) return reader.GetByte(ordinal);
        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal GetDecimalSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0m;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(decimal)) return reader.GetDecimal(ordinal);
        if (fieldType == typeof(double)) return (decimal)reader.GetDouble(ordinal);
        if (fieldType == typeof(float)) return (decimal)reader.GetFloat(ordinal);
        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool GetBoolSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return false;
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(bool)) return reader.GetBoolean(ordinal);
        return Convert.ToBoolean(reader.GetValue(ordinal));
    }

    /// <summary>
    /// Updates order display sorting. Matching legacy webservices.aspx/updateordersorting.
    /// Sets order_displayorder for each order in the list.
    /// </summary>
    public async Task UpdateOrderSortingAsync(List<OrderSortingItem> items)
    {
        if (items == null || items.Count == 0) return;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var item in items)
        {
            await using var cmd = new SqlCommand(
                "UPDATE tbl_order SET order_displayorder = @displayOrder WHERE order_ID = @orderId", conn);
            cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
            cmd.Parameters.AddWithValue("@orderId", item.OrderID);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Gets ingredient list for an order detail. Matching legacy getingredientlist_popup.
    /// </summary>
    public async Task<List<IngredientListItem>> GetIngredientListAsync(long orderDetailId)
    {
        var items = new List<IngredientListItem>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT i.orderingredient_ID, i.orderingredient_batchid, 
                    i.orderingredient_sectionid, i.orderingredient_createdate,
                    ISNULL(p.product_Name, '') as product_Name,
                    ISNULL(p.product_image1, '') as product_image1
                    FROM tbl_orderingredient i
                    LEFT JOIN tbl_purchaseorder_det pod ON pod.PODet_BatchID = i.orderingredient_batchid
                    LEFT JOIN tbl_product p ON p.product_ID = pod.PODet_ProductID
                    WHERE i.orderingredient_orderdetailID = @orderDetailId
                    ORDER BY i.orderingredient_createdate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@orderDetailId", orderDetailId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var sectionNames = new[] { "Baking", "Filling", "Icing", "Decoration", "Finishing" };
            var sectionId = reader.IsDBNull(reader.GetOrdinal("orderingredient_sectionid")) ? 0 : reader.GetInt32(reader.GetOrdinal("orderingredient_sectionid"));
            items.Add(new IngredientListItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("orderingredient_ID")),
                BatchId = reader.IsDBNull(reader.GetOrdinal("orderingredient_batchid")) ? "" : reader.GetString(reader.GetOrdinal("orderingredient_batchid")),
                SectionId = sectionId,
                SectionName = sectionId >= 0 && sectionId < sectionNames.Length ? sectionNames[sectionId] : "Unknown",
                ProductName = reader.IsDBNull(reader.GetOrdinal("product_Name")) ? "" : reader.GetString(reader.GetOrdinal("product_Name")),
                ProductImage = reader.IsDBNull(reader.GetOrdinal("product_image1")) ? "" : reader.GetString(reader.GetOrdinal("product_image1")),
                CreateDate = reader.IsDBNull(reader.GetOrdinal("orderingredient_createdate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("orderingredient_createdate"))
            });
        }
        return items;
    }

    /// <summary>
    /// Adds an ingredient to an order detail. Matching legacy AddIng_popup_Click.
    /// </summary>
    public async Task<(bool Success, string Message)> AddIngredientAsync(long orderDetailId, long orderId, string batchId, int sectionId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if batch ID exists
        var checkSql = "SELECT COUNT(*) FROM tbl_purchaseorder_det WHERE PODet_BatchID = @batchId";
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@batchId", batchId);
            var count = (int)await checkCmd.ExecuteScalarAsync()!;
            if (count == 0)
                return (false, "Batch No not found. Please enter a valid Batch No.");
        }

        // Check for duplicate
        var dupSql = "SELECT COUNT(*) FROM tbl_orderingredient WHERE orderingredient_orderdetailID = @odId AND orderingredient_batchid = @batchId AND orderingredient_sectionid = @sectionId";
        await using (var dupCmd = new SqlCommand(dupSql, conn))
        {
            dupCmd.Parameters.AddWithValue("@odId", orderDetailId);
            dupCmd.Parameters.AddWithValue("@batchId", batchId);
            dupCmd.Parameters.AddWithValue("@sectionId", sectionId);
            var dupCount = (int)await dupCmd.ExecuteScalarAsync()!;
            if (dupCount > 0)
                return (false, "This ingredient with the same batch and section already exists.");
        }

        var insertSql = @"INSERT INTO tbl_orderingredient 
            (orderingredient_orderdetailID, orderingredient_orderID, orderingredient_batchid, orderingredient_sectionid, orderingredient_createdate)
            VALUES (@odId, @orderId, @batchId, @sectionId, GETDATE())";
        await using var cmd = new SqlCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("@odId", orderDetailId);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@sectionId", sectionId);
        await cmd.ExecuteNonQueryAsync();
        return (true, "");
    }

    /// <summary>
    /// Removes an ingredient. Matching legacy remIng_popup_Click.
    /// </summary>
    public async Task RemoveIngredientAsync(long ingredientId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "DELETE FROM tbl_orderingredient WHERE orderingredient_ID = @id", conn);
        cmd.Parameters.AddWithValue("@id", ingredientId);
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Request model for querying business orders.
/// </summary>
public class BusinessOrdersRequest
{
    public string WebshopId { get; set; } = "";
    public int OrderType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DateMode { get; set; } = 1; // 1=order date, 2=occasion date
    public string? SearchQuery { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int DeliveryMode { get; set; } = 0; // 0=all, 1=collection, 2=delivery by hand, 4=by post
}

/// <summary>
/// Result model containing paginated orders.
/// </summary>
public class BusinessOrdersResult
{
    public int OrderType { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int DeliveryModeFilter { get; set; }
    public List<BusinessOrderItem> Orders { get; set; } = new();
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Individual order item for display in the orders list.
/// </summary>
public class BusinessOrderItem
{
    public long OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime OccasionDate { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime DispatchDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Postcode { get; set; } = "";
    public int Qty { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal CSMargin { get; set; }
    public decimal ShopMargin { get; set; }
    public decimal PayoutRefund { get; set; }
    public decimal CsRefund { get; set; }
    public int DeliveryMode { get; set; }
    public int SaleType { get; set; }
    public int Status { get; set; }
    public bool IsRepeat { get; set; }
    public long FollowingOrderId { get; set; }
    public long ForwardedOrderId { get; set; }
    public long BakeryId { get; set; }
    public long BranchId { get; set; }
    public long CustomerId { get; set; }
    public long ShopId { get; set; }
    public string BranchName { get; set; } = "";
    public string BranchPostcode { get; set; } = "";
    public string PostcodeDistance { get; set; } = "";
    public string ReviewStars { get; set; } = "";
    public string ReviewRemarks { get; set; } = "";
    public List<OrderDetailItem> OrderDetails { get; set; } = new();

    public bool IsForwarded => ForwardedOrderId > 0;
    public decimal BakeryPayout => TotalPrice - CSMargin - ShopMargin;
    public decimal TotalRefund => PayoutRefund + CsRefund;
    public bool HasRefund => PayoutRefund > 0 || CsRefund > 0;

    /// <summary>
    /// Gets distance text like "15.1 Miles from UB1, 00:25"
    /// </summary>
    public string DistanceText
    {
        get
        {
            if (string.IsNullOrEmpty(PostcodeDistance)) return "";
            var parts = PostcodeDistance.Split(',');
            if (parts.Length < 2) return "";
            if (!double.TryParse(parts[0], out var miles)) return "";
            if (!double.TryParse(parts[1], out var seconds)) return "";
            var time = TimeSpan.FromSeconds(seconds);
            return $"{miles:F1} Miles from {BranchPostcode.ToUpper()}, {time:hh\\:mm}";
        }
    }

    /// <summary>
    /// Gets the delivery mode display text (matching legacy logic).
    /// </summary>
    public string GetDeliveryModeText(bool isCSBakery)
    {
        if (SaleType == 2) return "Shop";
        if (SaleType == 3)
        {
            if (CustomerId == ShopId) return "Shop";
            return "Customer";
        }
        bool isfranchise = BakeryId != BranchId;
        if (DeliveryMode == 1)
        {
            if (isfranchise)
                return "Delivery By Bakery<br/>Collection Point <b style='color:#aa041c;'> - " + BranchName + "</b>";
            return "Collection";
        }
        if (DeliveryMode == 2) return "Delivery By Bakery";
        if (DeliveryMode == 4) return "Delivery By Post<br/>(Postal Cake)";
        if (isCSBakery) return "Delivery By Cakerstreet";
        if (DeliveryMode == 3) return "Collection";
        return "Unknown";
    }

    /// <summary>
    /// Gets the status display text.
    /// </summary>
    public string StatusText => Status switch
    {
        0 => "Pending",
        1 => "Confirmed",
        2 => "Processed",
        3 => "Under Delivery",
        4 => "Completed",
        5 => "Job Assigned",
        10 => "Cancelled",
        11 => "Cancelled",
        _ => "Unknown"
    };

    public string StatusButtonClass => Status switch
    {
        0 or 10 or 11 => "btn-danger",
        4 => "btn-info",
        _ => "btn-success"
    };
}

/// <summary>
/// Order detail item (nested repeater data per order).
/// </summary>
public class OrderDetailItem
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public long SubOrderId { get; set; }
    public int Quantity { get; set; }
    public int PrdType { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductSeoUrl { get; set; } = "";
    public int ProductType { get; set; }
    public bool IsExpired { get; set; }
    public bool IsCustomizedPrdMapped { get; set; }
    public int TopperTypeId { get; set; }
    public int IsChangeOrderImageMarked { get; set; }
    public int IsUpdated { get; set; }
}

// ===== MUTATION REQUEST MODELS =====

/// <summary>
/// Request model for updating order status.
/// </summary>
public class OrderStatusRequest
{
    public long OrderId { get; set; }
    public int NewStatus { get; set; }
}

/// <summary>
/// Request model for soft-deleting an order.
/// </summary>
public class OrderDeleteRequest
{
    public long OrderId { get; set; }
}

/// <summary>
/// Request model for confirming an order (status 0 → 1 → 5).
/// </summary>
public class OrderConfirmRequest
{
    public long OrderId { get; set; }
}

/// <summary>
/// Request model for cancelling an order.
/// </summary>
public class OrderCancelRequest
{
    public long OrderId { get; set; }
    public string Reason { get; set; } = "";
    public string Comments { get; set; } = "";
    public bool NotifyCustomer { get; set; }
}

/// <summary>
/// Request model for saving topper type.
/// </summary>
public class SaveTopperRequest
{
    public long OrderId { get; set; }
    public long OrderDetailId { get; set; }
    public bool HasTopper { get; set; }
    public int TopperTypeId { get; set; }
}

public class SaveReviewRequest
{
    public long OrderId { get; set; }
    public int Stars { get; set; }
    public string Remarks { get; set; } = "";
}

public class MarkChangeImageRequest
{
    public long OrderDetailId { get; set; }
    public bool MarkForChange { get; set; }
}

/// <summary>
/// Result model for cancel order operation.
/// </summary>
public class CancelOrderResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Item for order sorting update request. Matching legacy updateordersorting.
/// </summary>
public class OrderSortingItem
{
    public long OrderID { get; set; }
    public int Ordersts { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request model for updating order sorting.
/// </summary>
public class UpdateSortingRequest
{
    public List<OrderSortingItem> Items { get; set; } = new();
}

/// <summary>
/// Ingredient list item for the ViewIngredients popup.
/// </summary>
public class IngredientListItem
{
    public long Id { get; set; }
    public string BatchId { get; set; } = "";
    public int SectionId { get; set; }
    public string SectionName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public DateTime CreateDate { get; set; }
}

public class AddIngredientRequest
{
    public long OrderDetailId { get; set; }
    public long OrderId { get; set; }
    public string BatchId { get; set; } = "";
    public int SectionId { get; set; }
}

public class RemoveIngredientRequest
{
    public long IngredientId { get; set; }
}

public class GetIngredientsRequest
{
    public long OrderDetailId { get; set; }
}
