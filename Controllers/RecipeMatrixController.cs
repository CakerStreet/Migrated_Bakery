using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy manageReceipeMatrix.aspx / manageReceipeMatrix.aspx.cs.
    /// Displays a matrix grid of recipes vs ingredients, with checkmarks showing which
    /// recipe uses which ingredient. Supports filtering by book status and ingredient category,
    /// bulk delete, and AJAX-based ingredient cross-referencing.
    /// </summary>
    [Route("managereceipematrix")]
    [Route("manageReceipeMatrix.aspx")]
    public class RecipeMatrixController : Controller
    {
        private readonly IConfiguration _config;

        public RecipeMatrixController(IConfiguration config)
        {
            _config = config;
        }

        public class MatrixColumn
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }

        public class MatrixRow
        {
            public long ReceipeId { get; set; }
            public string Title { get; set; }
            public List<MatrixCell> Cells { get; set; } = new List<MatrixCell>();
        }

        public class MatrixCell
        {
            public int ColumnId { get; set; }
            public int Value { get; set; } // 0 or 1
        }

        public class CategoryItem { public long Id { get; set; } public string Name { get; set; } }

        [HttpGet]
        [Route("")]
        public IActionResult Index(int bookstatus = 0, int catid = 0, string search = null,
            string searchtags = null, string msg = null)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            if (bakeryId == "0")
                return Redirect("/editbusinessinfo");

            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

            if (!string.IsNullOrEmpty(msg))
                ViewBag.TopMessage = msg;

            // Load ingredient categories
            var ingredientCategories = new List<CategoryItem>();
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT category_ID, category_name FROM tbl_receipeIngredient_category ORDER BY category_name", conn))
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            ingredientCategories.Add(new CategoryItem { Id = rdr.GetInt64(0), Name = rdr.GetString(1) });
                    }
                }
            }

            // Build matrix query using stored proc or raw SQL matching legacy getreceipeMAtrix
            var columns = new List<MatrixColumn>();
            var rows = new List<MatrixRow>();
            int totalRec = 0;
            int totalCol = 0;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Get all active ingredient groups
                string ingWhere = "receipeBookIngredientGrp_active = 1";
                if (catid > 0)
                    ingWhere += " AND receipeBookIngredientGrp_ID IN (SELECT lnkIngCat_ingGrpID FROM tbl_lnkIngCat WHERE lnkIngCat_catID = " + catid + ")";

                var ingredientGroups = new List<MatrixColumn>();
                using (var cmd = new SqlCommand("SELECT receipeBookIngredientGrp_ID, receipeBookIngredientGrp_ingredient FROM tbl_receipeBookIngredientGrp WHERE " + ingWhere + " ORDER BY receipeBookIngredientGrp_ingredient", conn))
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            ingredientGroups.Add(new MatrixColumn
                            {
                                Id = Convert.ToInt32(rdr.GetInt64(0)),
                                Title = rdr.GetString(1)
                            });
                        }
                    }
                }
                totalCol = ingredientGroups.Count;
                columns = ingredientGroups;

                // Get recipes
                string recipeWhere = "r.receipeBookReceipe_isDeleted = 0 AND r.receipeBookReceipe_isActive = 1";
                if (bookstatus == 1)
                    recipeWhere += " AND r.receipeBookReceipe_bookID = 1"; // 444 Sandwich Book
                else if (bookstatus == 2)
                    recipeWhere += " AND r.receipeBookReceipe_bookID = 2"; // Salad recipes Book

                if (!string.IsNullOrEmpty(search))
                    recipeWhere += " AND r.receipeBookReceipe_title LIKE '%" + search.Replace("'", "''") + "%'";

                if (!string.IsNullOrEmpty(searchtags))
                {
                    string cleanTags = searchtags.Replace("|", ",").Replace("+", " ").Replace("\"", "");
                    if (!string.IsNullOrEmpty(cleanTags))
                        recipeWhere += " AND r.receipeBookReceipe_ID IN (SELECT receipeBookIngredient_receipeID FROM tbl_receipeBookIngredient INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID WHERE lnkIngredient2Grp_GrpID IN (" + cleanTags + "))";
                }

                // Get recipe IDs and titles
                var recipeList = new List<(long Id, string Title)>();
                using (var cmd = new SqlCommand("SELECT r.receipeBookReceipe_ID, r.receipeBookReceipe_title FROM tbl_receipeBookReceipe r WHERE " + recipeWhere + " ORDER BY r.receipeBookReceipe_title", conn))
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            recipeList.Add((rdr.GetInt64(0), rdr.IsDBNull(1) ? "" : rdr.GetString(1)));
                    }
                }
                totalRec = recipeList.Count;

                // For each recipe, get which ingredient groups are linked
                if (recipeList.Count > 0 && ingredientGroups.Count > 0)
                {
                    string recipeIds = string.Join(",", recipeList.Select(r => r.Id));
                    // Get all links: receipeID -> grpID
                    var links = new HashSet<string>();
                    using (var cmd = new SqlCommand(@"SELECT DISTINCT i.receipeBookIngredient_receipeID, l.lnkIngredient2Grp_GrpID 
                        FROM tbl_receipeBookIngredient i 
                        INNER JOIN tbl_lnkIngredient2Grp l ON l.lnkIngredient2Grp_ingID = i.receipeBookIngredient_ID
                        WHERE i.receipeBookIngredient_receipeID IN (" + recipeIds + ")", conn))
                    {
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                                links.Add(rdr.GetInt64(0) + "_" + rdr.GetInt64(1));
                        }
                    }

                    foreach (var recipe in recipeList)
                    {
                        var row = new MatrixRow { ReceipeId = recipe.Id, Title = recipe.Title };
                        foreach (var col in ingredientGroups)
                        {
                            row.Cells.Add(new MatrixCell
                            {
                                ColumnId = col.Id,
                                Value = links.Contains(recipe.Id + "_" + col.Id) ? 1 : 0
                            });
                        }
                        rows.Add(row);
                    }
                }
            }

            ViewBag.Columns = columns;
            ViewBag.Rows = rows;
            ViewBag.IngredientCategories = ingredientCategories;
            ViewBag.TotalRecords = totalRec;
            ViewBag.TotalColumns = totalCol;
            ViewBag.BookStatus = bookstatus;
            ViewBag.SelectedCatId = catid;
            ViewBag.Search = search;

            return View("~/Views/RecipeMatrix/Index.cshtml");
        }

        [HttpPost]
        [Route("deleterecipes")]
        public IActionResult DeleteRecipes([FromBody] List<long> ids)
        {
            if (ids == null || ids.Count == 0) return Json(new { success = false });
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string idList = string.Join(",", ids);
                using (var cmd = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isDeleted = 1 WHERE receipeBookReceipe_ID IN (" + idList + ")", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { success = true });
        }

        /// <summary>
        /// WebMethod replacement: finds recipes that contain ALL selected ingredient group IDs.
        /// </summary>
        [HttpPost]
        [Route("showreceipebyingids")]
        public IActionResult ShowReceipeByIngIDs([FromBody] string ids)
        {
            string strret = "";
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"SELECT DISTINCT receipeBookReceipe_ID FROM tbl_receipeBookReceipe 
                    WHERE receipeBookReceipe_ID IN (
                        SELECT receipeBookIngredient_receipeID FROM tbl_receipeBookIngredient 
                        INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID 
                        AND lnkIngredient2Grp_GrpID IN (" + ids + "))", conn))
                {
                    var idList = new List<string>();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            idList.Add(rdr.GetInt64(0).ToString());
                    }
                    strret = string.Join(",", idList);
                }
            }
            return Json(strret);
        }
    }
}
