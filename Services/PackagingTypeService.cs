using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class PackagingTypeItem
{
    public int PackagingTypeId { get; set; }
    public string PackagingType { get; set; } = "";
    public int PrdCount { get; set; }
    public int PrdType { get; set; }
    public string ProductType { get; set; } = "";
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class PackagingTypeService
{
    private readonly string _connectionString;

    public PackagingTypeService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<List<PackagingTypeItem>> GetPackagingTypesAsync(int prdType, string search)
    {
        var list = new List<PackagingTypeItem>();
        var sql = @";SELECT PackagingTypeID, PackagingType, DisplayOrder, IsActive, PrdCount = ISNULL(PrdCount, 0),
            PrdType, ProductType = CASE WHEN PrdType = 4 THEN 'Cake Toppers' WHEN PrdType = 5 THEN 'Cutters / Moulds' WHEN PrdType = 7 THEN 'Packaging' WHEN PrdType = 8 THEN 'Supplies' 
            WHEN PrdType = 9 THEN 'Appliances' WHEN PrdType = 10 THEN 'Shop Setup' END
            FROM tbl_PackagingType pkg
            LEFT OUTER JOIN 
            (
                SELECT l.lnkPrd2Packaging_PackagingTypeID, PrdCount = COUNT(l.lnkPrd2Packaging_PrdID) 
                FROM tbl_lnkPrd2Packaging l 
                INNER JOIN tbl_products p ON l.lnkPrd2Packaging_PrdID = p.product_ID
                GROUP BY l.lnkPrd2Packaging_PackagingTypeID
            ) as lnk ON pkg.PackagingTypeID = lnk.lnkPrd2Packaging_PackagingTypeID
            WHERE 1 = 1 AND (@typeid = 0 OR PrdType = @typeid) AND (@search = '' OR PackagingType LIKE @search + '%')
            ORDER BY DisplayOrder";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@typeid", prdType);
        cmd.Parameters.AddWithValue("@search", search ?? "");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PackagingTypeItem
            {
                PackagingTypeId = Convert.ToInt32(reader["PackagingTypeID"]),
                PackagingType = Convert.ToString(reader["PackagingType"]) ?? "",
                PrdCount = Convert.ToInt32(reader["PrdCount"]),
                PrdType = Convert.ToInt32(reader["PrdType"]),
                ProductType = Convert.ToString(reader["ProductType"]) ?? "",
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                DisplayOrder = Convert.ToInt32(reader["DisplayOrder"])
            });
        }

        return list;
    }

    public async Task<PackagingTypeItem?> GetByIdAsync(int id)
    {
        var sql = @"SELECT PackagingTypeID, PackagingType, PrdType, DisplayOrder, IsActive 
                    FROM tbl_PackagingType 
                    WHERE PackagingTypeID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PackagingTypeItem
            {
                PackagingTypeId = Convert.ToInt32(reader["PackagingTypeID"]),
                PackagingType = Convert.ToString(reader["PackagingType"]) ?? "",
                PrdType = Convert.ToInt32(reader["PrdType"]),
                DisplayOrder = Convert.ToInt32(reader["DisplayOrder"]),
                IsActive = Convert.ToBoolean(reader["IsActive"])
            };
        }
        return null;
    }

    public async Task<bool> CheckExistsAsync(int id, string typeName)
    {
        var sql = "SELECT COUNT(1) FROM tbl_PackagingType WHERE LOWER(PackagingType) = @name AND PackagingTypeID != @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", typeName.Trim().ToLower());
        cmd.Parameters.AddWithValue("@id", id);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<int> GetMaxDisplayOrderAsync()
    {
        var sql = "SELECT ISNULL(MAX(DisplayOrder), 0) FROM tbl_PackagingType";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task SaveAsync(int id, string typeName, int prdType)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        if (id > 0)
        {
            var sql = "UPDATE tbl_PackagingType SET PackagingType = @name, PrdType = @prdType WHERE PackagingTypeID = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", typeName.Trim());
            cmd.Parameters.AddWithValue("@prdType", prdType);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            int maxOrder = await GetMaxDisplayOrderAsync();
            var sql = "INSERT INTO tbl_PackagingType (PackagingType, PrdType, DisplayOrder, IsActive) VALUES (@name, @prdType, @order, 1)";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", typeName.Trim());
            cmd.Parameters.AddWithValue("@prdType", prdType);
            cmd.Parameters.AddWithValue("@order", maxOrder + 1);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task UpdateDisplayOrderAsync(int id, int displayOrder)
    {
        var sql = "UPDATE tbl_PackagingType SET DisplayOrder = @order WHERE PackagingTypeID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@order", displayOrder);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetActiveStatusAsync(List<int> ids, bool isActive)
    {
        if (ids == null || ids.Count == 0) return;

        var sql = $"UPDATE tbl_PackagingType SET IsActive = @isActive WHERE PackagingTypeID IN ({string.Join(",", ids)})";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@isActive", isActive);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteOnlyWithNoProductsAsync(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return;

        // Verify which IDs have no products assigned
        var validDeleteIds = new List<int>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var id in ids)
        {
            var checkSql = @"
                SELECT COUNT(1) 
                FROM tbl_lnkPrd2Packaging l 
                INNER JOIN tbl_products p ON l.lnkPrd2Packaging_PrdID = p.product_ID
                WHERE l.lnkPrd2Packaging_PackagingTypeID = @id";

            await using var cmdCheck = new SqlCommand(checkSql, conn);
            cmdCheck.Parameters.AddWithValue("@id", id);
            var count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
            if (count == 0)
            {
                validDeleteIds.Add(id);
            }
        }

        if (validDeleteIds.Count > 0)
        {
            var sql = $"DELETE FROM tbl_PackagingType WHERE PackagingTypeID IN ({string.Join(",", validDeleteIds)})";
            await using var cmdDel = new SqlCommand(sql, conn);
            await cmdDel.ExecuteNonQueryAsync();
        }
    }
}
