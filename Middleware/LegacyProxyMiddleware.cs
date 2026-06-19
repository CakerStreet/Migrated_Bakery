using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CakerStreet.Business.Middleware;

/// <summary>
/// Middleware that proxies unmigrated legacy pages to the IIS business portal.
/// Any request matching a known legacy route that has NOT been migrated to a modern
/// controller will be forwarded to the legacy IIS server transparently.
/// 
/// Configuration:
///   LegacyProxy:BaseUrl — the base URL of the legacy IIS business portal
///   LegacyProxy:Enabled — set to true to enable proxying (default: false)
/// </summary>
public class LegacyProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LegacyProxyMiddleware> _logger;

    /// <summary>
    /// Legacy routes that have NOT been migrated and should be proxied to IIS.
    /// Key = the modern URL path (lowercase), Value = the legacy .aspx path on IIS.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        // ─── Pages with no modern controller ──────────────────────────────────
        { "/edittheme",                     "/edittheme.aspx" },
        { "/managecollectionpoint",         "/managecollectionpoint.aspx" },
        { "/managesection",                 "/managesection.aspx" },
        { "/printdeliveryreceipt",          "/printdeliveryreceipt.aspx" },
        { "/printorder",                    "/printorderspongelist.aspx" },
        { "/printorderspongelist",          "/printorderspongelist.aspx" },
        { "/viewspongeorderlist",           "/viewspongeorderlist.aspx" },

        // ─── POST-only form targets (upload endpoints) ────────────────────────
        { "/uploadcakepicture",             "/uploadCakePicture.aspx" },
        { "/uploadorderpicture",            "/uploadOrderPicture.aspx" },
    };

    public LegacyProxyMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<LegacyProxyMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var enabled = _config.GetValue<bool>("LegacyProxy:Enabled", false);

        if (!enabled || !LegacyRoutes.TryGetValue(path, out var legacyPath))
        {
            await _next(context);
            return;
        }

        var baseUrl = _config["LegacyProxy:BaseUrl"] ?? "http://localhost:27201";
        var targetUrl = baseUrl + legacyPath;

        // Append query string
        if (context.Request.QueryString.HasValue)
        {
            targetUrl += context.Request.QueryString.Value;
        }

        _logger.LogInformation("LegacyProxy: Forwarding {Path} -> {Target}", path, targetUrl);

        try
        {
            var client = _httpClientFactory.CreateClient("LegacyProxy");
            var requestMessage = CreateProxyRequest(context, targetUrl);
            var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            await CopyProxyResponseAsync(context, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LegacyProxy: Error forwarding {Path} to {Target}", path, targetUrl);
            context.Response.StatusCode = 502;
            await context.Response.WriteAsync($"Legacy proxy error: {ex.Message}");
        }
    }

    private static HttpRequestMessage CreateProxyRequest(HttpContext context, string targetUrl)
    {
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(targetUrl),
            Method = new HttpMethod(context.Request.Method)
        };

        // Forward request body for POST/PUT
        if (context.Request.ContentLength > 0 || context.Request.ContentType != null)
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType != null)
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
        }

        // Forward selected headers (cookies, auth, etc.)
        foreach (var header in context.Request.Headers)
        {
            var key = header.Key;
            // Skip hop-by-hop and host headers
            if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Connection", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!request.Headers.TryAddWithoutValidation(key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(key, header.Value.ToArray());
            }
        }

        return request;
    }

    private static async Task CopyProxyResponseAsync(HttpContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;

        // Copy response headers
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        // Remove transfer-encoding to avoid conflicts
        context.Response.Headers.Remove("transfer-encoding");

        await using var stream = await response.Content.ReadAsStreamAsync();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
