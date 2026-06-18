using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Franchise Stock Checklist print page.
/// Route: /printfranchisechecklist?checklistID={id}
/// Migrated from: printFranchiseChecklist.aspx / printFranchiseChecklist.aspx.cs
///
/// Displays a printable stock/sponge checklist for a franchise location.
/// The legacy code called clsMail.GetranchiseChecklistdata_inmail() which:
///   1. Loads StockBatch (header) joined with domains table
///   2. Loads StockPrd (items) joined with lnk_prd_domain, products, CakeSize
///   3. Renders an HTML table with checklist header info + product rows
///
/// This migration replicates the same queries using raw ADO.NET and builds
/// the model data for the Razor view to render (instead of building raw HTML).
///
/// Tables:
///   - StockBatch (stockBatch_ID, stockBatch_domainID, stockBatch_Remarks,
///                 stockBatch_title, stockBatch_ReqQty, stockBatch_Date, stockBatch_Name)
///   - domains (domain_ID, domain_Name) — from EposAdmin DB
///   - StockPrd (stockPrd_batchID, stockPrd_prdID, stockPrd_sizeID, stockPrd_reqqty, stockPrd_qty)
///   - lnk_prd_domain (domain_ID, product_ID, product_displayorder)
///   - tbl_products / products (product_ID, product_Name, product_image1, product_saletype, product_type)
///   - CakeSize (SizeID, SizeTitle)
/// </summary>
[Route("printfranchisechecklist")]
public class PrintFranchiseChecklistController : Controller
{
    private readonly IConfiguration _config;

    public PrintFranchiseChecklistController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Renders the printable franchise stock checklist.
    /// Legacy route: /printFranchiseChecklist?checklistID={id}
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? checklistID)
    {
        if (!checklistID.HasValue)
            return BadRequest("checklistID is required.");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var eposWebsiteUrl = _config["Epos_websiteURL"] ?? "";
        ViewBag.CdnBase = cdnBase;
        ViewBag.EposWebsiteUrl = eposWebsiteUrl;

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        long batchId = checklistID.Value;

        // ── Query 1: StockBatch header + domain name ──
        DataTable dtBatch;
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            var sql = @"SELECT sb.stockBatch_ID, sb.stockBatch_domainID,
                               sb.stockBatch_Remarks, sb.stockBatch_title,
                               sb.stockBatch_ReqQty, sb.stockBatch_Date,
                               sb.stockBatch_Name, d.domain_Name
                        FROM StockBatch sb
                        INNER JOIN domains d ON sb.stockBatch_domainID = d.domain_ID
                        WHERE sb.stockBatch_ID = @BatchId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BatchId", batchId);
            var adapter = new SqlDataAdapter(cmd);
            dtBatch = new DataTable();
            adapter.Fill(dtBatch);
        }

        if (dtBatch.Rows.Count == 0)
            return NotFound("Checklist not found.");

        var batchRow = dtBatch.Rows[0];
        ViewBag.DomainName = Convert.ToString(batchRow["domain_Name"]);
        ViewBag.ChecklistTitle = Convert.ToString(batchRow["stockBatch_title"]);
        ViewBag.TotalReqQty = Convert.ToString(batchRow["stockBatch_ReqQty"]);
        ViewBag.ChecklistDate = Convert.ToDateTime(batchRow["stockBatch_Date"]).ToLongDateString();
        ViewBag.ChecklistMadeBy = Convert.ToString(batchRow["stockBatch_Name"]);
        ViewBag.Remarks = Convert.ToString(batchRow["stockBatch_Remarks"]);

        long domainId = Convert.ToInt64(batchRow["stockBatch_domainID"]);

        // ── Query 2: Stock items with product info and sizes ──
        DataTable dtItems;
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            var sql = @"SELECT sp.stockPrd_prdID, sp.stockPrd_reqqty, sp.stockPrd_qty,
                               CASE WHEN sp.stockPrd_reqqty > 0 THEN 1 ELSE 0 END AS qtyWise,
                               p.product_image1,
                               p.product_Name + ISNULL(' - ' + cs.SizeTitle, '') AS product_Name,
                               lpd.product_displayorder
                        FROM StockPrd sp
                        INNER JOIN lnk_prd_domain lpd ON sp.stockPrd_prdID = lpd.product_ID
                                                      AND lpd.domain_ID = @DomainId
                        INNER JOIN tbl_products p ON sp.stockPrd_prdID = p.product_ID
                                                  AND p.product_saletype = 2
                                                  AND p.product_type = 3
                        LEFT JOIN CakeSize cs ON sp.stockPrd_sizeID = cs.SizeID
                        WHERE sp.stockPrd_batchID = @BatchId
                          AND sp.stockPrd_reqqty > 0
                        ORDER BY CASE WHEN sp.stockPrd_reqqty > 0 THEN 1 ELSE 0 END DESC,
                                 lpd.product_displayorder ASC";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BatchId", batchId);
            cmd.Parameters.AddWithValue("@DomainId", domainId);
            var adapter = new SqlDataAdapter(cmd);
            dtItems = new DataTable();
            adapter.Fill(dtItems);
        }

        // Build items list for the view
        var items = new List<Dictionary<string, object>>();
        foreach (DataRow r in dtItems.Rows)
        {
            var item = new Dictionary<string, object>();
            foreach (DataColumn col in dtItems.Columns)
                item[col.ColumnName] = r[col];

            // Build image URL
            var img = Convert.ToString(r["product_image1"]);
            item["ImageUrl"] = !string.IsNullOrEmpty(img)
                ? eposWebsiteUrl + "/upload/Product_images/resized_80_80/" + img
                : "";

            items.Add(item);
        }

        ViewBag.Items = items;

        return View("~/Views/PrintFranchiseChecklist/Index.cshtml");
    }
}
