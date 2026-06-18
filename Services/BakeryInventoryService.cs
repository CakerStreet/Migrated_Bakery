using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class InventoryFilterParams
{
    public long WebstoreId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public int ProductType { get; set; } = 0; // 0=All
    public int StatusFilter { get; set; } = -1; // -1=All, 0=Inactive, 1=Active
    public long CategoryId { get; set; }
    public long TemplateId { get; set; }
    public string? Sort { get; set; }
    public bool HasCutters { get; set; }
    public bool IsCsBakery { get; set; }
    public int TypeId { get; set; } // for accessories/packaging sub-type
}

public class InventoryListResult
{
    public List<InventoryProductItem> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class InventoryProductItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public string? ProductCode { get; set; }
    public decimal ProductPrice { get; set; }
    public int SortOrder { get; set; }
    public int SoldCount { get; set; }
    public int ProductType { get; set; }
    public bool IsActive { get; set; }
    public bool IsFranchise { get; set; }
    public int TotalStockQty { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string? SeoUrl { get; set; }
}

public class InventoryStockLocationItem
{
    public long LocationId { get; set; }
    public string FullLocation { get; set; } = "";
    public int Qty { get; set; }
    public long ProductId { get; set; }
}

public class QtyLogEntry
{
    public long LocationId { get; set; }
    public string FullLocation { get; set; } = "";
    public int Qty { get; set; }
    public long ProductId { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string StaffName { get; set; } = "";
}

public class CategoryItem
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public long ParentId { get; set; }
}

public class TemplateItem
{
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Bakery Inventory management.
/// Migrated from manageinventory.aspx.
/// Uses DefaultConnection with tbl_products, tbl_location, tbl_StockLocation, tbl_QtyLocationlog.
/// Module 4 (bakery types) / Module 5 (stock types) permission.
/// </summary>
public class BakeryInventoryService
{
    private readonly string _defaultConnection;

    public BakeryInventoryService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets paginated products with dynamic filtering and sorting.
    /// </summary>
    public async Task<InventoryListResult> GetProductsAsync(InventoryFilterParams filters)
    {
        var result = new InventoryListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Build dynamic WHERE clause
        var whereClauses = new List<string>();
        whereClauses.Add("p.product_isdeleted = 0");
        whereClauses.Add("p.product_WebstoreID = @wid");

        if (filters.ProductType > 0)
        {
            whereClauses.Add("p.product_type = @prdtype");
        }

        if (filters.StatusFilter == 1)
        {
            whereClauses.Add("p.product_isActive = 1");
        }
        else if (filters.StatusFilter == 0)
        {
            whereClauses.Add("p.product_isActive = 0");
        }

        if (filters.CategoryId > 0)
        {
            // tbl_productcategory may not exist — skip filter if table missing (category dropdown will be empty)
            whereClauses.Add("EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tbl_productcategory') AND p.product_ID IN (SELECT pc.productcategory_prdID FROM tbl_productcategory pc WHERE pc.productcategory_catID = @catid)");
        }
        else if (filters.CategoryId == -1)
        {
            // Uncategorized — skip if table doesn't exist
            whereClauses.Add("EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tbl_productcategory') AND p.product_ID NOT IN (SELECT pc.productcategory_prdID FROM tbl_productcategory pc)");
        }

        if (filters.TemplateId > 0)
        {
            whereClauses.Add("p.specificationTemplate_ID = @templateid");
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            whereClauses.Add("(p.product_Name LIKE @search OR p.product_code LIKE @search)");
        }

        if (filters.HasCutters)
        {
            whereClauses.Add("p.product_ID IN (SELECT prdcutter_prdID FROM tbl_prdcutter)");
        }

        if (filters.TypeId > 0)
        {
            whereClauses.Add("p.product_PackagingTypeID = @typeid");
        }

        string whereClause = string.Join(" AND ", whereClauses);

        // Build ORDER BY clause
        string orderBy = filters.Sort switch
        {
            "1" => "p.product_marketPrice ASC",       // price low to high
            "2" => "p.product_marketPrice DESC",      // price high to low
            "3" => "p.product_modifiedOn DESC",       // newest
            "4" => "p.product_Name ASC",              // sold low (table missing, fallback to name)
            "5" => "p.product_modifiedOn DESC",       // sold high (table missing, fallback to newest)
            _ => "p.product_displayOrder ASC, p.product_Name ASC" // default
        };

        int offset = (filters.Page - 1) * filters.PageSize;

        // Count query
        var countSql = $@"SELECT COUNT(1) 
            FROM tbl_products p
            WHERE {whereClause}";

        // Data query (tbl_productStats does not exist in this environment — SoldCount defaults to 0)
        var dataSql = $@"SELECT p.product_ID, p.product_Name, p.product_image1, p.product_code,
                p.product_marketPrice, p.product_displayOrder, 0 AS SoldCount,
                p.product_type, p.product_isActive, CAST(0 AS BIT) AS IsFranchise,
                ISNULL(sl.TotalQty, 0) AS TotalStockQty, p.product_modifiedOn, p.product_seourl
            FROM tbl_products p
            LEFT JOIN (SELECT Product_Id, SUM(Qty) AS TotalQty FROM tbl_StockLocation GROUP BY Product_Id) sl ON sl.Product_Id = p.product_ID
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        // Execute count
        await using var countCmd = new SqlCommand(countSql, conn);
        countCmd.CommandTimeout = 120;
        AddFilterParams(countCmd, filters, offset);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        result.TotalCount = totalCount;
        result.TotalPages = (int)Math.Ceiling((double)totalCount / filters.PageSize);

        // Execute data
        await using var dataCmd = new SqlCommand(dataSql, conn);
        dataCmd.CommandTimeout = 120;
        AddFilterParams(dataCmd, filters, offset);
        dataCmd.Parameters.AddWithValue("@offset", offset);
        dataCmd.Parameters.AddWithValue("@pageSize", filters.PageSize);

        await using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Items.Add(new InventoryProductItem
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ProductImage = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProductCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                ProductPrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                SortOrder = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                SoldCount = Convert.ToInt32(reader.GetValue(6)),
                ProductType = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                IsActive = reader.IsDBNull(8) ? false : Convert.ToBoolean(reader.GetValue(8)),
                IsFranchise = Convert.ToBoolean(reader.GetValue(9)),
                TotalStockQty = Convert.ToInt32(reader.GetValue(10)),
                ModifiedOn = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11),
                SeoUrl = reader.IsDBNull(12) ? null : reader.GetString(12)
            });
        }

