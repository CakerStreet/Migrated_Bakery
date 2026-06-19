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
    /// Migrated from legacy manageReceipe.aspx / manageReceipe.aspx.cs.
    /// Lists all recipes with paging, filtering by status/cooking/book/category/tag,
    /// search, bulk actions (active/inactive/delete/update/print), inline edit of title/price,
    /// assign categories/tags modal, and Show/Hide recipe detail via AJAX WebMethod calls.
    /// </summary>
    [Route("managereceipe")]
    [Route("manageReceipe.aspx")]
    public class ManageRecipeController : Controller
    {
        private readonly IConfiguration _config;

        public ManageRecipeController(IConfiguration config)
        {
            _config = config;
        }

        public class RecipeRow
        {
            public long ReceipeBookReceipe_ID { get; set; }
            public string ReceipeBookReceipe_title { get; set; }
            public decimal ReceipeBookReceipe_price { get; set; }
            public bool ReceipeBookReceipe_isActive { get; set; }
            public bool ReceipeBookReceipe_isCooking { get; set; }
            public int ReceipeBookReceipe_serving { get; set; }
            public string ReceipeBookReceipe_image { get; set; }
            public int ReceipeBookReceipe_productID { get; set; }
            public DateTime ReceipeBookReceipe_modifiedOn { get; set; }
            public string ReceipeBook_bookname { get; set; }
            public string ReceipeBookChapter_chaptername { get; set; }
            public string ReceipeCatIDs { get; set; }
        }

        public class BookItem { public long Id { get; set; } public string Name { get; set; } }
        public class ReceipeCatItem { public long Id { get; set; } public string Name { get; set; } public int CatType { get; set; } }
        public class PageInfo { public string PageNo { get; set; } public string PageCss { get; set; } }

        [HttpGet]
        [Route("")]
        [Route("page-{pageno}")]
        public IActionResult Index(int pageno = 1, string search = null, int filterstatus = 0,
            int cookingstatus = 0, int catid = 0, int receipecatid = 0, int receipetagid = 0,
            string searchtags = null, int ID = 0, string msg = null)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            if (bakeryId == "0")
            {
                return Redirect("/editbusinessinfo");
            }

            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            string cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

            // Message from redirect
            if (!string.IsNullOrEmpty(msg))
            {
                ViewBag.TopMessage = msg;
            }

            // Load books for dropdown
            var books = new List<BookItem>();
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT receipeBook_ID, receipeBook_bookname FROM tbl_receipeBook WHERE receipeBook_wsID = @wsId", conn))
                {
                    cmd.Parameters.AddWithValue("@wsId", long.Parse(bakeryId));
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            books.Add(new BookItem { Id = rdr.GetInt64(0), Name = rdr.GetString(1) });
                    }
                }
            }

            // Load recipe categories and tags
            var categories = new List<ReceipeCatItem>();
            var tags = new List<ReceipeCatItem>();
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT receipeCat_ID, receipeCat_categoryName, receipe_catType, receipeCat_parentID, receipeCat_displayOrder FROM tbl_receipeCat WHERE receipeCat_isActive = 1 AND receipeCat_isDeleted = 0 ORDER BY receipeCat_displayOrder", conn))
                {
                    var allCats = new List<dynamic>();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            allCats.Add(new { Id = rdr.GetInt64(0), Name = rdr.GetString(1), CatType = rdr.GetInt32(2), ParentID = rdr.GetInt64(3) });
                        }
                    }
                    // Build category list (catType=1)
                    foreach (var li in allCats.Where(c => c.ParentID == 0 && c.CatType == 1))
                    {
                        categories.Add(new ReceipeCatItem { Id = li.Id, Name = li.Name, CatType = 1 });
                        foreach (var inner in allCats.Where(c => c.ParentID == li.Id && c.CatType == 1))
                        {
                            categories.Add(new ReceipeCatItem { Id = inner.Id, Name = "-->> " + inner.Name, CatType = 1 });
                        }
                    }
                    // Build tags list (catType=2)
                    foreach (var li in allCats.Where(c => c.ParentID == 0 && c.CatType == 2))
                    {
                        tags.Add(new ReceipeCatItem { Id = li.Id, Name = li.Name, CatType = 2 });
                        foreach (var inner in allCats.Where(c => c.ParentID == li.Id && c.CatType == 2))
                        {
                            tags.Add(new ReceipeCatItem { Id = inner.Id, Name = "-->> " + inner.Name, CatType = 2 });
                        }
                    }
                }
            }

            // Build SQL for GetReceipeByWebstoreID
            int pageSize = 23;
            int totalRec = 0;
            var recipes = new List<RecipeRow>();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string whereClause = "r.receipeBookReceipe_isDeleted = 0";

                if (filterstatus == 1)
                    whereClause += " AND r.receipeBookReceipe_isActive = 1";
                else if (filterstatus == 2)
                    whereClause += " AND r.receipeBookReceipe_isActive = 0";

                if (cookingstatus == 1)
                    whereClause += " AND r.receipeBookReceipe_isCooking = 1";
                else if (cookingstatus == 2)
                    whereClause += " AND r.receipeBookReceipe_isCooking = 0";

                if (catid > 0)
                    whereClause += " AND r.receipeBookReceipe_bookID = " + catid;

                if (ID > 0)
                    whereClause += " AND r.receipeBookReceipe_ID = " + ID;

                if (!string.IsNullOrEmpty(search))
                {
                    string cleanSearch = search.Replace("$", "#").Replace("+", " ");
                    whereClause += " AND r.receipeBookReceipe_title LIKE @search";
                }

                if (receipecatid > 0)
                    whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipecatid + ")";

                if (receipetagid > 0)
                    whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipetagid + ")";

                if (!string.IsNullOrEmpty(searchtags))
                {
                    string cleanTags = searchtags.Replace("|", ",").Replace("+", " ").Replace("\"", "");
                    if (!string.IsNullOrEmpty(cleanTags))
                    {
                        whereClause += " AND r.receipeBookReceipe_ID IN (SELECT receipeBookIngredient_receipeID FROM tbl_receipeBookIngredient INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID WHERE lnkIngredient2Grp_GrpID IN (" + cleanTags + "))";
                    }
                }

                // Count
                string countSql = "SELECT COUNT(*) FROM tbl_receipeBookReceipe r LEFT JOIN tbl_receipeBook b ON r.receipeBookReceipe_bookID = b.receipeBook_ID WHERE " + whereClause;
                using (var cmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", "%" + search.Replace("$", "#").Replace("+", " ") + "%");
                    totalRec = (int)cmd.ExecuteScalar();
                }

                // Fetch page
                string dataSql = @"SELECT r.receipeBookReceipe_ID, r.receipeBookReceipe_title, r.receipeBookReceipe_price,
                    r.receipeBookReceipe_isActive, r.receipeBookReceipe_isCooking, r.receipeBookReceipe_serving,
                    r.receipeBookReceipe_image, r.receipeBookReceipe_productID, r.receipeBookReceipe_modifiedOn,
                    ISNULL(b.receipeBook_bookname,'') AS receipeBook_bookname,
                    ISNULL(ch.receipeBookChapter_chaptername,'') AS receipeBookChapter_chaptername,
                    ISNULL((SELECT STUFF((SELECT ',' + CAST(lnkreceipe2cat_catId AS VARCHAR) FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_receipeID = r.receipeBookReceipe_ID FOR XML PATH('')),1,1,'')),'') AS receipeCatIDs
                    FROM tbl_receipeBookReceipe r
                    LEFT JOIN tbl_receipeBook b ON r.receipeBookReceipe_bookID = b.receipeBook_ID
                    LEFT JOIN tbl_receipeBookChapter ch ON r.receipeBookReceipe_chapterID = ch.receipeBookChapter_ID
                    WHERE " + whereClause + @"
                    ORDER BY r.receipeBookReceipe_modifiedOn DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    cmd.Parameters.AddWithValue("@offset", (pageno - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", "%" + search.Replace("$", "#").Replace("+", " ") + "%");

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            recipes.Add(new RecipeRow
                            {
                                ReceipeBookReceipe_ID = rdr.GetInt64(0),
                                ReceipeBookReceipe_title = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                                ReceipeBookReceipe_price = rdr.GetDecimal(2),
                                ReceipeBookReceipe_isActive = rdr.GetBoolean(3),
                                ReceipeBookReceipe_isCooking = rdr.GetBoolean(4),
                                ReceipeBookReceipe_serving = rdr.GetInt32(5),
                                ReceipeBookReceipe_image = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                                ReceipeBookReceipe_productID = rdr.IsDBNull(7) ? 0 : Convert.ToInt32(rdr[7]),
                                ReceipeBookReceipe_modifiedOn = rdr.GetDateTime(8),
                                ReceipeBook_bookname = rdr.GetString(9),
                                ReceipeBookChapter_chaptername = rdr.GetString(10),
                                ReceipeCatIDs = rdr.GetString(11)
                            });
                        }
                    }
                }
            }

            ViewBag.Books = books;
            ViewBag.Categories = categories;
            ViewBag.Tags = tags;
            ViewBag.Recipes = recipes;
            ViewBag.TotalRecords = totalRec;
            ViewBag.CurrentPage = pageno;
            ViewBag.PageSize = pageSize;
            ViewBag.FilterStatus = filterstatus;
            ViewBag.CookingStatus = cookingstatus;
            ViewBag.SelectedCatId = catid;
            ViewBag.SelectedReceipeCatId = receipecatid;
            ViewBag.SelectedReceipeTagId = receipetagid;
            ViewBag.Search = search;
            ViewBag.SearchTags = searchtags;
            ViewBag.CdnBase = cdnBase;

            // Build paging
            int totalPages = (int)Math.Ceiling((double)totalRec / pageSize);
            var pages = new List<PageInfo>();
            if (totalPages > 1)
            {
                int pageButtonCount = 3;
                int min = pageno - pageButtonCount;
                int max = pageno + pageButtonCount;
                if (max > totalPages) min -= max - totalPages;
                else if (min < 1) max += 1 - min;

                for (int i = 1; i <= totalPages; i++)
                {
                    if (i <= 2 || i > totalPages - 2 || (min <= i && i <= max))
                    {
                        pages.Add(new PageInfo { PageNo = i.ToString(), PageCss = (i == pageno) ? "active" : "" });
                    }
                    else if (pages.Count == 0 || pages.Last().PageNo != "...")
                    {
                        pages.Add(new PageInfo { PageNo = "...", PageCss = "disabled" });
                    }
                }
            }
            ViewBag.Pages = pages;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/ManageRecipe/Index.cshtml");
        }

        [HttpPost]
        [Route("save")]
        public IActionResult SaveItem(long id, string title, string price)
        {
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_modifiedOn = GETDATE(), receipeBookReceipe_title = @title, receipeBookReceipe_price = @price WHERE receipeBookReceipe_ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@price", decimal.Parse(price));
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { success = true });
        }

        [HttpPost]
        [Route("setactive")]
        public IActionResult SetActive([FromBody] List<long> ids)
        {
            if (ids == null || ids.Count == 0) return Json(new { success = false });
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string idList = string.Join(",", ids);
                using (var cmd = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isActive = 1, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID IN (" + idList + ")", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { success = true });
        }

        [HttpPost]
        [Route("setinactive")]
        public IActionResult SetInactive([FromBody] List<long> ids)
        {
            if (ids == null || ids.Count == 0) return Json(new { success = false });
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string idList = string.Join(",", ids);
                using (var cmd = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isActive = 0, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID IN (" + idList + ")", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { success = true });
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
                using (var cmd = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isDeleted = 1, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID IN (" + idList + ")", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { success = true });
        }

        /// <summary>
        /// Gets category list HTML for a recipe row, matching legacy getCategoryList method.
        /// </summary>
        [HttpGet]
        [Route("getcategorylist")]
        public IActionResult GetCategoryList(string ids, string receipeId)
        {
            string strret = "";
            if (!string.IsNullOrEmpty(ids))
            {
                string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT receipeCat_ID, receipe_catType, receipeCat_categoryName FROM tbl_receipeCat WHERE receipeCat_ID IN (" + ids + ") ORDER BY receipe_catType", conn))
                    {
                        int chkCat = 0, chkTag = 0;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string catType = rdr["receipe_catType"].ToString();
                                if (chkCat == 0 && catType == "1") { chkCat = 1; strret += "<div class='headertext'>Categories</div>"; }
                                else if (chkTag == 0 && catType == "2") { chkTag = 1; strret += "<div class='headertext'>Tags</div>"; }
                                strret += "<div data-id='" + rdr["receipeCat_ID"] + "' class='itemli'>" + rdr["receipeCat_categoryName"] + "<a onclick='removeCatLinkingFromReceipe(" + rdr["receipeCat_ID"] + "," + receipeId + ",this);'>X</a></div>";
                            }
                        }
                        if (!string.IsNullOrEmpty(strret))
                            strret = "<div class='div_taglist'>" + strret + "</div>";
                    }
                }
            }
            return Content(strret, "text/html");
        }

        private string BuildPageUrl(int page, string search, int filterstatus, int cookingstatus, int catid, int receipecatid, int receipetagid, string searchtags)
        {
            string url = "/managereceipe";
            if (page > 1) url += "/page-" + page;
            var queryParts = new List<string>();
            if (!string.IsNullOrEmpty(search)) queryParts.Add("search=" + Uri.EscapeDataString(search));
            if (filterstatus != 0) queryParts.Add("filterstatus=" + filterstatus);
            if (cookingstatus != 0) queryParts.Add("cookingstatus=" + cookingstatus);
            if (catid != 0) queryParts.Add("catid=" + catid);
            if (receipecatid != 0) queryParts.Add("receipecatid=" + receipecatid);
            if (receipetagid != 0) queryParts.Add("receipetagid=" + receipetagid);
            if (!string.IsNullOrEmpty(searchtags)) queryParts.Add("searchtags=" + Uri.EscapeDataString(searchtags));
            if (queryParts.Count > 0) url += "?" + string.Join("&", queryParts);
            return url;
        }
    }
}
