using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for printing purchase order items received (goods receipt).
/// Route: /printpurchaseorderitemrecd?id={poId}
/// Migrated from printpurchaseorderitemrecd.aspx.
/// Also handles invoice file download.
/// </summary>
[Route("printpurchaseorderitemrecd")]
[Route("printpurchaseorderitemrecd.aspx")]
public class PrintPurchaseOrderItemRecdController : Controller
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public PrintPurchaseOrderItemRecdController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? id)
    {
        if (!id.HasValue)
            return BadRequest("PO id is required.");

        var connectionString = _config.GetConnectionString("aboraboraboraaboraaborab");
        var model = new PrintPOItemRecdModel();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        long wid = 0;
        long poItemsRecId = 0;

        // 1. Load PO Items Received header
        var headerSql = @"SELECT PO_ItemsRec_ID, PO_ID, PO_InvoiceNo, PO_InvoiceDate, PO_ReceivedDate, 
                                 PO_InvoiceFile, PO_ReceivedBy, PO_Remarks 
                          FROM tbl_PO_ItemsRec WHERE PO_ID = @id";
        await using (var cmd = new SqlCommand(headerSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", id.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                poItemsRecId = Convert.ToInt64(reader["PO_ItemsRec_ID"]);
                model.InvoiceNo = reader["PO_InvoiceNo"]?.ToString() ?? "";
                model.InvoiceDate = Convert.ToDateTime(reader["PO_InvoiceDate"]).ToLongDateString();
                model.ReceivedDate = Convert.ToDateTime(reader["PO_ReceivedDate"]).ToLongDateString();
                model.Remarks = reader["PO_Remarks"]?.ToString() ?? "";
                model.InvoiceFile = reader["PO_InvoiceFile"]?.ToString() ?? "";

                // Get received-by name
                var receivedBy = Convert.ToInt64(reader["PO_ReceivedBy"]);
                await using var conn2 = new SqlConnection(connectionString);
                await conn2.OpenAsync();
                var rbSql = "SELECT TOP 1 customer_Name FROM tbl_bakeryuser WHERE customer_type IN (2,3) AND customer_isActive = 1 AND customer_ID = @uid";
                await using var rbCmd = new SqlCommand(rbSql, conn2);
                rbCmd.Parameters.AddWithValue("@uid", receivedBy);
                var rbResult = await rbCmd.ExecuteScalarAsync();
                model.ReceivedBy = rbResult?.ToString() ?? "";
            }
            else
            {
                return NotFound("PO items received record not found.");
            }
        }

        // 2. Load PO header to get webstore ID and supplier
        var poSql = "SELECT PO_WebstoreID, PO_SupplierID FROM tbl_PO WHERE PO_ID = @id";
        await using (var cmd = new SqlCommand(poSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", id.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                wid = Convert.ToInt64(reader["PO_WebstoreID"]);
                var supplierID = Convert.ToInt64(reader["PO_SupplierID"]);

                await using var conn3 = new SqlConnection(connectionString);
                await conn3.OpenAsync();
                var sSql = "SELECT SupplierName FROM tbl_ProductSupplier WHERE SupplierId = @sid";
                await using var sCmd = new SqlCommand(sSql, conn3);
                sCmd.Parameters.AddWithValue("@sid", supplierID);
                var sResult = await sCmd.ExecuteScalarAsync();
                model.SupplierName = sResult?.ToString() ?? "";
            }
        }

        // 3. Check invoice file exists
        if (!string.IsNullOrEmpty(model.InvoiceFile))
        {
            var invoicePath = Path.Combine(_env.WebRootPath, "upload", "poinvoice", model.InvoiceFile);
            model.HasInvoiceFile = System.IO.File.Exists(invoicePath);
        }

        // 4. Load received items (complex CTE query)
        var itemsSql = @";WITH RCTE AS (
            SELECT LocationID, LocationTitle, CAST(LocationTitle AS VARCHAR(2000)) AS FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
            FROM tbl_location WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid
            UNION ALL
            SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle AS VARCHAR(2000)) AS FullLocation, 
                   rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
            FROM tbl_location rh INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID 
            WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
        ) SELECT LocationID, FullLocation INTO #t FROM RCTE WHERE Lvl = 3 ORDER BY DisplayOrder

        SELECT d.PODet_BatchID, ps.SupplierName, #t.FullLocation, p.product_Name, p.product_code, 
               PrdStockRequest_Id, POdet_PrdID, POdet_Qty, POdet_RatePerItem, 
               POdet_Amount, POdet_disc, POdet_Subtotal, POdet_VatPer, POdet_Vat, POdet_NetTotal 
        FROM tbl_products p 
        INNER JOIN tbl_POdet_ItemsRec d ON p.product_ID = d.POdet_PrdID 
        INNER JOIN tbl_PO_ItemsRec ie ON ie.PO_ItemsRec_ID = d.POdet_POID 
        INNER JOIN tbl_PO po ON po.PO_ID = ie.PO_ID 
        INNER JOIN tbl_ProductSupplier ps ON ps.SupplierId = po.PO_SupplierID 
        LEFT OUTER JOIN #t ON d.PODet_LocationID = #t.LocationID
        WHERE POdet_POID = @poid ORDER BY POdet_displayOrder
        DROP TABLE #t";

        try
        {
            await using var cmd = new SqlCommand(itemsSql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            cmd.Parameters.AddWithValue("@poid", poItemsRecId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                model.Items.Add(new POLineItem
                {
                    BatchId = reader["PODet_BatchID"]?.ToString() ?? "",
                    Location = reader["FullLocation"]?.ToString() ?? "",
                    ProductName = reader["product_Name"]?.ToString() ?? "",
                    ProductCode = reader["product_code"]?.ToString() ?? "",
                    Qty = reader.IsDBNull(reader.GetOrdinal("POdet_Qty")) ? 0 : Convert.ToInt32(reader["POdet_Qty"]),
                    RatePerItem = reader.IsDBNull(reader.GetOrdinal("POdet_RatePerItem")) ? 0m : Convert.ToDecimal(reader["POdet_RatePerItem"]),
                    Amount = reader.IsDBNull(reader.GetOrdinal("POdet_Amount")) ? 0m : Convert.ToDecimal(reader["POdet_Amount"]),
                    Discount = reader.IsDBNull(reader.GetOrdinal("POdet_disc")) ? 0m : Convert.ToDecimal(reader["POdet_disc"]),
                    Subtotal = reader.IsDBNull(reader.GetOrdinal("POdet_Subtotal")) ? 0m : Convert.ToDecimal(reader["POdet_Subtotal"]),
                    VatPercent = reader.IsDBNull(reader.GetOrdinal("POdet_VatPer")) ? 0m : Convert.ToDecimal(reader["POdet_VatPer"]),
                    Vat = reader.IsDBNull(reader.GetOrdinal("POdet_Vat")) ? 0m : Convert.ToDecimal(reader["POdet_Vat"]),
                    NetTotal = reader.IsDBNull(reader.GetOrdinal("POdet_NetTotal")) ? 0m : Convert.ToDecimal(reader["POdet_NetTotal"])
                });
            }
        }
        catch { }

        var websiteName = _config["websiteNamewithExt"] ?? "CakerStreet";
        ViewBag.WebsiteName = websiteName;
        ViewBag.WebsiteUrl = _config["CdnBase"] ?? "/";
        ViewBag.PoId = id.Value;

        return View("~/Views/PrintPurchaseOrderItemRecd/Index.cshtml", model);
    }

    /// <summary>
    /// Download the invoice file attached to a PO receipt.
    /// </summary>
    [HttpGet("downloadinvoice")]
    public IActionResult DownloadInvoice([FromQuery] string file)
    {
        if (string.IsNullOrEmpty(file))
            return BadRequest("File name required.");

        // Prevent directory traversal
        var fileName = Path.GetFileName(file);
        var filePath = Path.Combine(_env.WebRootPath, "upload", "poinvoice", fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found.");

        return PhysicalFile(filePath, "application/octet-stream", fileName);
    }
}

public class PrintPOItemRecdModel
{
    public string InvoiceNo { get; set; } = "";
    public string InvoiceDate { get; set; } = "";
    public string ReceivedDate { get; set; } = "";
    public string ReceivedBy { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string InvoiceFile { get; set; } = "";
    public bool HasInvoiceFile { get; set; }
    public List<POLineItem> Items { get; set; } = new();
}
