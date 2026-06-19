using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy ManageReceipe_Print.aspx / ManageReceipe_Print.aspx.cs.
    /// Standalone print page (no sidebar layout) that renders recipe details with ingredients 
    /// and directions in a print-friendly format. Reads from GetReceipeByWebstoreID_full stored procedure.
    /// </summary>
    [Route("managereceipe-print")]
    [Route("ManageReceipe_Print.aspx")]
    public class RecipePrintController : Controller
    {
        private readonly IConfiguration _config;

        public RecipePrintController(IConfiguration config)
        {
            _config = config;
        }

        public class DirectionTag
        {
            public long ID { get; set; }
            public string Replacetext { get; set; }
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index(string search = null, int filterstatus = 0, int cookingstatus = 0,
            int catid = 0, int receipecatid = 0, int receipetagid = 0, int ID = 0,
            string searchtags = null, string strIDs = "0")
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

            if (!string.IsNullOrEmpty(search))
                search = search.Replace("$", "#");

            string cleanSearchTags = "";
            if (!string.IsNullOrEmpty(searchtags))
                cleanSearchTags = searchtags.Replace("|", ",").Replace("+", " ").Replace("\"", "");

            // Build WHERE clause matching legacy GetReceipeByWebstoreID_full
            string whereClause = "r.receipeBookReceipe_isDeleted = 0";

            if (filterstatus == 1) whereClause += " AND r.receipeBookReceipe_isActive = 1";
            else if (filterstatus == 2) whereClause += " AND r.receipeBookReceipe_isActive = 0";

            if (cookingstatus == 1) whereClause += " AND r.receipeBookReceipe_isCooking = 1";
            else if (cookingstatus == 2) whereClause += " AND r.receipeBookReceipe_isCooking = 0";

            if (catid > 0) whereClause += " AND r.receipeBookReceipe_bookID = " + catid;
            if (ID > 0) whereClause += " AND r.receipeBookReceipe_ID = " + ID;
            if (receipecatid > 0) whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipecatid + ")";
            if (receipetagid > 0) whereClause += " AND r.receipeBookReceipe_ID IN (SELECT lnkreceipe2cat_receipeID FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_catId = " + receipetagid + ")";

            if (strIDs != "0" && !string.IsNullOrEmpty(strIDs))
                whereClause += " AND r.receipeBookReceipe_ID IN (" + strIDs + ")";

            if (!string.IsNullOrEmpty(search))
                whereClause += " AND r.receipeBookReceipe_title LIKE @search";

            if (!string.IsNullOrEmpty(cleanSearchTags))
                whereClause += " AND r.receipeBookReceipe_ID IN (SELECT receipeBookIngredient_receipeID FROM tbl_receipeBookIngredient INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID WHERE lnkIngredient2Grp_GrpID IN (" + cleanSearchTags + "))";

            var recipeData = new DataTable();
            var ingredientData = new DataTable();
            var directionData = new DataTable();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Get recipes
                string recipeSql = "SELECT r.receipeBookReceipe_ID, r.receipeBookReceipe_title, r.receipeBookReceipe_serving FROM tbl_receipeBookReceipe r WHERE " + whereClause + " ORDER BY r.receipeBookReceipe_modifiedOn DESC";
                using (var cmd = new SqlCommand(recipeSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(recipeData);
                    }
                }

                if (recipeData.Rows.Count > 0)
                {
                    string recipeIds = string.Join(",", recipeData.AsEnumerable().Select(r => r["receipeBookReceipe_ID"].ToString()));

                    // Get ingredients (typeID=1)
                    string ingSql = @"SELECT i.receipeBookIngredient_receipeID, i.receipeBookIngredient_displayorder, 
                        ISNULL(g.receipeBookIngredientGrp_ingredient,'') AS receipeBookIngredientGrp_ingredient,
                        ISNULL(g.receipeBookIngredientGrp_active, 1) AS receipeBookIngredientGrp_active,
                        ISNULL(g.receipeBookIngredientGrp_Img,'') AS receipeBookIngredientGrp_Img
                        FROM tbl_receipeBookIngredient i 
                        LEFT JOIN tbl_lnkIngredient2Grp l ON l.lnkIngredient2Grp_ingID = i.receipeBookIngredient_ID
                        LEFT JOIN tbl_receipeBookIngredientGrp g ON l.lnkIngredient2Grp_GrpID = g.receipeBookIngredientGrp_ID
                        WHERE i.receipeBookIngredient_typeID = 1 AND i.receipeBookIngredient_receipeID IN (" + recipeIds + ")";
                    using (var cmd2 = new SqlCommand(ingSql, conn))
                    {
                        using (var da = new SqlDataAdapter(cmd2))
                        {
                            da.Fill(ingredientData);
                        }
                    }

                    // Get directions (typeID=2)
                    string dirSql = @"SELECT i.receipeBookIngredient_receipeID, i.receipeBookIngredient_displayorder, 
                        i.receipeBookIngredient_Ingredient
                        FROM tbl_receipeBookIngredient i 
                        WHERE i.receipeBookIngredient_typeID = 2 AND i.receipeBookIngredient_receipeID IN (" + recipeIds + ")";
                    using (var cmd3 = new SqlCommand(dirSql, conn))
                    {
                        using (var da = new SqlDataAdapter(cmd3))
                        {
                            da.Fill(directionData);
                        }
                    }
                }
            }

            // Build HTML matching legacy template
            int recordCount = recipeData.Rows.Count;
            StringBuilder sbMain = new StringBuilder();

            foreach (DataRow drmain in recipeData.Rows)
            {
                string recipeId = drmain["receipeBookReceipe_ID"].ToString();

                // Ingredients
                StringBuilder sbIngs = new StringBuilder();
                var ingRows = ingredientData.Select("receipeBookIngredient_receipeID=" + recipeId, "receipeBookIngredientGrp_active desc,receipeBookIngredient_displayorder");
                foreach (DataRow dr in ingRows)
                {
                    string actClass = bool.Parse(dr["receipeBookIngredientGrp_active"].ToString()) ? "act" : "nact";
                    sbIngs.Append("<li class=\"ligrp cuttype1 " + actClass + "\"><font color=\"#f55\">#</font>" + dr["receipeBookIngredientGrp_ingredient"] + "</li>");
                }

                // Directions
                StringBuilder sbDir = new StringBuilder();
                var dirRows = directionData.Select("receipeBookIngredient_receipeID=" + recipeId, "receipeBookIngredient_displayorder");
                foreach (DataRow dr in dirRows)
                {
                    string dirText = dr["receipeBookIngredient_Ingredient"].ToString();
                    // Process direction tags (#ingredient~(id)) - matching legacy behaviour
                    if (dirText.Contains("#"))
                    {
                        var dirTags = new List<DirectionTag>();
                        foreach (string str in dirText.Split('#'))
                        {
                            if (str.Contains(")"))
                            {
                                string tempReplace = "#" + str.Substring(0, str.IndexOf(')') + 1);
                                try
                                {
                                    string tempId = tempReplace.Split('~')[1].Trim().Replace("(", "").Replace(")", "").Trim();
                                    dirTags.Add(new DirectionTag { ID = long.Parse(tempId), Replacetext = tempReplace });
                                }
                                catch { }
                            }
                        }
                        if (dirTags.Count > 0)
                        {
                            using (var conn = new SqlConnection(connStr))
                            {
                                conn.Open();
                                string grpIds = string.Join(",", dirTags.Select(t => t.ID));
                                using (var cmd = new SqlCommand(@"SELECT g.receipeBookIngredientGrp_ID, g.receipeBookIngredientGrp_ingredient, 
                                    g.receipeBookIngredientGrp_marking, g.receipeBookIngredientGrp_active,
                                    ISNULL((SELECT TOP 1 u.IngredientUnit_title FROM tbl_receipeIngredientUnit u 
                                    INNER JOIN tbl_lnkUnit2Grp lu ON u.IngredientUnit_ID = lu.lnkUnit2Grp_ingID 
                                    WHERE lu.lnkUnit2Grp_GrpID = g.receipeBookIngredientGrp_ID), '-') AS UnitTitle
                                    FROM tbl_receipeBookIngredientGrp g WHERE g.receipeBookIngredientGrp_ID IN (" + grpIds + ")", conn))
                                {
                                    using (var rdr = cmd.ExecuteReader())
                                    {
                                        while (rdr.Read())
                                        {
                                            long grpId = rdr.GetInt64(0);
                                            var tag = dirTags.FirstOrDefault(t => t.ID == grpId);
                                            if (tag != null)
                                            {
                                                bool active = rdr.GetBoolean(3);
                                                string fontClass = active ? "act" : "nact";
                                                string replacement = "<font class=\"" + fontClass + "\" ><font color='#df3f42' >#" + rdr.GetString(1) + "</font> [L" + rdr.GetString(2) + " | " + rdr.GetString(4) + "]</font>";
                                                dirText = dirText.Replace(tag.Replacetext, replacement);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    sbDir.Append("<ul class=\"directionul\"><li>" + dirText + "</li></ul>");
                }

                string inner = "<div class=\"div_reciepe col-sm-12 flush\" style='page-break-inside:avoid;'>" +
                    "<h3>" + drmain["receipeBookReceipe_title"] + "<span class='serving'>Serving: " + drmain["receipeBookReceipe_serving"] + "</span></h3>" +
                    "<div class=\"divIngredient col-sm-12 flush\"><h5><u>Ingredients:</u></h5><div class=\"divIngredientlistOuter col-sm-12 flush\"><ul >" + sbIngs + "</ul></div></div>" +
                    "<div class=\"divIngredient col-sm-12 flush\"><h5><u>Direction:</u></h5><div class=\"divIngredientlistOuter col-sm-12 flush\">" + sbDir + "</div></div>" +
                    "</div></div></div>";

                sbMain.Append(inner);
            }

            ViewBag.RecordCount = recordCount;
            ViewBag.PrintContent = sbMain.ToString();

            return View("~/Views/RecipePrint/Index.cshtml");
        }
    }
}
