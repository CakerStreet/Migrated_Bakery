using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("resetpassword")]
public class ResetPasswordController : Controller
{
    private readonly ResetPasswordService _resetPasswordService;
    private readonly BakeryAuthHelper _authHelper;

    public ResetPasswordController(ResetPasswordService resetPasswordService, BakeryAuthHelper authHelper)
    {
        _resetPasswordService = resetPasswordService;
        _authHelper = authHelper;
    }

    [HttpGet("{resetcode}")]
    public async Task<IActionResult> Index(string resetcode)
    {
        if (string.IsNullOrEmpty(resetcode))
        {
            ViewBag.ErrorMessage = "Wrong reset code.";
            return View("Error");
        }

        var info = await _resetPasswordService.GetResetPasswordInfoAsync(resetcode);
        if (info == null || info.PasswordCode.Equals("expired", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.ErrorMessage = "Wrong reset code or link has expired.";
            return View("Error");
        }

        if (DateTime.Now > info.ExpireDate)
        {
            ViewBag.ErrorMessage = "Reset link has been expired. Please generate a new link.";
            return View("Error");
        }

        ViewBag.ResetCode = resetcode;
        ViewBag.ErrorMessage = null;
        ViewBag.SuccessMessage = null;

        return View(info);
    }

    [HttpPost("{resetcode}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string resetcode, string password, string confirmPassword)
    {
        if (string.IsNullOrEmpty(resetcode))
        {
            ViewBag.ErrorMessage = "Wrong reset code.";
            return View("Error");
        }

        var info = await _resetPasswordService.GetResetPasswordInfoAsync(resetcode);
        if (info == null || info.PasswordCode.Equals("expired", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.ErrorMessage = "Wrong reset code or link has expired.";
            return View("Error");
        }

        if (DateTime.Now > info.ExpireDate)
        {
            ViewBag.ErrorMessage = "Reset link has been expired. Please generate a new link.";
            return View("Error");
        }

        ViewBag.ResetCode = resetcode;

        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.ErrorMessage = "Password and confirm password are required.";
            return View(info);
        }

        if (password != confirmPassword)
        {
            ViewBag.ErrorMessage = "Password and confirm password do not match.";
            return View(info);
        }

        try
        {
            if (info.CustomerType == 1) // customer
            {
                await _resetPasswordService.UpdateCustomerPasswordAsync(info.CustomerID, password.Trim());
                await _resetPasswordService.ExpireResetPasswordCodeAsync(resetcode);
                ViewBag.SuccessMessage = "Password has been reset successfully. You can now login.";
                return View("Success");
            }
            else if (info.CustomerType == 2) // bakery user
            {
                await _resetPasswordService.UpdateBakeryUserPasswordAsync(info.CustomerID, password.Trim());
                await _resetPasswordService.ExpireResetPasswordCodeAsync(resetcode);

                // Auto-login for bakery user
                var loginDetails = await _resetPasswordService.GetBakeryLoginDetailsAsync(info.CustomerID);
                if (loginDetails != null)
                {
                    _authHelper.SetAuthCookie(HttpContext, loginDetails);
                    return Redirect("/businessorders");
                }

                ViewBag.SuccessMessage = "Password has been reset successfully. You can now login.";
                return View("Success");
            }
            else
            {
                ViewBag.ErrorMessage = "Unknown user type.";
                return View(info);
            }
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = $"An error occurred: {ex.Message}";
            return View(info);
        }
    }
}
