using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class PersonalisedCakeService
{
    private readonly string _connectionString;

    public PersonalisedCakeService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<string> GetCustomPrdMsgTextAsync(long productId)
    {
        var sql = "SELECT customPrd_msgtext FROM tbl_customPrd WHERE customPrd_prdID = @pid";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value ? result.ToString() : "";
    }
}
