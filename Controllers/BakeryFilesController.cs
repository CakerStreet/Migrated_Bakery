using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class BakeryFilesController : Controller
{
    private readonly BakeryFilesService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public BakeryFilesController(
        BakeryFilesService service,
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

    [HttpGet("bakeryfiles")]
    [HttpGet("businessfiles")]
    [HttpGet("BakeryFiles.aspx")]
    public async Task<IActionResult> Index([FromQuery] string? orderID, [FromQuery] string? orderdetailid, [FromQuery] string? prdID)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        if (string.IsNullOrEmpty(orderID) && string.IsNullOrEmpty(orderdetailid) && string.IsNullOrEmpty(prdID))
        {
            return Redirect("/businessorders");
        }

        await PopulateLayoutMetadataAsync();
        ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

        long oId = 0;
        long odId = 0;
        long pId = 0;

        long.TryParse(orderID, out oId);
        long.TryParse(orderdetailid, out odId);
        long.TryParse(prdID, out pId);

        BakeryFilesOrderInfo? info = null;
        if (oId > 0 && odId > 0)
        {
            info = await _service.GetOrderAndProductDetailsAsync(oId, odId);
        }
        else if (pId > 0)
        {
            info = await _service.GetProductDetailsOnlyAsync(pId);
        }

        if (info == null)
        {
            return Redirect("/businessorders");
        }

        // Fetch related files & details
        var productFiles = await _service.GetProductFilesAsync(info.ProductId);
        var sizeFiles = await _service.GetCakeSizesForProductFilesAsync(info.ProductId);
        var orderFiles = await _service.GetOrderBakeryFilesAsync(oId, odId);
        var accessories = await _service.GetAccessoryDetailsAsync(oId, odId);
        var svgs = await _service.GetPersonalisedCakeSvgsAsync(oId, odId);

        // Map sizes dynamically to files
        var sizeMap = new Dictionary<long, List<CakeSizeForFileItem>>();
        foreach (var pf in productFiles)
        {
            sizeMap[pf.ProductFileId] = sizeFiles.Where(s => s.PrdFileId == pf.ProductFileId).OrderBy(s => s.DisplayOrder).ToList();
        }

        var webstoreId = GetWebshopId();
        var accessoryLocations = new Dictionary<long, List<Dictionary<string, object>>>();
        foreach (var acc in accessories)
        {
            var accId = Convert.ToInt64(acc["product_ID"]);
            accessoryLocations[accId] = await _service.GetTopperStockLocationsAsync(info.ProductId, accId, webstoreId);
        }

        ViewBag.OrderInfo = info;
        ViewBag.OrderId = oId;
        ViewBag.OrderDetailId = odId;
        ViewBag.ProductId = info.ProductId;
        ViewBag.ProductFiles = productFiles;
        ViewBag.SizeMap = sizeMap;
        ViewBag.OrderFiles = orderFiles;
        ViewBag.Accessories = accessories;
        ViewBag.AccessoryLocations = accessoryLocations;
        ViewBag.Svgs = svgs;

        // Fetch toppers and cutters
        // repToppers (product_type = 4)
        var sqlToppers = await GetProductToppersByTypeAsync(info.ProductId, 4);
        var topperLocations = new Dictionary<long, List<Dictionary<string, object>>>();
        foreach (var topper in sqlToppers)
        {
            var topperId = Convert.ToInt64(topper["product_ID"]);
            topperLocations[topperId] = await _service.GetTopperStockLocationsAsync(info.ProductId, topperId, webstoreId);
        }

        // repCutters (product_type = 5)
        var sqlCutters = await GetProductToppersByTypeAsync(info.ProductId, 5);
        var cutterLocations = new Dictionary<long, List<Dictionary<string, object>>>();
        foreach (var cutter in sqlCutters)
        {
            var cutterId = Convert.ToInt64(cutter["product_ID"]);
            cutterLocations[cutterId] = await _service.GetTopperStockLocationsAsync(info.ProductId, cutterId, webstoreId);
        }

        ViewBag.Toppers = sqlToppers;
        ViewBag.TopperLocations = topperLocations;
        ViewBag.Cutters = sqlCutters;
        ViewBag.CutterLocations = cutterLocations;

        // Build HTML for product documents (editable and non-editable rows)
        var sbPrd = new StringBuilder();
        if (oId > 0)
        {
            var orderDetailSizeId = info.OrderDetailSizeId;
            var editableFiles = productFiles.Where(pf => pf.IsAddtoOrder).ToList();
            var matchedFiles = editableFiles.Where(pf => sizeFiles.Any(s => s.PrdFileId == pf.ProductFileId && s.SizeId == orderDetailSizeId)).ToList();

            foreach (var mf in matchedFiles)
            {
                var pfModel = new OrderBakeryFileModel
                {
                    ProductId = mf.ProductId,
                    ProductFileTitle = mf.ProductFileTitle,
                    ProductFile = mf.ProductFile
                };
                sbPrd.Append(GetProductFileRowNotEditable(pfModel));
            }
        }

        foreach (var of in orderFiles)
        {
            sbPrd.Append(GetProductFileRow(of));
        }

        if (orderFiles.Count == 0)
        {
            var emptyModel = new OrderBakeryFileModel
            {
                RecNo = 1,
                ProductId = 0,
                ProductFileID = 0
            };
            sbPrd.Append(GetProductFileRow(emptyModel));
        }

        ViewBag.ProductDocumentRowsHtml = sbPrd.ToString();

        return View("~/Views/BakeryFiles/Index.cshtml");
    }

    private async Task<List<Dictionary<string, object>>> GetProductToppersByTypeAsync(long productId, int typeId)
    {
        var list = new List<Dictionary<string, object>>();
        var connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"SELECT att.product_ID, att.product_image1, att.product_SEOURL, att.product_Name, att.product_quantity 
                    FROM tbl_products att 
                    INNER JOIN tbl_product_topper lnk ON att.product_ID = lnk.Topper_PrdId 
                    WHERE att.product_type = @typeId AND lnk.product_Id = @pid";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        cmd.Parameters.AddWithValue("@pid", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object>
            {
                ["product_ID"] = reader["product_ID"],
                ["product_image1"] = reader["product_image1"]?.ToString() ?? "",
                ["product_SEOURL"] = reader["product_SEOURL"]?.ToString() ?? "",
                ["product_Name"] = reader["product_Name"]?.ToString() ?? "",
                ["product_quantity"] = reader["product_quantity"]
            };
            list.Add(dict);
        }
        return list;
    }

    private string GetProductFileRowNotEditable(OrderBakeryFileModel pm)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(@"<div class='col-md-12 row-height'>
                        <div class='form-group'>
                            <div class='col-sm-3 unbold labelleft'>
                                <img alt='#ProductFileTitle#' src='#docimg#'/> <b style='font-size: 13px;'>#ProductFileTitle#</b>
                            </div>
                            <div class='col-sm-5 caln'>
                                #fileupload# 
                            </div>
                            <div class='col-sm-4 raln'>
                            </div>
                        </div>
                    </div>");

        sb.Replace("#ProductFileTitle#", pm.ProductFileTitle);
        string siteUrl = "/";
        string docImgUrl = GetDocumentImgUrl(pm.ProductId.ToString(), pm.ProductFile ?? "");
        sb.Replace("#docimg#", docImgUrl);

        sb.Replace("#fileupload#", "<a id='ancViewFile' target='_blank' href='" + siteUrl + "bakeryfiles/download?file=upload/sku/" + pm.ProductId + "/files/" + pm.ProductFile + "' class='normallink font13'>Download File</a>");
        return sb.ToString();
    }

    private string GetProductFileRow(OrderBakeryFileModel pm)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(@"<div id='dvPrdFile_#index#' class='col-md-12 row-height div_productfile'>
                        <div class='form-group'>
                            <div class='col-sm-3 unbold labelleft'>
                                <input id='txtProductFileTitle' value='#ProductFileTitle#' class='form-control input-sm' placeholder='File Title'/>
                            </div>
                            <div class='col-sm-5 caln'>
                                #fileupload# <img id='imgLoader_#index#' src='images/bx_loader.gif' style='display:none;' />
                            </div>
                            <div class='col-sm-4 raln'>
                                #editremovefile#
                                <a id='ancSortOrder' data-tid='#index#' class='text-danger'><span class='glyphicon glyphicon-arrow-up' aria-hidden='true'></span></a>
                                <a id='ancDeletePrfFile' data-tid='#index#' class='text-danger'>X</a>
                                <input id='hfPrfFileDeleted' type='hidden' value='0'/>
                                <input id='hfProductFileID' type='hidden' value='#ProductFileID#'/>
                                <input id='hfProductFileName' type='hidden' value='#ProductFileName#'/>
                            </div>
                        </div>
                    </div>");

        sb.Replace("#index#", pm.RecNo.ToString());
        sb.Replace("#ProductFileTitle#", pm.ProductFileTitle);
        sb.Replace("#ProductFileName#", pm.ProductFile);
        sb.Replace("#ProductFileID#", pm.ProductFileID.ToString());

        string siteUrl = "/";
        if (pm.ProductFileID > 0 || (pm.ProductFileID == 0 && !string.IsNullOrEmpty(pm.ProductFile)))
        {
            sb.Replace("#fileupload#", "<input type='file' id='fuPrdFile' style='display:none;font-size:13px; margin: 0px auto;' /> <a id='ancUploadFile' data-tid='" + pm.RecNo + "' style='display:none;' class='normallink font13'>Upload</a> <a id='ancViewFile' data-tid='" + pm.RecNo + "' target='_blank' href='" + siteUrl + "bakeryfiles/download?file=upload/sku/" + pm.ProductId + "/files/" + pm.ProductFile + "' class='normallink font13'>Download File</a>");
            sb.Replace("#editremovefile#", "<a id='ancEditFile' data-tid='" + pm.RecNo + "' class='normallink font13'>Edit File</a> <a id='ancRemoveFile' data-tid='" + pm.RecNo + "' class='normallink font13'>Remove File</a> ");
        }
        else
        {
            sb.Replace("#fileupload#", "<input type='file' id='fuPrdFile' style='font-size:13px; margin: 0px auto;' /> <a id='ancUploadFile' data-tid='" + pm.RecNo + "' class='normallink font13'>Upload</a> <a id='ancViewFile' data-tid='" + pm.RecNo + "' style='display:none;' class='normallink font13'>Download File</a>");
            sb.Replace("#editremovefile#", "<a id='ancEditFile' data-tid='" + pm.RecNo + "' style='display:none;' class='normallink font13'>Edit File</a> <a id='ancRemoveFile' data-tid='" + pm.RecNo + "' style='display:none;' class='normallink font13'>Remove File</a> ");
        }

        return sb.ToString();
    }

    private string GetDocumentImgUrl(string productId, string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "/img/icons/rar.png";
        
        string fnLower = fileName.ToLower();
        if (fnLower.EndsWith(".zip") || fnLower.EndsWith(".rar"))
            return "/img/icons/rar.png";
        if (fnLower.EndsWith(".psd") || fnLower.EndsWith(".studio3") || fnLower.EndsWith(".ai"))
            return "/img/icons/psd.png";
        if (fnLower.EndsWith(".otf") || fnLower.EndsWith(".ttf") || fnLower.EndsWith(".fnt"))
            return "/img/icons/font.png";
        if (fnLower.EndsWith(".pdf"))
            return "/img/icons/pdf.png";
        if (fnLower.EndsWith(".svg"))
            return "/img/icons/svg.png";

        return "/upload/sku/" + productId + "/files/" + fileName;
    }

    // ─── WebMethods Mirroring ──────────────────────────────────────────────────────

    [HttpPost("BakeryFiles.aspx/SaveTopperQuantity")]
    [HttpPost("bakeryfiles/SaveTopperQuantity")]
    public async Task<IActionResult> SaveTopperQuantity([FromBody] SaveTopperQuantityRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            await _service.SaveTopperQuantityAsync(request.orderTopper);
            return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new { data_ID = 0, data_optionalstr = ex.Message } });
        }
    }

    [HttpPost("BakeryFiles.aspx/SaveAccessoryQuantity")]
    [HttpPost("bakeryfiles/SaveAccessoryQuantity")]
    public async Task<IActionResult> SaveAccessoryQuantity([FromBody] SaveTopperQuantityRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            await _service.SaveTopperQuantityAsync(request.orderTopper);
            return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new { data_ID = 0, data_optionalstr = ex.Message } });
        }
    }

    [HttpPost("BakeryFiles.aspx/GetAddtoOrder")]
    [HttpPost("bakeryfiles/GetAddtoOrder")]
    public async Task<IActionResult> GetAddtoOrder([FromBody] GetAddtoOrderRequest request)
    {
        var pf = await _service.GetProductFileByIdAsync(request.productfileId);
        if (pf != null)
        {
            var pm = new OrderBakeryFileModel
            {
                ProductId = pf.ProductId,
                ProductFileTitle = pf.ProductFileTitle,
                RecNo = request.rownumber
            };

            // Copy file physically if it exists
            string skuDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "sku", pf.ProductId.ToString(), "files");
            if (!string.IsNullOrEmpty(pf.ProductFile))
            {
                string srcPath = Path.Combine(skuDir, pf.ProductFile);
                if (System.IO.File.Exists(srcPath))
                {
                    string newFileName = DateTime.Now.Ticks + Path.GetExtension(pf.ProductFile);
                    string destPath = Path.Combine(skuDir, newFileName);
                    try
                    {
                        System.IO.File.Copy(srcPath, destPath, true);
                        pm.ProductFile = newFileName;
                    }
                    catch { }
                }
            }

            string html = GetProductFileRow(pm);
            return Json(new { d = html });
        }
        return Json(new { d = "" });
    }

    [HttpPost("BakeryFiles.aspx/GetProductFileRow")]
    [HttpPost("bakeryfiles/GetProductFileRow")]
    public IActionResult GetProductFileRowWeb([FromBody] GetProductFileRowRequest request)
    {
        string html = GetProductFileRow(request.pm);
        return Json(new { d = html });
    }

    [HttpPost("BakeryFiles.aspx/SaveBakeryDocument")]
    [HttpPost("bakeryfiles/SaveBakeryDocument")]
    public async Task<IActionResult> SaveBakeryDocument([FromBody] SaveBakeryDocumentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (request.lstPM == null || request.lstPM.Count == 0)
        {
            return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
        }

        long productId = request.lstPM[0].ProductId;
        long orderId = request.lstPM[0].OrderId;
        long orderDetailId = request.lstPM[0].OrderDetailID;

        try
        {
            await _service.SaveBakeryDocumentsAsync(request.lstPM, productId, orderId, orderDetailId);
            return Json(new { d = new { data_ID = 1, data_optionalstr = "" } });
        }
        catch (Exception ex)
        {
            return Json(new { d = new { data_ID = 0, data_optionalstr = ex.Message } });
        }
    }

    // ─── File Upload Handler Mirroring ─────────────────────────────────────────────

    [HttpPost("FileUploadHandler.ashx")]
    [HttpPost("bakeryfiles/upload")]
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

    [HttpGet("bakeryfiles/download")]
    public IActionResult Download([FromQuery] string file)
    {
        if (string.IsNullOrEmpty(file)) return BadRequest("File is required");

        // Validate and clean path to prevent directory traversal
        string cleanFile = file.Replace("~/", "").Replace("/", "\\");
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanFile);

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound("File not found");
        }

        string contentType = "application/octet-stream";
        return File(System.IO.File.OpenRead(fullPath), contentType, Path.GetFileName(fullPath));
    }
}

public class SaveTopperQuantityRequest
{
    public List<OrderTopperQtyInput> orderTopper { get; set; } = new();
}

public class GetAddtoOrderRequest
{
    public int rownumber { get; set; }
    public long productfileId { get; set; }
}

public class GetProductFileRowRequest
{
    public OrderBakeryFileModel pm { get; set; } = new();
}

public class SaveBakeryDocumentRequest
{
    public List<OrderBakeryFileModel> lstPM { get; set; } = new();
}
