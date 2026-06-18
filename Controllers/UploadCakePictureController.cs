using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Handles cake product image uploads.
/// Migrated from legacy WebForms page: uploadcakepicture.aspx / uploadcakepicture.aspx.cs
///
/// Legacy behaviour:
///   - Accepts multipart POST with form fields: product_id, apitype
///   - For apitype==1 (google product): generates a ticks-based filename, saves, updates googlesearch link
///   - For apitype!=1 (normal product): uses product SEO name for filename, saves original + 6 thumbnail
///     sizes + fbImage, updates tbl_products.product_image1, tbl_productImage, tbl_orderDetail
///     (via tbl_orderImageUpdate), tbl_prdUpdated, tbl_googlefeedprd
///   - Generates thumbnails at 135, 200, 300, 500, 80, 800 and fbImage 800×420
///   - Deletes old .webp counterparts, triggers webp regeneration
///
/// Modern version (Phase 1 – local storage):
///   - Saves file to wwwroot/uploads/cakes/{filename}
///   - Updates tbl_products.product_image1 and tbl_orderDetail via raw ADO.NET
///   - Thumbnail generation and S3/webp support to be added in a later phase
///   - Returns JSON { success, filename, url }
/// </summary>
[Route("uploadcakepicture")]
[Route("uploadcakepicture.aspx")]
public class UploadCakePictureController : Controller
{
    private readonly IConfiguration _config;

    public UploadCakePictureController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Accepts a cake product image upload via multipart POST.
    /// </summary>
    /// <remarks>
    /// Expected form fields:
    ///   - product_id (long): The product or google search ID
    ///   - apitype (int): 1 = google product, other = normal product
    /// Expected file: one image file in the multipart form data.
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> Index()
    {
        var form = Request.Form;

        // ── Validate required form fields ───────────────────────────────
        if (!long.TryParse(form["product_id"], out long productId) ||
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
            // Legacy: apitype==1 uses ticks; otherwise uses product SEO name or existing image name.
            // For now we use a ticks + productId based name for simplicity.
            string extension = Path.GetExtension(file.FileName);
            string filename = $"{DateTime.Now.Ticks}_{productId}{extension}";

            // ── Save to local uploads directory ─────────────────────────
            string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsDir = Path.Combine(wwwroot, "uploads", "cakes");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            string filePath = Path.Combine(uploadsDir, filename);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string relativeUrl = $"/uploads/cakes/{filename}";

            // ── Update database ─────────────────────────────────────────
            // For normal products (apitype != 1), update tbl_products and tbl_orderDetail
            string connectionString = _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

            if (apitype != 1 && !string.IsNullOrEmpty(connectionString))
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                // Update product image
                const string updateProductSql = @"
                    UPDATE tbl_products 
                    SET product_image1 = @filename,
                        Product_CDNSts = 0,
                        Product_image1isURL = 0,
                        product_modifiedOn = GETDATE()
                    WHERE product_ID = @pid";

                await using (var cmd = new SqlCommand(updateProductSql, conn))
                {
                    cmd.Parameters.AddWithValue("@filename", filename);
                    cmd.Parameters.AddWithValue("@pid", productId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Update order details that have pending image updates (mirrors legacy tbl_orderImageUpdate logic)
                const string updateOrdersSql = @"
                    IF EXISTS (
                        SELECT 1 FROM tbl_orderImageUpdate m 
                        INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                        WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid
                    )
                    BEGIN
                        UPDATE d 
                        SET d.orderDetail_ProductImage = @prdimage 
                        FROM tbl_orderImageUpdate m 
                        INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                        WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid;

                        UPDATE m 
                        SET m.IsUpdated = 1 
                        FROM tbl_orderImageUpdate m 
                        INNER JOIN tbl_orderDetail d ON m.OrderImage_orderDetail_ID = d.orderDetail_ID
                        WHERE m.IsUpdated = 0 AND d.orderDetail_productID = @pid;
                    END";

                await using (var cmd = new SqlCommand(updateOrdersSql, conn))
                {
                    cmd.Parameters.AddWithValue("@pid", productId);
                    cmd.Parameters.AddWithValue("@prdimage", relativeUrl);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Json(new { success = true, filename, url = relativeUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
