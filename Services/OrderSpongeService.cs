using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Models;

namespace CakerStreet.Business.Services;

/// <summary>
/// Service for the Order Sponge page (Phase 1 — read-only).
/// Migrated from orderspongelist.aspx.cs + clsglobaltext.cs Order_GetSizeSpongedata.
/// </summary>
public class OrderSpongeService
{
    private readonly string _connectionString;

    public OrderSpongeService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Builds the full sponge grid view model for the given bakery and date range.
    /// </summary>
    public async Task<OrderSpongeViewModel> GetSpongeGridAsync(
        long bakeryId, string fromDate, string toDate, bool includeRequested)
    {
        var model = new OrderSpongeViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            IncludeRequested = includeRequested
        };

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Step 1: Call SP to get flat order items
        var spItems = await CallSpongeSpAsync(conn, bakeryId, fromDate, toDate, includeRequested);
        if (spItems.Count == 0)
        {
            model.HasData = false;
            return model;
        }

        // Step 2: Enrichment — equivalent to Order_GetSizeSpongedata
        var enrichedItems = await EnrichWithSpongeDataAsync(conn, spItems, bakeryId);

        // Step 3: Group by ProductTypeID, Sponge, ShapeID, Dietary, sizeID
        var grouped = enrichedItems
            .GroupBy(g => new { g.ProductTypeId, g.Sponge, g.ShapeId, g.Dietary, g.SizeId })
            .Select((grp, idx) => new SpongeGridRow
            {
                RowId = idx + 1,
                ProductTypeId = grp.Key.ProductTypeId,
                Sponge = grp.Key.Sponge,
                Dietary = grp.Key.Dietary,
                ShapeId = grp.Key.ShapeId,
                Shape = grp.First().Shape,
                Size = grp.First().Size,
                Qty = grp.Sum(x => x.Qty),
                ReqQty = grp.Sum(x => x.Qty),
                OrderThumbs = grp.Select(x => new SpongeOrderThumb
                {
                    OrderId = x.OrderId,
                    Image = x.Image,
                    ProductId = x.ProductId
                }).ToList()
            })
            .OrderByDescending(o => o.Sponge)
            .ThenByDescending(o => o.Dietary)
            .ThenBy(o => o.Shape)
            .ThenByDescending(o => o.Size)
            .ToList();

        // Re-assign RowId after sorting
        for (int i = 0; i < grouped.Count; i++)
            grouped[i].RowId = i + 1;

        model.Rows = grouped;
        model.HasData = true;

        // Step 4: Load dropdown options
        await LoadDropdownOptionsAsync(conn, bakeryId, enrichedItems, model);

