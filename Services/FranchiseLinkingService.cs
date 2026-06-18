using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class FranchiseItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public int Status { get; set; }
    public bool IsActive { get; set; }
}

public class FranchiseCategoryItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsService { get; set; }
    public long ParentId { get; set; }
    public string ProductImage { get; set; } = "";
}

public class ProductFranchiseLinkingDetail
{
    public long Id { get; set; }
    public bool IsService { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string SeoUrl { get; set; } = "";
    public int Ordered { get; set; }
    public int Delivered { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string DeliveryReceivedBy { get; set; } = "";
    public int MinStockReq { get; set; }
    public DateTime? OrderDate { get; set; }
    public decimal Price { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public decimal TotalInvestment { get; set; }
    public string CategoryTitle { get; set; } = "";
    public long CategoryId { get; set; }
    public long FranchiseId { get; set; }
    public string SupplierRemarks { get; set; } = "";
    public string PrdStatus { get; set; } = "";
    public string Notes { get; set; } = "";
    public string AlternateSupplierName { get; set; } = "";
    public string AlternateSupplierRemarks { get; set; } = "";
    public string SupplierImage { get; set; } = "";
    public string ServiceDesc { get; set; } = "";
    public string ServiceRecommended { get; set; } = "";
    public string RecurringCustomMode { get; set; } = "";
}

public class FranchiseSupplierItem
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public long ProductId { get; set; }
}

public class SupplierCostDetail
{
    public string SupplierName { get; set; } = "";
    public string Remarks { get; set; } = "";
    public decimal Cost { get; set; }
    public int MinQty { get; set; }
    public decimal TotalInvestment { get; set; }
}

public class FranchiseLinkingService
{
    private readonly string _businessConnection;
    private readonly string _defaultConnection;

    public FranchiseLinkingService(IConfiguration config)
    {
        _businessConnection = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<List<FranchiseItem>> GetFranchisesAsync()
    {
        var list = new List<FranchiseItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT ID, Title, Status, isActive FROM tbl_tempFranchise WHERE IsDeleted = 0 ORDER BY Title";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Status = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                IsActive = reader.IsDBNull(3) ? false : reader.GetBoolean(3)
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> GetSectionsAsync()
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT ID, Title, IsService, Parent_Id FROM tbl_tempFranchiseCat WHERE Parent_Id = 0 ORDER BY Title";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = reader.IsDBNull(2) ? false : reader.GetBoolean(2),
                ParentId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3)
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> GetCategoriesAsync(long parentId)
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT ID, Title, IsService, Parent_Id FROM tbl_tempFranchiseCat WHERE Parent_Id = @parentId ORDER BY Title";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@parentId", parentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = reader.IsDBNull(2) ? false : reader.GetBoolean(2),
                ParentId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3)
            });
        }
        return list;
    }

    public async Task<FranchiseCategoryItem?> GetCategoryDetailsAsync(long catId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = "SELECT ID, Title, IsService, Parent_Id FROM tbl_tempFranchiseCat WHERE ID = @catId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catId", catId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = reader.IsDBNull(2) ? false : reader.GetBoolean(2),
                ParentId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3)
            };
        }
        return null;
    }

    public async Task<long> SaveFranchiseAsync(string title, int status)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Check if exists
        var checkSql = "SELECT ID FROM tbl_tempFranchise WHERE LOWER(TRIM(Title)) = LOWER(TRIM(@title)) AND IsDeleted = 0";
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@title", title);
            var existingId = await checkCmd.ExecuteScalarAsync();
            if (existingId != null)
            {
                return -1; // Already exists
            }
        }

        var insSql = @"INSERT INTO tbl_tempFranchise (Title, Status, isActive, IsDeleted, CreatedOn, ModifiedOn)
                       VALUES (@title, @status, 1, 0, GETDATE(), GETDATE());
                       SELECT SCOPE_IDENTITY();";
        await using var cmd = new SqlCommand(insSql, conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@status", status);
        
        var idVal = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(idVal);
    }

    public async Task<(int allCount, int pendingCount, int underDeliveryCount, int deliveredCount)> GetFranchiseLinkingCountsAsync(long catId, long fid)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT COUNT(1) [All], 
                   SUM(CASE WHEN l.Ordered = 0 THEN 1 ELSE 0 END) as Pending, 
                   SUM(CASE WHEN l.Ordered = 1 and l.Delivered = 0 THEN 1 ELSE 0 END) as UnderDelivery,
                   SUM(CASE WHEN l.Delivered = 1 THEN 1 ELSE 0 END) as Delivered
            FROM tbl_lnkItem2tempfranchise l 
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_products p on p.product_ID = l.ProductID
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_Service srv on l.Service_ID = srv.Service_ID
            INNER JOIN tbl_tempFranchiseCat c on l.tempFranchise_CatId = c.ID
            WHERE l.tempFranchise_CatId = @catid and l.tempFranchise_Id = @fid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@fid", fid);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int allCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            int pendingCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            int underDeliveryCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            int deliveredCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            return (allCount, pendingCount, underDeliveryCount, deliveredCount);
        }
        return (0, 0, 0, 0);
    }

    public async Task<List<ProductFranchiseLinkingDetail>> GetProductFranchiseLinkingAsync(long catId, long fid, int filter)
    {
        var list = new List<ProductFranchiseLinkingDetail>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT l.id, 
                   IsService = case when l.Service_ID > 0 then 1 else 0 end, 
                   product_id = case when l.Service_ID > 0 then srv.Service_ID else p.product_id end, 
                   product_name = case when l.Service_ID > 0 then srv.Service_Name else p.product_name end, 
                   product_image1 = case when l.Service_ID > 0 then srv.Service_Image else p.product_image1 end, 
                   product_code = case when l.Service_ID > 0 then '' else p.product_code end, 
                   product_seourl = case when l.Service_ID > 0 then '' else p.product_seourl end, 
                   l.Ordered,
                   l.Delivered, 
                   l.Delivery_Date, 
                   l.Delivery_ReceivedBy, 
                   l.Min_StockReq, 
                   l.Order_Date, 
                   l.Price, 
                   l.SupplierId, 
                   s.SupplierName, 
                   l.Total_Investment, 
                   c.Title, 
                   l.tempFranchise_CatId, 
                   l.tempFranchise_Id, 
                   Supplier_Remarks=lnk.Remarks, 
                   prd_status = case when l.Ordered = 0 then '<font color=''red''>Pending</font>' when l.Ordered = 1 and l.Delivered = 0 then '<font color=''orange''>Under Delivery</font>' when l.Delivered = 1 then '<font color=''green''>Delivered</font>' else '' end, 
                   Notes = isnull(orm.tempFranchiseNotes_Remarks, 'No Notes Found'),
                   l.Alternate_SupplierName,
                   l.Alternate_SupplierRemarks,
                   l.Supplier_Image,
                   srv.Service_Desc, 
                   Service_Recommened = case when Is_Recommed = 1 then ' (Recommened)' else '' end,
                   Recurring_CustomMode = case when Recurring_or_Online = 2 then 'Fixed' else + (case Recurring_Mode when 1 then 'Weekly' when 2 then 'Monthly' when 3 then 'Quaterly' when 4 then 'Yearly' else '' end) end
            FROM tbl_lnkItem2tempfranchise l 
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_products p on p.product_ID = l.ProductID
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_Service srv on l.Service_ID = srv.Service_ID
            INNER JOIN tbl_tempFranchiseCat c on l.tempFranchise_CatId = c.ID
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_ProductSupplier s on l.SupplierId = s.SupplierId
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_Product_Supplier_Linking lnk on lnk.SupplierId = s.SupplierId and lnk.Product_Id = l.ProductID
            LEFT OUTER JOIN
            (
                SELECT * FROM (
                    SELECT lnkItem2tempfranchise_ID, tempFranchiseNotes_Remarks, 
                           row_number() over (PARTITION BY lnkItem2tempfranchise_ID ORDER BY tempFranchiseNotes_modifiedOn DESC) as row_num 
                    FROM tbl_tempFranchiseNotes 
                ) as franchise_remarks WHERE franchise_remarks.row_num = 1
            ) as orm on l.ID = orm.lnkItem2tempfranchise_ID
            WHERE l.tempFranchise_CatId = @catid 
              and l.tempFranchise_Id = @fid 
              and (
                   (@filter = 0 and 1 = 1) or 
                   (@filter = 1 and l.Ordered = 0) or 
                   (@filter = 2 and (l.Ordered = 1 and l.Delivered = 0)) or 
                   (@filter = 3 and l.Delivered = 1)
              ) 
            ORDER BY l.ID desc";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@fid", fid);
        cmd.Parameters.AddWithValue("@filter", filter);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductFranchiseLinkingDetail
            {
                Id = reader.GetInt64(0),
                IsService = reader.GetBoolean(1),
                ProductId = reader.GetInt64(2),
                ProductName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ProductImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ProductCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SeoUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Ordered = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                Delivered = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                DeliveryDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                DeliveryReceivedBy = reader.IsDBNull(10) ? "" : reader.GetString(10),
                MinStockReq = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                OrderDate = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                Price = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                SupplierId = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                SupplierName = reader.IsDBNull(15) ? "" : reader.GetString(15),
                TotalInvestment = reader.IsDBNull(16) ? 0 : reader.GetDecimal(16),
                CategoryTitle = reader.IsDBNull(17) ? "" : reader.GetString(17),
                CategoryId = reader.GetInt64(18),
                FranchiseId = reader.GetInt64(19),
                SupplierRemarks = reader.IsDBNull(20) ? "" : reader.GetString(20),
                PrdStatus = reader.IsDBNull(21) ? "" : reader.GetString(21),
                Notes = reader.IsDBNull(22) ? "" : reader.GetString(22),
                AlternateSupplierName = reader.IsDBNull(23) ? "" : reader.GetString(23),
                AlternateSupplierRemarks = reader.IsDBNull(24) ? "" : reader.GetString(24),
                SupplierImage = reader.IsDBNull(25) ? "" : reader.GetString(25),
                ServiceDesc = reader.IsDBNull(26) ? "" : reader.GetString(26),
                ServiceRecommended = reader.IsDBNull(27) ? "" : reader.GetString(27),
                RecurringCustomMode = reader.IsDBNull(28) ? "" : reader.GetString(28),
            });
        }
        return list;
    }

    public async Task<List<FranchiseSupplierItem>> GetProductSuppliersAsync(string productIds, long webshopId)
    {
        var list = new List<FranchiseSupplierItem>();
        if (string.IsNullOrEmpty(productIds)) return list;

        // Strip any malicious characters to prevent SQL injection for In clause
        var cleanIds = string.Join(",", productIds.Split(',').Select(s => long.TryParse(s, out var v) ? v.ToString() : "0"));

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $@"SELECT s.SupplierId, s.SupplierName, l.Product_Id 
                     FROM tbl_ProductSupplier s 
                     INNER JOIN tbl_Product_Supplier_Linking l on s.SupplierId = l.SupplierId 
                     WHERE s.Suppllier_IsDeleted = 0 
                       AND s.WebstoreId = @wid 
                       AND l.Product_Id IN ({cleanIds})";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseSupplierItem
            {
                SupplierId = reader.GetInt32(0),
                SupplierName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductId = reader.GetInt64(2)
            });
        }
        return list;
    }

    public async Task<SupplierCostDetail?> GetSupplierCostDetailAsync(int supplierId, long productId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT s.SupplierName, l.Remarks, Cost, Min_Qty, Cost * Min_Qty 
                    FROM tbl_Product_Supplier_Linking l 
                    INNER JOIN tbl_ProductSupplier s on l.SupplierId = s.SupplierId 
                    WHERE l.SupplierId = @sid AND l.Product_Id = @pid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", supplierId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SupplierCostDetail
            {
                SupplierName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                Remarks = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Cost = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                MinQty = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                TotalInvestment = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
            };
        }
        return null;
    }

    public async Task<bool> UpdateLinkedItemAsync(
        long id, int supplierId, decimal price, int minStockReq, decimal totalInvestment,
        int ordered, DateTime? orderDate, int delivered, DateTime? deliveryDate, 
        string deliveryReceivedBy, string altSupplierName, string altSupplierRemarks, 
        string supplierImage = "")
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"
            UPDATE tbl_lnkItem2tempfranchise
            SET SupplierId = @supplierId,
                Price = @price,
                Min_StockReq = @minStockReq,
                Total_Investment = @totalInvestment,
                Ordered = @ordered,
                Order_Date = @orderDate,
                Delivered = @delivered,
                Delivery_Date = @deliveryDate,
                Delivery_ReceivedBy = @deliveryReceivedBy,
                Alternate_SupplierName = @altSupplierName,
                Alternate_SupplierRemarks = @altSupplierRemarks" +
                (!string.IsNullOrEmpty(supplierImage) ? ", Supplier_Image = @supplierImage" : "") + @"
            WHERE ID = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@supplierId", supplierId);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@minStockReq", minStockReq);
        cmd.Parameters.AddWithValue("@totalInvestment", totalInvestment);
        cmd.Parameters.AddWithValue("@ordered", ordered);
        cmd.Parameters.AddWithValue("@orderDate", (object?)orderDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@delivered", delivered);
        cmd.Parameters.AddWithValue("@deliveryDate", (object?)deliveryDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@deliveryReceivedBy", deliveryReceivedBy ?? "");
        cmd.Parameters.AddWithValue("@altSupplierName", altSupplierName ?? "");
        cmd.Parameters.AddWithValue("@altSupplierRemarks", altSupplierRemarks ?? "");
        cmd.Parameters.AddWithValue("@id", id);
        if (!string.IsNullOrEmpty(supplierImage))
        {
            cmd.Parameters.AddWithValue("@supplierImage", supplierImage);
        }

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UnlinkFranchiseItemAsync(long fid, long catId, long productId, int supplierId)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // In legacy: query = @"delete from tbl_lnkItem2tempfranchise where tempFranchise_CatId = @catid and tempFranchise_Id = @fid and ProductID = @pid"
        // Wait, why did the AJAX parameter include 'sid'? Ah, to match the layout. But let's delete using fid, catid and productid (or service_id if it's service!).
        var sql = @"DELETE FROM tbl_lnkItem2tempfranchise 
                    WHERE tempFranchise_CatId = @catid 
                      AND tempFranchise_Id = @fid 
                      AND (ProductID = @pid OR Service_ID = @pid)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@fid", fid);
        cmd.Parameters.AddWithValue("@pid", productId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> LinkProductOrServiceAsync(long fid, long catId, long productId, int isService)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        // Check if already linked
        var checkSql = isService == 1
            ? "SELECT ID FROM tbl_lnkItem2tempfranchise WHERE tempFranchise_CatId = @catid AND tempFranchise_Id = @fid AND Service_ID = @pid"
            : "SELECT ID FROM tbl_lnkItem2tempfranchise WHERE tempFranchise_CatId = @catid AND tempFranchise_Id = @fid AND ProductID = @pid";

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@catid", catId);
            checkCmd.Parameters.AddWithValue("@fid", fid);
            checkCmd.Parameters.AddWithValue("@pid", productId);
            var linked = await checkCmd.ExecuteScalarAsync();
            if (linked != null)
            {
                return false; // Already linked
            }
        }

        // Get initial price
        decimal price = 0;
        if (isService == 1)
        {
            price = await GetServicePriceAsync(productId);
        }

        var insSql = @"
            INSERT INTO tbl_lnkItem2tempfranchise 
            (Delivered, Delivery_Date, Delivery_ReceivedBy, Min_StockReq, Order_Date, Ordered, Price, ProductID, Service_ID, SupplierId, tempFranchise_CatId, tempFranchise_Id, Total_Investment)
            VALUES 
            (0, GETDATE(), '', @minStock, GETDATE(), 0, @price, @productId, @serviceId, 0, @catid, @fid, @price)";

        await using var cmd = new SqlCommand(insSql, conn);
        cmd.Parameters.AddWithValue("@minStock", isService == 1 ? 1 : 0);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@productId", isService == 0 ? productId : 0);
        cmd.Parameters.AddWithValue("@serviceId", isService == 1 ? productId : 0);
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@fid", fid);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private async Task<decimal> GetServicePriceAsync(long serviceId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT Market_Price FROM tbl_Service WHERE Service_ID = @sid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", serviceId);
        var res = await cmd.ExecuteScalarAsync();
        return res != null ? Convert.ToDecimal(res) : 0;
    }

    public async Task<List<FranchiseCategoryItem>> GetServiceCategoryDropdownAsync()
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            ;WITH RCTE AS (
                SELECT a.category_ID, a.category_Name, CAST(a.category_Name as varchar(2000)) as FullLocation, 
                       a.catgory_refCategoryID, 1 AS Lvl, a.category_displayOrder
                FROM tbl_category a 
                WHERE a.catgory_refCategoryID = 0 AND a.category_for = 3 AND a.category_isActive = 1

                UNION ALL

                SELECT rh.category_ID, rh.category_Name, CAST(rc.FullLocation + ' > ' + rh.category_Name as varchar(2000)) as FullLocation, 
                       rh.catgory_refCategoryID, Lvl+1 as Lvl, rh.category_displayOrder
                FROM tbl_category rh 
                INNER JOIN RCTE rc on rh.catgory_refCategoryID = rc.category_ID 
                WHERE rh.category_isActive = 1
            ) 
            SELECT category_ID, FullLocation FROM RCTE WHERE Lvl = 2 ORDER BY category_displayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> GetProductsByTagOrTypeAsync(int prdType, int tagId, long fid, long catId, long webshopId)
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        string query = "";
        SqlCommand cmd = conn.CreateCommand();
        cmd.Parameters.AddWithValue("@wid", webshopId);
        cmd.Parameters.AddWithValue("@fid", fid);
        cmd.Parameters.AddWithValue("@catid", catId);

        if (prdType > 0 && tagId > 0)
        {
            cmd.Parameters.AddWithValue("@prdtype", prdType);
            cmd.Parameters.AddWithValue("@prdtags", tagId);
            query = @"select p.product_id, p.product_name, p.product_image1
                      from tbl_products p 
                      inner join tbl_lnkStockPrdTag l on p.product_id = l.lnkStockPrdTag_prdID 
                      where l.lnkStockPrdTag_tagID = @prdtags 
                        and p.product_type = @prdtype 
                        and p.product_webstoreid = @wid 
                        and p.product_isdeleted = 0  
                        and p.product_id not in (
                            select ProductID from db_cakerstreet_business.dbo.tbl_lnkItem2tempfranchise 
                            where tempFranchise_Id = @fid and tempFranchise_CatId = @catid
                        )
                      order by p.product_name";
        }
        else if (prdType > 0 && tagId == 0)
        {
            cmd.Parameters.AddWithValue("@prdtype", prdType);
            query = @"select p.product_id, p.product_name, p.product_image1
                      from tbl_products p 
                      where p.product_type = @prdtype 
                        and p.product_webstoreid = @wid 
                        and p.product_isdeleted = 0  
                        and p.product_id not in (
                            select ProductID from db_cakerstreet_business.dbo.tbl_lnkItem2tempfranchise 
                            where tempFranchise_Id = @fid and tempFranchise_CatId = @catid
                        )
                      order by p.product_name";
        }
        else if (prdType == 0 && tagId > 0)
        {
            cmd.Parameters.AddWithValue("@prdtags", tagId);
            query = @"select p.product_id, p.product_name, p.product_image1
                      from tbl_products p 
                      inner join tbl_lnkStockPrdTag l on p.product_id = l.lnkStockPrdTag_prdID 
                      where l.lnkStockPrdTag_tagID = @prdtags 
                        and p.product_webstoreid = @wid 
                        and p.product_isdeleted = 0  
                        and p.product_id not in (
                            select ProductID from db_cakerstreet_business.dbo.tbl_lnkItem2tempfranchise 
                            where tempFranchise_Id = @fid and tempFranchise_CatId = @catid
                        )
                      order by p.product_name";
        }
        else
        {
            return list;
        }

        cmd.CommandText = query;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = false,
                ParentId = 0
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> GetServicesByCategoryDropdownSelectionAsync(int serviceCatId, long fid)
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            ;WITH RCTE AS (
                SELECT a.category_ID, a.category_Name, CAST(a.category_Name as varchar(2000)) as FullLocation, 
                       a.catgory_refCategoryID, 1 AS Lvl, a.category_displayOrder
                FROM tbl_category a 
                WHERE a.catgory_refCategoryID = 0 AND a.category_for = 3 AND a.category_isActive = 1

                UNION ALL

                SELECT rh.category_ID, rh.category_Name, CAST(rc.FullLocation + ' > ' + rh.category_Name as varchar(2000)) as FullLocation, 
                       rh.catgory_refCategoryID, Lvl+1 as Lvl, rh.category_displayOrder
                FROM tbl_category rh 
                INNER JOIN RCTE rc on rh.catgory_refCategoryID = rc.category_ID 
                WHERE rh.category_isActive = 1
            ) 
            SELECT category_ID, FullLocation INTO #t FROM RCTE WHERE Lvl = 2 ORDER BY category_displayOrder;

            SELECT s.Service_ID, s.Service_Name, s.Service_Image, s.Market_Price
            FROM tbl_Service s 
            LEFT OUTER JOIN #t t on s.Service_CatID = t.category_ID
            WHERE (@catid = 0 OR s.Service_CatID = @catid)  
              AND s.Is_Active = 1 
              AND s.service_isdeleted = 0  
              AND s.Service_ID not in (
                  SELECT Service_ID FROM db_cakerstreet_business.dbo.tbl_lnkItem2tempfranchise 
                  WHERE Service_ID <> 0 AND tempFranchise_Id = @frID
              )
            ORDER BY s.Display_Order;

            DROP TABLE #t;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catid", serviceCatId);
        cmd.Parameters.AddWithValue("@frID", fid);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = true,
                ParentId = 0
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> SearchProductListAsync(string keyword, int searchType, int isService, long webshopId)
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        string query = "";
        if (isService == 1)
        {
            query = "SELECT TOP 20 Service_ID, Service_Name, Service_Image FROM tbl_Service WHERE service_isdeleted = 0 AND Service_Name LIKE '%' + @keyword + '%'";
        }
        else
        {
            if (searchType == 1) // Search by Tag
            {
                query = @"
                    SELECT l.lnkStockPrdTag_prdID, t.StocktypeTag_title 
                    INTO #t 
                    FROM tbl_StockPrdTag t 
                    INNER JOIN tbl_lnkStockPrdTag l on l.lnkStockPrdTag_tagID = t.StocktypeTag_ID
                    WHERE t.StocktypeTag_title LIKE '%' + @keyword + '%';

                    SELECT DISTINCT TOP 20 product_id, product_name = product_name + ' (' + tmp.List_Output + ')', product_image1 
                    FROM tbl_products p 
                    INNER JOIN (
                        SELECT lnkStockPrdTag_prdID,
                               STUFF((SELECT distinct ', ' + CAST(StocktypeTag_title AS VARCHAR(100)) [text()]
                                      FROM #t 
                                      WHERE lnkStockPrdTag_prdID = t.lnkStockPrdTag_prdID
                                      FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'),1,2,' ') List_Output
                        FROM #t t
                        GROUP BY lnkStockPrdTag_prdID
                    ) as tmp on p.product_ID = tmp.lnkStockPrdTag_prdID
                    WHERE product_type in (2,4,5,7,8,9) 
                      AND product_webstoreid = @wid 
                      AND product_isdeleted = 0;
                    DROP TABLE #t;";
            }
            else // Search by Keyword
            {
                query = @"SELECT TOP 20 product_id, product_name, product_image1 
                          FROM tbl_products 
                          WHERE product_type in (2,4,5,7,8,9) 
                            AND product_webstoreid = @wid 
                            AND product_isdeleted = 0
                            AND product_name LIKE '%' + @keyword + '%'";
            }
        }

        await using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@keyword", keyword);
        cmd.Parameters.AddWithValue("@wid", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt64(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IsService = isService == 1,
                ParentId = 0,
                ProductImage = reader.IsDBNull(2) ? "" : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<List<FranchiseCategoryItem>> SearchTagListAsync(string keyword)
    {
        var list = new List<FranchiseCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT DISTINCT TOP 20 t.StocktypeTag_ID, t.StocktypeTag_title 
                    FROM tbl_StockPrdTag t 
                    WHERE t.StocktypeTag_title LIKE '%' + @keyword + '%' 
                      AND t.StocktypeTag_ID in (select lnkStockPrdTag_tagID from tbl_lnkStockPrdTag)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@keyword", keyword);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FranchiseCategoryItem
            {
                Id = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    // ─── Franchise Notes Operations ───────────────────────────────────────────────

    public async Task<ProductFranchiseLinkingDetail?> GetFranchiseNotesDetailsAsync(long id)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT p.product_name, p.product_image1, p.product_code, c.Title, 
                   prd_status = case when l.Ordered = 0 then '<font color=''red''>Pending</font>' when l.Ordered = 1 and l.Delivered = 0 then '<font color=''orange''>Under Delivery</font>' when l.Delivered = 1 then '<font color=''green''>Delivered</font>' else '' end,
                   l.ProductID, l.Service_ID, l.tempFranchise_CatId, l.tempFranchise_Id
            FROM tbl_lnkItem2tempfranchise l
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_products p on p.product_ID = l.ProductID
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_Service srv on l.Service_ID = srv.Service_ID
            INNER JOIN tbl_tempFranchiseCat c on l.tempFranchise_CatId = c.ID
            WHERE l.id = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            bool isSrv = reader.IsDBNull(5) ? false : reader.GetInt64(6) > 0;
            return new ProductFranchiseLinkingDetail
            {
                Id = id,
                IsService = isSrv,
                ProductName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                ProductImage = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductCode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                CategoryTitle = reader.IsDBNull(3) ? "" : reader.GetString(3),
                PrdStatus = reader.IsDBNull(4) ? "" : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task<List<Dictionary<string, object>>> GetFranchiseNotesListAsync(long id)
    {
        var list = new List<Dictionary<string, object>>();
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"SELECT tempFranchiseNotes_ID, tempFranchiseNotes_custID, tempFranchiseNotes_custname, 
                           tempFranchiseNotes_type, lnkItem2tempfranchise_ID, tempFranchiseNotes_Remarks, 
                           tempFranchiseNotes_modifiedOn 
                    FROM tbl_tempFranchiseNotes 
                    WHERE lnkItem2tempfranchise_ID = @id 
                    ORDER BY tempFranchiseNotes_modifiedOn ASC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>
            {
                ["tempFranchiseNotes_ID"] = reader.GetInt64(0),
                ["tempFranchiseNotes_custID"] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ["tempFranchiseNotes_custname"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ["tempFranchiseNotes_type"] = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ["lnkItem2tempfranchise_ID"] = reader.GetInt64(4),
                ["tempFranchiseNotes_Remarks"] = reader.IsDBNull(5) ? "" : reader.GetString(5),
                ["tempFranchiseNotes_modifiedOn"] = reader.GetDateTime(6)
            };
            list.Add(dict);
        }
        return list;
    }

    public async Task<long> AddFranchiseNoteAsync(long crfId, int custId, string custName, int type, string remarks)
    {
        await using var conn = new SqlConnection(_businessConnection);
        await conn.OpenAsync();

        var sql = @"INSERT INTO tbl_tempFranchiseNotes 
                    (tempFranchiseNotes_custID, tempFranchiseNotes_custname, tempFranchiseNotes_type, lnkItem2tempfranchise_ID, tempFranchiseNotes_Remarks, tempFranchiseNotes_modifiedOn)
                    VALUES 
                    (@custId, @custName, @type, @crfId, @remarks, GETDATE());
                    SELECT SCOPE_IDENTITY();";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@custId", custId);
        cmd.Parameters.AddWithValue("@custName", custName ?? "");
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@crfId", crfId);
        cmd.Parameters.AddWithValue("@remarks", remarks ?? "");

        var res = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(res);
    }
}
