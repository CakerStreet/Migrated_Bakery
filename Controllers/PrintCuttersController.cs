using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Print Cutters / Print Images page.
/// Route: /printcutters
/// Migrated from: printcutters.aspx / printcutters.aspx.cs
///
/// Displays a printable grid of product images for cutters (topper images).
/// Accepts product IDs, page-size, items-per-row, and image-box height as parameters
/// (passed via session in legacy; here via query string).
///
/// The legacy code stored a TempPrintCutter object in Session with:
///   - pid: List of "productId-count" strings
///   - pagesize, width, height, itemperrow
///
/// Uses stored procedure: USPGetCuttersProductsForPrint_new
///   Params: @prdids (TVP), @webstoreID, @PageNumber, @ProductsPerPage, @HowManyProducts (out)
///
/// Returns columns: product_code, product_name, product_image1, product_type, sizes, countprd, product_ID
/// </summary>
[Route("printcutters")]
public class PrintCuttersController : Controller
{
    private readonly IConfiguration _config;

    public PrintCuttersController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Renders the printable product cutters grid page.
    /// Products are specified via session "TempPrintCutter" data (set by another page),
    /// or via query-string parameters for a simplified flow.
    ///
    /// Query params (optional overrides):
    ///   pagesize (default 16), itemsperrow (default 4), height (default 176),
    ///   exclude (product ID-count to exclude), pid (comma-separated productId-count list)
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int pagesize = 16,
        [FromQuery] int itemsperrow = 4,
        [FromQuery] int height = 176,
        [FromQuery] string? exclude = null,
        [FromQuery] string? pid = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(webshopId))
            return Redirect("/businesslogin");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var customerWebsite = _config["customer_websiteLogo"] ?? "";
        ViewBag.CdnBase = cdnBase;
        ViewBag.CustomerWebsite = customerWebsite;

        // ── Build product ID list ──
        // Prefer query string pid, fall back to session
        List<string> pids;
        if (!string.IsNullOrWhiteSpace(pid))
        {
            pids = pid.Replace(" ", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else
        {
            // Try to read from session (legacy TempPrintCutter equivalent)
            var sessionPids = HttpContext.Session.GetString("TempPrintCutter_Pids");
            pids = !string.IsNullOrWhiteSpace(sessionPids)
                ? sessionPids.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();

            // Also read saved layout params from session
            var sp = HttpContext.Session.GetString("TempPrintCutter_PageSize");
            var si = HttpContext.Session.GetString("TempPrintCutter_ItemsPerRow");
            var sh = HttpContext.Session.GetString("TempPrintCutter_Height");
            if (!string.IsNullOrEmpty(sp) && int.TryParse(sp, out var ps)) pagesize = ps;
            if (!string.IsNullOrEmpty(si) && int.TryParse(si, out var ir)) itemsperrow = ir;
            if (!string.IsNullOrEmpty(sh) && int.TryParse(sh, out var ht)) height = ht;
        }

        // Exclude a product if requested (legacy removePrd command)
        if (!string.IsNullOrWhiteSpace(exclude))
        {
            pids = pids.Where(w => w != exclude).ToList();
        }

        // Save current state to session
        HttpContext.Session.SetString("TempPrintCutter_Pids", string.Join(",", pids));
        HttpContext.Session.SetString("TempPrintCutter_PageSize", pagesize.ToString());
        HttpContext.Session.SetString("TempPrintCutter_ItemsPerRow", itemsperrow.ToString());
        HttpContext.Session.SetString("TempPrintCutter_Height", height.ToString());

        ViewBag.PageSize = pagesize;
        ViewBag.ItemsPerRow = itemsperrow;
        ViewBag.Height = height;

        if (pids.Count == 0)
        {
            ViewBag.Products = new List<Dictionary<string, object>>();
            ViewBag.TotalCount = 0;
            return View("~/Views/PrintCutters/Index.cshtml");
        }

        // ── Build table-valued parameter for stored procedure ──
        var tvp = new DataTable();
        tvp.Columns.Add("prdID", typeof(long));
        tvp.Columns.Add("countprd", typeof(int));
        foreach (var s in pids)
        {
            var parts = s.Split('-');
            if (parts.Length >= 2
                && long.TryParse(parts[0], out var prdId)
                && int.TryParse(parts[1], out var count))
            {
                tvp.Rows.Add(prdId, count);
            }
        }

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        DataTable dtProducts;
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("USPGetCuttersProductsForPrint_new", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            var pPrdIds = cmd.Parameters.AddWithValue("@prdids", tvp);
            pPrdIds.SqlDbType = SqlDbType.Structured;
            cmd.Parameters.AddWithValue("@webstoreID", Convert.ToInt32(webshopId));
            cmd.Parameters.AddWithValue("@PageNumber", 1);
            cmd.Parameters.AddWithValue("@ProductsPerPage", pagesize);
            var pOut = cmd.Parameters.Add("@HowManyProducts", SqlDbType.Int);
            pOut.Direction = ParameterDirection.Output;

            var adapter = new SqlDataAdapter(cmd);
            dtProducts = new DataTable();
            adapter.Fill(dtProducts);
        }

        // Build products list
        var products = new List<Dictionary<string, object>>();
        foreach (DataRow r in dtProducts.Rows)
        {
            var item = new Dictionary<string, object>();
            foreach (DataColumn col in dtProducts.Columns)
                item[col.ColumnName] = r[col];

            // Determine image URL
            var image1 = Convert.ToString(r["product_image1"]);
            // In MVC we can't do File.Exists on the customer website physical path,
            // so just use the image path directly with a fallback in the view via onerror
            item["ImageUrl"] = !string.IsNullOrEmpty(image1)
                ? customerWebsite + "/upload/Product_Images/resized_300_300/" + image1
                : customerWebsite + "/images/blankImages/img75.jpg";

            // Parse sizes for product_type == 1
            var productType = Convert.ToString(r["product_type"]);
            if (productType == "1")
            {
                var sizesStr = Convert.ToString(r["sizes"]).Replace(" ", "");
                item["SizeList"] = sizesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                item["ShowSizes"] = true;
            }
            else
            {
                item["SizeList"] = new List<string>();
                item["ShowSizes"] = false;
            }

            products.Add(item);
        }

        ViewBag.Products = products;
        ViewBag.TotalCount = dtProducts.Rows.Count;

        return View("~/Views/PrintCutters/Index.cshtml");
    }

    /// <summary>
    /// POST handler for form submission (Submit / update layout params).
    /// Saves layout params + product IDs to session and redirects back.
    /// </summary>
    [HttpPost("")]
    public IActionResult Submit(
        [FromForm] int txtNoOfItems = 16,
        [FromForm] int txtItemsperRow = 4,
        [FromForm] int txtHeight = 176,
        [FromForm] string? txtPRdIDs = null)
    {
        var pids = !string.IsNullOrWhiteSpace(txtPRdIDs)
            ? txtPRdIDs.Replace(" ", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        HttpContext.Session.SetString("TempPrintCutter_Pids", string.Join(",", pids));
        HttpContext.Session.SetString("TempPrintCutter_PageSize", txtNoOfItems.ToString());
        HttpContext.Session.SetString("TempPrintCutter_ItemsPerRow", txtItemsperRow.ToString());
        HttpContext.Session.SetString("TempPrintCutter_Height", txtHeight.ToString());

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Updates the count for a specific product and redirects.
    /// Legacy: drpCountPrd_OnSelectedIndexChanged
    /// </summary>
    [HttpPost("updatecount")]
    public IActionResult UpdateCount([FromForm] string productId, [FromForm] int count)
    {
        var sessionPids = HttpContext.Session.GetString("TempPrintCutter_Pids") ?? "";
        var pids = sessionPids.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var updated = new List<string>();
        foreach (var item in pids)
        {
            var parts = item.Split('-');
            if (parts.Length >= 2 && parts[0] == productId)
                updated.Add(productId + "-" + count);
            else
                updated.Add(item);
        }

        HttpContext.Session.SetString("TempPrintCutter_Pids", string.Join(",", updated));
        return RedirectToAction(nameof(Index));
    }
}