        return result;
    }

    private void AddFilterParams(SqlCommand cmd, InventoryFilterParams filters, int offset)
    {
        cmd.Parameters.AddWithValue("@wid", filters.WebstoreId);
        if (filters.ProductType > 0)
            cmd.Parameters.AddWithValue("@prdtype", filters.ProductType);
        if (filters.CategoryId != 0)
            cmd.Parameters.AddWithValue("@catid", filters.CategoryId);
        if (filters.TemplateId > 0)
            cmd.Parameters.AddWithValue("@templateid", filters.TemplateId);
        if (!string.IsNullOrWhiteSpace(filters.Search))
            cmd.Parameters.AddWithValue("@search", "%" + filters.Search + "%");
        if (filters.TypeId > 0)
            cmd.Parameters.AddWithValue("@typeid", filters.TypeId);
    }

    /// <summary>
    /// Gets stock locations for a product using recursive CTE (3 levels).
    /// </summary>
    public async Task<List<InventoryStockLocationItem>> GetStockLocationsAsync(long productId, long webstoreId)
    {
        var items = new List<InventoryStockLocationItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

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

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new InventoryStockLocationItem
            {
                LocationId = reader.GetInt64(0),
                FullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Qty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                ProductId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets the last 5 qty log entries for a product.
    /// </summary>
    public async Task<List<QtyLogEntry>> GetQtyLogAsync(long productId, long webstoreId)
    {
        var items = new List<QtyLogEntry>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

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
        SELECT TOP 5 RCTE.LocationID, FullLocation, Qty, Product_Id, ModifiedOn, 
               customer_Name AS Staffname 
        FROM RCTE 
        INNER JOIN tbl_QtyLocationlog ON tbl_QtyLocationlog.LocationID = RCTE.LocationID 
            AND Product_Id = @pid 
        INNER JOIN tbl_bakeryuser ON customer_ID = ModifiedBy 
        WHERE Lvl = 3 
        ORDER BY ModifiedOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new QtyLogEntry
            {
                LocationId = reader.GetInt64(0),
                FullLocation = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Qty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                ProductId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                ModifiedOn = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                StaffName = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return items;
    }

    /// <summary>
    /// Adds stock quantity to a location. INSERT if not exists, UPDATE (Qty = Qty + @qty) if exists.
    /// Also inserts a qty log entry.
    /// </summary>
    public async Task<bool> AddStockQtyAsync(long productId, long locationId, int qty, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Check if record exists
            var checkSql = "SELECT COUNT(1) FROM tbl_StockLocation WHERE Product_Id = @pid AND LocationID = @locId";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@pid", productId);
            checkCmd.Parameters.AddWithValue("@locId", locationId);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                var updateSql = "UPDATE tbl_StockLocation SET Qty = Qty + @qty WHERE Product_Id = @pid AND LocationID = @locId";
                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@qty", qty);
                updateCmd.Parameters.AddWithValue("@pid", productId);
                updateCmd.Parameters.AddWithValue("@locId", locationId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertSql = "INSERT INTO tbl_StockLocation (Product_Id, LocationID, Qty) VALUES (@pid, @locId, @qty)";
                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@pid", productId);
                insertCmd.Parameters.AddWithValue("@locId", locationId);
                insertCmd.Parameters.AddWithValue("@qty", qty);
                await insertCmd.ExecuteNonQueryAsync();
            }

            // Insert qty log
            var logSql = @"INSERT INTO tbl_QtyLocationlog (LocationID, Product_Id, Qty, ModifiedOn, ModifiedBy) 
                           VALUES (@locId, @pid, @qty, GETDATE(), @userId)";
            await using var logCmd = new SqlCommand(logSql, conn);
            logCmd.Parameters.AddWithValue("@locId", locationId);
            logCmd.Parameters.AddWithValue("@pid", productId);
            logCmd.Parameters.AddWithValue("@qty", qty);
            logCmd.Parameters.AddWithValue("@userId", userId);
            await logCmd.ExecuteNonQueryAsync();

            // Update total product quantity
            var totalSql = @"UPDATE tbl_products SET product_modifiedOn = GETDATE(), 
                             product_quantity = product_quantity + @qty WHERE product_ID = @pid";
            await using var totalCmd = new SqlCommand(totalSql, conn);
            totalCmd.Parameters.AddWithValue("@qty", qty);
            totalCmd.Parameters.AddWithValue("@pid", productId);
            await totalCmd.ExecuteNonQueryAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Edits (sets) stock quantity at a location. UPDATE tbl_StockLocation + INSERT log.
    /// </summary>
    public async Task<bool> EditStockQtyAsync(long productId, long locationId, int newQty, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var updateSql = "UPDATE tbl_StockLocation SET Qty = @qty WHERE Product_Id = @pid AND LocationID = @locId";
            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@qty", newQty);
            updateCmd.Parameters.AddWithValue("@pid", productId);
            updateCmd.Parameters.AddWithValue("@locId", locationId);
            await updateCmd.ExecuteNonQueryAsync();

            // Insert qty log
            var logSql = @"INSERT INTO tbl_QtyLocationlog (LocationID, Product_Id, Qty, ModifiedOn, ModifiedBy) 
                           VALUES (@locId, @pid, @qty, GETDATE(), @userId)";
            await using var logCmd = new SqlCommand(logSql, conn);
            logCmd.Parameters.AddWithValue("@locId", locationId);
            logCmd.Parameters.AddWithValue("@pid", productId);
            logCmd.Parameters.AddWithValue("@qty", newQty);
            logCmd.Parameters.AddWithValue("@userId", userId);
            await logCmd.ExecuteNonQueryAsync();

            // Recalculate total product quantity from all locations
            var totalSql = @"UPDATE tbl_products SET product_modifiedOn = GETDATE(), 
                             product_quantity = (SELECT ISNULL(SUM(Qty), 0) FROM tbl_StockLocation WHERE Product_Id = @pid) 
                             WHERE product_ID = @pid";
            await using var totalCmd = new SqlCommand(totalSql, conn);
            totalCmd.Parameters.AddWithValue("@pid", productId);
            await totalCmd.ExecuteNonQueryAsync();

            // Insert product log
            var plogSql = @"INSERT INTO tbl_productLog (productLog_prdID, productLog_typeID, productLog_Remarks, productLog_modifiedOn, productLog_modifiedby) 
                            VALUES (@pid, 3, @remarks, GETDATE(), @userId)";
            await using var plogCmd = new SqlCommand(plogSql, conn);
            plogCmd.Parameters.AddWithValue("@pid", productId);
            plogCmd.Parameters.AddWithValue("@remarks", $"Location:{locationId}; Qty:{newQty}");
            plogCmd.Parameters.AddWithValue("@userId", userId);
            await plogCmd.ExecuteNonQueryAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk set active/inactive for products.
    /// </summary>
    public async Task<bool> BulkSetActiveAsync(List<long> ids, bool isActive)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Build parameterized IN clause
            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                paramNames.Add($"@id{i}");

            var sql = $"UPDATE tbl_products SET product_isActive = @isActive WHERE product_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@isActive", isActive);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk soft-delete products: set product_isdeleted=1, insert into tbl_DeletedProducts for Google feed, insert product log.
    /// </summary>
    public async Task<bool> BulkDeleteAsync(List<long> ids, long webstoreId, int userId)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            foreach (var id in ids)
            {
                // Soft delete
                var deleteSql = "UPDATE tbl_products SET product_isdeleted = 1 WHERE product_WebstoreID = @wid AND product_ID = @id";
                await using var deleteCmd = new SqlCommand(deleteSql, conn);
                deleteCmd.Parameters.AddWithValue("@wid", webstoreId);
                deleteCmd.Parameters.AddWithValue("@id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                // Insert into deleted products for Google feed
                var feedSql = @"INSERT INTO tbl_DeletedProducts (Product_Code, Is_Updated, CreatedOn)
                    SELECT p.product_code, 0, GETDATE() 
                    FROM tbl_googlefeedprd AS g 
                    INNER JOIN tbl_products AS p ON g.google_prdID = p.product_ID 
                    WHERE p.product_ID = @id";
                await using var feedCmd = new SqlCommand(feedSql, conn);
                feedCmd.Parameters.AddWithValue("@id", id);
                await feedCmd.ExecuteNonQueryAsync();

                // Product log
                var logSql = @"INSERT INTO tbl_productLog (productLog_prdID, productLog_typeID, productLog_Remarks, productLog_modifiedOn, productLog_modifiedby) 
                    VALUES (@id, 5, 'Product deleted', GETDATE(), @userId)";
                await using var logCmd = new SqlCommand(logSql, conn);
                logCmd.Parameters.AddWithValue("@id", id);
                logCmd.Parameters.AddWithValue("@userId", userId);
                await logCmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bulk set/remove franchise flag for products.
    /// </summary>
    public async Task<bool> BulkSetFranchiseAsync(List<long> ids, bool isFranchise)
    {
        if (ids == null || ids.Count == 0) return false;
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                paramNames.Add($"@id{i}");

            var sql = $"UPDATE tbl_products SET product_isFranchise = @isFranchise WHERE product_ID IN ({string.Join(",", paramNames)})";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@isFranchise", isFranchise);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets categories for filter dropdown. For bakery types (1,3), gets from tbl_productcategory hierarchy.
    /// Returns empty list if tbl_productcategory does not exist.
    /// </summary>
    public async Task<List<CategoryItem>> GetCategoriesAsync(long webstoreId, int productType)
    {
        var items = new List<CategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // Check if tbl_productcategory exists
        var checkSql = "SELECT OBJECT_ID('tbl_productcategory', 'U')";
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            var result = await checkCmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return items; // Table doesn't exist — return empty
        }

        // Get categories linked to this webstore's products
        var sql = @"SELECT DISTINCT c.category_ID, c.category_Name, ISNULL(c.catgory_refCategoryID, 0) AS ParentId
            FROM tbl_category c
            INNER JOIN tbl_productcategory pc ON pc.productcategory_catID = c.category_ID
            INNER JOIN tbl_products p ON p.product_ID = pc.productcategory_prdID
            WHERE p.product_WebstoreID = @wid AND p.product_isdeleted = 0
              AND c.category_isActive = 1 AND c.category_isDeleted = 0
            ORDER BY c.category_Name";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new CategoryItem
            {
                CategoryId = reader.GetInt64(0),
                CategoryName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ParentId = reader.GetInt64(2)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets specification templates for filter dropdown.
    /// </summary>
    public async Task<List<TemplateItem>> GetTemplatesAsync(long webstoreId, int prdType = 0)
    {
        var items = new List<TemplateItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // prdType: 0 = cakes, 1 = cupcakes
        int templatePrdType = prdType == 6 ? 1 : 0;

        var sql = @"SELECT specificationTemplate_ID, specificationTemplate_Name 
            FROM tbl_specificationTemplate 
            WHERE specificationTemplate_uid = @wid AND specificationTemplate_prdtype = @prdtype
            ORDER BY specificationTemplate_displayOrder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);
        cmd.Parameters.AddWithValue("@prdtype", templatePrdType);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TemplateItem
            {
                TemplateId = reader.GetInt64(0),
                TemplateName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }
}
