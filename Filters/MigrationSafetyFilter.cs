using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CakerStreet.Business.Filters;

/// <summary>
/// Global action filter that blocks all write operations (POST/PUT/DELETE)
/// when MigrationSafety:ReadOnlyMode is enabled in configuration.
/// 
/// This prevents any INSERT/UPDATE/DELETE SQL from being executed against
/// production database backups during migration testing.
/// 
/// Whitelisted paths are allowed through even in read-only mode (e.g. login).
/// </summary>
public class MigrationSafetyFilter : IAsyncActionFilter
{
    private readonly bool _readOnlyMode;

    // Paths allowed to process POST even in read-only mode.
    // Login/auth writes are audit-level inserts (OTP, login attempts) — not business data mutations.
    private static readonly HashSet<string> WhitelistedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/businesslogin",        // POST login (password check, OTP generation, login attempt log)
        "/businesslogin/verifyotp", // POST OTP verification
        "/managesearchtags"      // POST tag updates (Phase 2 CRM mutations — inline edit, toggle active, bulk activate/deactivate)
    };

    public MigrationSafetyFilter(IConfiguration config)
    {
        _readOnlyMode = config.GetValue<bool>("MigrationSafety:ReadOnlyMode", false);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_readOnlyMode)
        {
            var method = context.HttpContext.Request.Method;

            // Block POST, PUT, DELETE, PATCH — allow GET, HEAD, OPTIONS
            if (method is "POST" or "PUT" or "DELETE" or "PATCH")
            {
                var path = context.HttpContext.Request.Path.Value ?? "";

                // Check whitelist
                if (!IsWhitelisted(path))
                {
                    // Return safety message as JSON for AJAX calls, or as plain text for form submissions
                    var acceptsJson = context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json")
                                   || context.HttpContext.Request.ContentType?.Contains("application/json") == true
                                   || context.HttpContext.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

                    if (acceptsJson)
                    {
                        context.Result = new JsonResult(new
                        {
                            success = false,
                            message = "Migration safety mode: write operation blocked.",
                            readOnlyMode = true
                        })
                        {
                            StatusCode = 403
                        };
                    }
                    else
                    {
                        context.Result = new ContentResult
                        {
                            Content = "Migration safety mode: write operation blocked.",
                            ContentType = "text/plain",
                            StatusCode = 403
                        };
                    }

                    return; // Short-circuit — do not execute the action
                }
            }
        }

        // Allow the request through
        await next();
    }

    private static bool IsWhitelisted(string path)
    {
        foreach (var wp in WhitelistedPaths)
        {
            if (path.StartsWith(wp, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
