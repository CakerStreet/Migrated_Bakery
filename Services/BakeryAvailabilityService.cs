using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class AvailabilitySpecialDayItem
{
    public long SpecialDayId { get; set; }
    public long WebstoreId { get; set; }
    public DateTime Date { get; set; }
    public bool IsClosed { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public bool IsBusy { get; set; }
}

public class BakeryAvailabilityService
{
    private readonly string _connectionString;

    public BakeryAvailabilityService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<List<AvailabilitySpecialDayItem>> GetBusyDaysAsync(long webshopId, int month, int year)
    {
        var list = new List<AvailabilitySpecialDayItem>();
        var sql = @"SELECT webstoreSpecialDay_ID, webstoreSpecialDay_webstoreid, webstoreSpecialDay_Date, 
                           webstoreSpecialDay_isclosed, webstoreSpecialDay_from, webstoreSpecialDay_to, 
                           webstoreSpecialDay_isbusy 
                    FROM tbl_webstoreSpecialDays 
                    WHERE webstoreSpecialDay_webstoreid = @wid AND webstoreSpecialDay_isbusy = 1 
                      AND MONTH(webstoreSpecialDay_Date) = @month AND YEAR(webstoreSpecialDay_Date) = @year";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webshopId);
        cmd.Parameters.AddWithValue("@month", month);
        cmd.Parameters.AddWithValue("@year", year);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AvailabilitySpecialDayItem
            {
                SpecialDayId = Convert.ToInt64(reader["webstoreSpecialDay_ID"]),
                WebstoreId = Convert.ToInt64(reader["webstoreSpecialDay_webstoreid"]),
                Date = Convert.ToDateTime(reader["webstoreSpecialDay_Date"]),
                IsClosed = Convert.ToBoolean(reader["webstoreSpecialDay_isclosed"]),
                From = Convert.ToInt32(reader["webstoreSpecialDay_from"]),
                To = Convert.ToInt32(reader["webstoreSpecialDay_to"]),
                IsBusy = Convert.ToBoolean(reader["webstoreSpecialDay_isbusy"])
            });
        }
        return list;
    }

    public async Task<bool> ToggleBusyDateAsync(long webshopId, DateTime date)
    {
        // Strip time part of date
        var dtDate = date.Date;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if date is currently busy
        var checkSql = @"SELECT COUNT(1) FROM tbl_webstoreSpecialDays 
                         WHERE webstoreSpecialDay_webstoreid = @wid AND webstoreSpecialDay_Date = @date AND webstoreSpecialDay_isbusy = 1";
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@wid", webshopId);
        checkCmd.Parameters.AddWithValue("@date", dtDate);
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

        if (exists)
        {
            // Delete busy day
            var delSql = @"DELETE FROM tbl_webstoreSpecialDays 
                           WHERE webstoreSpecialDay_webstoreid = @wid AND webstoreSpecialDay_Date = @date AND webstoreSpecialDay_isbusy = 1";
            await using var delCmd = new SqlCommand(delSql, conn);
            delCmd.Parameters.AddWithValue("@wid", webshopId);
            delCmd.Parameters.AddWithValue("@date", dtDate);
            await delCmd.ExecuteNonQueryAsync();
            return false; // Returns new state: not busy
        }
        else
        {
            // Add busy day
            var insSql = @"INSERT INTO tbl_webstoreSpecialDays 
                           (webstoreSpecialDay_webstoreid, webstoreSpecialDay_Date, webstoreSpecialDay_from, 
                            webstoreSpecialDay_to, webstoreSpecialDay_isbusy, webstoreSpecialDay_isclosed, 
                            webstoreSpecialDay_modifiedOn) 
                           VALUES 
                           (@wid, @date, 0, 0, 1, 1, GETDATE())";
            await using var insCmd = new SqlCommand(insSql, conn);
            insCmd.Parameters.AddWithValue("@wid", webshopId);
            insCmd.Parameters.AddWithValue("@date", dtDate);
            await insCmd.ExecuteNonQueryAsync();
            return true; // Returns new state: busy
        }
    }
}
