using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy manageeeceipecategory.aspx / manageeeceipecategory.aspx.cs.
    /// Manages recipe categories and tags in a hierarchical tree structure (parent → sub-category → sub-sub-category).
    /// Categories are loaded from tbl_receipeCat filtered by catType (1=Category, 2=Tags).
    /// The page renders editable inputs for each level and uses JavaScript to handle add/remove/reorder.
    /// </summary>
    [Route("managereceipecategory")]
    [Route("manageeeceipecategory.aspx")]
    public class RecipeCategoryController : Controller
    {
        private readonly IConfiguration _config;

        public RecipeCategoryController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index(int catID = 1, string returl = null)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            if (bakeryId == "0")
            {
                return Redirect("/?returl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
            }

            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            long receipeid = long.Parse(bakeryId);

            string businessName = "";
            bool isCrmUser = false;

            // Get webstore info
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT webstore_businessName FROM tbl_webstore WHERE webstore_ID = @wsId", conn))
                {
                    cmd.Parameters.AddWithValue("@wsId", receipeid);
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        return Redirect("/editbusinessinfo?returl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
                    }
                    businessName = result.ToString();
                }
            }

            string backUrl = !string.IsNullOrEmpty(returl) ? returl : "/managereceipe";

            // Build category tree HTML (matching legacy getcustomerDet)
            StringBuilder sb = new StringBuilder();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Get top-level categories
                DataTable dtcat = new DataTable();
                using (var cmd = new SqlCommand(@"SELECT * FROM tbl_receipeCat 
                    WHERE receipe_catType = @catType AND receipeCat_isDeleted = 0 AND receipeCat_isActive = 1 
                    AND receipeCat_parentID = 0 AND receipe_ID = @receipeId 
                    ORDER BY receipeCat_displayOrder", conn))
                {
                    cmd.Parameters.AddWithValue("@catType", catID);
                    cmd.Parameters.AddWithValue("@receipeId", receipeid);
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dtcat);
                    }
                }

                if (dtcat.Rows.Count > 0)
                {
                    foreach (DataRow dr in dtcat.Rows)
                    {
                        string catIdStr = dr["receipeCat_ID"].ToString();
                        string catName = dr["receipeCat_categoryName"].ToString();

                        sb.Append("<div class='form-group specOuter' data-tid='" + catIdStr + "'><div class='col-md-8 clearleft'>");
                        sb.Append("<div class='headershipping bggrey'>");
                        if (isCrmUser) sb.Append("<span class='showcatid'>" + catIdStr + " </span>");
                        sb.Append("<div class='colleft2_1'><input type='text' placeholder='Enter Category Name' value='" + catName + "' class='txtcategory form-control'><span class='descriptiontext_maxchar'>Max 30 Characters</span></div>");
                        sb.Append("<div class='colleft2_2'><a class='removeSpecfication maincat'>X</a><a class='upwardSpecfication maincat'>&nbsp;</a><a class='removeSpecfication seocat' data-text='" + catName + "' data-tid='" + catIdStr + "'>S</a></div></div>");

                        // Sub-categories
                        DataTable dtsubcat = new DataTable();
                        using (var cmd2 = new SqlCommand(@"SELECT * FROM tbl_receipeCat 
                            WHERE receipeCat_isDeleted = 0 AND receipeCat_isActive = 1 
                            AND receipeCat_parentID = @parentId AND receipe_ID = @receipeId 
                            ORDER BY receipeCat_displayOrder", conn))
                        {
                            cmd2.Parameters.AddWithValue("@parentId", long.Parse(catIdStr));
                            cmd2.Parameters.AddWithValue("@receipeId", receipeid);
                            using (var da = new SqlDataAdapter(cmd2))
                            {
                                da.Fill(dtsubcat);
                            }
                        }

                        sb.Append("<div class='Businesssubcat_outer subcat2' data-tid=" + catIdStr + ">");
                        foreach (DataRow drsubcat in dtsubcat.Rows)
                        {
                            string subCatId = drsubcat["receipeCat_ID"].ToString();
                            string subCatName = drsubcat["receipeCat_categoryName"].ToString();

                            sb.Append("<div class='headershipping bgwhite' data-tid='" + subCatId + "'>");
                            if (isCrmUser) sb.Append("<span class='showcatid'>" + subCatId + " </span>");
                            sb.Append("<span class='bullet_bg'>-</span><div class='colleft2_1'><input type='text' value='" + subCatName + "' placeholder='Enter sub-category Name' class='txtSubcategory form-control'></div>");
                            sb.Append("<div class='colleft2_2'><a class='removeSpecfication subcat'>X</a><a class='removeSpecfication addsubcat3'><span>+</span></a><a class='removeSpecfication seocat' data-text='" + subCatName + "' data-tid='" + subCatId + "'>S</a></div></div>");

                            // Sub-sub-categories (level 3)
                            DataTable dtsubcat3 = new DataTable();
                            using (var cmd3 = new SqlCommand(@"SELECT * FROM tbl_receipeCat 
                                WHERE receipeCat_isDeleted = 0 AND receipeCat_isActive = 1 
                                AND receipeCat_parentID = @parentId AND receipe_ID = @receipeId 
                                ORDER BY receipeCat_displayOrder", conn))
                            {
                                cmd3.Parameters.AddWithValue("@parentId", long.Parse(subCatId));
                                cmd3.Parameters.AddWithValue("@receipeId", receipeid);
                                using (var da = new SqlDataAdapter(cmd3))
                                {
                                    da.Fill(dtsubcat3);
                                }
                            }

                            sb.Append("<div class='Businesssubcat_outer3 subcat3_outer' data-tid=" + subCatId + ">");
                            foreach (DataRow drsubcat3 in dtsubcat3.Rows)
                            {
                                string sub3Id = drsubcat3["receipeCat_ID"].ToString();
                                string sub3Name = drsubcat3["receipeCat_categoryName"].ToString();

                                sb.Append("<div class='headershipping bgwhite3' data-tid='" + sub3Id + "'>");
                                if (isCrmUser) sb.Append("<span class='showcatid'>" + sub3Id + " </span>");
                                sb.Append("<span class='bullet_bg'>-</span><div class='colleft2_1'><input type='text' value='" + sub3Name + "' placeholder='Enter sub-category Name' class='txtSubcategory3 form-control'><a class='addnewsubcat3'><span>+</span></a></div>");
                                sb.Append("<div class='colleft2_2'><a class='removeSpecfication subcat3'>X</a><a class='upwardSpecfication subcat3'>&nbsp;</a><a class='removeSpecfication seocat' data-text='" + sub3Name + "' data-tid='" + sub3Id + "'>S</a></div></div>");
                            }
                            sb.Append("</div>");
                        }
                        sb.Append("</div>");
                        sb.Append("<div class='headershipping bgBlue_link' ><div class='colfullleft'><a class='addmoreShipping'><span>+</span> Add sub-category</a></div></div>");
                        sb.Append("</div></div>");
                    }
                }
                else
                {
                    // Empty state - two empty category slots (matching legacy)
                    for (int e = 0; e < 2; e++)
                    {
                        sb.Append("<div class='form-group specOuter' data-tid='0'><div class='col-md-8 clearleft'>");
                        sb.Append("<div class='headershipping bggrey'><div class='colleft2_1'><input type='text' placeholder='Enter Category Name' class='txtcategory form-control'><span class='descriptiontext_maxchar'>Max 30 Characters</span></div><div class='colleft2_2'><a class='removeSpecfication maincat'>X</a><a class='upwardSpecfication maincat'>&nbsp;</a></div></div>");
                        sb.Append("<div class='Businesssubcat_outer' data-tid='0'></div>");
                        sb.Append("<div class='headershipping bgBlue_link' ><div class='colfullleft'><a class='addmoreShipping'><span>+</span> Add sub-category</a></div></div>");
                        sb.Append("</div></div>");
                    }
                }
            }

            ViewBag.BusinessName = businessName;
            ViewBag.CatType = catID;
            ViewBag.BackUrl = backUrl;
            ViewBag.CategoryHtml = sb.ToString();
            ViewBag.WebstoreId = bakeryId;

            return View("~/Views/RecipeCategory/Index.cshtml");
        }
    }
}
