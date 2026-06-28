using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Handles bakery/business login.
/// Pixel-perfect migration of legacy adminlogin.aspx + adminlogin.aspx.cs.
/// Route: /adminlogin (primary, matching legacy), /businesslogin (backward compat)
/// Standalone page (no layout).
/// </summary>
[Route("adminlogin")]
[Route("adminlogin.aspx")]
[Route("businesslogin")]
[Route("business/login")]
public class BusinessLoginController : Controller
{
    private readonly BakeryAuthHelper _authHelper;

    public BusinessLoginController(BakeryAuthHelper authHelper)
    {
        _authHelper = authHelper;
    }

    [HttpGet("")]
    public IActionResult Index(string? returl, string? logout)
    {
        // Handle logout
        if (logout == "1")
        {
            _authHelper.ClearAuthCookie(HttpContext);
            return RedirectToAction("Index");
        }

        // Dev bypass: skip login entirely — auto-authenticate and go straight to destination
        if (_authHelper.IsDevBypassActive)
        {
            if (!string.IsNullOrEmpty(returl))
                return Redirect(returl);
            return Redirect("/staffrota");
        }

        // If already authenticated, redirect (matching legacy behavior)
        var userId = _authHelper.GetAuthenticatedUserId(HttpContext);
        if (userId != null)
        {
            if (!string.IsNullOrEmpty(returl))
                return Redirect(returl);
            return Redirect("/staffrota");
        }

        ViewBag.ReturnUrl = returl;
        ViewBag.ErrorMessage = null;
        ViewBag.ShowOtpModal = false;
        return View();
    }

