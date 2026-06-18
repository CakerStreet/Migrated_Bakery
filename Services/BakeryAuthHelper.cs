using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Services;

/// <summary>
/// Authentication helper for the Bakery/Business Portal.
/// Reads/writes the bakeryDet_cakerstreet_ver5 cookie.
/// Queries tbl_bakeryuser directly for login validation (matching legacy adminlogin.aspx.cs).
/// </summary>
public class BakeryAuthHelper
{
    private const string BakeryCookieName = "bakeryDet_cakerstreet_ver5";
    private const string FranchiseCookieName = "franchiseDet_cakerstreet";

    private readonly string _connectionString;
    private readonly string _businessConnectionString;
    private readonly bool _devBypassEnabled;
    private readonly int _devBypassUserId;
    private readonly string _devBypassWebshopId;
    private readonly string _devBypassUserType;
    private readonly string _devBypassUserName;
    private readonly string _devBypassBusinessName;

    public BakeryAuthHelper(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _devBypassEnabled = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
                            && config.GetValue<bool>("Features:DevBypassBakeryAuth", false);
        _devBypassUserId = config.GetValue<int>("Features:DevBypassUserId", 1);
        _devBypassWebshopId = config.GetValue<string>("Features:DevBypassWebshopId") ?? "1";
        _devBypassUserType = config.GetValue<string>("Features:DevBypassUserType") ?? "1";
        _devBypassUserName = config.GetValue<string>("Features:DevBypassUserName") ?? "Dev Baker";
        _devBypassBusinessName = config.GetValue<string>("Features:DevBypassBusinessName") ?? "Dev Bakery";
    }

    /// <summary>
    /// Returns true if dev bypass is active (Development environment + config flag).
    /// </summary>
    public bool IsDevBypassActive => _devBypassEnabled;

    // ----------------------------------------------------------------
    // Cookie getters (unchanged — same indices, backward compatible)
    // ----------------------------------------------------------------

    /// <summary>
    /// Gets the authenticated user ID from the bakery cookie.
    /// Cookie format: split by ~ (take index 0), then split by ,
    /// Index [0] = customerID
    /// </summary>
    public int? GetAuthenticatedUserId(HttpContext ctx)
    {
        if (_devBypassEnabled)
            return _devBypassUserId;

        var cookieValue = ctx.Request.Cookies[BakeryCookieName];
        if (string.IsNullOrEmpty(cookieValue))
            return null;

        var parts = cookieValue.Split('~')[0].Split(',');
        if (parts.Length == 0)
            return null;

        if (int.TryParse(parts[0], out var userId) && userId > 0)
            return userId;

        return null;
    }

    /// <summary>
    /// Gets the bakery webshop ID from cookie. Index [3] = webshopID.
    /// </summary>
    public string? GetBakeryWebshopId(HttpContext ctx)
    {
        if (_devBypassEnabled)
            return _devBypassWebshopId;

        var parts = GetCookieParts(ctx);
        return parts != null && parts.Length > 3 ? parts[3] : null;
    }

    /// <summary>
    /// Gets the user type from cookie. Index [1] = userType.
    /// 1=owner, 2=manager, 3=staff, 4=admin, 11=supplier
    /// </summary>
    public string? GetBakeryUserType(HttpContext ctx)
    {
        if (_devBypassEnabled)
            return _devBypassUserType;

        var parts = GetCookieParts(ctx);
        return parts != null && parts.Length > 1 ? parts[1] : null;
    }

    /// <summary>
    /// Gets the user name from cookie. Index [2] = userName.
    /// </summary>
    public string? GetBakeryUserName(HttpContext ctx)
    {
        if (_devBypassEnabled)
            return _devBypassUserName;

        var parts = GetCookieParts(ctx);
        return parts != null && parts.Length > 2 ? parts[2] : null;
    }

