using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class PaymentSettingsModel
{
    public int Id { get; set; }
    public string BankName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNo { get; set; } = "";        // FULL value from DB (never exposed in UI)
    public string AccountNoMasked { get; set; } = "";  // ****1234 for display
    public string IFSCCode { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string SortCode { get; set; } = "";
    public string SwiftCode { get; set; } = "";
    public string RoutingNo { get; set; } = "";
}

public class PaymentSettingsSaveModel
{
    public string BankName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string IFSCCode { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string SortCode { get; set; } = "";
    public string SwiftCode { get; set; } = "";
    public string RoutingNo { get; set; } = "";
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Payment Settings module.
/// Migrated from sellerPaymentSettings.aspx.
/// Uses DefaultConnection with tbl_webstorebank table.
/// Module 2 permission check.
/// </summary>
public class PaymentSettingsService
{
    private readonly string _defaultConnection;

    public PaymentSettingsService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets payment settings for the given webstore.
    /// </summary>
    public async Task<PaymentSettingsModel?> GetPaymentSettingsAsync(long webstoreId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT webstorebank_ID, webstorebank_BankName, webstorebank_AccountName,
                           webstorebank_AccountNo, webstorebank_IFSCCode, webstorebank_Accountype,
                           webstorebank_sortCode, webstorebank_swiftCode, webstorebank_RoutingNo
                    FROM tbl_webstorebank
                    WHERE webstorebank_webstoreID = @webstoreId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var accountNo = reader.IsDBNull(3) ? "" : reader.GetString(3);

            return new PaymentSettingsModel
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                BankName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                AccountName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                AccountNo = accountNo,
                AccountNoMasked = MaskAccountNumber(accountNo),
                IFSCCode = reader.IsDBNull(4) ? "" : reader.GetString(4),
                AccountType = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SortCode = reader.IsDBNull(6) ? "" : reader.GetString(6),
                SwiftCode = reader.IsDBNull(7) ? "" : reader.GetString(7),
                RoutingNo = reader.IsDBNull(8) ? "" : reader.GetString(8)
            };
        }

        return null;
    }

    /// <summary>
    /// Saves payment settings (upsert: update if exists, insert if not).
    /// </summary>
    public async Task<bool> SavePaymentSettingsAsync(PaymentSettingsSaveModel model, long webstoreId, int userId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Check if record exists
            var checkSql = "SELECT COUNT(1) FROM tbl_webstorebank WHERE webstorebank_webstoreID = @webstoreId";
            await using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                var updateSql = @"UPDATE tbl_webstorebank SET
                                    webstorebank_BankName = @bankName,
                                    webstorebank_AccountName = @accountName,
                                    webstorebank_AccountNo = @accountNo,
                                    webstorebank_IFSCCode = @ifscCode,
                                    webstorebank_Accountype = @accountType,
                                    webstorebank_sortCode = @sortCode,
                                    webstorebank_swiftCode = @swiftCode,
                                    webstorebank_RoutingNo = @routingNo,
                                    webstorebank_ModifiedOn = @modifiedOn
                                  WHERE webstorebank_webstoreID = @webstoreId";

                await using var updateCmd = new SqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("@bankName", model.BankName ?? "");
                updateCmd.Parameters.AddWithValue("@accountName", model.AccountName ?? "");
                updateCmd.Parameters.AddWithValue("@accountNo", model.AccountNo ?? "");
                updateCmd.Parameters.AddWithValue("@ifscCode", model.IFSCCode ?? "");
                updateCmd.Parameters.AddWithValue("@accountType", model.AccountType ?? "");
                updateCmd.Parameters.AddWithValue("@sortCode", model.SortCode ?? "");
                updateCmd.Parameters.AddWithValue("@swiftCode", model.SwiftCode ?? "");
                updateCmd.Parameters.AddWithValue("@routingNo", model.RoutingNo ?? "");
                updateCmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@webstoreId", webstoreId);

                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertSql = @"INSERT INTO tbl_webstorebank
                                    (webstorebank_webstoreID, webstorebank_custID, webstorebank_BankName,
                                     webstorebank_AccountName, webstorebank_AccountNo, webstorebank_IFSCCode,
                                     webstorebank_Accountype, webstorebank_sortCode, webstorebank_swiftCode,
                                     webstorebank_RoutingNo, webstorebank_ModifiedOn)
                                  VALUES
                                    (@webstoreId, @custId, @bankName,
                                     @accountName, @accountNo, @ifscCode,
                                     @accountType, @sortCode, @swiftCode,
                                     @routingNo, @modifiedOn)";

                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                insertCmd.Parameters.AddWithValue("@custId", userId);
                insertCmd.Parameters.AddWithValue("@bankName", model.BankName ?? "");
                insertCmd.Parameters.AddWithValue("@accountName", model.AccountName ?? "");
                insertCmd.Parameters.AddWithValue("@accountNo", model.AccountNo ?? "");
                insertCmd.Parameters.AddWithValue("@ifscCode", model.IFSCCode ?? "");
                insertCmd.Parameters.AddWithValue("@accountType", model.AccountType ?? "");
                insertCmd.Parameters.AddWithValue("@sortCode", model.SortCode ?? "");
                insertCmd.Parameters.AddWithValue("@swiftCode", model.SwiftCode ?? "");
                insertCmd.Parameters.AddWithValue("@routingNo", model.RoutingNo ?? "");
                insertCmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);

                await insertCmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Masks account number for display: shows only last 4 digits.
    /// If length > 4: "****" + last 4 chars. If <= 4: "****".
    /// </summary>
    public static string MaskAccountNumber(string accountNo)
    {
        if (string.IsNullOrEmpty(accountNo))
            return "";

        if (accountNo.Length > 4)
            return "****" + accountNo[^4..];

        return "****";
    }
}
