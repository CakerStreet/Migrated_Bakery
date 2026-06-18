using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class BusinessInfoModel
{
    public long WebstoreId { get; set; }
    public string BusinessName { get; set; } = "";
    public string BusinessPhone { get; set; } = "";
    public string OrderEmail { get; set; } = "";
    public string QuoteSMSNo { get; set; } = "";
    public string BusinessDescription { get; set; } = "";
    public string County { get; set; } = "";
    public string City { get; set; } = "";
    public string StoreURL { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Address { get; set; } = "";
    public string Logo { get; set; } = "";
    public bool IsCollectable { get; set; }
    public bool IsDeliverable { get; set; }
    public decimal DeliveryMinOrder { get; set; }
    public decimal DeliveryMiles { get; set; }
}

public class BusinessInfoSaveModel
{
    public string BusinessName { get; set; } = "";
    public string BusinessPhone { get; set; } = "";
    public string OrderEmail { get; set; } = "";
    public string QuoteSMSNo { get; set; } = "";
    public string BusinessDescription { get; set; } = "";
    public string County { get; set; } = "";
    public string City { get; set; } = "";
    public string StoreURL { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Address { get; set; } = "";
}

public class StoreTimingItem
{
    public int DayId { get; set; }
    public string DayName { get; set; } = "";
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public bool IsClosed { get; set; }
}

public class StoreTimingSaveModel
{
    public int DayId { get; set; }
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public bool IsClosed { get; set; }
}

public class SpecialDayItem
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public bool IsClosed { get; set; }
}

public class SpecialDaySaveModel
{
    public long Id { get; set; }
    public string Date { get; set; } = "";
    public int FromHour { get; set; }
    public int ToHour { get; set; }
    public bool IsClosed { get; set; }
}

public class DeliverySettingsSaveModel
{
    public bool IsDeliverable { get; set; }
    public decimal DeliveryMinOrder { get; set; }
    public decimal DeliveryMiles { get; set; }
    public bool IsCollectable { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Edit Business Info module.
/// Migrated from editStoreInfo.aspx.
/// Uses DefaultConnection with tbl_webstore, tbl_webstoreTiming, tbl_webstoreSpecialDays tables.
/// Module 1 permission check.
/// </summary>
public class EditBusinessInfoService
{
    private readonly string _defaultConnection;

    public EditBusinessInfoService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets business info for the given webstore.
    /// </summary>
    public async Task<BusinessInfoModel?> GetBusinessInfoAsync(long webstoreId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT webstore_ID, webstore_businessName, webstore_businessPhone, 
                           webstore_OrderEmail, webstore_QuoteSMSNo, webstore_businessdet,
                           webstore_State, webstore_city, webstore_storeURL, 
                           webstore_postcode, webstore_address, webstore_logo,
                           webstore_IsCollectable, webstore_IsDeliverable,
                           webstore_DeliveryminOrder, webstore_DeliverMiles
                    FROM tbl_webstore 
                    WHERE webstore_ID = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new BusinessInfoModel
            {
                WebstoreId = reader.GetInt64(0),
                BusinessName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                BusinessPhone = reader.IsDBNull(2) ? "" : reader.GetString(2),
                OrderEmail = reader.IsDBNull(3) ? "" : reader.GetString(3),
                QuoteSMSNo = reader.IsDBNull(4) ? "" : reader.GetString(4),
                BusinessDescription = reader.IsDBNull(5) ? "" : reader.GetString(5),
                County = reader.IsDBNull(6) ? "" : reader.GetString(6),
                City = reader.IsDBNull(7) ? "" : reader.GetString(7),
                StoreURL = reader.IsDBNull(8) ? "" : reader.GetString(8),
                Postcode = reader.IsDBNull(9) ? "" : reader.GetString(9),
                Address = reader.IsDBNull(10) ? "" : reader.GetString(10),
                Logo = reader.IsDBNull(11) ? "" : reader.GetString(11),
                IsCollectable = !reader.IsDBNull(12) && reader.GetBoolean(12),
                IsDeliverable = !reader.IsDBNull(13) && reader.GetBoolean(13),
                DeliveryMinOrder = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14),
                DeliveryMiles = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15)
            };
        }

        return null;
    }

    /// <summary>
    /// Saves core business info fields (name, phone, email, SMS, description, address fields, URL).
    /// </summary>
    public async Task<bool> SaveBusinessInfoAsync(BusinessInfoSaveModel model, long webstoreId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_webstore SET 
                            webstore_businessName = @businessName,
                            webstore_businessPhone = @businessPhone,
                            webstore_OrderEmail = @orderEmail,
                            webstore_QuoteSMSNo = @quoteSMSNo,
                            webstore_businessdet = @businessDet,
                            webstore_State = @county,
                            webstore_city = @city,
                            webstore_storeURL = @storeURL,
                            webstore_postcode = @postcode,
                            webstore_address = @address
                        WHERE webstore_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@businessName", model.BusinessName ?? "");
            cmd.Parameters.AddWithValue("@businessPhone", model.BusinessPhone ?? "");
            cmd.Parameters.AddWithValue("@orderEmail", model.OrderEmail ?? "");
            cmd.Parameters.AddWithValue("@quoteSMSNo", model.QuoteSMSNo ?? "");
            cmd.Parameters.AddWithValue("@businessDet", model.BusinessDescription ?? "");
            cmd.Parameters.AddWithValue("@county", model.County ?? "");
            cmd.Parameters.AddWithValue("@city", model.City ?? "");
            cmd.Parameters.AddWithValue("@storeURL", model.StoreURL ?? "");
            cmd.Parameters.AddWithValue("@postcode", model.Postcode ?? "");
            cmd.Parameters.AddWithValue("@address", model.Address ?? "");
            cmd.Parameters.AddWithValue("@id", webstoreId);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets store timings (7 days) for the given webstore.
    /// Returns all 7 days, creating defaults if missing.
    /// </summary>
    public async Task<List<StoreTimingItem>> GetTimingsAsync(long webstoreId)
    {
        var dayNames = new[] { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        var timings = new List<StoreTimingItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT webstoreTiming_dayid, webstoreTiming_from, webstoreTiming_to, webstoreTiming_isclosed
                    FROM tbl_webstoreTiming 
                    WHERE webstoreTiming_webstoreid = @id
                    ORDER BY webstoreTiming_dayid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", webstoreId);

        var existingDays = new Dictionary<int, StoreTimingItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dayId = reader.GetInt32(0);
            existingDays[dayId] = new StoreTimingItem
            {
                DayId = dayId,
                DayName = dayId >= 1 && dayId <= 7 ? dayNames[dayId] : $"Day {dayId}",
                FromHour = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ToHour = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                IsClosed = !reader.IsDBNull(3) && reader.GetBoolean(3)
            };
        }

        // Ensure all 7 days are present
        for (int d = 1; d <= 7; d++)
        {
            if (existingDays.TryGetValue(d, out var existing))
            {
                timings.Add(existing);
            }
            else
            {
                timings.Add(new StoreTimingItem
                {
                    DayId = d,
                    DayName = dayNames[d],
                    FromHour = 9,
                    ToHour = 17,
                    IsClosed = false
                });
            }
        }

        return timings;
    }

    /// <summary>
    /// Saves store timings for all 7 days (upsert pattern: delete + insert).
    /// </summary>
    public async Task<bool> SaveTimingsAsync(List<StoreTimingSaveModel> timings, long webstoreId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Delete existing timings for this webstore
            var deleteSql = "DELETE FROM tbl_webstoreTiming WHERE webstoreTiming_webstoreid = @id";
            await using (var deleteCmd = new SqlCommand(deleteSql, conn))
            {
                deleteCmd.Parameters.AddWithValue("@id", webstoreId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // Insert all 7 days
            foreach (var timing in timings)
            {
                var insertSql = @"INSERT INTO tbl_webstoreTiming 
                                  (webstoreTiming_webstoreid, webstoreTiming_dayid, webstoreTiming_from, webstoreTiming_to, webstoreTiming_isclosed)
                                  VALUES (@webstoreId, @dayId, @fromHour, @toHour, @isClosed)";

                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                insertCmd.Parameters.AddWithValue("@dayId", timing.DayId);
                insertCmd.Parameters.AddWithValue("@fromHour", timing.FromHour);
                insertCmd.Parameters.AddWithValue("@toHour", timing.ToHour);
                insertCmd.Parameters.AddWithValue("@isClosed", timing.IsClosed);

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
    /// Gets special days (non-busy) for the given webstore, ordered by date descending.
    /// </summary>
    public async Task<List<SpecialDayItem>> GetSpecialDaysAsync(long webstoreId)
    {
        var items = new List<SpecialDayItem>();

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT webstoreSpecialDay_ID, webstoreSpecialDay_Date, 
                           webstoreSpecialDay_from, webstoreSpecialDay_to, webstoreSpecialDay_isclosed
                    FROM tbl_webstoreSpecialDays 
                    WHERE webstoreSpecialDay_webstoreid = @id AND webstoreSpecialDay_isbusy = 0
                    ORDER BY webstoreSpecialDay_Date DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SpecialDayItem
            {
                Id = reader.GetInt64(0),
                Date = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                FromHour = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                ToHour = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                IsClosed = !reader.IsDBNull(4) && reader.GetBoolean(4)
            });
        }

        return items;
    }

    /// <summary>
    /// Saves (add or update) a special day.
    /// </summary>
    public async Task<bool> SaveSpecialDayAsync(SpecialDaySaveModel model, long webstoreId)
    {
        try
        {
            if (!DateTime.TryParse(model.Date, out var parsedDate))
                return false;

            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            if (model.Id == 0)
            {
                // INSERT
                var sql = @"INSERT INTO tbl_webstoreSpecialDays 
                            (webstoreSpecialDay_webstoreid, webstoreSpecialDay_Date, webstoreSpecialDay_from, 
                             webstoreSpecialDay_to, webstoreSpecialDay_isclosed, webstoreSpecialDay_isbusy, webstoreSpecialDay_modifiedOn)
                            VALUES (@webstoreId, @date, @fromHour, @toHour, @isClosed, 0, @modifiedOn)";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                cmd.Parameters.AddWithValue("@date", parsedDate);
                cmd.Parameters.AddWithValue("@fromHour", model.FromHour);
                cmd.Parameters.AddWithValue("@toHour", model.ToHour);
                cmd.Parameters.AddWithValue("@isClosed", model.IsClosed);
                cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);

                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // UPDATE
                var sql = @"UPDATE tbl_webstoreSpecialDays SET 
                                webstoreSpecialDay_Date = @date,
                                webstoreSpecialDay_from = @fromHour,
                                webstoreSpecialDay_to = @toHour,
                                webstoreSpecialDay_isclosed = @isClosed,
                                webstoreSpecialDay_modifiedOn = @modifiedOn
                            WHERE webstoreSpecialDay_ID = @id AND webstoreSpecialDay_webstoreid = @webstoreId";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@date", parsedDate);
                cmd.Parameters.AddWithValue("@fromHour", model.FromHour);
                cmd.Parameters.AddWithValue("@toHour", model.ToHour);
                cmd.Parameters.AddWithValue("@isClosed", model.IsClosed);
                cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", model.Id);
                cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes a special day by ID.
    /// </summary>
    public async Task<bool> DeleteSpecialDayAsync(long id)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = "DELETE FROM tbl_webstoreSpecialDays WHERE webstoreSpecialDay_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves delivery settings (isDeliverable, minOrder, miles, isCollectable).
    /// </summary>
    public async Task<bool> SaveDeliverySettingsAsync(DeliverySettingsSaveModel model, long webstoreId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_webstore SET 
                            webstore_IsDeliverable = @isDeliverable,
                            webstore_DeliveryminOrder = @minOrder,
                            webstore_DeliverMiles = @miles,
                            webstore_IsCollectable = @isCollectable
                        WHERE webstore_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@isDeliverable", model.IsDeliverable);
            cmd.Parameters.AddWithValue("@minOrder", model.DeliveryMinOrder);
            cmd.Parameters.AddWithValue("@miles", model.DeliveryMiles);
            cmd.Parameters.AddWithValue("@isCollectable", model.IsCollectable);
            cmd.Parameters.AddWithValue("@id", webstoreId);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