    /// <summary>
    /// Gets the business name from cookie. Index [5] = businessName.
    /// </summary>
    public string? GetBakeryBusinessName(HttpContext ctx)
    {
        if (_devBypassEnabled)
            return _devBypassBusinessName;

        var parts = GetCookieParts(ctx);
        return parts != null && parts.Length > 5 ? parts[5] : null;
    }

    // ----------------------------------------------------------------
    // Login: tbl_bakeryuser query (pixel-perfect match of legacy)
    // Replaces old SP customerLoginChk_crm
    // ----------------------------------------------------------------

    /// <summary>
    /// Finds a bakery user by email from tbl_bakeryuser.
    /// Returns null if not found. Caller checks password and status.
    /// Matches legacy: db.BakeryUser.Where(w => w.customer_EmailID.ToLower() == txtName.Text.Trim()).FirstOrDefault()
    /// </summary>
    public async Task<BakeryLoginResult?> FindBakeryUserAsync(string email)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 customer_ID, customer_type, customer_Name, customer_EmailID, customer_password, " +
            "customer_isActive, customer_isOpen, customer_webshopID, customer_stafftype, customer_istemporary " +
            "FROM tbl_bakeryuser WHERE LOWER(customer_EmailID) = @email", conn);
        cmd.Parameters.AddWithValue("@email", email.ToLower());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows || !await reader.ReadAsync())
            return null;

        return new BakeryLoginResult
        {
            CustomerId = GetInt64Safe(reader, "customer_ID"),
            UserType = GetStringSafe(reader, "customer_type"),
            UserName = GetStringSafe(reader, "customer_Name"),
            Email = GetStringSafe(reader, "customer_EmailID"),
            Password = GetStringSafe(reader, "customer_password"),
            WebshopId = GetStringSafe(reader, "customer_webshopID"),
            IsActive = !reader.IsDBNull(reader.GetOrdinal("customer_isActive")) && reader.GetBoolean(reader.GetOrdinal("customer_isActive")),
            IsOpen = !reader.IsDBNull(reader.GetOrdinal("customer_isOpen")) && reader.GetBoolean(reader.GetOrdinal("customer_isOpen")),
            StaffType = GetInt32Safe(reader, "customer_stafftype"),
            IsTemporary = GetInt32Safe(reader, "customer_istemporary"),
        };
    }

    /// <summary>
    /// Backward-compatible AuthenticateAsync.
    /// Now queries tbl_bakeryuser directly instead of CRM SP.
    /// </summary>
    public async Task<BakeryLoginResult?> AuthenticateAsync(string email, string password)
    {
        var result = await FindBakeryUserAsync(email);
        if (result == null)
            return null;

        // Password check (case-insensitive, matching legacy: txtPwd.Text.ToLower() != bu.customer_password.ToLower())
        if (!string.Equals(password, result.Password, StringComparison.OrdinalIgnoreCase))
        {
            result.ReturnCode = 2; // Wrong password
            return result;
        }

        if (!result.IsActive || !result.IsOpen)
        {
            result.ReturnCode = 5; // Deactivated
            return result;
        }

        result.ReturnCode = 1; // Success
        return result;
    }

    // ----------------------------------------------------------------
    // Webstore lookup (db_cakerstreet_live)
    // ----------------------------------------------------------------

    /// <summary>
    /// Gets webstore info by ID. Returns businessName and logo.
    /// Matches legacy: db.webstore.Where(w => w.webstore_ID == webshopID).ToList()
    /// </summary>
    public async Task<WebstoreInfo?> GetWebstoreAsync(long webshopId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 webstore_ID, webstore_businessName, webstore_logo FROM tbl_webstore WHERE webstore_ID = @id", conn);
        cmd.Parameters.AddWithValue("@id", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows || !await reader.ReadAsync())
            return null;

        return new WebstoreInfo
        {
            WebstoreId = GetInt64Safe(reader, "webstore_ID"),
            BusinessName = GetStringSafe(reader, "webstore_businessName"),
            Logo = GetStringSafe(reader, "webstore_logo"),
        };
    }

    /// <summary>
    /// Checks if a webstore has franchise users linked.
    /// Matches legacy: db.franchiseUser.Where(w => w.franchiseUser_webstoreID == intwid).Any()
    /// </summary>
    public async Task<bool> HasFranchiseUsersAsync(int webstoreId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 1 FROM tbl_franchiseUser WHERE franchiseUser_webstoreID = @wid", conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    // ----------------------------------------------------------------
    // Franchise login (tbl_tempFranchise in business DB)
    // ----------------------------------------------------------------

    /// <summary>
    /// Finds a franchise user by username from tbl_tempFranchise.
    /// Matches legacy: bsent.tempFranchise.Where(w => w.username.Trim().ToLower() == txtName.Text.Trim().ToLower()).FirstOrDefault()
    /// </summary>
    public async Task<FranchiseLoginResult?> FindFranchiseUserAsync(string username)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ID, Title, username, password, IsDeleted, isActive " +
            "FROM tbl_tempFranchise WHERE LOWER(LTRIM(RTRIM(username))) = @username", conn);
        cmd.Parameters.AddWithValue("@username", username.Trim().ToLower());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows || !await reader.ReadAsync())
            return null;

        return new FranchiseLoginResult
        {
            Id = GetInt64Safe(reader, "ID"),
            Title = GetStringSafe(reader, "Title"),
            Username = GetStringSafe(reader, "username"),
            Password = GetStringSafe(reader, "password"),
            IsDeleted = !reader.IsDBNull(reader.GetOrdinal("IsDeleted")) && reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
            IsActive = !reader.IsDBNull(reader.GetOrdinal("isActive")) && reader.GetBoolean(reader.GetOrdinal("isActive")),
        };
    }

    // ----------------------------------------------------------------
    // OTP / LoginConfirmed (business DB)
    // ----------------------------------------------------------------

    /// <summary>
    /// Checks if a user+userAgent combination is already confirmed (OTP already done).
    /// Matches legacy: dbOTP.LoginConfirmed.Where(w => w.LoginConfirmed_userID == userID && w.LoginConfirmed_userAgent == struserAgent).Any()
    /// </summary>
    public async Task<bool> IsLoginConfirmedAsync(long userId, string userAgent)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 1 FROM tbl_LoginConfirmed WHERE LoginConfirmed_userID = @uid AND LoginConfirmed_userAgent = @ua", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@ua", userAgent);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    /// <summary>
    /// Checks if there's already a valid (unchecked, not expired) OTP for this user.
    /// Matches legacy: dbOTP.OTP.Where(w => w.OTP_userID == userID && w.OTP_validUpto > dtnow && w.OTP_isChecked == false).Any()
    /// </summary>
    public async Task<bool> HasValidOtpAsync(long userId)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 1 FROM tbl_OTP WHERE OTP_userID = @uid AND OTP_validUpto > @now AND OTP_isChecked = 0", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@now", DateTime.Now);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    /// <summary>
    /// Generates OTP, saves to tbl_OTP, returns OTP record ID.
    /// Matches legacy OTP creation in imgbtnLogin_Click (lines 373-386).
    /// SMS/Email sending is YARP (not implemented here).
    /// </summary>
    public async Task<long> GenerateOtpAsync(long userId, string userName, string userAgent)
    {
        string otpText = MakeCaptcha();
        string smsTo = "7402886853";
        string emailTo = "dennis@cakerstreet.com|amit.gupta@cakerstreet.com";

        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "INSERT INTO tbl_OTP (OTP_createdOn, OTP_isChecked, OTP_name, OTP_OTPText, OTP_smsTo, OTP_userID, OTP_validUpto, OTP_emailTo, OTP_userAgent) " +
            "VALUES (@createdOn, 0, @name, @otpText, @smsTo, @userId, @validUpto, @emailTo, @userAgent); " +
            "SELECT SCOPE_IDENTITY();", conn);
        cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);
        cmd.Parameters.AddWithValue("@name", userName);
        cmd.Parameters.AddWithValue("@otpText", otpText);
        cmd.Parameters.AddWithValue("@smsTo", smsTo);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@validUpto", DateTime.Now.AddMinutes(20));
        cmd.Parameters.AddWithValue("@emailTo", emailTo);
        cmd.Parameters.AddWithValue("@userAgent", userAgent);

        var result = await cmd.ExecuteScalarAsync();
        long otpId = Convert.ToInt64(result);

        // TODO-YARP: Twilio SMS — OTP generated and saved to DB but SMS NOT sent.
        // Legacy sends SMS to 7402886853 via Twilio (sms_accountSid, sms_authToken, sms_fromNumber).
        // Message format: "Please share OTP - {OTP} with {userName} to let user access the business profile.\nValidity upto: {validUpto}"

        // TODO-YARP: Email OTP — OTP generated but email NOT sent.
        // Legacy sends to dennis@cakerstreet.com|amit.gupta@cakerstreet.com via clsMail.
        // Subject: "{websiteName} - New Business Login OTP has been generated"

        return otpId;
    }

    /// <summary>
    /// Verifies an OTP. Returns the OTP record ID if valid, null if invalid/expired.
    /// Marks OTP as checked.
    /// Matches legacy: db.OTP.Where(w => w.OTP_OTPText == strOTPText && w.OTP_isChecked == false && w.OTP_validUpto > DateTime.Now)
    /// </summary>
    public async Task<long?> VerifyOtpAsync(string otpText)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        // Find valid OTP
        await using var findCmd = new SqlCommand(
            "SELECT TOP 1 OTP_ID FROM tbl_OTP WHERE OTP_OTPText = @otp AND OTP_isChecked = 0 AND OTP_validUpto > @now", conn);
        findCmd.Parameters.AddWithValue("@otp", otpText.ToUpper());
        findCmd.Parameters.AddWithValue("@now", DateTime.Now);

        var result = await findCmd.ExecuteScalarAsync();
        if (result == null)
            return null;

        long otpId = Convert.ToInt64(result);

        // Mark as checked (matches legacy: ep.OTP_isChecked = true; db.SaveChanges())
        await using var updateCmd = new SqlCommand(
            "UPDATE tbl_OTP SET OTP_isChecked = 1 WHERE OTP_ID = @id", conn);
        updateCmd.Parameters.AddWithValue("@id", otpId);
        await updateCmd.ExecuteNonQueryAsync();

        return otpId;
    }

    /// <summary>
    /// Saves a LoginConfirmed record (device remembered after OTP).
    /// Matches legacy: tbl_LoginConfirmed insert in imgbtnLogin_withOTP (lines 535-542).
    /// </summary>
    public async Task SaveLoginConfirmedAsync(long userId, string userAgent, long otpId)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        // Check if already exists (matches legacy: if (!varcheck.Any()))
        await using var checkCmd = new SqlCommand(
            "SELECT TOP 1 1 FROM tbl_LoginConfirmed WHERE LoginConfirmed_userID = @uid AND LoginConfirmed_userAgent = @ua", conn);
        checkCmd.Parameters.AddWithValue("@uid", userId);
        checkCmd.Parameters.AddWithValue("@ua", userAgent);

        if (await checkCmd.ExecuteScalarAsync() != null)
            return; // Already exists

        await using var insertCmd = new SqlCommand(
            "INSERT INTO tbl_LoginConfirmed (LoginConfirmed_OTPID, LoginConfirmed_userAgent, LoginConfirmed_createdOn, LoginConfirmed_userID) " +
            "VALUES (@otpId, @ua, @now, @uid)", conn);
        insertCmd.Parameters.AddWithValue("@otpId", otpId);
        insertCmd.Parameters.AddWithValue("@ua", userAgent);
        insertCmd.Parameters.AddWithValue("@now", DateTime.Now);
        insertCmd.Parameters.AddWithValue("@uid", userId);
        await insertCmd.ExecuteNonQueryAsync();
    }

    // ----------------------------------------------------------------
    // Login audit logging (db_cakerstreet_live)
    // ----------------------------------------------------------------

    /// <summary>
    /// Logs a login attempt to tbl_loginAttempt.
    /// Matches legacy GetUserEnvironment (lines 36-67).
    /// IP geolocation is YARP (location fields left empty).
    /// </summary>
    public async Task LogLoginAttemptAsync(string ip, string browserDetail, string username, string password)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "INSERT INTO tbl_loginAttempt (loginAttempt_browserDetail, loginAttempt_createdOn, loginAttempt_IP, " +
                "loginAttempt_logintype, loginAttempt_username, loginAttempt_password, loginAttempt_locationID, " +
                "loginAttempt_location, loginAttempt_postcode, loginAttempt_latt, loginAttempt_long, loginAttempt_CrmID) " +
                "VALUES (@browser, @now, @ip, 1, @username, @password, 0, '', '', '', '', 0)", conn);
            cmd.Parameters.AddWithValue("@browser", browserDetail ?? "");
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@ip", ip ?? "");
            cmd.Parameters.AddWithValue("@username", username ?? "");
            cmd.Parameters.AddWithValue("@password", password ?? "");
            await cmd.ExecuteNonQueryAsync();

            // TODO-YARP: IP2Location geolocation — login attempt recorded but location fields are empty.
            // Legacy calls: locaTemp.Resolve(IPAddress) then updates loginAttempt_locationID, loginAttempt_location,
            // loginAttempt_postcode, loginAttempt_latt, loginAttempt_long via SetLocation().
        }
        catch
        {
            // Swallow errors in audit logging (matches legacy try/catch in imgbtnLogin_Click lines 213-220)
        }
    }

    // ----------------------------------------------------------------
    // Cookie management
    // ----------------------------------------------------------------

    /// <summary>
    /// Sets the bakery auth cookie with 30-day expiry.
    /// Cookie format: 12 fields matching legacy clsUsers.updateBakeryUser_cookie:
    /// customerID,userType,userName,webshopID,email,businessName,crmID,isfranchise,stafftype,logo,SupplierId,istemporary
    /// </summary>
    public void SetAuthCookie(HttpContext ctx, BakeryLoginResult result)
    {
        var cookieValue = $"{result.CustomerId},{result.UserType},{result.UserName},{result.WebshopId}," +
                          $"{result.Email},{result.BusinessName},{result.CrmId},{result.IsFranchise}," +
                          $"{result.StaffType},{result.Logo},{result.SupplierId},{result.IsTemporary}";

        ctx.Response.Cookies.Append(BakeryCookieName, cookieValue, new CookieOptions
        {
            Expires = DateTimeOffset.Now.AddDays(30),
            Path = "/",
            HttpOnly = false
        });
    }

    /// <summary>
    /// Sets the franchise auth cookie.
    /// Matches legacy: HttpCookie(franchiseCookieName, "franchise,ID,Title,username")
    /// Cookie format: franchise,{ID},{Title},{username}
    /// </summary>
    public void SetFranchiseCookie(HttpContext ctx, long franchiseId, string title, string username)
    {
        var cookieValue = $"franchise,{franchiseId},{title},{username}";

        ctx.Response.Cookies.Append(FranchiseCookieName, cookieValue, new CookieOptions
        {
            Expires = DateTimeOffset.Now.AddMonths(1),
            Path = "/",
            HttpOnly = false
        });
    }

    /// <summary>
    /// Clears the bakery auth cookie by expiring it.
    /// </summary>
    public void ClearAuthCookie(HttpContext ctx)
    {
        ctx.Response.Cookies.Append(BakeryCookieName, "", new CookieOptions
        {
            Expires = DateTimeOffset.Now.AddMinutes(-1),
            Path = "/"
        });
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// Checks if the current request is local (development).
    /// Matches legacy clsglobaltext.isLocalRequest().
    /// </summary>
    public static bool IsLocalRequest(HttpContext ctx)
    {
        var connection = ctx.Connection;
        if (connection.RemoteIpAddress != null)
        {
            if (connection.LocalIpAddress != null)
                return connection.RemoteIpAddress.Equals(connection.LocalIpAddress);
            return System.Net.IPAddress.IsLoopback(connection.RemoteIpAddress);
        }
        return true;
    }

    /// <summary>
    /// Generates a 6-character OTP code.
    /// Pixel-perfect match of legacy makeCaptcha() + genrateRandomNumbers() (lines 587-619).
    /// Note: legacy has "9" instead of "W" at index 32 — preserved for exact match.
    /// </summary>
    private static string MakeCaptcha()
    {
        string[] chars = {
            "0","1","2","3","4","5","6","7","8","9",
            "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","9","X","Y","Z"
        };
        var rnd = new Random();
        string result = "";
        for (int i = 0; i < 6; i++)
        {
            result += chars[rnd.Next(0, 35)];
        }
        return result;
    }

    private string[]? GetCookieParts(HttpContext ctx)
    {
        var cookieValue = ctx.Request.Cookies[BakeryCookieName];
        if (string.IsNullOrEmpty(cookieValue))
            return null;

        var firstSection = cookieValue.Split('~')[0];
        var parts = firstSection.Split(',');
        return parts.Length > 0 ? parts : null;
    }

    private static long GetInt64Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        return Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static int GetInt32Safe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string GetStringSafe(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return "";
        return reader.GetValue(ordinal).ToString() ?? "";
    }
}

