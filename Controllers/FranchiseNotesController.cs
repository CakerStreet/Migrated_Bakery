using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays franchise notes for a specific product/franchise item with real-time note submission.
/// Standalone page (no sidebar layout). Renders its own HTML with Bootstrap.
/// Migrated from legacy franchisenotes.aspx / franchisenotes.aspx.cs.
/// Route: /franchisenotes/{id}
/// </summary>
public class FranchiseNotesController : Controller
{
    private readonly IConfiguration _config;

    public FranchiseNotesController(IConfiguration config)
    {
        _config = config;
    }

    private string ConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";
    private string InvConnStr => _config["ConnectionStrings:InventoryManagement"] ?? ConnStr;

    [HttpGet("franchisenotes/{id}")]
    [HttpGet("franchisenotes.aspx")]
    public async Task<IActionResult> Index(long id)
    {
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var custWebsite = _config["CustomerWebsiteLogo"] ?? cdnBase;

        string productName = "", productImage = "", productCode = "", categoryTitle = "", prdStatus = "";
        string imgSrc = custWebsite + "/images/blankImages/img75.jpg";

        // Load product info
        await using var conn = new SqlConnection(InvConnStr);
        await conn.OpenAsync();

        var sql = @"SELECT product_name, product_image1, product_code, c.Title,
            prd_status = case when l.Ordered = 0 then '<font color=''red''>Pending</font>'
            when l.Ordered = 1 and l.Delivered = 0 then '<font color=''orange''>Under Delivery</font>'
            when l.Delivered = 1 then '<font color=''green''>Delivered</font>' else '' end
            FROM db_cakerstreet_live.dbo.tbl_products p
            INNER JOIN tbl_lnkItem2tempfranchise l ON p.product_ID = l.ProductID
            INNER JOIN tbl_tempFranchiseCat c ON l.tempFranchise_CatId = c.ID
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_ProductSupplier s ON l.SupplierId = s.SupplierId
            LEFT OUTER JOIN db_cakerstreet_live.dbo.tbl_Product_Supplier_Linking lnk ON lnk.SupplierId = s.SupplierId AND lnk.Product_Id = l.ProductID
            WHERE l.id = @id";

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                productName = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                string img1 = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                productCode = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                categoryTitle = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                prdStatus = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                if (!string.IsNullOrEmpty(img1))
                    imgSrc = custWebsite + "/upload/Product_Images/resized_80_80/" + img1;
            }
        }

        // Load notes
        var notes = new List<NoteItem>();
        var sqlNotes = @"SELECT tempFranchiseNotes_ID, tempFranchiseNotes_custname, tempFranchiseNotes_Remarks, tempFranchiseNotes_modifiedOn
                         FROM tbl_tempFranchiseNotes
                         WHERE lnkItem2tempfranchise_ID = @id
                         ORDER BY tempFranchiseNotes_modifiedOn DESC";
        await using (var cmd = new SqlCommand(sqlNotes, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                notes.Add(new NoteItem
                {
                    NoteId = rdr.GetInt64(0),
                    CustName = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                    Remarks = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    ModifiedOn = rdr.GetDateTime(3)
                });
            }
        }

        // Determine user type for dropdown default
        var franchiseId = HttpContext.Items["FranchiseId"]?.ToString() ?? "0";
        string selectedNoteType = franchiseId == "0" ? "1" : "2";

        ViewBag.CrfId = id;
        ViewBag.ImgSrc = imgSrc;
        ViewBag.ProductDesc = $"<b>#{productName}</b><br/>#{productCode}<br/>{categoryTitle}<br/>{prdStatus}";
        ViewBag.Notes = notes;
        ViewBag.NoRecords = notes.Count == 0;
        ViewBag.SelectedNoteType = selectedNoteType;

        return View("~/Views/FranchiseNotes/Index.cshtml");
    }

    [HttpPost("franchisenotes/addnote")]
    public async Task<IActionResult> AddNote([FromForm] long crfId, [FromForm] string remarks, [FromForm] int noteType)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "User";

        await using var conn = new SqlConnection(InvConnStr);
        await conn.OpenAsync();

        var sql = @"INSERT INTO tbl_tempFranchiseNotes
            (lnkItem2tempfranchise_ID, tempFranchiseNotes_custname, tempFranchiseNotes_Remarks, tempFranchiseNotes_modifiedOn)
            VALUES (@cid, @name, @remarks, @now)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", crfId);
        cmd.Parameters.AddWithValue("@name", userName);
        cmd.Parameters.AddWithValue("@remarks", remarks);
        cmd.Parameters.AddWithValue("@now", DateTime.Now);
        await cmd.ExecuteNonQueryAsync();

        return Redirect($"/franchisenotes/{crfId}");
    }

    public class NoteItem
    {
        public long NoteId { get; set; }
        public string CustName { get; set; } = "";
        public string Remarks { get; set; } = "";
        public DateTime ModifiedOn { get; set; }
    }
}
