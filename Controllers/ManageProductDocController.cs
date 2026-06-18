using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class ManageProductDocController : Controller
{
    private readonly ManageProductDocService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public ManageProductDocController(
        ManageProductDocService service,
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

    [HttpGet("manageproductdoc")]
    [HttpGet("manageproductdoc.aspx")]
    public async Task<IActionResult> Index([FromQuery] long id, [FromQuery] string? returl = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        if (id == 0)
            return Redirect("/mywebstore");

        var product = await _service.GetProductDetailsAsync(id);
        if (product == null)
            return Redirect("/mywebstore");

        await PopulateLayoutMetadataAsync();

        var productFiles = await _service.GetProductFilesAsync(id);

        // Build file rows HTML
        var sbPrd = new StringBuilder();
        int productType = product.ProductType;

        for (int i = 0; i < productFiles.Count; i++)
        {
            var pf = productFiles[i];
            var sizes = await _service.GetSizesForProductFileAsync(id, pf.ProductFileID);
            sbPrd.Append(BuildProductFileRowHtml(pf.RecNo, pf.ProductFileTitle, pf.ProductFile, pf.ProductFileID,
                pf.IsAddtoOrder, pf.ProductFileID > 0 ? pf.CreatedOn.ToString("dd/MM/yyyy hh:mm tt") : "",
                pf.ProductId, productType, sizes));
        }

        if (productFiles.Count == 0)
        {
            var sizes = await _service.GetSizesForProductFileAsync(id, 0);
            sbPrd.Append(BuildProductFileRowHtml(1, "", "", 0, false, "", id, productType, sizes));
        }

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var prdImageUrl = cdnBase + "upload/Product_images/resized_300_300/" + (product.ProductImage ?? "");

        // Build back URL
        string backUrl = returl ?? "/mywebstore";

        // Product link
        string productLink;
        if (productType == 5)
        {
            productLink = $"/edititem?prdID={product.ProductId}";
        }
        else
        {
            productLink = $"/{product.SeoUrl}-p{product.ProductId}";
        }

        ViewBag.ProductId = id;
        ViewBag.ProductType = productType;
        ViewBag.Product = product;
        ViewBag.ProductImageUrl = prdImageUrl;
        ViewBag.ProductLink = productLink;
        ViewBag.ProductDisplayName = $"{product.ProductName} (#{product.ProductCode})";
        ViewBag.HeaderText = productType == 5 ? "Repository" : "Document";
        ViewBag.ShowTypewiseFilter = productType != 5;
        ViewBag.ShowAddToOrder = productType != 5;
        ViewBag.ProductDocumentRowsHtml = sbPrd.ToString();
        ViewBag.BackUrl = backUrl;
        ViewBag.EditUrl = $"/mywebstore?search={product.ProductId}";

        return View("~/Views/ManageProductDoc/Index.cshtml");
    }

    // ─── AJAX Endpoints (mirroring legacy WebMethods) ─────────────────────────────

    [HttpPost("manageproductdoc.aspx/SaveProductDocument")]
    [HttpPost("manageproductdoc/SaveProductDocument")]
    public async Task<IActionResult> SaveProductDocument([FromBody] SaveProductDocumentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            if (request.lstPM == null || request.lstPM.Count == 0)
            {
                return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
            }

            long productId = request.lstPM[0].ProductId;

            // Collect files to delete before saving
            var filesToDelete = _service.GetFilesToDelete(request.lstPM);

            await _service.SaveProductDocumentsAsync(request.lstPM);

            // Delete physical files
            string skuDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "sku", productId.ToString(), "files");
            foreach (string file in filesToDelete)
            {
                string filePath = Path.Combine(skuDir, file);
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { }
                }
            }

            return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new { data_ID = 0, data_optionalstr = ex.Message } });
        }
    }

    [HttpPost("manageproductdoc.aspx/GetProductFileRow")]
    [HttpPost("manageproductdoc/GetProductFileRow")]
    public async Task<IActionResult> GetProductFileRow([FromBody] GetProductDocFileRowRequest request)
    {
        var pm = request.pm;
        var sizes = await _service.GetSizesForProductFileAsync(request.prdid, pm.ProductFileID);

        string html = BuildProductFileRowHtml(pm.RecNo, pm.ProductFileTitle, pm.ProductFile, pm.ProductFileID,
            false, "", pm.ProductId, request.prdType, sizes);

        return Json(new { d = html });
    }

    // ─── File Upload Handler ──────────────────────────────────────────────────────

    [HttpPost("productdocupload.ashx")]
    [HttpPost("manageproductdoc/upload")]
    public async Task<IActionResult> UploadFile()
    {
        if (Request.Form.Files.Count > 0)
        {
            var postedFile = Request.Form.Files[0];
            long.TryParse(Request.Form["prdid"], out long productId);

            if (productId == 0)
            {
                return BadRequest("Invalid product ID");
            }

            string skuDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "sku", productId.ToString(), "files");
            if (!Directory.Exists(skuDir))
            {
                Directory.CreateDirectory(skuDir);
            }

            string fileName = DateTime.Now.ToFileTimeUtc() + Path.GetExtension(postedFile.FileName);
            string filePath = Path.Combine(skuDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await postedFile.CopyToAsync(stream);
            }

            return Json(new { name = fileName });
        }

        return BadRequest("No files uploaded");
    }

    // ─── HTML Builder ─────────────────────────────────────────────────────────────

    private string BuildProductFileRowHtml(int index, string productFileTitle, string productFile,
        long productFileId, bool isAddtoOrder, string productFileDate, long productId, int prdType,
        List<ProductDocSizeItem> sizes)
    {
        var sb = new StringBuilder();
        sb.Append(@"<div id='dvPrdFile_#index#' class='col-md-12 row-height div_productfile'>
	
                                                    <div class='form-group'>

                       <div class='col-sm-2 caln'>
#ProductFileDate#
</div>
                                                        <div class='col-sm-3 unbold labelleft'>
                                                            <input id='txtProductFileTitle' value='#ProductFileTitle#' class='form-control input-sm' placeholder='File Title'/>
                                                        </div>
                                                        <div class='col-sm-3 caln' #showaddtoorder#>
                                                        <label class='checkbox-inline'>
                                                            <input id='chkIsAddtoOrder' type='checkbox' #ischecked#  />
                                                            Add to Order
                                                        </label>
                                                        </div>
                                                        <div class='col-sm-2 caln'>
                                                            #fileupload# <img id='imgLoader_#index#' src='images/bx_loader.gif' style='display:none;' />
                                                        </div>
                                                       
                                                        <div class='col-sm-2 caln'>
#editremovefile#
                                                            <a id='ancSortOrder'  data-tid='#index#' class='normallink'><span class='glyphicon glyphicon-arrow-up' aria-hidden='true'></span></a>
                                                            <a id='ancDeletePrfFile' data-tid='#index#' class='normallink'>X</a>
                                                            <input id='hfPrfFileDeleted' type='hidden' value='0'/>
                                                            <input id='hfProductFileID' type='hidden' value='#ProductFileID#'/>
                                                            <input id='hfProductFileName' type='hidden' value='#ProductFileName#'/>
                                                        </div>
                                                    </div>
#sizes#

                        </div>
");

        sb.Replace("#index#", index.ToString());
        sb.Replace("#ProductFileTitle#", productFileTitle ?? "");
        sb.Replace("#ProductFileName#", productFile ?? "");
        sb.Replace("#ProductFileID#", productFileId.ToString());
        sb.Replace("#ischecked#", isAddtoOrder ? "checked='checked'" : "");
        sb.Replace("#showaddtoorder#", prdType == 5 ? "style=display:none;" : "");

        if (productFileId > 0)
        {
            sb.Replace("#ProductFileDate#", productFileDate);
            sb.Replace("#fileupload#", "<input type='file' id='fuPrdFile' style='display:none;' /> <a id='ancUploadFile' data-tid='" + index + "' style='display:none;'>Upload</a> <a id='ancViewFile' target='_blank' href='/bakeryfiles/download?file=upload/sku/" + productId + "/files/" + productFile + "' data-tid='" + index + "' class='normallink'>Download File</a>");
            sb.Replace("#editremovefile#", "<a id='ancEditFile' data-tid='" + index + "' class='normallink'><span class='glyphicon glyphicon-pencil' aria-hidden='true'></span></a> <a id='ancRemoveFile' data-tid='" + index + "' class='normallink'><span class='glyphicon glyphicon-trash' aria-hidden='true'></span></a> ");
        }
        else
        {
            sb.Replace("#ProductFileDate#", "");
            sb.Replace("#fileupload#", "<input type='file' id='fuPrdFile' /> <a id='ancUploadFile' data-tid='" + index + "'>Upload</a> <a id='ancViewFile' data-tid='" + index + "' style='display:none;' class='normallink'>Download File</a>");
            sb.Replace("#editremovefile#", "<a id='ancEditFile' data-tid='" + index + "' style='display:none;' class='normallink'><span class='glyphicon glyphicon-pencil' aria-hidden='true'></span></a> <a id='ancRemoveFile' data-tid='" + index + "' style='display:none;' class='normallink'><span class='glyphicon glyphicon-trash' aria-hidden='true'></span></a> ");
        }

        if (sizes != null && sizes.Count > 0)
        {
            var sbSizes = new StringBuilder("<div class='form-horizontal form-group'><div class='col-sm-offset-2 col-sm-10 div_PrdFileSize'>");
            foreach (var s in sizes)
            {
                sbSizes.AppendFormat(@"<div class='checkbox'>
  <label><input type='checkbox' value='{0}' {1} />{2}</label></div>", s.SizeID, s.IsLinked ? "checked='checked'" : "", s.SizeTitle);
            }
            sbSizes.Append("</div></div>");
            sb.Replace("#sizes#", sbSizes.ToString());
        }
        else
        {
            sb.Replace("#sizes#", "");
        }

        return sb.ToString();
    }
}

public class SaveProductDocumentRequest
{
    public List<ProductDocSaveModel> lstPM { get; set; } = new();
}

public class GetProductDocFileRowRequest
{
    public ProductDocSaveModel pm { get; set; } = new();
    public long prdid { get; set; }
    public int prdType { get; set; }
}