/// <summary>
/// Result from bakery user lookup (tbl_bakeryuser).
/// Cookie fields: customerID,userType,userName,webshopID,email,businessName,crmID,isfranchise,stafftype,logo,SupplierId,istemporary
/// </summary>
public class BakeryLoginResult
{
    public int ReturnCode { get; set; }
    public long CustomerId { get; set; }           // cookie[0]
    public string UserType { get; set; } = "";     // cookie[1] - customer_type
    public string UserName { get; set; } = "";     // cookie[2] - customer_Name
    public string Email { get; set; } = "";        // cookie[4] - customer_EmailID
    public string WebshopId { get; set; } = "";    // cookie[3] - customer_webshopID
    public string BusinessName { get; set; } = ""; // cookie[5] - webstore_businessName (populated from tbl_webstore)
    public string Password { get; set; } = "";     // customer_password (for comparison, NOT in cookie)
    public bool IsActive { get; set; }             // customer_isActive (NOT in cookie)
    public bool IsOpen { get; set; }               // customer_isOpen (NOT in cookie)
    // Additional cookie fields (legacy 12-field format)
    public string CrmId { get; set; } = "0";       // cookie[6] - from query string ?crmid=
    public int IsFranchise { get; set; } = 0;      // cookie[7] - from tbl_franchiseUser check
    public int StaffType { get; set; } = 0;        // cookie[8] - customer_stafftype
    public string Logo { get; set; } = "";         // cookie[9] - webstore_logo
    public string SupplierId { get; set; } = "0";  // cookie[10] - TODO-YARP: GetSupplierIDByUserID
    public int IsTemporary { get; set; } = 0;      // cookie[11] - TODO-YARP: bakeryuser_istemporarybyid
}

/// <summary>
/// Result from franchise user lookup (tbl_tempFranchise).
/// </summary>
public class FranchiseLoginResult
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Webstore info for cookie population (tbl_webstore).
/// </summary>
public class WebstoreInfo
{
    public long WebstoreId { get; set; }
    public string BusinessName { get; set; } = "";
    public string Logo { get; set; } = "";
}
