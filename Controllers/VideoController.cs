using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("managevideos")]
public class VideoController : Controller
{
    private readonly VideoService _videoService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public VideoController(
        VideoService videoService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _videoService = videoService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] long? id = null,
        [FromQuery] long? editId = null,
        [FromQuery] string? msg = null,
        [FromQuery] string? errorMsg = null)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopIdStr) || !long.TryParse(webshopIdStr, out var webshopId))
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        if (!id.HasValue || id.Value <= 0)
        {
            return Redirect("/mywebstore");
        }

        var product = await _videoService.GetProductDetailsAsync(id.Value, webshopId);
        if (product == null)
        {
            return Redirect("/mywebstore");
        }

        var videos = await _videoService.GetVideosByProductIdAsync(id.Value);

        PrdVideoItem? editVideo = null;
        if (editId.HasValue && editId.Value > 0)
        {
            editVideo = await _videoService.GetVideoByIdAsync(editId.Value);
        }

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopIdStr, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        ViewBag.Product = product;
        ViewBag.Videos = videos;
        ViewBag.EditVideo = editVideo;
        ViewBag.SuccessMessage = msg;
        ViewBag.ErrorMessage = errorMsg;

        return View("~/Views/Video/Index.cshtml");
    }

    [HttpPost("savedesc")]
    public async Task<IActionResult> SaveDesc([FromForm] long id, [FromForm] string description)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        await _videoService.SaveProductDescriptionAsync(id, description);
        return Redirect($"/managevideos?id={id}&msg={Uri.EscapeDataString("Description updated successfully.")}");
    }

    [HttpPost("savevideo")]
    public async Task<IActionResult> SaveVideo(
        [FromForm] long id, // Product ID
        [FromForm] long videoId, // Video ID (0 for new, >0 for edit)
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string? hfThumb,
        [FromForm] string? hfVideo,
        IFormFile? fupThumb,
        IFormFile? fupVideo)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        if (string.IsNullOrWhiteSpace(title))
        {
            return Redirect($"/managevideos?id={id}&errorMsg={Uri.EscapeDataString("Title is required.")}");
        }

        // New video requires files
        if (videoId == 0)
        {
            if (fupThumb == null || fupThumb.Length == 0)
            {
                return Redirect($"/managevideos?id={id}&errorMsg={Uri.EscapeDataString("Thumbnail is required for new videos.")}");
            }
            if (fupVideo == null || fupVideo.Length == 0)
            {
                return Redirect($"/managevideos?id={id}&errorMsg={Uri.EscapeDataString("Video file is required for new videos.")}");
            }
        }

        string thumbFile = hfThumb ?? "";
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "videos");
        var photoDir = Path.Combine(baseDir, "photo");
        var thumbDir = Path.Combine(baseDir, "photothumb");

        if (fupThumb != null && fupThumb.Length > 0)
        {
            Directory.CreateDirectory(photoDir);
            Directory.CreateDirectory(thumbDir);

            string fileName = DateTime.UtcNow.ToFileTime().ToString() + Path.GetExtension(fupThumb.FileName);
            string photoPath = Path.Combine(photoDir, fileName);
            string thumbPath = Path.Combine(thumbDir, fileName);

            using (var stream = new FileStream(photoPath, FileMode.Create))
            {
                await fupThumb.CopyToAsync(stream);
            }

            try
            {
                VideoService.GenerateThumbnails(photoPath, thumbPath, 100, 100);
            }
            catch
            {
                // Fallback in case of GDI/System.Drawing issues
                System.IO.File.Copy(photoPath, thumbPath, true);
            }

            // Cleanup old thumb files if editing
            if (!string.IsNullOrEmpty(hfThumb))
            {
                try
                {
                    var oldPhoto = Path.Combine(photoDir, hfThumb);
                    var oldThumb = Path.Combine(thumbDir, hfThumb);
                    if (System.IO.File.Exists(oldPhoto)) System.IO.File.Delete(oldPhoto);
                    if (System.IO.File.Exists(oldThumb)) System.IO.File.Delete(oldThumb);
                }
                catch { }
            }

            thumbFile = fileName;
        }

        string videoFile = hfVideo ?? "";
        if (fupVideo != null && fupVideo.Length > 0)
        {
            Directory.CreateDirectory(baseDir);

            string fileName = DateTime.UtcNow.ToFileTime().ToString() + Path.GetExtension(fupVideo.FileName);
            string videoPath = Path.Combine(baseDir, fileName);

            using (var stream = new FileStream(videoPath, FileMode.Create))
            {
                await fupVideo.CopyToAsync(stream);
            }

            // Cleanup old video file if editing
            if (!string.IsNullOrEmpty(hfVideo))
            {
                try
                {
                    var oldVideo = Path.Combine(baseDir, hfVideo);
                    if (System.IO.File.Exists(oldVideo)) System.IO.File.Delete(oldVideo);
                }
                catch { }
            }

            videoFile = fileName;
        }

        var videoItem = new PrdVideoItem
        {
            VideoId = videoId,
            ProductId = id,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            VideoThumb = thumbFile,
            Video = videoFile
        };

        await _videoService.SaveVideoAsync(videoItem);

        string successMsg = videoId > 0 ? "Video updated successfully." : "Video added successfully.";
        return Redirect($"/managevideos?id={id}&msg={Uri.EscapeDataString(successMsg)}");
    }

    [HttpPost("updateorders")]
    public async Task<IActionResult> UpdateOrders([FromForm] long id, IFormCollection form)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        foreach (var key in form.Keys)
        {
            if (key.StartsWith("displayOrder_"))
            {
                var vidIdStr = key.Substring("displayOrder_".Length);
                if (long.TryParse(vidIdStr, out var vidId))
                {
                    var orderVal = form[key].ToString();
                    if (int.TryParse(orderVal, out var order))
                    {
                        await _videoService.UpdateDisplayOrderAsync(vidId, order);
                    }
                }
            }
        }

        return Redirect($"/managevideos?id={id}&msg={Uri.EscapeDataString("Display orders updated successfully.")}");
    }

    [HttpGet("delete/{id}")]
    public async Task<IActionResult> Delete(long id, [FromQuery] long prdId)
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopIdStr))
            return Redirect("/businesslogin");

        var video = await _videoService.GetVideoByIdAsync(id);
        if (video != null)
        {
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "videos");
            try
            {
                if (!string.IsNullOrEmpty(video.VideoThumb))
                {
                    var photoPath = Path.Combine(baseDir, "photo", video.VideoThumb);
                    var thumbPath = Path.Combine(baseDir, "photothumb", video.VideoThumb);
                    if (System.IO.File.Exists(photoPath)) System.IO.File.Delete(photoPath);
                    if (System.IO.File.Exists(thumbPath)) System.IO.File.Delete(thumbPath);
                }
                if (!string.IsNullOrEmpty(video.Video))
                {
                    var videoPath = Path.Combine(baseDir, video.Video);
                    if (System.IO.File.Exists(videoPath)) System.IO.File.Delete(videoPath);
                }
            }
            catch { }

            await _videoService.DeleteVideoAsync(id);
        }

        return Redirect($"/managevideos?id={prdId}&msg={Uri.EscapeDataString("Video deleted successfully.")}");
    }
}
