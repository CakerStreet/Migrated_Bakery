using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for printing a purchase order.
/// Route: /printpurchaseorder?id={poId}
/// Migrated from printpurchaseorder.aspx.
/// Standalone print page showing PO header, supplier details, and line items.
/// </summary>
[Route("printpurchaseorder")]
[Route("printpurchaseorder.aspx")]
public class PrintPurchaseOrderController : Controller
{
    private readonly IConfiguration _config;

    public PrintPurchaseOrderController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? id)
    {
        if (!id.HasValue)
            return BadRequest("PO id is required.");

        var connectionString = _config.GetConnectionString("aboraboraboraaboraaborab");
        var model = new PrintPurchaseOrderModel();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // 1. Load PO header
        long supplierID = 0, wid = 0;
        var poSql = "SELECT PO_SysNo, PO_Date, PO_SupplierID, PO_Status, PO_WebstoreID FROM tbl_PO WHERE PO_ID = @id";
        await using (var cmd = new SqlCommand(poSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", id.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                model.InvoiceNo = reader["PO_SysNo"]?.ToString() ?? "";
                model.PurchaseDate = Convert.ToDateTime(reader["PO_Date"]).ToLongDateString();
                supplierID = Convert.ToInt64(reader["PO_SupplierID"]);
                model.StatusText = GetStatusText(Convert.ToInt32(reader["PO_Status"]));
                wid = Convert.ToInt64(reader["PO_WebstoreID"]);
            }
            else
            {
                return NotFound("Purchase order not found.");
            }
        }

        // 2. Load supplier details
        var supplierSql = @"SELECT SupplierName, Supplier_AddressDetail, Supplier_Remarks, 
                                   Supplier_IsTopper, Supplier_IsAccessory 
                            FROM tbl_ProductSupplier WHERE SupplierId = @sid";
        await using (var cmd = new SqlCommand(supplierSql, conn))
        {
            cmd.Parameters.AddWithValue("@sid", supplierID);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                model.SupplierName = reader["SupplierName"]?.ToString() ?? "";
                model.AddressDetail = (reader["Supplier_AddressDetail"]?.ToString() ?? "").Replace("\n", "<br />").Replace("\r", "");
                model.Remarks = (reader["Supplier_Remarks"]?.ToString() ?? "").Replace("\n", "<br />").Replace("\r", "");
                model.SuppliesTopper = Convert.ToBoolean(reader["Supplier_IsTopper"]) ? "Yes" : "No";
                model.SuppliesAccessories = Convert.ToBoolean(reader["Supplier_IsAccessory"]) ? "Yes" : "No";
            }
        }

        // 3. Load PO line items (complex CTE query from legacy)
        var itemsSql = @";WITH RCTE AS (
            SELECT LocationID, LocationTitle, CAST(LocationTitle AS VARCHAR(2000)) AS FullLocation, ParentLocationId, 1 AS Lvl, DisplayOrder
            FROM tbl_location WHERE ParentLocationId = 0 AND location_isactive = 1 AND location_isdeleted = 0 AND webstoreid = @wid
            UNION ALL
            SELECT rh.LocationID, rh.LocationTitle, CAST(rc.FullLocation + ' > ' + rh.LocationTitle AS VARCHAR(2000)) AS FullLocation, 
                   rh.ParentLocationId, Lvl+1 AS Lvl, rh.DisplayOrder 
            FROM tbl_location rh INNER JOIN RCTE rc ON rh.ParentLocationId = rc.LocationID 
            WHERE rh.Location_IsDeleted = 0 AND location_isactive = 1
        ) SELECT LocationID, FullLocation INTO #t FROM RCTE WHERE Lvl = 3 ORDER BY DisplayOrder

        SELECT di.PODet_BatchID, #t.FullLocation, p.product_Name, p.product_code, 
               d.PrdStockRequest_Id, d.POdet_PrdID, d.POdet_Qty, d.POdet_RatePerItem, 
               d.POdet_Amount, d.POdet_disc, d.POdet_Subtotal, d.POdet_VatPer, d.POdet_Vat, d.POdet_NetTotal 
        FROM tbl_products p 
        INNER JOIN tbl_POdet d ON p.product_ID = d.POdet_PrdID
        LEFT OUTER JOIN tbl_POdet_ItemsRec di ON p.product_ID = di.POdet_PrdID 
        INNER JOIN #t ON di.PODet_LocationID = #t.LocationID
        WHERE d.POdet_POID = @poid ORDER BY d.POdet_displayOrder
        DROP TABLE #t;";

        try
        {
            await using var cmd = new SqlCommand(itemsSql, conn);
            cmd.Parameters.AddWithValue("@wid", wid);
            cmd.Parameters.AddWithValue("@poid", id.Value);
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
        catch { /* CTE query may fail if cross-DB refs are not available */ }

        var websiteName = _config["websiteNamewithExt"] ?? "CakerStreet";
        ViewBag.WebsiteName = websiteName;
        ViewBag.WebsiteUrl = _config["CdnBase"] ?? "/";

        return View("~/Views/PrintPurchaseOrder/Index.cshtml", model);
    }

    private static string GetStatusText(int status) => status switch
    {
        0 => "PO Pending Approvals",
        1 => "PO Approved by 1 dept",
        2 => "PO Approved fully",
        3 => "PO sent to supplier",
        4 => "PO Completed",
        _ => ""
    };
}

public class PrintPurchaseOrderModel
{
    public string InvoiceNo { get; set; } = "";
    public string PurchaseDate { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string AddressDetail { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string SuppliesTopper { get; set; } = "No";
    public string SuppliesAccessories { get; set; } = "No";
    public List<POLineItem> Items { get; set; } = new();
}

public class POLineItem
{
    public string BatchId { get; set; } = "";
    public string Location { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int Qty { get; set; }
    public decimal RatePerItem { get; set; }
    public decimal Amount { get; set; }
    public decimal Discount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatPercent { get; set; }
    public decimal Vat { get; set; }
    public decimal NetTotal { get; set; }
}
