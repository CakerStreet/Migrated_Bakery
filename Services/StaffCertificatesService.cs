using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class StaffCertificateItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Filename { get; set; } = "";
}

public class StaffCertificatesGroup
{
    public long StaffId { get; set; }
    public string StaffName { get; set; } = "";
    public List<StaffCertificateModel> Certificates { get; set; } = new();
}

public class StaffCertificateModel
{
    public long CertificateId { get; set; }
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
}

public class StaffCertificatesService
{
    private readonly string _defaultConnection;
    private readonly string _staffAssessmentConnection;

    public StaffCertificatesService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
        _staffAssessmentConnection = config.GetConnectionString("StaffAssessmentConnection") ?? "";
    }

    public async Task<List<StaffCertificateItem>> GetStaffCertificatesByStaffIdAsync(long staffId)
    {
        var list = new List<StaffCertificateItem>();
        var sql = @"
            SELECT staffCertificate_ID, staffCertificate_title, staffCertificate_file 
            FROM tbl_staffCertificate 
            WHERE staffCertificate_staffID = @staffId
            ORDER BY staffCertificate_createdOn DESC";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staffId", staffId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffCertificateItem
            {
                Id = Convert.ToInt64(reader["staffCertificate_ID"]),
                Name = Convert.ToString(reader["staffCertificate_title"]) ?? "",
                Filename = Convert.ToString(reader["staffCertificate_file"]) ?? ""
            });
        }
        return list;
    }

    public async Task<List<StaffCertificatesGroup>> GetStaffCertificatesGroupedAsync()
    {
        var list = new List<StaffCertificatesGroup>();
        var sql = @"
            SELECT bu.customer_ID AS StaffID, bu.customer_Name AS StaffName
            FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu
            WHERE bu.customer_ID IN (SELECT DISTINCT staffCertificate_staffID FROM tbl_staffCertificate)
            ORDER BY bu.customer_Name";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffCertificatesGroup
            {
                StaffId = Convert.ToInt64(reader["StaffID"]),
                StaffName = Convert.ToString(reader["StaffName"]) ?? ""
            });
        }

        // Fetch certificates for each staff member
        foreach (var group in list)
        {
            var certSql = @"
                SELECT staffCertificate_ID, staffCertificate_title, staffCertificate_file
                FROM tbl_staffCertificate
                WHERE staffCertificate_staffID = @staffId
                ORDER BY staffCertificate_createdOn DESC";

            await using var cmdCert = new SqlCommand(certSql, conn);
            cmdCert.Parameters.AddWithValue("@staffId", group.StaffId);

            await using var readerCert = await cmdCert.ExecuteReaderAsync();
            while (await readerCert.ReadAsync())
            {
                group.Certificates.Add(new StaffCertificateModel
                {
                    CertificateId = Convert.ToInt64(readerCert["staffCertificate_ID"]),
                    Title = Convert.ToString(readerCert["staffCertificate_title"]) ?? "",
                    File = Convert.ToString(readerCert["staffCertificate_file"]) ?? ""
                });
            }
        }

        return list;
    }

    public async Task SaveStaffCertificateAsync(long id, long staffId, string title, string file, string createdBy)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        if (id > 0)
        {
            var sql = @"
                UPDATE tbl_staffCertificate
                SET staffCertificate_title = @title,
                    staffCertificate_file = @file,
                    staffCertificate_createdBy = @createdBy,
                    staffCertificate_createdOn = GETDATE()
                WHERE staffCertificate_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@file", file);
            cmd.Parameters.AddWithValue("@createdBy", createdBy);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var sql = @"
                INSERT INTO tbl_staffCertificate (staffCertificate_staffID, staffCertificate_title, staffCertificate_file, staffCertificate_createdBy, staffCertificate_createdOn)
                VALUES (@staffId, @title, @file, @createdBy, GETDATE())";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@staffId", staffId);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@file", file);
            cmd.Parameters.AddWithValue("@createdBy", createdBy);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<StaffCertificateModel?> GetCertificateByIdAsync(long id)
    {
        var sql = "SELECT staffCertificate_ID, staffCertificate_title, staffCertificate_file FROM tbl_staffCertificate WHERE staffCertificate_ID = @id";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new StaffCertificateModel
            {
                CertificateId = Convert.ToInt64(reader["staffCertificate_ID"]),
                Title = Convert.ToString(reader["staffCertificate_title"]) ?? "",
                File = Convert.ToString(reader["staffCertificate_file"]) ?? ""
            };
        }
        return null;
    }

    public async Task DeleteCertificateAsync(long id)
    {
        var sql = "DELETE FROM tbl_staffCertificate WHERE staffCertificate_ID = @id";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
