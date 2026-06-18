using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class ServiceWebservicesResponse
{
    public int data_ID { get; set; }
    public string data_str { get; set; } = "";
    public string data_optionalstr { get; set; } = "";
}

public class GetCategoriesRequest
{
    public int catId { get; set; }
    public int intlevel { get; set; }
}

public class AddUpdateServiceRequest
{
    public ServiceModel prdList { get; set; } = new();
    public List<ImageModel> prdImage { get; set; } = new();
}

public class ServiceModel
{
    public long product_ID { get; set; }
    public int product_catID { get; set; }
    public string product_Name { get; set; } = "";
    public string product_desc { get; set; } = "";
    public decimal product_startingtPrice { get; set; }
    public decimal product_marketPrice { get; set; }
    public bool product_isActive { get; set; }
    public int shapeid { get; set; } // Recurring=1, OneTime=2
    public int typeid { get; set; } // Frequency 1=Weekly, 2=Monthly, etc.
}

public class ImageModel
{
    public string productImage_imagename { get; set; } = "";
    public bool productImage_isnew { get; set; }
    public bool productImage_isdefaultimage { get; set; }
}

public class ServiceBulkActionRequest
{
    public string Action { get; set; } = "";
    public List<long> SelectedIds { get; set; } = new();
}

public class ServiceSingleActionRequest
{
    public long ServiceID { get; set; }
}

public class ServiceController : Controller
{
    private readonly ServiceService _service;
    private readonly IWebHostEnvironment _env;

    public ServiceController(ServiceService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    // ─── List View (GET) ───────────────────────────────────────────────────────

    [HttpGet("manageservices")]
    [HttpGet("manageservice")]
    [HttpGet("manageservice.aspx")]
    [HttpGet("manageservices/page-{pageno}")]
    public async Task<IActionResult> Index(
        [FromRoute] int pageno = 1,
        [FromQuery] string search = "",
        [FromQuery] int filterstatus = 0)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        int pageSize = 23;
        var cleanSearch = (search ?? "").Replace("+", " ");

        var result = await _service.GetServicesAsync(filterstatus, cleanSearch, pageno, pageSize);

        ViewBag.Result = result;
        ViewBag.FilterStatus = filterstatus;
        ViewBag.SearchKeyword = cleanSearch;
        ViewBag.CurrentPage = pageno;

        return View("~/Views/Service/Index.cshtml");
    }

    // ─── Bulk Action (POST) ────────────────────────────────────────────────────

    [HttpPost("manageservices/bulkaction")]
    public async Task<IActionResult> BulkAction([FromForm] string actionType, [FromForm] List<long> selectedIds)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        if (selectedIds != null && selectedIds.Count > 0)
        {
            if (actionType == "active")
            {
                await _service.UpdateActiveStatusAsync(selectedIds, true);
            }
            else if (actionType == "inactive")
            {
                await _service.UpdateActiveStatusAsync(selectedIds, false);
            }
            else if (actionType == "delete")
            {
                await _service.BulkDeleteAsync(selectedIds);
            }
        }

