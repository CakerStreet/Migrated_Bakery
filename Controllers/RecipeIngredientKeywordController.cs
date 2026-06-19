using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy managereceipeIngredient_keywords.aspx / managereceipeIngredient_keywords.aspx.cs.
    /// Find and replace ingredient text across all recipes. Displays a grid of previous replacements
    /// (from tbl_receipeReplace), supports adding new find/replace operations and deleting records.
    /// </summary>
    [Route("managereceipe-findandreplace")]
    [Route("managereceipeIngredient_keywords.aspx")]
    public class RecipeIngredientKeywordController : Controller
    {
        private readonly IConfiguration _config;

        public RecipeIngredientKeywordController(IConfiguration config)
        {
            _config = config;
        }

        public class ReplaceRecord
        {
            public long RedirectID { get; set; }
            public string SourceUrl { get; set; }
            public string DestinationUrl { get; set; }
            public DateTime CreatedOn { get; set; }
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index(string searchKeyword = null, int page = 1)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            int pageSize = 20;

            var records = new List<ReplaceRecord>();
            int totalRec = 0;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string whereClause = "1 = 1";
                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    whereClause = "(SourceUrl LIKE @search OR DestinationUrl LIKE @search)";
                }

                // Count
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM tbl_receipeReplace WHERE " + whereClause, conn))
                {
                    if (!string.IsNullOrEmpty(searchKeyword))
                        cmd.Parameters.AddWithValue("@search", "%" + searchKeyword + "%");
                    totalRec = (int)cmd.ExecuteScalar();
                }

                // Data
                string sql = "SELECT RedirectID, SourceUrl, DestinationUrl, CreatedOn FROM tbl_receipeReplace WHERE " + whereClause + " ORDER BY CreatedOn DESC OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(searchKeyword))
                        cmd.Parameters.AddWithValue("@search", "%" + searchKeyword + "%");
                    cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            records.Add(new ReplaceRecord
                            {
                                RedirectID = rdr.GetInt64(0),
                                SourceUrl = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                                DestinationUrl = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                                CreatedOn = rdr.GetDateTime(3)
                            });
                        }
                    }
                }
            }

            ViewBag.Records = records;
            ViewBag.TotalRecords = totalRec;
            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            return View("~/Views/RecipeIngredientKeyword/Index.cshtml");
        }

        [HttpPost]
        [Route("save")]
        public IActionResult Save(string txtSourceUrl, string txtDestinationUrl)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            int replacedCount = 0;

            string fromText = txtSourceUrl?.ToLower().Trim() ?? "";
            string toText = txtDestinationUrl ?? "";

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Find and replace in ingredient text (matching legacy cls301Redirect.findandreplaceIngredient)
                using (var cmd = new SqlCommand(@"UPDATE tbl_receipeBookIngredient 
                    SET receipeBookIngredient_Ingredient = REPLACE(receipeBookIngredient_Ingredient, @fromText, @toText) 
                    WHERE receipeBookIngredient_Ingredient LIKE '%' + @fromText + '%'", conn))
                {
                    cmd.Parameters.AddWithValue("@fromText", fromText);
                    cmd.Parameters.AddWithValue("@toText", toText);
                    replacedCount = cmd.ExecuteNonQuery();
                }

                // Log the replacement
                using (var cmd2 = new SqlCommand(@"INSERT INTO tbl_receipeReplace (SourceUrl, DestinationUrl, CreatedOn, CreatedBy) 
                    VALUES (@source, @dest, @created, @createdBy)", conn))
                {
                    cmd2.Parameters.AddWithValue("@source", fromText);
                    cmd2.Parameters.AddWithValue("@dest", toText);
                    cmd2.Parameters.AddWithValue("@created", DateTime.Now);
                    cmd2.Parameters.AddWithValue("@createdBy", bakeryId);
                    cmd2.ExecuteNonQuery();
                }
            }

            TempData["Message"] = replacedCount + " Records replaced Successfully";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Route("delete")]
        public IActionResult Delete([FromForm] string selectedIds)
        {
            if (string.IsNullOrEmpty(selectedIds)) return RedirectToAction("Index");

            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                // selectedIds is comma-separated
                using (var cmd = new SqlCommand("DELETE FROM tbl_receipeReplace WHERE RedirectID IN (" + selectedIds + ")", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["Message"] = "Record(s) Deleted Successfully";
            return RedirectToAction("Index");
        }
    }
}
