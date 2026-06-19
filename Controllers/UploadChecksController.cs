using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Manages the upload, edit, download, and deletion of "other" checklist documents.
/// Each category from tbl_checklistCat can have multiple uploaded files.
///
/// Migrated from legacy WebForms page: addupduploadchecks.aspx / addupduploadchecks.aspx.cs
///
/// Legacy behaviour:
///   - Uses BakeryMaster.master and UpdatePanel for partial postback.
///   - BindUploadChecklist() loads categories from tbl_checklistCat (constr_staffAssessment).
///   - For each category row, a nested repeater (rpFiles) shows uploaded files from
///     tbl_checklistFileUploaded joined with tbl_bakeryuser for uploader / staff names.
///   - Upload form per category: FileUpload, Document Title, Document Date, Staff dropdown,
///     Remarks, Submit button.
///   - Submit handler (rpUploadedChecks_OnItemCommand) inserts or updates
///     tbl_checklistFileUploaded via EF (db_StaffAssessmentEntities).
///   - Download handlers serve files from ~/upload/haccp/docs/ and ~/upload/haccp/template/.
///   - Edit handler pre-populates the upload form from an existing record.
///   - Delete handler removes DB record and physical file.
///   - Staff dropdown populated from tbl_bakeryuser where customer_isActive=true,
///     customer_webshopID=82, customer_type=3.
///
/// Modern version:
///   - GET shows categories with uploaded files and upload forms.
///   - POST /submit handles file upload/update.
///   - POST /delete/{id} handles deletion.
///   - GET /downloadtemplate and /downloadfile serve file downloads.
///   - All DB access via raw ADO.NET.
/// </summary>
[Route("addupduploadchecks")]
[Route("addupduploadchecks.aspx")]
public class UploadChecksController : Controller
{
    private readonly IConfiguration _config;

    public UploadChecksController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Displays checklist categories with uploaded files and upload forms.
    /// </summary>
    [HttpGet]
    [Route("")]
    [Route("~/addupduploadchecks.aspx")]
    public async Task<IActionResult> Index([FromQuery] string msg = null, [FromQuery] long? editId = null)
    {
        try
        {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        DateTime today = DateTime.Today;

        var categories = new List<Dictionary<string, object>>();
        var categoryFiles = new Dictionary<long, List<Dictionary<string, object>>>();
        var staffList = new List<Dictionary<string, object>>();

        using (var con = new SqlConnection(connStr))
        {
            await con.OpenAsync();

            // Load categories
            using (var cmd = new SqlCommand("SELECT checklistCat_ID, checklistCat_title, checklistCat_file FROM tbl_checklistCat", con))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var cat = new Dictionary<string, object>
                        {
                            ["checklistCat_ID"] = reader.GetInt64(reader.GetOrdinal("checklistCat_ID")),
                            ["checklistCat_title"] = reader["checklistCat_title"]?.ToString() ?? "",
                            ["checklistCat_file"] = reader["checklistCat_file"]?.ToString() ?? ""
                        };
                        categories.Add(cat);
                    }
                }
            }

            // Load uploaded files per category
            foreach (var cat in categories)
            {
                long catId = Convert.ToInt64(cat["checklistCat_ID"]);
                categoryFiles[catId] = await GetUploadedRecordsByCatId(con, catId);
            }

