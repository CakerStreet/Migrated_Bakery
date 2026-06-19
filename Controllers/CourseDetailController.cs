using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Course Detail page.
/// Shows course navigation, module list, chapter content with prev/next buttons.
/// Migrated from legacy courseDetail.aspx / courseDetail.aspx.cs.
/// </summary>
[Route("coursedetail")]
public class CourseDetailController : Controller
{
    private readonly IConfiguration _config;

    public CourseDetailController(IConfiguration config)
    {
        _config = config;
    }

    private string CourseConnStr => _config.GetConnectionString("StaffAssessment")
                                    ?? _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";
    private string SiteUrl => _config["SiteUrl"] ?? "/";

    /// <summary>Course overview: /course/{courseURL}</summary>
    [HttpGet("~/course/{courseURL}")]
    public async Task<IActionResult> Index(string courseURL)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseDetailViewModel();

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        // Get course
        using var cmdCourse = new SqlCommand("SELECT * FROM tbl_course WHERE course_seoURL=@url", conn);
        cmdCourse.Parameters.AddWithValue("@url", courseURL);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();

        var courseID = rdrCourse["course_ID"]?.ToString() ?? "";
        model.CourseName = rdrCourse["course_Name"]?.ToString() ?? "";
        ViewData["Title"] = "Course Content - " + model.CourseName;
        rdrCourse.Close();

        model.ShowBackFoodStandards = true;
        model.ShowStartButton = true;

        // Build module navigation
        model.NavigationHtml = "<div class='div_moduleNavigaionMain'>" +
            await GetModuleNavigations(conn, courseID, courseURL) + "</div>";

        // Get first module for Start button
        using var cmdFirst = new SqlCommand(
            "SELECT * FROM tbl_courseModules WHERE courseModules_courseID=@cid AND courseModules_displayorder=1 AND courseModules_isActive=1 ORDER BY courseModules_displayorder", conn);
        cmdFirst.Parameters.AddWithValue("@cid", courseID);
        using var rdrFirst = await cmdFirst.ExecuteReaderAsync();
        if (await rdrFirst.ReadAsync())
        {
            model.StartUrl = SiteUrl + "course/" + courseURL + "/" + rdrFirst["courseModules_ModuleseoURL"]?.ToString();
        }

