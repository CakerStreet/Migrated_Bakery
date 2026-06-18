using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Handles order-specific image uploads (customer reference photos for orders).
/// Migrated from legacy WebForms page: uploadorderpicture.aspx / uploadorderpicture.aspx.cs
///
/// Legacy behaviour:
///   - Accepts multipart POST with form fields: product_id (actually an order ID), apitype
///   - Generates filename as {ticks}_{orderId}.{ext}
///   - Saves original to upload/Product_images/, generates thumbnails at 6 sizes
///   - Updates tbl_orderDetail.orderDetail_ProductImage via clsCustomDelete.CustomUpdate
///     setting it to {customer_websiteLogo}upload/Product_images/resized_500_500/{filename}
///     WHERE orderDetail_orderID = {product_id}
///
/// Modern version (Phase 1 – local storage):
///   - Saves file to wwwroot/uploads/orders/{filename}
///   - Updates tbl_orderDetail.orderDetail_ProductImage via raw ADO.NET
///   - Thumbnail generation and S3 support to be added in a later phase
///   - Returns JSON { success, filename, url }
/// </summary>
[Route("uploadorderpicture")]
[Route("uploadorderpicture.aspx")]
public class UploadOrderPictureController : Controller
{
    private readonly IConfiguration _config;

    public UploadOrderPictureController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Accepts an order image upload via multipart POST.
    /// </summary>
    /// <remarks>
    /// Expected form fields:
    ///   - product_id (long): Despite the name, this is actually the order ID 
    ///     (legacy naming preserved for backwards compatibility)
    ///   - apitype (int): Upload type identifier
    /// Expected file: one image file in the multipart form data.
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> Index()
    {
        var form = Request.Form;

        // ── Validate required form fields ───────────────────────────────
        // Legacy field name is "product_id" but it's actually the order ID
        if (!long.TryParse(form["product_id"], out long orderId) ||
            !int.TryParse(form["apitype"], out int apitype))
        {
            return BadRequest(new { success = false, error = "product_id and apitype are required." });
        }

        if (Request.Form.Files.Count == 0)
        {
            return BadRequest(new { success = false, error = "No file uploaded." });
        }

        var file = Request.Form.Files[0];
        if (file.Length == 0)
        {
            return BadRequest(new { success = false, error = "Uploaded file is empty." });
        }

        try
        {
            // ── Generate filename ───────────────────────────────────────
            // Legacy: DateTime.Now.Ticks + "_" + product_id + extension
            string extension = Path.GetExtension(file.FileName);
            string filename = $"{DateTime.Now.Ticks}_{orderId}{extension}";

            // ── Save to local uploads directory ─────────────────────────
            string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsDir = Path.Combine(wwwroot, "uploads", "orders");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            string filePath = Path.Combine(uploadsDir, filename);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string relativeUrl = $"/uploads/orders/{filename}";

            // ── Update database ─────────────────────────────────────────
            // Legacy: clsCustomDelete updates tbl_orderDetail SET orderDetail_ProductImage = ...
            //         WHERE orderDetail_orderID = {orderId}
            string connectionString = _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

            if (!string.IsNullOrEmpty(connectionString))
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                const string updateSql = @"
                    UPDATE tbl_orderDetail 
                    SET orderDetail_ProductImage = @imageUrl
                    WHERE orderDetail_orderID = @orderId";

                await using var cmd = new SqlCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@imageUrl", relativeUrl);
                cmd.Parameters.AddWithValue("@orderId", orderId);
                await cmd.ExecuteNonQueryAsync();
            }

            return Json(new { success = true, filename, url = relativeUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