            // Load staff list
            string staffQuery = @"SELECT customer_ID, customer_Name FROM tbl_bakeryuser 
WHERE customer_isActive = 1 AND customer_webshopID = 82 AND customer_type = 3";
            using (var cmd = new SqlCommand(staffQuery, con))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        staffList.Add(new Dictionary<string, object>
                        {
                            ["customer_ID"] = reader.GetInt64(reader.GetOrdinal("customer_ID")),
                            ["customer_Name"] = reader["customer_Name"]?.ToString() ?? ""
                        });
                    }
                }
            }
        }

        // If editing, load the record
        Dictionary<string, object> editItem = null;
        if (editId.HasValue && editId.Value > 0)
        {
            using (var con = new SqlConnection(connStr))
            {
                await con.OpenAsync();
                string editQuery = @"SELECT checklistFileUploaded_ID, checklistFileUploaded_catID,
checklistFileUploaded_file, checklistFileUploaded_filetitle, checklistFileUploaded_filedate,
checklistFileUploaded_staffID, checklistFileUploaded_remarks
FROM tbl_checklistFileUploaded WHERE checklistFileUploaded_ID = @id";
                using (var cmd = new SqlCommand(editQuery, con))
                {
                    cmd.Parameters.AddWithValue("@id", editId.Value);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            editItem = new Dictionary<string, object>
                            {
                                ["checklistFileUploaded_ID"] = reader.GetInt64(reader.GetOrdinal("checklistFileUploaded_ID")),
                                ["checklistFileUploaded_catID"] = reader.GetInt32(reader.GetOrdinal("checklistFileUploaded_catID")),
                                ["checklistFileUploaded_file"] = reader["checklistFileUploaded_file"]?.ToString() ?? "",
                                ["checklistFileUploaded_filetitle"] = reader["checklistFileUploaded_filetitle"]?.ToString() ?? "",
                                ["checklistFileUploaded_filedate"] = reader["checklistFileUploaded_filedate"] != DBNull.Value ? Convert.ToDateTime(reader["checklistFileUploaded_filedate"]) : DateTime.Now,
                                ["checklistFileUploaded_staffID"] = reader["checklistFileUploaded_staffID"] != DBNull.Value ? Convert.ToInt32(reader["checklistFileUploaded_staffID"]) : 0,
                                ["checklistFileUploaded_remarks"] = reader["checklistFileUploaded_remarks"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }
        }

        ViewBag.TodayDate = today;
        ViewBag.Categories = categories;
        ViewBag.CategoryFiles = categoryFiles;
        ViewBag.StaffList = staffList;
        ViewBag.SuccessMessage = msg;
        ViewBag.EditItem = editItem;
        ViewBag.UserType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";

        return View("~/Views/UploadChecks/Index.cshtml");
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Upload Checks";
            ViewBag.MissingTable = "tbl_checklistCat";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>
    /// Handles file upload/update submission for a category.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        [FromForm] long checklistFileUploadedId,
        [FromForm] int checklistCatId,
        [FromForm] int staffId,
        [FromForm] string fileTitle,
        [FromForm] string docDate,
        [FromForm] string remarks,
        IFormFile fileUpload)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "docs");

        DateTime fileDate = DateTime.Now;
        if (!string.IsNullOrEmpty(docDate))
        {
            DateTime.TryParse(docDate, out fileDate);
        }

        string fileName = "";
        string oldFile = "";

        // Get old file if editing
        if (checklistFileUploadedId > 0)
        {
            using (var con = new SqlConnection(connStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand("SELECT checklistFileUploaded_file FROM tbl_checklistFileUploaded WHERE checklistFileUploaded_ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", checklistFileUploadedId);
                    var result = await cmd.ExecuteScalarAsync();
                    oldFile = result?.ToString() ?? "";
                }
            }
        }

        // Handle file upload
        if (fileUpload != null && fileUpload.Length > 0)
        {
            // Delete old file if exists
            if (!string.IsNullOrEmpty(oldFile))
            {
                string oldPath = Path.Combine(uploadDir, oldFile);
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            string safeTitle = string.IsNullOrWhiteSpace(fileTitle) ? fileUpload.FileName : fileTitle;
            string safeBase = System.Text.RegularExpressions.Regex.Replace(safeTitle.ToLower(), @"[^a-z0-9_.\-]", "-");
            fileName = $"{safeBase}-{DateTime.Now.Ticks}{Path.GetExtension(fileUpload.FileName)}";

            string filePath = Path.Combine(uploadDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await fileUpload.CopyToAsync(stream);
            }

            if (string.IsNullOrEmpty(fileTitle))
                fileTitle = fileUpload.FileName;
        }
        else
        {
            fileName = oldFile;
            if (string.IsNullOrEmpty(fileTitle))
                fileTitle = oldFile;
        }

        using (var con = new SqlConnection(connStr))
        {
            await con.OpenAsync();

            if (checklistFileUploadedId > 0)
            {
                // Update existing record
                string updateQuery = @"UPDATE tbl_checklistFileUploaded SET
checklistFileUploaded_byID = @byID,
checklistFileUploaded_file = @file,
checklistFileUploaded_filedate = @filedate,
checklistFileUploaded_filetitle = @filetitle,
checklistFileUploaded_remarks = @remarks,
checklistFileUploaded_staffID = @staffID,
checklistFileUploaded_modifiedOn = @modifiedOn
WHERE checklistFileUploaded_ID = @id";
                using (var cmd = new SqlCommand(updateQuery, con))
                {
                    cmd.Parameters.AddWithValue("@byID", userId);
                    cmd.Parameters.AddWithValue("@file", fileName);
                    cmd.Parameters.AddWithValue("@filedate", fileDate);
                    cmd.Parameters.AddWithValue("@filetitle", fileTitle ?? "");
                    cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
                    cmd.Parameters.AddWithValue("@staffID", staffId);
                    cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@id", checklistFileUploadedId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // Insert new record
                string insertQuery = @"INSERT INTO tbl_checklistFileUploaded
(checklistFileUploaded_catID, checklistFileUploaded_byID, checklistFileUploaded_file,
checklistFileUploaded_filedate, checklistFileUploaded_filetitle, checklistFileUploaded_remarks,
checklistFileUploaded_staffID, checklistFileUploaded_createdon, checklistFileUploaded_modifiedOn)
VALUES (@catID, @byID, @file, @filedate, @filetitle, @remarks, @staffID, @createdon, @modifiedOn)";
                using (var cmd = new SqlCommand(insertQuery, con))
                {
                    cmd.Parameters.AddWithValue("@catID", checklistCatId);
                    cmd.Parameters.AddWithValue("@byID", userId);
                    cmd.Parameters.AddWithValue("@file", fileName);
                    cmd.Parameters.AddWithValue("@filedate", fileDate);
                    cmd.Parameters.AddWithValue("@filetitle", fileTitle ?? "");
                    cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
                    cmd.Parameters.AddWithValue("@staffID", staffId);
                    cmd.Parameters.AddWithValue("@createdon", DateTime.Now);
                    cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        return Redirect("/addupduploadchecks?msg=" + Uri.EscapeDataString("Cleaning Checklist has been saved successfully"));
    }

    /// <summary>
    /// Deletes an uploaded checklist file record and its physical file.
    /// </summary>
    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
        string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "docs");

        using (var con = new SqlConnection(connStr))
        {
            await con.OpenAsync();

            // Get file name before deleting
            string fileName = "";
            using (var cmd = new SqlCommand("SELECT checklistFileUploaded_file FROM tbl_checklistFileUploaded WHERE checklistFileUploaded_ID = @id", con))
            {
                cmd.Parameters.AddWithValue("@id", id);
                var result = await cmd.ExecuteScalarAsync();
                fileName = result?.ToString() ?? "";
            }

            // Delete physical file
            if (!string.IsNullOrEmpty(fileName))
            {
                string filePath = Path.Combine(uploadDir, fileName);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            // Delete DB record
            using (var cmd = new SqlCommand("DELETE FROM tbl_checklistFileUploaded WHERE checklistFileUploaded_ID = @id", con))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        return Redirect("/addupduploadchecks?msg=" + Uri.EscapeDataString("Document has been deleted successfully"));
    }

    /// <summary>
    /// Downloads a sample/template file from upload/haccp/template/.
    /// </summary>
    [HttpGet("downloadtemplate")]
    public IActionResult DownloadTemplate([FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return BadRequest("No file specified.");

        string filepath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "template", file);
        if (!System.IO.File.Exists(filepath))
            return NotFound("File not found.");

        return PhysicalFile(filepath, "application/pdf", file);
    }

    /// <summary>
    /// Downloads an uploaded staff file from upload/haccp/docs/.
    /// </summary>
    [HttpGet("downloadfile")]
    public IActionResult DownloadFile([FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return BadRequest("No file specified.");

        string filepath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload", "haccp", "docs", file);
        if (!System.IO.File.Exists(filepath))
            return NotFound("File not found.");

        return PhysicalFile(filepath, "application/pdf", file);
    }

    private async Task<List<Dictionary<string, object>>> GetUploadedRecordsByCatId(SqlConnection con, long catId)
    {
        var files = new List<Dictionary<string, object>>();
        string query = @"SELECT f.checklistFileUploaded_ID, f.checklistFileUploaded_remarks, 
f.checklistFileUploaded_createdon, f.checklistFileUploaded_modifiedOn, checklistFileUploaded_file,
f.checklistFileUploaded_filetitle, f.checklistFileUploaded_filedate,
Uploadby_CustName = bu.customer_Name, StaffName = s.customer_Name 
FROM tbl_checklistFileUploaded f 
LEFT OUTER JOIN db_Cakerstreet_live.dbo.tbl_bakeryuser bu ON f.checklistFileUploaded_byID = bu.customer_ID
LEFT OUTER JOIN db_Cakerstreet_live.dbo.tbl_bakeryuser s ON f.checklistFileUploaded_staffID = s.customer_ID
WHERE f.checklistFileUploaded_catID = @catid 
ORDER BY checklistFileUploaded_filedate DESC, checklistFileUploaded_filetitle";

        using (var cmd = new SqlCommand(query, con))
        {
            cmd.Parameters.AddWithValue("@catid", catId);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    files.Add(new Dictionary<string, object>
                    {
                        ["checklistFileUploaded_ID"] = reader.GetInt64(reader.GetOrdinal("checklistFileUploaded_ID")),
                        ["checklistFileUploaded_file"] = reader["checklistFileUploaded_file"]?.ToString() ?? "",
                        ["checklistFileUploaded_filetitle"] = reader["checklistFileUploaded_filetitle"]?.ToString() ?? "",
                        ["checklistFileUploaded_filedate"] = reader["checklistFileUploaded_filedate"] != DBNull.Value ? Convert.ToDateTime(reader["checklistFileUploaded_filedate"]) : (object)DBNull.Value,
                        ["checklistFileUploaded_remarks"] = reader["checklistFileUploaded_remarks"]?.ToString() ?? ""
                    });
                }
            }
        }

        return files;
    }
}