    /// <summary>
    /// Login POST — pixel-perfect match of legacy imgbtnLogin_Click (lines 206-467).
    /// Flow: bakeryuser lookup → franchise fallback → password check → OTP check → cookie → redirect
    /// </summary>
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string email, string password, string? returl, string? crmid)
    {
        ViewBag.ReturnUrl = returl;
        ViewBag.ShowOtpModal = false;
        ViewBag.Username = email;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.ErrorMessage = "Username/password not matched.";
            return View();
        }

        try
        {
            // Log login attempt (matching legacy: if (!clsglobaltext.isLocalRequest()) { GetUserEnvironment(Request); })
            if (!BakeryAuthHelper.IsLocalRequest(HttpContext))
            {
                try
                {
                    string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                    if (Request.Headers.ContainsKey("X-Forwarded-For"))
                        ip = Request.Headers["X-Forwarded-For"].ToString();
                    await _authHelper.LogLoginAttemptAsync(ip, Request.Headers.UserAgent.ToString(), email.Trim(), password.Trim());
                }
                catch
                {
                    // Swallow (matches legacy empty catch)
                }
            }

            // Step 1: Find bakery user (matching legacy line 228)
            var bu = await _authHelper.FindBakeryUserAsync(email.Trim());

            if (bu == null)
            {
                // Step 2: Fallback to franchise (matching legacy lines 232-294)
                return await HandleFranchiseLogin(email.Trim(), password.Trim());
            }

            // Step 3: Check password (case-insensitive, matching legacy line 300)
            if (!string.Equals(password.Trim(), bu.Password, StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.ErrorMessage = "Wrong Password.";
                return View();
            }

            // Step 4: Check active status (matching legacy line 305)
            if (!bu.IsActive || !bu.IsOpen)
            {
                ViewBag.ErrorMessage = "Your account is not active";
                return View();
            }

            // Step 5: Get webstore info (matching legacy lines 298-325)
            long webshopId = 0;
            long.TryParse(bu.WebshopId, out webshopId);
            var webstore = await _authHelper.GetWebstoreAsync(webshopId);
            string businessName = webstore?.BusinessName ?? "";
            string logo = webstore?.Logo ?? "";
            int isfranchise = 0;

            if (webstore != null)
            {
                isfranchise = await _authHelper.HasFranchiseUsersAsync((int)webstore.WebstoreId) ? 1 : 0;
            }

            // Step 6: Check OTP / LoginConfirmed (matching legacy lines 327-460)
            string userAgent = Request.Headers.UserAgent.ToString();
            bool isConfirmed = await _authHelper.IsLoginConfirmedAsync(bu.CustomerId, userAgent);
            bool isLocal = BakeryAuthHelper.IsLocalRequest(HttpContext);

            if (isConfirmed || isLocal)
            {
                // Direct login — device confirmed or local request
                // Matching legacy lines 332-364
                string supplierId = "0";

                if (bu.UserType == "11")
                {
                    // TODO-YARP: Supplier lookup — GetSupplierIDByUserID not implemented. Hardcoded to "0".
                    // Legacy: clsInventoryManagement cv = new clsInventoryManagement();
                    // string strsupp = cv.GetSupplierIDByUserID(Convert.ToString(bu.customer_ID));
                    // SupplierId = strsupp.Split(',')[0]; webstorebusinessname = strsupp.Split(',')[1];
                }

                string qscrmid = crmid ?? Request.Query["crmid"].FirstOrDefault() ?? "0";

                bu.BusinessName = businessName;
                bu.Logo = logo;
                bu.IsFranchise = isfranchise;
                bu.CrmId = qscrmid;
                bu.SupplierId = supplierId;
                // TODO-YARP: istemporary check — bakeryuser_istemporarybyid not implemented. Hardcoded to 0.
                // Legacy: ((clsglobaltext.bakeryuser_istemporarybyid(bu.customer_istemporary)) ? 1 : 0)

                _authHelper.SetAuthCookie(HttpContext, bu);

                // Redirect (matching legacy lines 347-364 exactly)
                if (!string.IsNullOrEmpty(returl))
                    return Redirect(returl);

                if (bu.UserType == "11")
                    return Redirect("/supplier/managesupplyorder?status=0");

                return Redirect(isfranchise == 0 ? "/staffrota" : "/myaccountbalance");
            }
            else
            {
                // Need OTP — generate if no valid one exists (matching legacy lines 366-459)
                if (!await _authHelper.HasValidOtpAsync(bu.CustomerId))
                {
                    await _authHelper.GenerateOtpAsync(bu.CustomerId, bu.UserName, userAgent);
                }

                ViewBag.ShowOtpModal = true;
                ViewBag.ErrorMessage = null;
                return View();
            }
        }
        catch
        {
            // Swallow (matches legacy empty catch block, lines 464-467)
            ViewBag.ErrorMessage = null;
            return View();
        }
    }

    /// <summary>
    /// OTP Verify POST — pixel-perfect match of legacy imgbtnLogin_OTP_Click + imgbtnLogin_withOTP.
    /// </summary>
    [HttpPost("verifyotp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string otp, string email, string password, string? returl, string? crmid)
    {
        ViewBag.ReturnUrl = returl;
        ViewBag.ShowOtpModal = true;
        ViewBag.Username = email;

        if (string.IsNullOrWhiteSpace(otp))
        {
            ViewBag.OtpError = "Enter OTP";
            return View("Index");
        }

        try
        {
            // Step 1: Verify OTP (matching legacy lines 187-204)
            var otpId = await _authHelper.VerifyOtpAsync(otp);
            if (otpId == null)
            {
                // OTP invalid or expired (matching legacy ScriptManager alert)
                ViewBag.OtpError = "OTP is Invalid or Expired!";
                return View("Index");
            }

            // Step 2: Re-log login attempt (matching legacy imgbtnLogin_withOTP lines 473-483)
            if (!BakeryAuthHelper.IsLocalRequest(HttpContext))
            {
                try
                {
                    string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                    if (Request.Headers.ContainsKey("X-Forwarded-For"))
                        ip = Request.Headers["X-Forwarded-For"].ToString();
                    await _authHelper.LogLoginAttemptAsync(ip, Request.Headers.UserAgent.ToString(), email?.Trim() ?? "", password?.Trim() ?? "");
                }
                catch
                {
                    // Swallow
                }
            }

            // Step 3: Re-find bakery user (matching legacy line 491)
            var bu = await _authHelper.FindBakeryUserAsync(email?.Trim() ?? "");
            if (bu == null)
            {
                ViewBag.ErrorMessage = "Username/password not matched.";
                ViewBag.ShowOtpModal = false;
                return View("Index");
            }

            // Step 4: Re-check password (matching legacy line 503)
            if (!string.Equals(password?.Trim(), bu.Password, StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.ErrorMessage = "Wrong Password.";
                ViewBag.ShowOtpModal = false;
                return View("Index");
            }

            // Step 5: Re-check active (matching legacy line 508)
            if (!bu.IsActive || !bu.IsOpen)
            {
                ViewBag.ErrorMessage = "Your account is not active";
                ViewBag.ShowOtpModal = false;
                return View("Index");
            }

            // Step 6: Save LoginConfirmed (matching legacy lines 533-542)
            string userAgent = Request.Headers.UserAgent.ToString();
            await _authHelper.SaveLoginConfirmedAsync(bu.CustomerId, userAgent, otpId.Value);

            // Step 7: Get webstore info (matching legacy lines 501-527)
            long webshopId = 0;
            long.TryParse(bu.WebshopId, out webshopId);
            var webstore = await _authHelper.GetWebstoreAsync(webshopId);
            string businessName = webstore?.BusinessName ?? "";
            string logo = webstore?.Logo ?? "";
            int isfranchise = 0;

            if (webstore != null)
            {
                isfranchise = await _authHelper.HasFranchiseUsersAsync((int)webstore.WebstoreId) ? 1 : 0;
            }

            // Step 8: Set cookie (matching legacy lines 545-555)
            string supplierId = "0";
            if (bu.UserType == "11")
            {
                // TODO-YARP: Supplier lookup for type=11
            }

            string qscrmid = crmid ?? Request.Query["crmid"].FirstOrDefault() ?? "0";
            bu.BusinessName = businessName;
            bu.Logo = logo;
            bu.IsFranchise = isfranchise;
            bu.CrmId = qscrmid;
            bu.SupplierId = supplierId;

            _authHelper.SetAuthCookie(HttpContext, bu);

            // Step 9: Redirect (matching legacy lines 560-577)
            if (!string.IsNullOrEmpty(returl))
                return Redirect(returl);

            if (bu.UserType == "11")
                return Redirect("/supplier/managesupplyorder?status=0");

            return Redirect(isfranchise == 0 ? "/staffrota" : "/myaccountbalance");
        }
        catch
        {
            // Swallow (matching legacy empty catch)
            ViewBag.OtpError = "OTP is Invalid or Expired!";
            return View("Index");
        }
    }

    /// <summary>
    /// Handles franchise login when user not found in tbl_bakeryuser.
    /// Matches legacy franchise fallback in imgbtnLogin_Click (lines 232-294).
    /// </summary>
    private async Task<IActionResult> HandleFranchiseLogin(string username, string password)
    {
        var franchise = await _authHelper.FindFranchiseUserAsync(username);

        if (franchise == null)
        {
            // Matching legacy line 240
            ViewBag.ErrorMessage = "Username/password not matched.";
            return View();
        }

        if (franchise.IsDeleted)
        {
            // Matching legacy line 247
            ViewBag.ErrorMessage = "This franchise is not active.";
            return View();
        }

        if (!string.Equals(password, franchise.Password, StringComparison.OrdinalIgnoreCase))
        {
            // Matching legacy line 252
            ViewBag.ErrorMessage = "Wrong Password.";
            return View();
        }

        if (!franchise.IsActive)
        {
            // Matching legacy line 257
            ViewBag.ErrorMessage = "Your account is not active";
            return View();
        }

        // Set franchise cookie and redirect (matching legacy lines 261-290)
        _authHelper.SetFranchiseCookie(HttpContext, franchise.Id, franchise.Title, franchise.Username);
        return Redirect("/franchise/manageproductwithfranchise");
    }
}
