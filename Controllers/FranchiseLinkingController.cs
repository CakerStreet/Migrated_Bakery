using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class FranchiseLinkingController : Controller
{
    private readonly FranchiseLinkingService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public FranchiseLinkingController(
        FranchiseLinkingService service,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _service = service;
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

    private int GetCurrentUserId()
    {
        return HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
    }

    private long GetWebshopId()
    {
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        long.TryParse(webshopIdStr, out var webshopId);
        return webshopId;
    }

    [HttpGet("linkproductwithfranchise")]
    [HttpGet("linkproductwithfranchise.aspx")]
    public async Task<IActionResult> Index(
        [FromQuery] long? franchiseid, 
        [FromQuery] long? sectionid, 
        [FromQuery] long? catid, 
        [FromQuery] int filter = 0,
        [FromQuery] string? msg = null,
        [FromQuery] string? error = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        await PopulateLayoutMetadataAsync();

        var franchises = await _service.GetFranchisesAsync();
        var sections = new List<FranchiseCategoryItem>();
        var categories = new List<FranchiseCategoryItem>();
        var linkedDetails = new List<ProductFranchiseLinkingDetail>();
        var productSuppliers = new Dictionary<long, List<FranchiseSupplierItem>>();
        var counts = (allCount: 0, pendingCount: 0, underDeliveryCount: 0, deliveredCount: 0);

        if (franchiseid.HasValue && franchiseid.Value > 0)
        {
            sections = await _service.GetSectionsAsync();
        }

        if (sectionid.HasValue && sectionid.Value > 0)
        {
            categories = await _service.GetCategoriesAsync(sectionid.Value);
        }

        FranchiseCategoryItem? selectedCat = null;
        if (catid.HasValue && catid.Value > 0)
        {
            selectedCat = await _service.GetCategoryDetailsAsync(catid.Value);
            if (selectedCat != null && franchiseid.HasValue)
            {
                linkedDetails = await _service.GetProductFranchiseLinkingAsync(catid.Value, franchiseid.Value, filter);
                counts = await _service.GetFranchiseLinkingCountsAsync(catid.Value, franchiseid.Value);

                var productIds = string.Join(",", linkedDetails.Where(d => !d.IsService).Select(d => d.ProductId));
                var suppliersList = await _service.GetProductSuppliersAsync(productIds, GetWebshopId());
                foreach (var item in suppliersList)
                {
                    if (!productSuppliers.ContainsKey(item.ProductId))
                    {
                        productSuppliers[item.ProductId] = new List<FranchiseSupplierItem>();
                    }
                    productSuppliers[item.ProductId].Add(item);
                }
            }
        }

        ViewBag.Franchises = franchises;
        ViewBag.Sections = sections;
        ViewBag.Categories = categories;
        ViewBag.SelectedFranchiseId = franchiseid ?? 0;
        ViewBag.SelectedSectionId = sectionid ?? 0;
        ViewBag.SelectedCategoryId = catid ?? 0;
        ViewBag.SelectedCat = selectedCat;
        ViewBag.Filter = filter;
        ViewBag.LinkedDetails = linkedDetails;
        ViewBag.ProductSuppliers = productSuppliers;
        ViewBag.Counts = counts;
        ViewBag.SuccessMessage = msg;
        ViewBag.ErrorMessage = error;

        // For selection popups
        ViewBag.ServiceCategories = await _service.GetServiceCategoryDropdownAsync();

        return View("~/Views/FranchiseLinking/Index.cshtml");
    }

    [HttpPost("linkproductwithfranchise/savefranchise")]
    public async Task<IActionResult> SaveFranchise([FromForm] string title, [FromForm] int status)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        if (string.IsNullOrEmpty(title))
        {
            return Redirect("/linkproductwithfranchise?error=" + Uri.EscapeDataString("Franchise title is required."));
        }

        var newId = await _service.SaveFranchiseAsync(title, status);
        if (newId == -1)
        {
            return Redirect("/linkproductwithfranchise?error=" + Uri.EscapeDataString("This franchise already exists."));
        }

        return Redirect($"/linkproductwithfranchise?franchiseid={newId}&msg=" + Uri.EscapeDataString("Franchise added successfully."));
    }

    [HttpPost("linkproductwithfranchise/linkproduct")]
    public async Task<IActionResult> LinkProduct(
        [FromForm] long franchiseid, 
        [FromForm] long catid, 
        [FromForm] long productId, 
        [FromForm] int isService,
        [FromForm] long sectionid)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        var success = await _service.LinkProductOrServiceAsync(franchiseid, catid, productId, isService);
        string retUrl = $"/linkproductwithfranchise?franchiseid={franchiseid}&sectionid={sectionid}&catid={catid}";
        if (success)
        {
            return Redirect(retUrl + "&msg=" + Uri.EscapeDataString(isService == 1 ? "Service linked successfully." : "Product linked successfully."));
        }
        else
        {
            return Redirect(retUrl + "&error=" + Uri.EscapeDataString("This product/service is already linked."));
        }
    }

    [HttpPost("linkproductwithfranchise/linkselectedservices")]
    public async Task<IActionResult> LinkSelectedServices(
        [FromForm] long franchiseid, 
        [FromForm] long sectionid, 
        [FromForm] long catid, 
        [FromForm] List<long> selectedServices)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        int linkedCount = 0;
        foreach (var srvId in selectedServices)
        {
            var success = await _service.LinkProductOrServiceAsync(franchiseid, catid, srvId, 1);
            if (success) linkedCount++;
        }

        string retUrl = $"/linkproductwithfranchise?franchiseid={franchiseid}&sectionid={sectionid}&catid={catid}";
        return Redirect(retUrl + $"&msg={linkedCount} service(s) linked successfully.");
    }

    [HttpPost("linkproductwithfranchise/linkselectedproducts")]
    public async Task<IActionResult> LinkSelectedProducts(
        [FromForm] long franchiseid, 
        [FromForm] long sectionid, 
        [FromForm] long catid, 
        [FromForm] List<long> selectedProducts)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        int linkedCount = 0;
        foreach (var prdId in selectedProducts)
        {
            var success = await _service.LinkProductOrServiceAsync(franchiseid, catid, prdId, 0);
            if (success) linkedCount++;
        }

        string retUrl = $"/linkproductwithfranchise?franchiseid={franchiseid}&sectionid={sectionid}&catid={catid}";
        return Redirect(retUrl + $"&msg={linkedCount} product(s) linked successfully.");
    }

    // Ajax routes matching legacy WebMethods

    [HttpPost("/webservices.aspx/UpdatelnkItemtempfranchise")]
    public async Task<IActionResult> UpdateLinkedItem([FromBody] System.Text.Json.JsonElement requestBody)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Json(new { d = 0 });

        try
        {
            var ep = requestBody.GetProperty("ep");
            long id = ep.GetProperty("ID").GetInt64();
            int supplierId = ep.GetProperty("SupplierId").GetInt32();
            decimal price = ep.GetProperty("Price").GetDecimal();
            int minStockReq = ep.GetProperty("Min_StockReq").GetInt32();
            decimal totalInvestment = ep.GetProperty("Total_Investment").GetDecimal();
            int ordered = ep.GetProperty("Ordered").GetInt32();
            
            DateTime? orderDate = null;
            if (ep.TryGetProperty("Order_Date", out var odProp) && !string.IsNullOrEmpty(odProp.GetString()))
            {
                if (DateTime.TryParse(odProp.GetString(), out var od)) orderDate = od;
            }

            int delivered = ep.GetProperty("Delivered").GetInt32();

            DateTime? deliveryDate = null;
            if (ep.TryGetProperty("Delivery_Date", out var ddProp) && !string.IsNullOrEmpty(ddProp.GetString()))
            {
                if (DateTime.TryParse(ddProp.GetString(), out var dd)) deliveryDate = dd;
            }

            string deliveryReceivedBy = ep.TryGetProperty("Delivery_ReceivedBy", out var drb) ? drb.GetString() ?? "" : "";
            string altSupplierName = ep.TryGetProperty("Alternate_SupplierName", out var asn) ? asn.GetString() ?? "" : "";
            string altSupplierRemarks = ep.TryGetProperty("Alternate_SupplierRemarks", out var asr) ? asr.GetString() ?? "" : "";

            var success = await _service.UpdateLinkedItemAsync(
                id, supplierId, price, minStockReq, totalInvestment,
                ordered, orderDate, delivered, deliveryDate, deliveryReceivedBy,
                altSupplierName, altSupplierRemarks);

            return Json(new { d = success ? 1 : 0 });
        }
        catch (Exception)
        {
            return Json(new { d = 0 });
        }
    }

    [HttpPost("/webservices.aspx/getsuppliercostdetail")]
    public async Task<IActionResult> GetSupplierCostDetail([FromBody] System.Text.Json.JsonElement requestBody)
    {
        try
        {
            int sid = requestBody.GetProperty("sid").GetInt32();
            long pid = requestBody.GetProperty("pid").GetInt64();

            var detail = await _service.GetSupplierCostDetailAsync(sid, pid);
            if (detail == null)
            {
                return Json(new { d = new { Cost = 0, Min_Qty = 0, Total_Investment = 0, Remarks = "No Supplier Found" } });
            }

            return Json(new { d = new { 
                Cost = detail.Cost, 
                Min_Qty = detail.MinQty, 
                Total_Investment = detail.TotalInvestment, 
                Remarks = detail.Remarks,
                SupplierName = detail.SupplierName
            } });
        }
        catch (Exception)
        {
            return Json(new { d = new { Cost = 0, Min_Qty = 0, Total_Investment = 0, Remarks = "Error" } });
        }
    }

    [HttpPost("/webservices.aspx/unlinkfranchiseitem")]
    public async Task<IActionResult> UnlinkFranchiseItem([FromBody] System.Text.Json.JsonElement requestBody)
    {
        try
        {
            long fid = requestBody.GetProperty("fid").GetInt64();
            long catid = requestBody.GetProperty("catid").GetInt64();
            long pid = requestBody.GetProperty("pid").GetInt64();
            int sid = requestBody.GetProperty("sid").GetInt32(); // Wait, in legacy, 'sid' is product_id, but the query uses 'ProductID' to delete anyway.

            var success = await _service.UnlinkFranchiseItemAsync(fid, catid, pid, sid);
            return Json(new { d = success ? 1 : 0 });
        }
        catch (Exception)
        {
            return Json(new { d = 0 });
        }
    }

    // Autocomplete mappings
    [HttpPost("linkproductwithfranchise.aspx/GetProductList")]
    [HttpPost("linkproductwithfranchise/GetProductList")]
    public async Task<IActionResult> GetProductList([FromBody] System.Text.Json.JsonElement requestBody)
    {
        try
        {
            string keyword = requestBody.GetProperty("keyword").GetString() ?? "";
            int searchType = Convert.ToInt32(requestBody.GetProperty("searchType").GetString());
            int isService = requestBody.GetProperty("IsService").GetInt32();
            var wid = GetWebshopId();

            var list = await _service.SearchProductListAsync(keyword, searchType, isService, wid);
            var results = list.Select(i => new {
                product_id = i.Id,
                product_name = i.Title,
                product_image1 = i.ProductImage
            });
            return Json(new { d = results });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("linkproductwithfranchise.aspx/GettagList")]
    [HttpPost("linkproductwithfranchise/GettagList")]
    public async Task<IActionResult> GettagList([FromBody] System.Text.Json.JsonElement requestBody)
    {
        try
        {
            string keyword = requestBody.GetProperty("keyword").GetString() ?? "";
            int searchType = Convert.ToInt32(requestBody.GetProperty("searchType").GetString());

            var list = await _service.SearchTagListAsync(keyword);
            var results = list.Select(t => new {
                product_id = t.Id,
                product_name = t.Title,
                product_image1 = ""
            });
            return Json(new { d = results });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ─── File Upload Handler Mirroring ─────────────────────────────────────────────

    [HttpPost("linkproductwithfranchise/upload")]
    [HttpPost("linkproductwithfranchiseHandler.ashx")]
    public async Task<IActionResult> UploadSupplierImage()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Challenge();

        try
        {
            if (Request.Form.Files.Count > 0)
            {
                var file = Request.Form.Files[0];
                long fid = Convert.ToInt64(Request.Form["fid"]);
                long catid = Convert.ToInt64(Request.Form["catid"]);
                long pid = Convert.ToInt64(Request.Form["pid"]);
                int supplierId = Convert.ToInt32(Request.Form["supplierid"]);
                string oldImage = Request.Form["oldImage"].ToString() ?? "";
                string remarks = Request.Form["remarks"].ToString() ?? "";

                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "supplierImages");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                string resizedFolder = Path.Combine(uploadFolder, "resized_150_150");
                if (!Directory.Exists(resizedFolder)) Directory.CreateDirectory(resizedFolder);

                // Delete old image
                if (!string.IsNullOrEmpty(oldImage))
                {
                    string oldPath = Path.Combine(uploadFolder, oldImage);
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                    string oldResizedPath = Path.Combine(resizedFolder, oldImage);
                    if (System.IO.File.Exists(oldResizedPath)) System.IO.File.Delete(oldResizedPath);
                }

                string newFileName = DateTime.UtcNow.ToFileTimeUtc().ToString() + Path.GetExtension(file.FileName);
                string newFilePath = Path.Combine(uploadFolder, newFileName);
                string newResizedFilePath = Path.Combine(resizedFolder, newFileName);

                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Copy to resized folder to ensure compatibility
                System.IO.File.Copy(newFilePath, newResizedFilePath, true);

                // Fetch linked item ID to update
                var items = await _service.GetProductFranchiseLinkingAsync(catid, fid, 0);
                var linkItem = items.FirstOrDefault(i => (i.IsService && i.ProductId == pid) || (!i.IsService && i.ProductId == pid));
                if (linkItem != null)
                {
                    // In linkproductwithfranchiseHandler.ashx:
                    // ep.SupplierId = SupplierId;
                    // ep.Supplier_Image = fileName;
                    // ep.Delivered = ep.Ordered = 1;
                    // ep.Delivery_Date = ep.Order_Date = DateTime.Now;
                    // ep.Delivery_ReceivedBy = "";
                    // ep.Min_StockReq = 0;
                    // ep.Total_Investment = ep.Price = 0;
                    // ep.Price = 0;
                    // ep.Alternate_SupplierName = "";
                    // ep.Alternate_SupplierRemarks = remarks;
                    await _service.UpdateLinkedItemAsync(
                        linkItem.Id, supplierId, 0, 0, 0,
                        1, DateTime.Now, 1, DateTime.Now, "",
                        "", remarks, newFileName);

                    string siteUrl = $"{Request.Scheme}://{Request.Host}/";
                    string imgurl = siteUrl + "upload/supplierImages/resized_150_150/" + newFileName;
                    string imagelink = siteUrl + "upload/supplierImages/" + newFileName;

                    return Json(new {
                        image_name = newFileName,
                        image_url = imgurl,
                        image_link = imagelink
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        return BadRequest("Upload failed");
    }

    // ─── Franchise Notes View & Methods ───────────────────────────────────────────

    [HttpGet("franchisenotes/{id}")]
    public async Task<IActionResult> Notes([FromRoute] long id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var details = await _service.GetFranchiseNotesDetailsAsync(id);
        if (details == null)
        {
            return Content("Invalid Notes Reference ID");
        }

        var notesList = await _service.GetFranchiseNotesListAsync(id);

        ViewBag.CrfId = id;
        ViewBag.Details = details;
        ViewBag.NotesList = notesList;

        // User type check: in legacy: clsfranchisecookie.getFranchiseID() == "0" -> type 1 (business user), else type 2 (franchise user)
        // Since we are inside the business portal, the user is always business user (type 1).
        ViewBag.DefaultNotesType = 1; 

        return View("~/Views/FranchiseLinking/Notes.cshtml");
    }

    [HttpPost("/webservices.aspx/SendRemarks_FranchiseNotes")]
    public async Task<IActionResult> SendRemarksFranchiseNotes([FromBody] System.Text.Json.JsonElement requestBody)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            long crfID = requestBody.GetProperty("crfID").GetInt64();
            var retdata = requestBody.GetProperty("retdata");
            int optionalId = retdata.GetProperty("data_optionalId").GetInt32(); // 1 = Business User, 2 = Franchise User, 3 = Remarks
            string remarks = retdata.GetProperty("data_str").GetString() ?? "";

            string userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "Business User";

            long newNoteId = await _service.AddFranchiseNoteAsync(crfID, userId, userName, optionalId, remarks);

            // In legacy, the response matches:
            // return new { data_optionalId = 1, data_ID = newNoteId, data_optionalstr = userName, data_str = DateTime.Now.ToString("dd/MM/yyyy HH:mm") }
            return Json(new { d = new {
                data_optionalId = 1, // trigger append mode in javascript
                data_ID = newNoteId,
                data_optionalstr = userName,
                data_str = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new { data_optionalId = 0, error = ex.Message } });
        }
    }

    [HttpGet("linkproductwithfranchise/GetProductsByFilter")]
    public async Task<IActionResult> GetProductsByFilter([FromQuery] int prdType, [FromQuery] int tagId, [FromQuery] long fid, [FromQuery] long catId)
    {
        try
        {
            var wid = GetWebshopId();
            var list = await _service.GetProductsByTagOrTypeAsync(prdType, tagId, fid, catId, wid);
            var results = list.Select(i => new {
                id = i.Id,
                title = i.Title
            });
            return Json(results);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("linkproductwithfranchise/GetServicesByCat")]
    public async Task<IActionResult> GetServicesByCat([FromQuery] int serviceCatId, [FromQuery] long fid)
    {
        try
        {
            var list = await _service.GetServicesByCategoryDropdownSelectionAsync(serviceCatId, fid);
            var results = list.Select(i => new {
                id = i.Id,
                title = i.Title
            });
            return Json(results);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

