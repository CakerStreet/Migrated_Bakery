using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy managereceipe_filter.aspx / managereceipe_filter.aspx.cs.
    /// Standalone filter page (no sidebar layout) that displays recipe list filtered by category/tag
    /// with paging. Shows ingredients inline like a print-preview with filter dropdowns.
    /// </summary>
    [Route("managereceipe-filter")]
    [Route("managereceipe_filter.aspx")]
    public class RecipeFilterController : Controller
    {
        private readonly IConfiguration _config;

        public RecipeFilterController(IConfiguration config)
        {
            _config = config;
        }

        public class ReceipeCatItem { public long Id { get; set; } public string Name { get; set; } }
        public class PageInfo { public string PageNo { get; set; } public string PageCss { get; set; } }

        [HttpGet]
        [Route("")]
        public IActionResult Index(int pageno = 1, string search = null, int filterstatus = 0,
            int cookingstatus = 0, int catid = 0, int receipecatid = 0, int receipetagid = 0,
            int ID = 0, string searchtags = null, string strIDs = "0")
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

            if (!string.IsNullOrEmpty(search))
                search = search.Replace("$", "#");

            string cleanSearchTags = "";
            if (!string.IsNullOrEmpty(searchtags))
                cleanSearchTags = searchtags.Replace("|", ",").Replace("+", " ").Replace("\"", "");

            // Load category/tag dropdowns
            var categories = new List<ReceipeCatItem>();
            var tags = new List<ReceipeCatItem>();
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT receipeCat_ID, receipeCat_categoryName, receipe_catType, receipeCat_parentID FROM tbl_receipeCat WHERE receipeCat_isActive = 1 AND receipeCat_isDeleted = 0 ORDER BY receipeCat_displayOrder", conn))
                {
                    var allCats = new List<dynamic>();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            allCats.Add(new { Id = rdr.GetInt64(0), Name = rdr.GetString(1), CatType = rdr.GetInt32(2), ParentID = rdr.GetInt64(3) });
                    }
                    foreach (var li in allCats.Where(c => c.ParentID == 0 && c.CatType == 1))
                    {
                        categories.Add(new ReceipeCatItem { Id = li.Id, Name = li.Name });
                        foreach (var inner in allCats.Where(c => c.ParentID == li.Id && c.CatType == 1))
                            categories.Add(new ReceipeCatItem { Id = inner.Id, Name = "-->> " + inner.Name });
                    }
                    foreach (var li in allCats.Where(c => c.ParentID == 0 && c.CatType == 2))
                        tags.Add(new ReceipeCatItem { Id = li.Id, Name = li.Name });
                }
            }

            // Build WHERE clause
            int pageSize = 50;
            string whereClause = "r.receipeBookReceipe_isDeleted = 0";

            if (filterstatus == 1) whereClause += " AND r.receipeBookReceipe_isActive = 1";
            else if (filterstatus == 2) whereClause += " AND r.receipeBookReceipe_isActive = 0";
            if (cookingstatus == 1) whereClause += " AND r.receipeBookReceipe_isCooking = 1";
            else if (cookingstatus == 2) whereClause += " AND r.receipeBookReceipe_isCooking = 0";
            if (catid > 0) whereClause += " AND r.receipeBookReceipe_bookID = " + catid;
            if (ID > 0) whereClause += " AND r.receipeBookReceipe_ID = " + ID;
            if (receipecatid > 0) whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipecatid + ")";
            if (receipetagid > 0) whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipetagid + ")";
            if (!string.IsNullOrEmpty(search)) whereClause += " AND r.receipeBookReceipe_title LIKE @search";
            if (!string.IsNullOrEmpty(cleanSearchTags))
                whereClause += " AND r.receipeBookReceipe_ID IN (SELECT receipeBookIngredient_receipeID FROM tbl_receipeBookIngredient INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID WHERE lnkIngredient2Grp_GrpID IN (" + cleanSearchTags + "))";

            int totalRec = 0;
            var recipeData = new DataTable();
            var ingredientData = new DataTable();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Count
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM tbl_receipeBookReceipe r WHERE " + whereClause, conn))
                {
                    if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                    totalRec = (int)cmd.ExecuteScalar();
                }

                // Fetch recipes with paging
                string dataSql = "SELECT r.receipeBookReceipe_ID, r.receipeBookReceipe_title, r.receipeBookReceipe_serving FROM tbl_receipeBookReceipe r WHERE " + whereClause + " ORDER BY r.receipeBookReceipe_modifiedOn DESC OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                    cmd.Parameters.AddWithValue("@offset", (pageno - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    using (var da = new SqlDataAdapter(cmd)) { da.Fill(recipeData); }
                }

                // Fetch ingredients for those recipes
                if (recipeData.Rows.Count > 0)
                {
                    string recipeIds = string.Join(",", recipeData.AsEnumerable().Select(r => r["receipeBookReceipe_ID"].ToString()));
                    string ingSql = @"SELECT i.receipeBookIngredient_receipeID, i.receipeBookIngredient_displayorder,
                        ISNULL(g.receipeBookIngredientGrp_ingredient,'') AS receipeBookIngredientGrp_ingredient,
                        ISNULL(g.receipeBookIngredientGrp_active,1) AS receipeBookIngredientGrp_active,
                        ISNULL(g.receipeBookIngredientGrp_Img,'') AS receipeBookIngredientGrp_Img
                        FROM tbl_receipeBookIngredient i 
                        LEFT JOIN tbl_lnkIngredient2Grp l ON l.lnkIngredient2Grp_ingID = i.receipeBookIngredient_ID
                        LEFT JOIN tbl_receipeBookIngredientGrp g ON l.lnkIngredient2Grp_GrpID = g.receipeBookIngredientGrp_ID
                        WHERE i.receipeBookIngredient_typeID = 1 AND i.receipeBookIngredient_receipeID IN (" + recipeIds + ")";
                    using (var cmd2 = new SqlCommand(ingSql, conn))
                    {
                        using (var da = new SqlDataAdapter(cmd2)) { da.Fill(ingredientData); }
                    }
                }
            }

            // Build HTML
            StringBuilder sbMain = new StringBuilder();
            foreach (DataRow drmain in recipeData.Rows)
            {
                string recipeId = drmain["receipeBookReceipe_ID"].ToString();
                StringBuilder sbIngs = new StringBuilder();
                var ingRows = ingredientData.Select("receipeBookIngredient_receipeID=" + recipeId, "receipeBookIngredientGrp_active desc,receipeBookIngredient_displayorder");
                foreach (DataRow dr in ingRows)
                {
                    string actClass = bool.Parse(dr["receipeBookIngredientGrp_active"].ToString()) ? "act" : "nact";
                    sbIngs.Append("<li class=\"ligrp cuttype1 " + actClass + "\" style=\"background-image:url('/upload/receipeBookIngredient/resized_80_80/" + dr["receipeBookIngredientGrp_Img"] + "')\"><span class=\"ingtext\"><font color=\"#f55\">#</font>" + dr["receipeBookIngredientGrp_ingredient"] + "</span></li>");
                }

                string inner = "<div class=\"div_reciepe col-sm-12 flush\" style='page-break-inside:avoid;'>" +
                    "<h3>" + drmain["receipeBookReceipe_title"] + "<span class='serving'>Serving: " + drmain["receipeBookReceipe_serving"] + "</span></h3>" +
                    "<div class=\"divIngredient col-sm-12 flush\"><h5><u>Ingredients:</u></h5><div class=\"divIngredientlistOuter col-sm-12 flush\"><ul >" + sbIngs + "</ul></div></div></div></div></div>";
                sbMain.Append(inner);
            }

            // Paging
            int totalPages = (int)Math.Ceiling((double)totalRec / pageSize);
            var pages = new List<PageInfo>();
            if (totalPages > 1)
            {
                int pbCount = 3;
                int min = pageno - pbCount;
                int max = pageno + pbCount;
                if (max > totalPages) min -= max - totalPages;
                else if (min < 1) max += 1 - min;
                for (int i = 1; i <= totalPages; i++)
                {
                    if (i <= 2 || i > totalPages - 2 || (min <= i && i <= max))
                        pages.Add(new PageInfo { PageNo = i.ToString(), PageCss = (i == pageno) ? "active" : "" });
                    else if (pages.Count == 0 || pages.Last().PageNo != "...")
                        pages.Add(new PageInfo { PageNo = "...", PageCss = "disabled" });
                }
            }

            ViewBag.Categories = categories;
            ViewBag.Tags = tags;
            ViewBag.SelectedReceipeCatId = receipecatid;
            ViewBag.SelectedReceipeTagId = receipetagid;
            ViewBag.TotalRecords = totalRec;
            ViewBag.PrintContent = sbMain.ToString();
            ViewBag.Pages = pages;
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = pageno;

            return View("~/Views/RecipeFilter/Index.cshtml");
        }
    }
}
