using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    /// <summary>
    /// Migrated from legacy addnewreceipe.aspx / addnewreceipe.aspx.cs.
    /// Provides form to add a new recipe with title, price, book, chapter, isCooking, servings.
    /// On POST inserts into tbl_receipeBookReceipe and sets receipeBookReceipe_No = new ID.
    /// </summary>
    [Route("addnewreceipe")]
    [Route("addnewreceipe.aspx")]
    public class RecipeAddController : Controller
    {
        private readonly IConfiguration _config;

        public RecipeAddController(IConfiguration config)
        {
            _config = config;
        }

        public class BookItem
        {
            public long Id { get; set; }
            public string Name { get; set; }
        }

        public class ChapterItem
        {
            public long Id { get; set; }
            public string Name { get; set; }
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            if (bakeryId == "0")
            {
                return Redirect("/businesslogin?returl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
            }

            var books = new List<BookItem>();
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT receipeBook_ID, receipeBook_bookname FROM tbl_receipeBook WHERE receipeBook_wsID = @wsId", conn))
                {
                    cmd.Parameters.AddWithValue("@wsId", long.Parse(bakeryId));
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            books.Add(new BookItem
                            {
                                Id = rdr.GetInt64(0),
                                Name = rdr.GetString(1)
                            });
                        }
                    }
                }
            }

            ViewBag.Books = books;
            return View("~/Views/RecipeAdd/Index.cshtml");
        }

        [HttpGet]
        [Route("getchapters")]
        public IActionResult GetChapters(long bookId)
        {
            var chapters = new List<ChapterItem>();
            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT receipeBookChapter_ID, receipeBookChapter_chaptername FROM tbl_receipeBookChapter WHERE receipeBookChapter_bookID = @bookId", conn))
                {
                    cmd.Parameters.AddWithValue("@bookId", bookId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            chapters.Add(new ChapterItem
                            {
                                Id = rdr.GetInt64(0),
                                Name = rdr.GetString(1)
                            });
                        }
                    }
                }
            }
            return Json(chapters);
        }

        [HttpPost]
        [Route("")]
        public IActionResult Save(string txtTitle, string txtPrice, long ddlBook, long ddlChapter, bool chkIsCooking, int txtServings)
        {
            var bakeryId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
            if (bakeryId == "0")
            {
                return Redirect("/businesslogin");
            }

            string connStr = _config.GetConnectionString("aboraboraboraaboraaborab");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                string sql = @"INSERT INTO tbl_receipeBookReceipe 
                    (receipeBookReceipe_bookID, receipeBookReceipe_chapterID, receipeBookReceipe_createdOn, 
                     receipeBookReceipe_image, receipeBookReceipe_isActive, receipeBookReceipe_isCooking, 
                     receipeBookReceipe_isDeleted, receipeBookReceipe_modifiedOn, receipeBookReceipe_No, 
                     receipeBookReceipe_price, receipeBookReceipe_productID, receipeBookReceipe_serving, 
                     receipeBookReceipe_servingDet, receipeBookReceipe_title)
                    VALUES 
                    (@bookID, @chapterID, @createdOn, 
                     '', 1, @isCooking, 
                     0, @modifiedOn, 0, 
                     @price, 0, @serving, 
                     @servingDet, @title);
                    SELECT SCOPE_IDENTITY();";

                long newId;
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@bookID", ddlBook);
                    cmd.Parameters.AddWithValue("@chapterID", ddlChapter);
                    cmd.Parameters.AddWithValue("@createdOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@isCooking", chkIsCooking);
                    cmd.Parameters.AddWithValue("@modifiedOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@price", decimal.Parse(txtPrice));
                    cmd.Parameters.AddWithValue("@serving", txtServings);
                    cmd.Parameters.AddWithValue("@servingDet", string.Format("Serving: {0}", txtServings));
                    cmd.Parameters.AddWithValue("@title", txtTitle);
                    newId = Convert.ToInt64(cmd.ExecuteScalar());
                }

                // Update receipeBookReceipe_No = ID (legacy behaviour)
                using (var cmd2 = new SqlCommand("UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_No = @id WHERE receipeBookReceipe_ID = @id", conn))
                {
                    cmd2.Parameters.AddWithValue("@id", newId);
                    cmd2.ExecuteNonQuery();
                }
            }

            return Redirect("/managereceipe?msg=Recipe detail has been saved successfully;");
        }
    }
}