        return View("~/Views/CourseDetail/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Detail";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>Module detail: /course/{courseURL}/{moduleURL}</summary>
    [HttpGet("~/course/{courseURL}/{moduleURL}")]
    public async Task<IActionResult> Module(string courseURL, string moduleURL)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseDetailViewModel();
        model.ShowBackLink = true;
        model.BackUrl = "/course/" + courseURL;
        model.ShowPrevNext = true;

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        // Get course
        using var cmdCourse = new SqlCommand("SELECT * FROM tbl_course WHERE course_seoURL=@url", conn);
        cmdCourse.Parameters.AddWithValue("@url", courseURL);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();
        var courseID = rdrCourse["course_ID"]?.ToString() ?? "";
        model.CourseName = rdrCourse["course_Name"]?.ToString() ?? "";
        ViewData["Title"] = "Course Content - " + model.CourseName;
        rdrCourse.Close();

        model.NavigationHtml = "<div class='div_moduleNavigaionMain'>" +
            await GetModuleDetail(conn, courseID, courseURL, moduleURL, model) + "</div>";

        return View("~/Views/CourseDetail/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Detail";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>Chapter detail: /course/{courseURL}/{moduleURL}/{chapterURL}</summary>
    [HttpGet("~/course/{courseURL}/{moduleURL}/{chapterURL}")]
    public async Task<IActionResult> Chapter(string courseURL, string moduleURL, string chapterURL)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseDetailViewModel();
        model.ShowBackLink = true;
        model.BackUrl = "/course/" + courseURL;
        model.ShowPrevNext = true;

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        using var cmdCourse = new SqlCommand("SELECT * FROM tbl_course WHERE course_seoURL=@url", conn);
        cmdCourse.Parameters.AddWithValue("@url", courseURL);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();
        var courseID = rdrCourse["course_ID"]?.ToString() ?? "";
        model.CourseName = rdrCourse["course_Name"]?.ToString() ?? "";
        ViewData["Title"] = "Course Content - " + model.CourseName;
        rdrCourse.Close();

        model.NavigationHtml = "<div class='div_moduleNavigaionMain div_content'>" +
            await GetChapterDetail(conn, courseID, courseURL, moduleURL, chapterURL, model) + "</div>";

        return View("~/Views/CourseDetail/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Detail";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    private async Task<string> GetModuleNavigations(SqlConnection conn, string courseID, string courseURL)
    {
        using var cmd = new SqlCommand(
            "SELECT * FROM tbl_courseModules WHERE courseModules_courseID=@cid AND courseModules_isActive=1 ORDER BY courseModules_displayorder", conn);
        cmd.Parameters.AddWithValue("@cid", courseID);
        var dt = new DataTable();
        dt.Load(await cmd.ExecuteReaderAsync());

        int modulecounter = 0;
        string strRet = "<ul><li class='headertext'>Courses Navigation</li>";
        foreach (DataRow dr in dt.Rows)
        {
            modulecounter++;
            strRet += "<li>Module " + modulecounter + ": <a href='" + SiteUrl + "course/" + courseURL + "/" +
                      dr["courseModules_ModuleseoURL"] + "'>" + dr["courseModules_ModuleName"] + "</a></li>";

            using var cmdCh = new SqlCommand(
                "SELECT * FROM tbl_Post WHERE CategoryID=@mid AND PostIsActive=1 ORDER BY DisplayOrder", conn);
            cmdCh.Parameters.AddWithValue("@mid", dr["courseModules_ID"]);
            var dtCh = new DataTable();
            dtCh.Load(await cmdCh.ExecuteReaderAsync());
            strRet += "<ul>";
            foreach (DataRow drCh in dtCh.Rows)
            {
                strRet += "<li><a href='" + SiteUrl + "course/" + courseURL + "/" +
                          dr["courseModules_ModuleseoURL"] + "/" + drCh["PostSEOUrl"] + "'>" +
                          drCh["PostName"] + "</a></li>";
            }
            strRet += "</ul>";
        }

        var courseName = ""; // will be set in caller
        strRet += "<li><a style='color:red;text-decoration: underline;' href='" + SiteUrl + "course/assessment/" +
                  courseURL + "'>Assessment</a></li>";
        strRet += "<li><a style='color:red;text-decoration: underline;' href='" + SiteUrl + "course/result/" +
                  courseURL + "'>Result</a></li></ul>";
        return strRet;
    }

    private async Task<string> GetModuleDetail(SqlConnection conn, string courseID, string courseURL,
        string moduleURL, CourseDetailViewModel model)
    {
        using var cmd = new SqlCommand(
            "SELECT * FROM tbl_courseModules WHERE courseModules_courseID=@cid AND courseModules_ModuleseoURL=@murl AND courseModules_isActive=1 ORDER BY courseModules_displayorder", conn);
        cmd.Parameters.AddWithValue("@cid", courseID);
        cmd.Parameters.AddWithValue("@murl", moduleURL);
        var dt = new DataTable();
        dt.Load(await cmd.ExecuteReaderAsync());
        if (dt.Rows.Count == 0) return "";

        string strRet = "<ul><li class='headertext'>Module " + dt.Rows[0]["courseModules_displayorder"] +
                         ": " + dt.Rows[0]["courseModules_ModuleName"] + "</li>";

        foreach (DataRow dr in dt.Rows)
        {
            using var cmdCh = new SqlCommand(
                "SELECT * FROM tbl_Post WHERE CategoryID=@mid AND PostIsActive=1 ORDER BY DisplayOrder", conn);
            cmdCh.Parameters.AddWithValue("@mid", dr["courseModules_ID"]);
            var dtCh = new DataTable();
            dtCh.Load(await cmdCh.ExecuteReaderAsync());
            strRet += "<ul>";
            int chapterCounter = 0;
            foreach (DataRow drCh in dtCh.Rows)
            {
                chapterCounter++;
                strRet += "<li><a href='" + SiteUrl + "course/" + courseURL + "/" +
                          dr["courseModules_ModuleseoURL"] + "/" + drCh["PostSEOUrl"] + "'>" +
                          drCh["PostName"] + "</a></li>";
                if (chapterCounter == 1)
                {
                    model.NextUrl = SiteUrl + "course/" + courseURL + "/" +
                                    dr["courseModules_ModuleseoURL"] + "/" + drCh["PostSEOUrl"];
                }
            }
            strRet += "</ul>";
        }
        strRet += "</ul>";

        // Prev link
        if (dt.Rows[0]["courseModules_displayorder"]?.ToString() == "1")
        {
            model.PrevUrl = SiteUrl + "course/" + courseURL;
        }
        else
        {
            int prevOrder = int.Parse(dt.Rows[0]["courseModules_displayorder"]?.ToString() ?? "1") - 1;
            using var cmdPrev = new SqlCommand(@"
                SELECT * FROM tbl_Post INNER JOIN tbl_courseModules ON CategoryID=courseModules_ID
                WHERE courseModules_courseID=@cid AND courseModules_displayorder=@order 
                AND courseModules_isActive=1 AND PostIsActive=1 ORDER BY DisplayOrder DESC", conn);
            cmdPrev.Parameters.AddWithValue("@cid", courseID);
            cmdPrev.Parameters.AddWithValue("@order", prevOrder);
            var dtPrev = new DataTable();
            dtPrev.Load(await cmdPrev.ExecuteReaderAsync());
            if (dtPrev.Rows.Count > 0)
            {
                model.PrevUrl = SiteUrl + "course/" + courseURL + "/" +
                                dtPrev.Rows[0]["courseModules_ModuleseoURL"] + "/" + dtPrev.Rows[0]["PostSEOUrl"];
            }
        }

        return strRet;
    }

    private async Task<string> GetChapterDetail(SqlConnection conn, string courseID, string courseURL,
        string moduleURL, string chapterURL, CourseDetailViewModel model)
    {
        using var cmd = new SqlCommand(@"
            SELECT * FROM tbl_Post INNER JOIN tbl_courseModules ON CategoryID=courseModules_ID
            WHERE PostSEOUrl=@curl AND PostIsActive=1 AND courseModules_isActive=1 ORDER BY DisplayOrder", conn);
        cmd.Parameters.AddWithValue("@curl", chapterURL);
        var dt = new DataTable();
        dt.Load(await cmd.ExecuteReaderAsync());
        if (dt.Rows.Count == 0) return "";

        string strRet = "<ul><li class='headertext'>" + dt.Rows[0]["PostName"] + "</li>";
        strRet += "<li class='contenttext'>" + dt.Rows[0]["Description"] + "</li></ul>";

        string postDisplayOrder = dt.Rows[0]["DisplayOrder"]?.ToString() ?? "1";

        // Prev link
        if (postDisplayOrder == "1")
        {
            model.PrevUrl = SiteUrl + "course/" + courseURL + "/" + moduleURL;
        }
        else
        {
            int prevOrder = int.Parse(postDisplayOrder) - 1;
            using var cmdPrev = new SqlCommand(
                "SELECT * FROM tbl_Post WHERE CategoryID=@mid AND DisplayOrder=@order AND PostIsActive=1 ORDER BY DisplayOrder", conn);
            cmdPrev.Parameters.AddWithValue("@mid", dt.Rows[0]["courseModules_ID"]);
            cmdPrev.Parameters.AddWithValue("@order", prevOrder);
            var dtPrev = new DataTable();
            dtPrev.Load(await cmdPrev.ExecuteReaderAsync());
            if (dtPrev.Rows.Count > 0)
            {
                model.PrevUrl = SiteUrl + "course/" + courseURL + "/" + moduleURL + "/" + dtPrev.Rows[0]["PostSEOUrl"];
            }
        }

        // Next link
        int nextOrder = int.Parse(postDisplayOrder) + 1;
        using var cmdNext = new SqlCommand(
            "SELECT * FROM tbl_Post WHERE CategoryID=@mid AND DisplayOrder=@order AND PostIsActive=1 ORDER BY DisplayOrder", conn);
        cmdNext.Parameters.AddWithValue("@mid", dt.Rows[0]["courseModules_ID"]);
        cmdNext.Parameters.AddWithValue("@order", nextOrder);
        var dtNext = new DataTable();
        dtNext.Load(await cmdNext.ExecuteReaderAsync());
        if (dtNext.Rows.Count > 0)
        {
            model.NextUrl = SiteUrl + "course/" + courseURL + "/" + moduleURL + "/" + dtNext.Rows[0]["PostSEOUrl"];
        }
        else
        {
            // Try next module
            int moduleOrder = int.Parse(dt.Rows[0]["courseModules_displayorder"]?.ToString() ?? "1") + 1;
            using var cmdNextMod = new SqlCommand(
                "SELECT * FROM tbl_courseModules WHERE courseModules_courseID=@cid AND courseModules_displayorder=@order AND courseModules_isActive=1 ORDER BY courseModules_displayorder", conn);
            cmdNextMod.Parameters.AddWithValue("@cid", courseID);
            cmdNextMod.Parameters.AddWithValue("@order", moduleOrder);
            var dtNextMod = new DataTable();
            dtNextMod.Load(await cmdNextMod.ExecuteReaderAsync());
            if (dtNextMod.Rows.Count > 0)
            {
                model.NextUrl = SiteUrl + "course/" + courseURL + "/" + dtNextMod.Rows[0]["courseModules_ModuleseoURL"];
            }
            else
            {
                model.NextUrl = SiteUrl + "course/assessment/" + courseURL;
                model.NextText = "Start Assessment >>";
            }
        }

        return strRet;
    }
}

public class CourseDetailViewModel
{
    public string CourseName { get; set; } = "";
    public string NavigationHtml { get; set; } = "";
    public bool ShowBackLink { get; set; }
    public string BackUrl { get; set; } = "";
    public bool ShowBackFoodStandards { get; set; }
    public bool ShowStartButton { get; set; }
    public string StartUrl { get; set; } = "";
    public bool ShowPrevNext { get; set; }
    public string PrevUrl { get; set; } = "";
    public string NextUrl { get; set; } = "";
    public string NextText { get; set; } = "Next >>";
    public string ButtonsHtml { get; set; } = "";
}
