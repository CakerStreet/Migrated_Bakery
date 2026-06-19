using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("personalised-cake-ops")]
public class PersonalisedCakeController : Controller
{
    private readonly PersonalisedCakeService _personalisedCakeService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public PersonalisedCakeController(
        PersonalisedCakeService personalisedCakeService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _personalisedCakeService = personalisedCakeService;
        _menuService = menuService;
        _config = config;
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
    }

    [HttpGet("createsvgforpersonalisedcake")]
    public async Task<IActionResult> CreateSvg([FromQuery] long? pid)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        }

        await PopulateLayoutMetadataAsync();

        ViewBag.PrdID = pid ?? 0;
        ViewBag.IsPrdSvgAvailable = "0";
        ViewBag.SvgText = "";
        ViewBag.TemplateBox = "cupcake.png";
        ViewBag.TemplatePic = "camera.jpg";

        if (pid > 0)
        {
            string msgText = await _personalisedCakeService.GetCustomPrdMsgTextAsync(pid.Value);
            if (!string.IsNullOrEmpty(msgText))
            {
                ViewBag.SvgText = msgText;
                ViewBag.IsPrdSvgAvailable = "1";
            }
        }

        return View();
    }

    [HttpPost("createsvgforpersonalisedcake")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSvg(
        [FromForm] long pid,
        [FromForm] string hfTemplateBox,
        [FromForm] string hfTemplatePic,
        [FromForm] string hfIsPrdSvgAvailable,
        [FromForm] string uploadType,
        IFormFile? uploadFile)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Redirect($"/businesslogin");
        }

        await PopulateLayoutMetadataAsync();

        ViewBag.PrdID = pid;
        ViewBag.IsPrdSvgAvailable = hfIsPrdSvgAvailable;
        ViewBag.SvgText = "";
        ViewBag.TemplateBox = string.IsNullOrEmpty(hfTemplateBox) ? "cupcake.png" : hfTemplateBox;
        ViewBag.TemplatePic = string.IsNullOrEmpty(hfTemplatePic) ? "camera.jpg" : hfTemplatePic;

        if (uploadFile != null && uploadFile.Length > 0)
        {
            try
            {
                string filename = DateTime.Now.Ticks + Path.GetExtension(uploadFile.FileName);
                string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string savePath = Path.Combine(wwwroot, "upload", "personalised");

                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                using (var stream = new FileStream(Path.Combine(savePath, filename), FileMode.Create))
                {
                    await uploadFile.CopyToAsync(stream);
                }

                if (uploadType == "1") // Template Box
                {
                    ViewBag.TemplateBox = filename;
                }
                else // Template Pic
                {
                    ViewBag.TemplatePic = filename;
                }
            }
            catch (Exception ex)
            {
                ViewBag.UploadError = ex.Message;
            }
        }

        return View();
    }

    [HttpGet("createimagefrombyte")]
    public async Task<IActionResult> CreateImageFromByte()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        }

        await PopulateLayoutMetadataAsync();

        ViewBag.ByteString = "";
        ViewBag.ImageUrl = "";
        ViewBag.ErrorMessage = null;

        return View();
    }

    [HttpPost("createimagefrombyte")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateImageFromByte([FromForm] string txtByte)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Redirect($"/businesslogin");
        }

        await PopulateLayoutMetadataAsync();

        ViewBag.ByteString = txtByte;
        ViewBag.ImageUrl = "";
        ViewBag.ErrorMessage = null;

        if (!string.IsNullOrEmpty(txtByte))
        {
            try
            {
                string base64Data = txtByte.Replace("data:image/png;base64,", "")
                                           .Replace("data:img/png;base64,", "")
                                           .Replace("data:image/jpeg;base64,", "")
                                           .Replace("data:image/jpg;base64,", "");

                byte[] byteImages = Convert.FromBase64String(base64Data);

                string filename = Guid.NewGuid().ToString().Substring(0, 10).Replace("-", "") + "-cake.jpg";
                string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string savePath = Path.Combine(wwwroot, "upload", "caketemplates");

                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                string fullPath = Path.Combine(savePath, filename);
                await System.IO.File.WriteAllBytesAsync(fullPath, byteImages);

                string siteUrl = "/";
                ViewBag.ImageUrl = $"{siteUrl}upload/caketemplates/{filename}";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error converting image: {ex.Message}";
            }
        }

        return View();
    }
}
