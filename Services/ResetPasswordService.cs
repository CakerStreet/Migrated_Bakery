using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class ResetPasswordInfo
{
    public long CustomerID { get; set; }
    public int CustomerType { get; set; } // 1 = customer, 2 = bakery user
    public DateTime ExpireDate { get; set; }
    public string PasswordCode { get; set; } = "";
}

public class ResetPasswordService
{
    private readonly string _connectionString;

    public ResetPasswordService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<ResetPasswordInfo?> GetResetPasswordInfoAsync(string code)
    {
        var sql = @"
            SELECT CustomerID, CustomerType, ExpireDate, PasswordCode 
            FROM tbl_ResetPassword 
            WHERE PasswordCode = @code";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@code", code);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ResetPasswordInfo
            {
                CustomerID = Convert.ToInt64(reader["CustomerID"]),
                CustomerType = Convert.ToInt32(reader["CustomerType"]),
                ExpireDate = Convert.ToDateTime(reader["ExpireDate"]),
                PasswordCode = Convert.ToString(reader["PasswordCode"]) ?? ""
            };
        }
        return null;
    }

    public async Task ExpireResetPasswordCodeAsync(string code)
    {
        var sql = "UPDATE tbl_ResetPassword SET PasswordCode = 'expired' WHERE PasswordCode = @code";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@code", code);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateCustomerPasswordAsync(long customerId, string password)
    {
        var sql = "UPDATE tbl_customers SET customer_password = @password WHERE customer_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@password", password);
        cmd.Parameters.AddWithValue("@id", customerId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateBakeryUserPasswordAsync(long customerId, string password)
    {
        var sql = "UPDATE tbl_BakeryUser SET customer_password = @password WHERE customer_ID = @id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@password", password);
        cmd.Parameters.AddWithValue("@id", customerId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<BakeryLoginResult?> GetBakeryLoginDetailsAsync(long customerId)
    {
        var sql = @"
            SELECT u.customer_ID, u.customer_type, u.customer_Name, u.customer_webshopID, u.customer_EmailID,
                   w.webstore_businessName
            FROM tbl_BakeryUser u
            LEFT OUTER JOIN tbl_webstore w ON u.customer_webshopID = w.webstore_ID
            WHERE u.customer_ID = @id AND u.customer_isActive = 1";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", customerId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new BakeryLoginResult
            {
                CustomerId = Convert.ToInt64(reader["customer_ID"]),
                UserType = Convert.ToString(reader["customer_type"]) ?? "",
                UserName = Convert.ToString(reader["customer_name"]) ?? "",
                Email = Convert.ToString(reader["customer_EmailID"]) ?? "",
                WebshopId = Convert.ToString(reader["customer_webshopID"]) ?? "",
                BusinessName = Convert.ToString(reader["webstore_businessName"]) ?? "",
                ReturnCode = 1
            };
        }
        return null;
    }
}
