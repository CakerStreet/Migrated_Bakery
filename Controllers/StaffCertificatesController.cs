using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("managestaffcerificates")]
public class StaffCertificatesController : Controller
{
    private readonly StaffCertificatesService _certificatesService;
    private readonly DailyChecklistService _checklistService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public StaffCertificatesController(
        StaffCertificatesService certificatesService,
        DailyChecklistService checklistService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _certificatesService = certificatesService;
        _checklistService = checklistService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? staffId = null, [FromQuery] string? msg = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        long wid = long.TryParse(webshopId, out var w) ? w : 82L;

        // Fetch staff list for dropdown
        var staffList = await _checklistService.GetBakeryUsersAsync(wid);

        // Fetch staff grouped list (search list)
        var groupedList = await _certificatesService.GetStaffCertificatesGroupedAsync();

        // If a staff member is selected, load their certificates and pad with 10 empty rows
        var uploadList = new List<StaffCertificateItem>();
        if (staffId.HasValue && staffId.Value > 0)
        {
            var existing = await _certificatesService.GetStaffCertificatesByStaffIdAsync(staffId.Value);
            uploadList.AddRange(existing);

            // Pad with 10 empty rows to match legacy behaviour
            for (int i = 0; i < 10; i++)
            {
                uploadList.Add(new StaffCertificateItem
                {
                    Id = 0,
                    Name = "",
                    Filename = ""
                });
            }
        }

        // Set layout and custom view bags
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        ViewBag.StaffList = staffList;
        ViewBag.GroupedList = groupedList;
        ViewBag.UploadList = uploadList;
        ViewBag.SelectedStaffId = staffId ?? 0;
        ViewBag.SuccessMessage = msg;

        return View("~/Views/StaffCertificates/Index.cshtml");
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        [FromForm] long staffId,
        [FromForm] List<long> ids,
        [FromForm] List<string> titles,
        [FromForm] List<string> oldFiles)
    {
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "System";

        if (staffId <= 0 || ids == null || ids.Count == 0)
        {
            return Redirect("/managestaffcerificates");
        }

        string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "staffcertificates");

        for (int i = 0; i < ids.Count; i++)
        {
            long certId = ids[i];
            string title = titles.Count > i ? titles[i] : "";
            string oldFile = oldFiles.Count > i ? oldFiles[i] : "";
            var fileUpload = Request.Form.Files[$"file_{i}"];

            bool hasNewFile = fileUpload != null && fileUpload.Length > 0;
            bool hasOldFile = !string.IsNullOrEmpty(oldFile);

            if (hasNewFile || hasOldFile)
            {
                string filename = oldFile;

                if (hasNewFile)
                {
                    // Ensure upload folder exists
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    // Delete old file if present
                    if (!string.IsNullOrEmpty(oldFile))
                    {
                        string oldPath = Path.Combine(uploadDir, oldFile);
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    string safeTitle = string.IsNullOrWhiteSpace(title) ? fileUpload.FileName : title;
                    string slug = Regex.Replace(safeTitle.ToLower(), @"[^a-z0-9_.-]", "-");
                    filename = $"{slug}-{DateTime.Now.Ticks}{Path.GetExtension(fileUpload.FileName)}";

                    string filePath = Path.Combine(uploadDir, filename);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await fileUpload.CopyToAsync(stream);
                    }
                }

                if (string.IsNullOrEmpty(title))
                {
                    title = hasNewFile ? fileUpload!.FileName : filename;
                }

                await _certificatesService.SaveStaffCertificateAsync(certId, staffId, title, filename, userName);
            }
        }

        return Redirect($"/managestaffcerificates?staffId={staffId}&msg={Uri.EscapeDataString("Staff certificates saved successfully.")}");
    }

    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(long id, [FromQuery] long? staffId = null)
    {
        var cert = await _certificatesService.GetCertificateByIdAsync(id);
        if (cert != null)
        {
            if (!string.IsNullOrEmpty(cert.File))
            {
                string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "staffcertificates");
                string filePath = Path.Combine(uploadDir, cert.File);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            await _certificatesService.DeleteCertificateAsync(id);
        }

        string redir = "/managestaffcerificates?msg=" + Uri.EscapeDataString("Staff certificate deleted successfully.");
        if (staffId.HasValue && staffId.Value > 0)
        {
            redir += $"&staffId={staffId.Value}";
        }

        return Redirect(redir);
    }
}
