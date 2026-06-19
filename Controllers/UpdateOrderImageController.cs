using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Update Order Image page.
/// Displays product image for an order with download and upload buttons.
/// Migrated from legacy updateorderimage.aspx / updateorderimage.aspx.cs.
/// </summary>
[Route("updateorderimage")]
public class UpdateOrderImageController : Controller
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public UpdateOrderImageController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    [HttpGet("")]
    [Route("~/updateorderimage.aspx")]
    public async Task<IActionResult> Index([FromQuery] string? orderID)
    {
        if (string.IsNullOrEmpty(orderID))
            return Redirect("/businessorders");

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        var model = new UpdateOrderImageViewModel
        {
            OrderId = orderID,
            BusinessName = "Bakery Files (#" + orderID + ")"
        };

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT od.*, o.*, p.*,
                   CASE WHEN g.google_prdID IS NULL THEN 0 ELSE 1 END AS IsGooglePrd, 
                   0 AS prd_apitype
            FROM tbl_orderDetail od 
            INNER JOIN tbl_order o ON orderDetail_orderID=order_ID 
            LEFT OUTER JOIN tbl_skumapping s ON s.SkuMapping_newPrdID = od.orderDetail_productID
            INNER JOIN tbl_products p ON product_Id = CASE WHEN s.SkuMapping_refPrdID IS NULL THEN orderDetail_productID ELSE s.SkuMapping_refPrdID END
            LEFT OUTER JOIN tbl_googlefeedprd g ON p.product_id = g.google_prdID
            WHERE order_ID=@orderId", conn);
        cmd.Parameters.AddWithValue("@orderId", orderID);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            model.ProductId = reader["product_ID"]?.ToString() ?? "0";
            model.ProductImageUrl = reader["orderDetail_ProductImage"]?.ToString() ?? "";
            model.ProductName = string.Format("{0} (#{1})",
                reader["product_name"]?.ToString() ?? "",
                reader["product_code"]?.ToString() ?? "");
            model.DownloadArgument = reader["orderDetail_ProductImage"]?.ToString() + "#s#" +
                                     reader["prd_apitype"]?.ToString();
        }

        return View("~/Views/UpdateOrderImage/Index.cshtml", model);
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string? arg)
    {
        if (string.IsNullOrEmpty(arg))
            return BadRequest("No file specified");

        string[] separators = { "#s#" };
        string[] arr = arg.Split(separators, StringSplitOptions.None);
        string filename = arr[0];
        string errorMsg = "Error Message - File not available for download.";

        try
        {
            using var webClient = new HttpClient();
            webClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");

            var imageBytes = await webClient.GetByteArrayAsync(filename);
            var ext = Path.GetExtension(filename);
            if (ext.Contains('?'))
                ext = ext.Split('?')[0];

            var downloadFilename = DateTime.Now.Ticks + ext;
            return File(imageBytes, "application/octet-stream", downloadFilename);
        }
        catch (Exception)
        {
            return Content(errorMsg);
        }
    }
}

public class UpdateOrderImageViewModel
{
    public string OrderId { get; set; } = "0";
    public string ProductId { get; set; } = "0";
    public string ProductImageUrl { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BusinessName { get; set; } = "Edit Order";
    public string DownloadArgument { get; set; } = "";
}
