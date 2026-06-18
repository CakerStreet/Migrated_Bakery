using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

// ─── Models ────────────────────────────────────────────────────────────────────

public class SocialLinkItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LinkURL { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActivated { get; set; }
    public string IconURL { get; set; } = "";
}

public class SocialLinkSaveModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LinkURL { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActivated { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Service for Manage Social Links module.
/// Migrated from managesociallinks.aspx.
/// Uses DefaultConnection with tbl_sociallinks table.
/// Module 22 permission check.
/// </summary>
public class SocialLinksService
{
    private readonly string _defaultConnection;

    // Fixed social link types (preserving legacy "Pinterst" typo)
    private static readonly string[] FixedTypes = { "Facebook", "LinkedIn", "Pinterst", "Twitter", "Youtube", "Googleplus" };

    // Icon URL mapping (name lowercase → icon key)
    private static readonly Dictionary<string, string> IconMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Facebook", "facebook" },
        { "LinkedIn", "linkedin" },
        { "Pinterst", "pinterest" },
        { "Twitter", "twitter" },
        { "Youtube", "youtube" },
        { "Googleplus", "google" }
    };

    public SocialLinksService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets social links for the given webstore.
    /// LEFT JOINs 6 fixed types with existing DB records.
    /// If no record exists for a type, returns default (id=0, empty URL, order=1, inactive).
    /// </summary>
    public async Task<List<SocialLinkItem>> GetSocialLinksAsync(long webstoreId)
    {
        // Query existing records for this webstore
        var existingLinks = new Dictionary<string, SocialLinkItem>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT sociallinks_ID, sociallinks_Name, sociallinks_linkURL, 
                           sociallinks_displayorder, sociallinks_isActivated, sociallinks_iconURL
                    FROM tbl_sociallinks 
                    WHERE sociallinks_webstoreID = @wid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
            existingLinks[name] = new SocialLinkItem
            {
                Id = reader.GetInt32(0),
                Name = name,
                LinkURL = reader.IsDBNull(2) ? "" : reader.GetString(2),
                DisplayOrder = reader.IsDBNull(3) ? 1 : reader.GetInt32(3),
                IsActivated = !reader.IsDBNull(4) && reader.GetBoolean(4),
                IconURL = reader.IsDBNull(5) ? "" : reader.GetString(5)
            };
        }

        // Build result: for each fixed type, use DB record or default
        var result = new List<SocialLinkItem>();
        foreach (var type in FixedTypes)
        {
            if (existingLinks.TryGetValue(type, out var existing))
            {
                result.Add(existing);
            }
            else
            {
                result.Add(new SocialLinkItem
                {
                    Id = 0,
                    Name = type,
                    LinkURL = "",
                    DisplayOrder = 1,
                    IsActivated = false,
                    IconURL = IconMapping.GetValueOrDefault(type, "")
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Saves all social links for the webstore.
    /// INSERT if id==0, UPDATE if id>0.
    /// </summary>
    public async Task<bool> SaveAllAsync(List<SocialLinkSaveModel> items, long webstoreId)
    {
        try
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            foreach (var item in items)
            {
                var iconUrl = IconMapping.TryGetValue(item.Name ?? "", out var mapped) ? mapped : "";

                if (item.Id == 0)
                {
                    // INSERT
                    var sql = @"INSERT INTO tbl_sociallinks 
                                (sociallinks_Name, sociallinks_linkURL, sociallinks_displayorder, 
                                 sociallinks_isActivated, sociallinks_webstoreID, sociallinks_iconURL, sociallinks_modifiedOn)
                                VALUES (@name, @url, @displayOrder, @isActivated, @webstoreId, @iconUrl, @modifiedOn)";

                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@name", item.Name ?? "");
                    cmd.Parameters.AddWithValue("@url", item.LinkURL ?? "");
                    cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
                    cmd.Parameters.AddWithValue("@isActivated", item.IsActivated);
                    cmd.Parameters.AddWithValue("@webstoreId", webstoreId);
                    cmd.Parameters.AddWithValue("@iconUrl", iconUrl);
                    cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // UPDATE
                    var sql = @"UPDATE tbl_sociallinks SET 
                                    sociallinks_linkURL = @url,
                                    sociallinks_displayorder = @displayOrder,
                                    sociallinks_isActivated = @isActivated,
                                    sociallinks_iconURL = @iconUrl,
                                    sociallinks_modifiedOn = @modifiedOn
                                WHERE sociallinks_ID = @id AND sociallinks_webstoreID = @webstoreId";

                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@url", item.LinkURL ?? "");
                    cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
                    cmd.Parameters.AddWithValue("@isActivated", item.IsActivated);
                    cmd.Parameters.AddWithValue("@iconUrl", iconUrl);
                    cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@id", item.Id);
                    cmd.Parameters.AddWithValue("@webstoreId", webstoreId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
