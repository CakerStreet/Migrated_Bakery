using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for converting Base64 image byte strings to image files.
/// Route: /createimagefrombyte
/// Migrated from createImagefrombyte.aspx.
/// Accepts a base64-encoded image string, saves it as a JPG file, and returns the URL.
/// Access: authenticated bakery users.
/// </summary>
[Route("createimagefrombyte")]
[Route("createImagefrombyte.aspx")]
public class CreateImageFromByteController : Controller
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public CreateImageFromByteController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    /// <summary>
    /// Display the form to enter base64 string.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        return View("~/Views/CreateImageFromByte/Index.cshtml");
    }

    /// <summary>
    /// Convert base64 to image and return the URL.
    /// </summary>
    [HttpPost("")]
    public IActionResult Convert([FromForm] string txtByte)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        string resultUrl = "";

        if (!string.IsNullOrEmpty(txtByte))
        {
            try
            {
                var filename = Guid.NewGuid().ToString().Substring(0, 10).Replace("-", "") + "-cake.jpg";
                var savePath = Path.Combine(_env.WebRootPath, "uploads", "caketemplates");

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                // Strip base64 prefix
                var base64Data = txtByte
                    .Replace("data:image/png;base64,", "")
                    .Replace("data:img/png;base64,", "")
                    .Replace("data:image/jpeg;base64,", "");

                var bytes = System.Convert.FromBase64String(base64Data);
                var filePath = Path.Combine(savePath, filename);
                System.IO.File.WriteAllBytes(filePath, bytes);

                var cdnBase = _config["CdnBase"] ?? "";
                resultUrl = $"{cdnBase}upload/caketemplates/{filename}";
            }
            catch { }
        }

        ViewBag.ResultUrl = resultUrl;
        return View("~/Views/CreateImageFromByte/Index.cshtml");
    }
}
