using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("order-image-ops")]
public class OrderImageController : Controller
{
    private readonly OrderImageService _orderImageService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public OrderImageController(
        OrderImageService orderImageService,
        BakeryMenuService menuService,
        IConfiguration config,
        HttpClient httpClient)
    {
        _orderImageService = orderImageService;
        _menuService = menuService;
        _config = config;
        _httpClient = httpClient;
    }

    private async Task PopulateLayoutMetadataAsync()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        ViewBag.HdGlobalUrl = "http://localhost:5202";
        ViewBag.HdCustGlobalUrl = "http://localhost:5000";
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";
    }

    [HttpGet("updateorderimage")]
    [HttpGet("business-order-image")]
    public async Task<IActionResult> Index([FromQuery] long? orderID, [FromQuery] long? orderId)
    {
        long actualOrderId = orderID ?? orderId ?? 0;
        if (actualOrderId <= 0)
        {
            return Redirect("/businessorders");
        }

        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        }

        await PopulateLayoutMetadataAsync();

        var info = await _orderImageService.GetOrderImageInfoAsync(actualOrderId);
        if (info == null)
        {
            return Redirect("/businessorders");
        }

        return View(info);
    }

    [HttpGet("business-order-image/download")]
    public async Task<IActionResult> Download([FromQuery] string file, [FromQuery] int apitype)
    {
        if (string.IsNullOrEmpty(file))
        {
            return BadRequest("File is required");
        }

        string fileName = Path.GetFileName(file);
        if (fileName.Contains('?'))
        {
            fileName = fileName.Split('?')[0];
        }

        if (file.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var responseBytes = await _httpClient.GetByteArrayAsync(file);
                return File(responseBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return Content($"Error downloading file from external link: {ex.Message}");
            }
        }

        // Relative path
        string cleanFile = file.Replace("~/", "").Replace("/", "\\");
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanFile);

        if (!System.IO.File.Exists(fullPath))
        {
            // Try in resized_500_500 if not found in root
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "Product_images", "resized_500_500", fileName);
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("File not found locally");
            }
        }

        var fileStream = System.IO.File.OpenRead(fullPath);
        return File(fileStream, "application/octet-stream", fileName);
    }

    [HttpPost("uploadorderpicture.aspx")]
    [HttpPost("business-order-image/upload-order-picture")]
    public async Task<IActionResult> UploadOrderPicture()
    {
        var form = Request.Form;
        if (!long.TryParse(form["product_id"], out long orderId) || !int.TryParse(form["apitype"], out int apitype))
        {
            return BadRequest("Invalid product_id or apitype");
        }

        if (Request.Form.Files.Count == 0)
        {
            return BadRequest("No files uploaded");
        }

        var file = Request.Form.Files[0];
        if (file.Length == 0)
        {
            return BadRequest("Uploaded file is empty");
        }

        try
        {
            string filename = DateTime.Now.Ticks + "_" + orderId.ToString() + Path.GetExtension(file.FileName);
            string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            List<string> dirs = new List<string>
            {
                Path.Combine(wwwroot, "upload", "Product_images"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_135_135"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_200_200"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_300_300"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_500_500"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_80_80"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_800_800")
            };

            int[] width = { 1000, 135, 200, 300, 500, 80, 800 };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            string originalPath = Path.Combine(dirs[0], filename);
            using (var stream = new FileStream(originalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Generate thumbnails
            for (int i = 1; i < dirs.Count; i++)
            {
                string thumbPath = Path.Combine(dirs[i], filename);
                GenerateThumbnails(originalPath, thumbPath, width[i], width[i]);
            }

            // Save in database
            string customerWebsiteLogo = "/";
            string relativeDbPath = customerWebsiteLogo + "upload/Product_images/resized_500_500/" + filename;
            await _orderImageService.UpdateOrderDetailImageAsync(orderId, relativeDbPath);

            return Json(new { success = true, filename = filename });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("uploadcakepicture.aspx")]
    [HttpPost("business-order-image/upload-cake-picture")]
    public async Task<IActionResult> UploadCakePicture()
    {
        var form = Request.Form;
        if (!long.TryParse(form["product_id"], out long productId) || !int.TryParse(form["apitype"], out int apitype))
        {
            return BadRequest("Invalid product_id or apitype");
        }

        if (Request.Form.Files.Count == 0)
        {
            return BadRequest("No files uploaded");
        }

        var file = Request.Form.Files[0];
        if (file.Length == 0)
        {
            return BadRequest("Uploaded file is empty");
        }

        try
        {
            string filename = DateTime.Now.Ticks + "_" + productId.ToString() + Path.GetExtension(file.FileName);
            string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            List<string> dirs = new List<string>
            {
                Path.Combine(wwwroot, "upload", "Product_images"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_135_135"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_200_200"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_300_300"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_500_500"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_80_80"),
                Path.Combine(wwwroot, "upload", "Product_images", "resized_800_800"),
                Path.Combine(wwwroot, "upload", "Product_images", "fbImage")
            };

            int[] width = { 1000, 135, 200, 300, 500, 80, 800, 800 }; // 800x420 for fbImage

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            string originalPath = Path.Combine(dirs[0], filename);
            using (var stream = new FileStream(originalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Read resolution
            string imgRes = "0_0";
            try
            {
                using (var bitmap = new Bitmap(originalPath))
                {
                    imgRes = $"{bitmap.Width}_{bitmap.Height}";
                }
            }
            catch { }

            // Generate thumbnails
            for (int i = 1; i < dirs.Count - 1; i++)
            {
                string thumbPath = Path.Combine(dirs[i], filename);
                GenerateThumbnails(originalPath, thumbPath, width[i], width[i]);
            }

            // Fixed size for FB Image (last dir)
            string fbPath = Path.Combine(dirs[dirs.Count - 1], filename);
            GenerateThumbnailsFixedSize(originalPath, fbPath, 800, 420);

            // Save in database
            string customerWebsiteLogo = "/";
            await _orderImageService.UpdateProductAndOrderImageAsync(productId, filename, apitype, imgRes, customerWebsiteLogo);

            return Json(new { success = true, filename = filename });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static void GenerateThumbnails(string filename, string tbfilename, int newWidth, int newHeight)
    {
        using (Bitmap mybitmap = new Bitmap(filename))
        {
            double currentWidth = mybitmap.Width;
            double currentHeight = mybitmap.Height;

            double multiplier;
            if (currentHeight > currentWidth)
                multiplier = (double)newHeight / currentHeight;
            else
                multiplier = (double)newWidth / currentWidth;

            int finalWidth = Convert.ToInt32(currentWidth * multiplier);
            int finalHeight = Convert.ToInt32(currentHeight * multiplier);

            using (Bitmap tbimage = new Bitmap(finalWidth, finalHeight))
            {
                using (Graphics g = Graphics.FromImage(tbimage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(mybitmap, 0, 0, finalWidth, finalHeight);
                }
                tbimage.Save(tbfilename, ImageFormat.Jpeg);
            }
        }
    }

    private static void GenerateThumbnailsFixedSize(string filename, string tbfilename, int newWidth, int newHeight)
    {
        using (Bitmap mybitmap = new Bitmap(filename))
        {
            using (Bitmap tbimage = new Bitmap(newWidth, newHeight))
            {
                using (Graphics g = Graphics.FromImage(tbimage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(mybitmap, 0, 0, newWidth, newHeight);
                }
                tbimage.Save(tbfilename, ImageFormat.Jpeg);
            }
        }
    }
}
