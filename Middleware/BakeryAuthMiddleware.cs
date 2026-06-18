namespace CakerStreet.Business.Middleware;

using CakerStreet.Business.Services;

/// <summary>
/// Middleware that enforces bakery authentication on all routes except login, print, and health.
/// On missing/invalid cookie: redirects to /businesslogin?returl={encodedPath}
/// On valid cookie: stores user info in HttpContext.Items.
/// Supplier userType "11": redirects to legacy (not migrated).
/// </summary>
public class BakeryAuthMiddleware
{
    private readonly RequestDelegate _next;

    public BakeryAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, BakeryAuthHelper authHelper)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for login page, print page, health endpoint, and static files
        if (ShouldSkipAuth(path))
        {
            await _next(context);
            return;
        }

        // Dev bypass: set configured dev user info and continue
        if (authHelper.IsDevBypassActive)
        {
            SetContextItems(context, authHelper, isDevMode: true);
            await _next(context);
            return;
        }

        // Check authentication
        var userId = authHelper.GetAuthenticatedUserId(context);
        if (userId == null)
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/businesslogin?returl=" + Uri.EscapeDataString(returnUrl));
            return;
        }

        // Check for supplier userType "11" — redirect to legacy (not migrated)
        var userType = authHelper.GetBakeryUserType(context);
        if (userType == "11")
        {
            context.Response.Redirect("http://localhost:27201/supplier/managesupplyorder?status=0");
            return;
        }

        // Auth passed — store user info in HttpContext.Items
        SetContextItems(context, authHelper, isDevMode: false);

        await _next(context);
    }

    private static bool ShouldSkipAuth(string path)
    {
        // Skip login page
        if (path.Equals("/businesslogin", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/business/login", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip print page
        if (path.Equals("/printorderbakers", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip customer invoice print (accessible via email links with wccode)
        if (path.StartsWith("/printorder/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip health endpoint
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip static files (wwwroot)
        if (path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void SetContextItems(HttpContext context, BakeryAuthHelper authHelper, bool isDevMode)
    {
        context.Items["BakeryUserId"] = authHelper.GetAuthenticatedUserId(context) ?? 0;
        context.Items["BakeryWebshopId"] = authHelper.GetBakeryWebshopId(context) ?? "";
        context.Items["BakeryUserType"] = authHelper.GetBakeryUserType(context) ?? "";
        context.Items["BakeryUserName"] = authHelper.GetBakeryUserName(context) ?? "";
        context.Items["BakeryBusinessName"] = authHelper.GetBakeryBusinessName(context) ?? "";
        context.Items["BakeryIsDevMode"] = isDevMode;
    }
}