        return Redirect("/manageservices");
    }

    // ─── Single Item Remove Action (POST) ──────────────────────────────────────

    [HttpPost("manageservices/remove")]
    public async Task<IActionResult> Remove([FromForm] long serviceID)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        await _service.DeleteServiceAsync(serviceID);
        return Redirect("/manageservices");
    }

    // ─── Add/Edit Item View (GET) ──────────────────────────────────────────────

    [HttpGet("addnewservice")]
    [HttpGet("editservice")]
    public async Task<IActionResult> AddNewService([FromQuery] long? serviceID)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        // Parent categories list
        var parentCategories = await _service.GetCategoriesAsync(0);
        ViewBag.ParentCategories = parentCategories;
        ViewBag.SubCategories = new List<ServiceCategoryItem>();
        ViewBag.ServiceID = 0;
        ViewBag.SelectedParentCatId = -1;
        ViewBag.SelectedSubCatId = -1;

        ServiceItem? model = null;
        List<ServiceImageItem> images = new();

        if (serviceID.HasValue && serviceID.Value > 0)
        {
            model = await _service.GetServiceByIdAsync(serviceID.Value);
            if (model != null)
            {
                ViewBag.ServiceID = model.ServiceId;
                images = await _service.GetServiceImagesAsync(model.ServiceId);

                // Fetch subcategories
                // Check if current cat belongs to parent
                // Need to find category's parent
                // We'll run a quick inline query or let the service do it
                // To do this simply, we will search parent category by checking the tbl_category table for CategoryId
                // Let's query category by ID to find the parent. Let's create a helper method or query directly.
                // We will load all categories for service and search
                var currentCatId = model.ServiceCatId;
                // Query database for this category's refCategoryID
                int parentCatId = -1;
                using (var conn = new SqlConnection(HttpContext.RequestServices.GetService<IConfiguration>().GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();
                    var sql = "SELECT catgory_refCategoryID FROM tbl_category WHERE category_ID = @cid AND category_for = 3";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cid", currentCatId);
                    var val = await cmd.ExecuteScalarAsync();
                    if (val != null && val != DBNull.Value)
                    {
                        parentCatId = Convert.ToInt32(val);
                    }
                }

                if (parentCatId != -1)
                {
                    ViewBag.SelectedParentCatId = parentCatId;
                    ViewBag.SelectedSubCatId = currentCatId;
                    ViewBag.SubCategories = await _service.GetCategoriesAsync(parentCatId);
                }
            }
        }

        ViewBag.Images = images;
        return View("~/Views/Service/AddNewService.cshtml", model);
    }

    // ─── Webservice Cascading Category Endpoint (POST) ─────────────────────────

    [HttpPost("webservices.aspx/getServiceCategoriesbyCatID")]
    public async Task<IActionResult> GetServiceCategoriesbyCatID([FromBody] GetCategoriesRequest request)
    {
        var categories = await _service.GetCategoriesAsync(request.catId);
        var sb = new System.Text.StringBuilder();
        foreach (var cat in categories)
        {
            sb.Append($"<option value='{cat.CategoryId}'>{cat.CategoryName}</option>");
        }

        var response = new ServiceWebservicesResponse();
        if (categories.Count > 0)
        {
            response.data_ID = 1;
            response.data_str = $"<select id='ddlSubCat' data-tid='{request.intlevel + 1}' class='form-control form-inline'><option value='-1'>--Select Category--</option>{sb}</select>";
        }
        else
        {
            response.data_ID = 0;
            response.data_str = "";
        }

        return Json(new { d = response });
    }

    // ─── AJAX Image Upload Endpoint (POST) ─────────────────────────────────────

    [HttpPost("upload/FileUploadHandler.ashx")]
    public async Task<IActionResult> FileUploadHandler()
    {
        if (Request.Form.Files.Count == 0)
        {
            return Json(new { error = "No file uploaded.", upfile = "" });
        }

        var file = Request.Form.Files[0];
        string ext = Path.GetExtension(file.FileName).ToLower();
        string tempFileName = Guid.NewGuid().ToString().Replace("-", "") + ext;

        string tempPath = Path.Combine(_env.WebRootPath, "upload", "temp");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        string originalFilePath = Path.Combine(tempPath, tempFileName);
        using (var stream = new FileStream(originalFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Json(new { error = "", upfile = tempFileName });
    }

    // ─── Webservice Save Service Endpoint (POST) ───────────────────────────────

    [HttpPost("webservices.aspx/AddUpdateService")]
    public async Task<IActionResult> AddUpdateService([FromBody] AddUpdateServiceRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
        {
            return Json(new { d = new ServiceWebservicesResponse { data_ID = 0, data_optionalstr = "Unauthorized session. Please login." } });
        }

        var service = new ServiceItem
        {
            ServiceId = request.prdList.product_ID,
            ServiceCatId = request.prdList.product_catID,
            Name = request.prdList.product_Name,
            Desc = request.prdList.product_desc,
            WsPrice = request.prdList.product_startingtPrice,
            MarketPrice = request.prdList.product_marketPrice,
            IsRecommend = request.prdList.product_isActive,
            RecurringOrOnline = request.prdList.shapeid,
            RecurringModeVal = request.prdList.typeid,
            IsActive = true
        };

        var images = new List<ServiceImageItem>();
        string tempDir = Path.Combine(_env.WebRootPath, "upload", "temp");
        string serviceImagesDir = Path.Combine(_env.WebRootPath, "upload", "service_images");

        if (!Directory.Exists(serviceImagesDir))
            Directory.CreateDirectory(serviceImagesDir);

        var sizes = new[] { "800_800", "500_500", "300_300", "80_80", "200_200", "135_135" };
        foreach (var sz in sizes)
        {
            string szPath = Path.Combine(serviceImagesDir, "resized_" + sz);
            if (!Directory.Exists(szPath))
                Directory.CreateDirectory(szPath);
        }
        string fbPath = Path.Combine(serviceImagesDir, "fbImage");
        if (!Directory.Exists(fbPath))
            Directory.CreateDirectory(fbPath);

        for (int i = 0; i < request.prdImage.Count; i++)
        {
            var img = request.prdImage[i];
            string finalImgName = img.productImage_imagename;

            if (img.productImage_isnew)
            {
                // Move file from temp to final directory and create copies
                string tempFilePath = Path.Combine(tempDir, img.productImage_imagename);
                if (System.IO.File.Exists(tempFilePath))
                {
                    // Generate slugged clean filename matching legacy format
                    string cleanSlug = Common_CleanSlug(request.prdList.product_Name);
                    string ext = Path.GetExtension(img.productImage_imagename).ToUpper();
                    finalImgName = cleanSlug + "-" + Guid.NewGuid().ToString().Replace("-", "").Substring(0, 9) + ext;

                    string finalPath = Path.Combine(serviceImagesDir, finalImgName);
                    System.IO.File.Copy(tempFilePath, finalPath, true);

                    // Create mock resized files
                    foreach (var sz in sizes)
                    {
                        System.IO.File.Copy(tempFilePath, Path.Combine(serviceImagesDir, "resized_" + sz, finalImgName), true);
                    }
                    System.IO.File.Copy(tempFilePath, Path.Combine(fbPath, finalImgName), true);

                    // Delete temp file
                    try { System.IO.File.Delete(tempFilePath); } catch { }
                }
            }

            images.Add(new ServiceImageItem
            {
                ImageName = finalImgName,
                IsDefaultImage = img.productImage_isdefaultimage,
                ImgNo = i + 1,
                ImageType = 1
            });
        }

        try
        {
            var id = await _service.AddUpdateServiceAsync(service, images, userId);
            return Json(new { d = new ServiceWebservicesResponse { data_ID = 1, data_optionalstr = "1" } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new ServiceWebservicesResponse { data_ID = 0, data_optionalstr = ex.Message } });
        }
    }

    private string Common_CleanSlug(string name)
    {
        if (string.IsNullOrEmpty(name)) return "service";
        string phrase = name.ToLower();
        phrase = System.Text.RegularExpressions.Regex.Replace(phrase, @"[^a-z0-9\s-]", "");
        phrase = System.Text.RegularExpressions.Regex.Replace(phrase, @"\s+", " ").Trim();
        phrase = phrase.Substring(0, phrase.Length <= 45 ? phrase.Length : 45).Trim();
        phrase = System.Text.RegularExpressions.Regex.Replace(phrase, @"\s", "-");
        return phrase;
    }
}