        return model;
    }

    // ─── SP Call ───────────────────────────────────────────────────────────────

    private async Task<List<SpOrderItem>> CallSpongeSpAsync(
        SqlConnection conn, long bakeryId, string fromDate, string toDate, bool includeRequested)
    {
        var items = new List<SpOrderItem>();

        await using var cmd = new SqlCommand("dbo.getordermenifestlistbybakeryID_Sponge", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@bakeryID", (int)bakeryId);
        cmd.Parameters.AddWithValue("@dtnow", fromDate);
        cmd.Parameters.AddWithValue("@dt", toDate);
        cmd.Parameters.AddWithValue("@inc", includeRequested ? 1 : 0);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SpOrderItem
            {
                OrderId = reader.IsDBNull(reader.GetOrdinal("order_ID")) ? 0 : Convert.ToInt64(reader["order_ID"]),
                OrderDetailId = reader.IsDBNull(reader.GetOrdinal("orderDetail_ID")) ? 0 : Convert.ToInt64(reader["orderDetail_ID"]),
                ProductId = reader.IsDBNull(reader.GetOrdinal("orderDetail_productID")) ? 0 : Convert.ToInt64(reader["orderDetail_productID"]),
                ProductImage = reader.IsDBNull(reader.GetOrdinal("orderDetail_ProductImage")) ? "" : reader["orderDetail_ProductImage"].ToString() ?? ""
            });
        }

        return items;
    }

    // ─── Enrichment (Order_GetSizeSpongedata equivalent) ───────────────────────

    private async Task<List<EnrichedSpongeItem>> EnrichWithSpongeDataAsync(
        SqlConnection conn, List<SpOrderItem> spItems, long bakeryId)
    {
        var result = new List<EnrichedSpongeItem>();
        if (spItems.Count == 0) return result;

        var orderIds = spItems.Select(x => x.OrderId).Distinct().ToList();
        var orderIdList = string.Join(",", orderIds);

        // Query order details with product info for these orders
        var detailSql = $@"SELECT od.orderDetail_ID, od.orderDetail_orderID, od.orderDetail_productID,
                od.orderDetail_shapeId, od.orderDetail_SizeID, od.orderDetail_Quantity,
                p.product_type,
                CASE WHEN od.orderDetail_ProductImage LIKE 'http%' 
                     THEN od.orderDetail_ProductImage 
                     ELSE 'https://www.cakerstreet.com' + ISNULL(od.orderDetail_ProductImage, '') 
                END AS PrdImage
            FROM tbl_orderDetail od
            INNER JOIN tbl_products p ON p.product_ID = od.orderDetail_productID
            WHERE od.orderDetail_orderID IN ({orderIdList})
              AND od.orderDetail_shapeId > 0";

        var details = new List<OrderDetailRow>();
        await using (var cmd = new SqlCommand(detailSql, conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var shapeId = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]);
                // Shape ID 13 maps to 12 (legacy line 6852)
                if (shapeId == 13) shapeId = 12;

                details.Add(new OrderDetailRow
                {
                    OrderDetailId = Convert.ToInt64(reader[0]),
                    OrderId = Convert.ToInt64(reader[1]),
                    ProductId = Convert.ToInt64(reader[2]),
                    ShapeId = shapeId,
                    SizeId = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]),
                    Quantity = reader.IsDBNull(5) ? 1 : Convert.ToInt32(reader[5]),
                    ProductType = reader.IsDBNull(6) ? 1 : Convert.ToInt32(reader[6]),
                    Image = reader.IsDBNull(7) ? "" : reader[7].ToString() ?? ""
                });
            }
        }

        if (details.Count == 0) return result;

        // Load shapes
        var shapeIds = details.Select(d => d.ShapeId).Where(s => s > 0).Distinct().ToList();
        var shapeLookup = new Dictionary<int, string>();
        if (shapeIds.Count > 0)
        {
            var shapeIdList = string.Join(",", shapeIds);
            var shapeSql = $"SELECT CakeShapeId, CakeShapeTitle FROM tbl_CakeShape WHERE CakeShapeId IN ({shapeIdList})";
            await using var cmd = new SqlCommand(shapeSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shapeLookup[Convert.ToInt32(reader[0])] = reader.IsDBNull(1) ? "" : reader[1].ToString() ?? "";
            }
        }

        // Load sizes
        var sizeIds = details.Select(d => d.SizeId).Where(s => s > 0).Distinct().ToList();
        var sizeLookup = new Dictionary<int, string>();
        if (sizeIds.Count > 0)
        {
            var sizeIdList = string.Join(",", sizeIds);
            var sizeSql = $"SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE SizeID IN ({sizeIdList})";
            await using var cmd = new SqlCommand(sizeSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sizeLookup[Convert.ToInt32(reader[0])] = reader.IsDBNull(1) ? "" : reader[1].ToString() ?? "";
            }
        }

        // Load order attributes (flavourType = 2) for sponge/dietary resolution
        var orderDetailIds = details.Select(d => d.OrderDetailId).Distinct().ToList();
        var orderDetailIdList = string.Join(",", orderDetailIds);

        var attSql = $@"SELECT orderAttDet_orderdetID, orderAttDet_ParentAttId, orderAttDet_AttIDs
            FROM tbl_orderAttDet
            WHERE orderAttDet_flavourType = 2
              AND orderAttDet_orderdetID IN ({orderDetailIdList})";

        var attRows = new List<AttDetailRow>();
        await using (var cmd = new SqlCommand(attSql, conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                attRows.Add(new AttDetailRow
                {
                    OrderDetailId = Convert.ToInt64(reader[0]),
                    ParentAttId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                    AttIds = reader.IsDBNull(2) ? "" : reader[2].ToString() ?? ""
                });
            }
        }

        // Resolve parent flavour titles and child flavour titles
        var allParentIds = attRows.Select(a => a.ParentAttId).Where(id => id > 0).Distinct().ToList();
        var allChildIds = attRows
            .SelectMany(a => ParseIds(a.AttIds))
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var allFlavourIds = allParentIds.Concat(allChildIds).Distinct().ToList();

        var flavourLookup = new Dictionary<int, string>();
        if (allFlavourIds.Count > 0)
        {
            var flavourIdList = string.Join(",", allFlavourIds);
            var flavourSql = $"SELECT flavourID, FlavourTitle FROM tbl_custflavour WHERE flavourID IN ({flavourIdList})";
            await using var cmd = new SqlCommand(flavourSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                flavourLookup[Convert.ToInt32(reader[0])] = reader.IsDBNull(1) ? "" : reader[1].ToString() ?? "";
            }
        }

        // Build enriched items
        foreach (var detail in details)
        {
            var sponge = "";
            var dietary = "";
            var spongeId = 0;
            var dietaryId = 0;

            var detailAtts = attRows.Where(a => a.OrderDetailId == detail.OrderDetailId).ToList();
            foreach (var att in detailAtts)
            {
                var parentTitle = flavourLookup.GetValueOrDefault(att.ParentAttId, "");
                var childIds = ParseIds(att.AttIds);

                if (parentTitle.Contains("sponge", StringComparison.OrdinalIgnoreCase))
                {
                    // Last matching child becomes the sponge value
                    foreach (var childId in childIds)
                    {
                        if (flavourLookup.TryGetValue(childId, out var childTitle) && !string.IsNullOrEmpty(childTitle))
                        {
                            sponge = childTitle;
                            spongeId = childId;
                        }
                    }
                }
                else if (parentTitle.Contains("dietary", StringComparison.OrdinalIgnoreCase))
                {
                    // Last matching child becomes the dietary value
                    foreach (var childId in childIds)
                    {
                        if (flavourLookup.TryGetValue(childId, out var childTitle) && !string.IsNullOrEmpty(childTitle))
                        {
                            dietary = childTitle;
                            dietaryId = childId;
                        }
                    }
                }
            }

            // Special case: "Nut Free" → "No Dietary Restrictions"
            if (dietary.Equals("Nut Free", StringComparison.OrdinalIgnoreCase))
                dietary = "No Dietary Restrictions";

            result.Add(new EnrichedSpongeItem
            {
                OrderId = detail.OrderId,
                OrderDetailId = detail.OrderDetailId,
                ProductId = detail.ProductId,
                ProductTypeId = detail.ProductType,
                ShapeId = detail.ShapeId,
                Shape = shapeLookup.GetValueOrDefault(detail.ShapeId, ""),
                SizeId = detail.SizeId,
                Size = sizeLookup.GetValueOrDefault(detail.SizeId, ""),
                Sponge = sponge,
                SpongeId = spongeId,
                Dietary = dietary,
                DietaryId = dietaryId,
                Qty = detail.Quantity,
                Image = detail.Image
            });
        }

        return result;
    }

    // ─── Dropdown Options ──────────────────────────────────────────────────────

    private async Task LoadDropdownOptionsAsync(
        SqlConnection conn, long bakeryId, List<EnrichedSpongeItem> items, OrderSpongeViewModel model)
    {
        // Shapes: active shapes ordered by display order
        var shapes = new List<ShapeOption>();
        var shapeSql = "SELECT CakeShapeId, CakeShapeTitle FROM tbl_CakeShape WHERE IsActive = 1 ORDER BY DisplayOrder";
        await using (var cmd = new SqlCommand(shapeSql, conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shapes.Add(new ShapeOption
                {
                    Id = Convert.ToInt32(reader[0]),
                    Title = reader.IsDBNull(1) ? "" : reader[1].ToString() ?? ""
                });
            }
        }
        model.Shapes = shapes;

        // Sizes: bakery-specific sizes
        var sizes = new List<string>();
        var sizeSql = "SELECT SizeTitle FROM tbl_CakeSize WHERE custid = @bakeryId AND IsActive = 1 ORDER BY DisplayOrder";
        await using (var cmd = new SqlCommand(sizeSql, conn))
        {
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var title = reader.IsDBNull(0) ? "" : reader[0].ToString() ?? "";
                if (!string.IsNullOrEmpty(title))
                    sizes.Add(title);
            }
        }
        model.Sizes = sizes;

        // Sponge types: from flavour table based on sponge IDs found in data
        var spongeIds = items.Where(i => i.SpongeId > 0).Select(i => i.SpongeId).Distinct().ToList();
        if (spongeIds.Count > 0)
        {
            var spongeIdList = string.Join(",", spongeIds);
            var spongeSql = $@"SELECT FlavourTitle FROM tbl_custflavour 
                WHERE floavour_parentid IN (
                    SELECT floavour_parentid FROM tbl_custflavour 
                    WHERE flavourID IN ({spongeIdList}) AND Flavour_WebstoreID = @bakeryId
                ) AND FlavourTitle <> '' 
                GROUP BY FlavourTitle";
            await using var cmd = new SqlCommand(spongeSql, conn);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var title = reader.IsDBNull(0) ? "" : reader[0].ToString() ?? "";
                if (!string.IsNullOrEmpty(title))
                    model.SpongeTypes.Add(title);
            }
        }

        // Dietary types: from flavour table based on dietary IDs found in data
        var dietaryIds = items.Where(i => i.DietaryId > 0).Select(i => i.DietaryId).Distinct().ToList();
        if (dietaryIds.Count > 0)
        {
            var dietaryIdList = string.Join(",", dietaryIds);
            var dietarySql = $@"SELECT FlavourTitle FROM tbl_custflavour 
                WHERE floavour_parentid IN (
                    SELECT floavour_parentid FROM tbl_custflavour 
                    WHERE flavourID IN ({dietaryIdList}) AND Flavour_WebstoreID = @bakeryId
                ) AND FlavourTitle NOT IN ('', 'Nut Free') 
                GROUP BY FlavourTitle";
            await using var cmd = new SqlCommand(dietarySql, conn);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var title = reader.IsDBNull(0) ? "" : reader[0].ToString() ?? "";
                if (!string.IsNullOrEmpty(title))
                    model.DietaryTypes.Add(title);
            }
        }
    }

    // ─── Order History ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the sponge order history for a bakery (read-only list).
    /// Source: managespongeorderlist.aspx.cs bindgrid()
    /// </summary>
    public async Task<List<SpongeOrderHistoryItem>> GetOrderHistoryAsync(long webshopId)
    {
        var items = new List<SpongeOrderHistoryItem>();
        
        var sql = @"SELECT TOP 50 so.spongeOrder_ID, so.spongeOrder_modifiedOn, so.spongeOrder_name, 
            so.spongeOrder_emailID, so.spongeOrder_TotalQty, so.spongeOrder_remarks,
            bu.customer_Name
            FROM tbl_spongeOrder so
            INNER JOIN tbl_bakeryuser bu ON so.spongeOrder_custID = bu.customer_ID
            WHERE so.spongeOrder_isdeleted = 0 AND so.spongeOrder_WebstoreId = @webshopId
            ORDER BY so.spongeOrder_modifiedOn DESC";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SpongeOrderHistoryItem
            {
                Id = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader[0]),
                ModifiedOn = reader.IsDBNull(1) ? DateTime.MinValue : Convert.ToDateTime(reader[1]),
                Name = reader.IsDBNull(2) ? "" : reader[2].ToString() ?? "",
                Email = reader.IsDBNull(3) ? "" : reader[3].ToString() ?? "",
                TotalQty = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]),
                Remarks = reader.IsDBNull(5) ? "" : reader[5].ToString() ?? "",
                CreatedBy = reader.IsDBNull(6) ? "" : reader[6].ToString() ?? ""
            });
        }
        
        return items;
    }

    // ─── Submit ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Phase 2B-1: Creates sponge order records (header + details + order ID links).
    /// NO shape propagation. NO email. Wrapped in a single transaction.
    /// Source: orderspongelist.aspx.cs btnSubmit_Onclick Steps 1-4, 7.
    /// </summary>
    public async Task<SpongeSubmitResult> SubmitSpongeOrderAsync(
        long webshopId, int userId, SpongeSubmitRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return new SpongeSubmitResult { Success = false, Error = "Full Name is required" };
        if (string.IsNullOrWhiteSpace(request.OrderDate))
            return new SpongeSubmitResult { Success = false, Error = "Enter Order Date" };
        if (string.IsNullOrWhiteSpace(request.DeliveryDate))
            return new SpongeSubmitResult { Success = false, Error = "Enter Delivery Date" };
        if (request.Rows == null || request.Rows.Count == 0)
            return new SpongeSubmitResult { Success = false, Error = "No sponge items to submit" };
        if (request.SendMail && string.IsNullOrWhiteSpace(request.Email))
            return new SpongeSubmitResult { Success = false, Error = "Email id is required" };

        // Parse dates
        DateTime orderDate, deliveryDate, fromDate, toDate;
        try
        {
            orderDate = DateTime.ParseExact(request.OrderDate, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            deliveryDate = DateTime.ParseExact(request.DeliveryDate, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            fromDate = DateTime.ParseExact(request.FromDate, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            toDate = DateTime.ParseExact(request.ToDate, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return new SpongeSubmitResult { Success = false, Error = "Invalid date format. Use dd/MM/yyyy." };
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            // Step 1: INSERT tbl_spongeOrder
            var insertHeaderSql = @"INSERT INTO tbl_spongeOrder 
                (spongeOrder_custID, spongeOrder_modifiedOn, spongeOrder_remarks, spongeOrder_name,
                 spongeOrder_emailID, spongeOrder_Orderdate, spongeOrder_ReqDate, spongeOrder_TotalQty,
                 spongeOrder_TotalorgQty, spongeOrder_WebstoreId, spongeOrder_FromDate,
                 spongeOrder_ToDate, spongeOrder_isdeleted)
                OUTPUT INSERTED.spongeOrder_ID
                VALUES (@custId, GETDATE(), @remarks, @name, @email, @orderDate, @deliveryDate, 0, 0,
                        @webshopId, @fromDate, @toDate, 0)";

            long spongeOrderId;
            await using (var cmd = new SqlCommand(insertHeaderSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@custId", userId);
                cmd.Parameters.AddWithValue("@remarks", request.Remarks ?? "");
                cmd.Parameters.AddWithValue("@name", request.Name);
                cmd.Parameters.AddWithValue("@email", request.Email ?? "");
                cmd.Parameters.AddWithValue("@orderDate", orderDate);
                cmd.Parameters.AddWithValue("@deliveryDate", deliveryDate);
                cmd.Parameters.AddWithValue("@webshopId", webshopId);
                cmd.Parameters.AddWithValue("@fromDate", fromDate);
                cmd.Parameters.AddWithValue("@toDate", toDate);
                spongeOrderId = (long)(await cmd.ExecuteScalarAsync())!;
            }

            int totalQty = 0;
            int totalOrgQty = 0;

            // Step 2-4: For each row, insert detail + order ID links
            foreach (var row in request.Rows)
            {
                // Step 2: INSERT tbl_spongeOrderDet
                var insertDetSql = @"INSERT INTO tbl_spongeOrderDet
                    (spongeOrderDet_fkID, spongeOrderDet_PrdType, spongeOrderDet_OrderID,
                     spongeOrderDet_SpongeTitle, spongeOrderDet_shape, spongeOrderDet_size,
                     spongeOrderDet_dietery, spongeOrderDet_sponge, spongeOrderDet_orgqty,
                     spongeOrderDet_qty, spongeOrderDet_modifiedOn)
                    OUTPUT INSERTED.spongeOrderDet_ID
                    VALUES (@fkId, @prdType, @orderId, @spongeTitle, @shape, @size,
                            @dietary, @sponge, @orgQty, @reqQty, GETDATE())";

                long detId;
                await using (var cmd = new SqlCommand(insertDetSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fkId", spongeOrderId);
                    cmd.Parameters.AddWithValue("@prdType", row.ProductTypeId);
                    cmd.Parameters.AddWithValue("@orderId", row.OrderIds.Count > 0 ? row.OrderIds[0] : 0);
                    cmd.Parameters.AddWithValue("@spongeTitle", "");
                    cmd.Parameters.AddWithValue("@shape", row.ShapeTitle);
                    cmd.Parameters.AddWithValue("@size", row.Size);
                    cmd.Parameters.AddWithValue("@dietary", row.Dietary);
                    cmd.Parameters.AddWithValue("@sponge", row.Sponge);
                    cmd.Parameters.AddWithValue("@orgQty", row.OriginalQty);
                    cmd.Parameters.AddWithValue("@reqQty", row.RequestedQty);
                    detId = (long)(await cmd.ExecuteScalarAsync())!;
                }

                // Step 3: For each order ID, insert link
                foreach (var orderId in row.OrderIds)
                {
                    var insertLinkSql = @"INSERT INTO tbl_spongeOrderDet_OrderIds (OrderID, spongeOrderDet_ID)
                        VALUES (@orderId, @detId)";
                    await using var cmd = new SqlCommand(insertLinkSql, conn, transaction);
                    cmd.Parameters.AddWithValue("@orderId", orderId);
                    cmd.Parameters.AddWithValue("@detId", detId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Step 4: Update SpongeTitle with comma-joined order IDs (legacy quirk)
                if (row.OrderIds.Count > 0)
                {
                    var updateTitleSql = @"UPDATE tbl_spongeOrderDet SET spongeOrderDet_SpongeTitle = @title
                        WHERE spongeOrderDet_ID = @detId";
                    await using var cmd = new SqlCommand(updateTitleSql, conn, transaction);
                    cmd.Parameters.AddWithValue("@title", string.Join(", ", row.OrderIds));
                    cmd.Parameters.AddWithValue("@detId", detId);
                    await cmd.ExecuteNonQueryAsync();
                }

                totalQty += row.RequestedQty;
                totalOrgQty += row.OriginalQty;
            }

            // Step 7: Update totals
            var updateTotalsSql = @"UPDATE tbl_spongeOrder 
                SET spongeOrder_TotalQty = @totalQty, spongeOrder_TotalorgQty = @totalOrgQty
                WHERE spongeOrder_ID = @id";
            await using (var cmd = new SqlCommand(updateTotalsSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@totalQty", totalQty);
                cmd.Parameters.AddWithValue("@totalOrgQty", totalOrgQty);
                cmd.Parameters.AddWithValue("@id", spongeOrderId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return new SpongeSubmitResult { Success = true, SpongeOrderId = spongeOrderId };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return new SpongeSubmitResult { Success = false, Error = "Save failed. Please try again." };
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static List<int> ParseIds(string commaSeparated)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated)) return new List<int>();
        return commaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    // ─── Internal DTOs ─────────────────────────────────────────────────────────

    private class SpOrderItem
    {
        public long OrderId { get; set; }
        public long OrderDetailId { get; set; }
        public long ProductId { get; set; }
        public string ProductImage { get; set; } = "";
    }

    private class OrderDetailRow
    {
        public long OrderDetailId { get; set; }
        public long OrderId { get; set; }
        public long ProductId { get; set; }
        public int ShapeId { get; set; }
        public int SizeId { get; set; }
        public int Quantity { get; set; }
        public int ProductType { get; set; }
        public string Image { get; set; } = "";
    }

    private class AttDetailRow
    {
        public long OrderDetailId { get; set; }
        public int ParentAttId { get; set; }
        public string AttIds { get; set; } = "";
    }

    private class EnrichedSpongeItem
    {
        public long OrderId { get; set; }
        public long OrderDetailId { get; set; }
        public long ProductId { get; set; }
        public int ProductTypeId { get; set; }
        public int ShapeId { get; set; }
        public string Shape { get; set; } = "";
        public int SizeId { get; set; }
        public string Size { get; set; } = "";
        public string Sponge { get; set; } = "";
        public int SpongeId { get; set; }
        public string Dietary { get; set; } = "";
        public int DietaryId { get; set; }
        public int Qty { get; set; }
        public string Image { get; set; } = "";
    }
}
