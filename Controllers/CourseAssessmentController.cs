using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the Course Assessment page.
/// Supports 3 modes: assessment questions, results listing, and individual result view.
/// Migrated from legacy courseAssessment.aspx / courseAssessment.aspx.cs.
/// </summary>
[Route("courseassessment")]
public class CourseAssessmentController : Controller
{
    private readonly IConfiguration _config;

    public CourseAssessmentController(IConfiguration config)
    {
        _config = config;
    }

    private string CourseConnStr => _config.GetConnectionString("StaffAssessment")
                                    ?? _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";
    private string MainConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

    /// <summary>Assessment questions mode: /course/assessment/{courseURL}</summary>
    [HttpGet("~/course/assessment/{courseURL}")]
    public async Task<IActionResult> Index(string courseURL)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseAssessmentViewModel { Mode = "assessment" };
        model.BackUrl = "/course/" + courseURL;

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        // Get course
        using var cmdCourse = new SqlCommand("SELECT * FROM tbl_course WHERE course_seoURL=@url", conn);
        cmdCourse.Parameters.AddWithValue("@url", courseURL);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();

        model.CourseId = rdrCourse["course_ID"]?.ToString() ?? "";
        model.CourseName = rdrCourse["course_Name"]?.ToString() + " -  Assessment";
        ViewData["Title"] = "Course Assessment - " + rdrCourse["course_Name"]?.ToString();
        rdrCourse.Close();

        // Get questions
        using var cmdQ = new SqlCommand(
            "SELECT * FROM tbl_courseAssessment WHERE courseAssessment_courseID=@cid ORDER BY courseAssessment_displayOrder", conn);
        cmdQ.Parameters.AddWithValue("@cid", model.CourseId);
        using var rdrQ = await cmdQ.ExecuteReaderAsync();
        while (await rdrQ.ReadAsync())
        {
            var q = new AssessmentQuestion
            {
                QuestionId = rdrQ["courseAssessment_ID"]?.ToString() ?? "",
                QuestionText = rdrQ["courseAssessment_Question"]?.ToString() ?? ""
            };
            model.Questions.Add(q);
        }
        rdrQ.Close();

        // Get answers for each question
        foreach (var q in model.Questions)
        {
            using var cmdA = new SqlCommand(
                "SELECT * FROM tbl_assessAnsList WHERE assessAnsList_quesID=@qid ORDER BY assessAnsList_displaOrder", conn);
            cmdA.Parameters.AddWithValue("@qid", q.QuestionId);
            using var rdrA = await cmdA.ExecuteReaderAsync();
            while (await rdrA.ReadAsync())
            {
                q.Answers.Add(new AssessmentAnswer
                {
                    AnswerId = rdrA["assessAnsList_ID"]?.ToString() ?? "",
                    QuestionId = rdrA["assessAnsList_quesID"]?.ToString() ?? "",
                    Title = rdrA["assessAnsList_title"]?.ToString() ?? ""
                });
            }
        }

        return View("~/Views/CourseAssessment/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Assessment";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>Results listing mode: /course/result/{courseURL}</summary>
    [HttpGet("~/course/result/{courseURLall}")]
    public async Task<IActionResult> ResultList(string courseURLall)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseAssessmentViewModel { Mode = "resultlist" };
        model.BackUrl = "/course/" + courseURLall;
        model.ShowSubmitButton = false;

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        using var cmdCourse = new SqlCommand("SELECT * FROM tbl_course WHERE course_seoURL=@url", conn);
        cmdCourse.Parameters.AddWithValue("@url", courseURLall);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();

        model.CourseId = rdrCourse["course_ID"]?.ToString() ?? "";
        model.CourseName = rdrCourse["course_Name"]?.ToString() + " -  Assessment Result";
        ViewData["Title"] = "Course Assessment - " + rdrCourse["course_Name"]?.ToString();
        rdrCourse.Close();

        // Get results
        using var cmdR = new SqlCommand(@"
            SELECT * FROM tbl_assessResult 
            INNER JOIN db_Cakerstreet_live.dbo.tbl_bakeryuser ON assessResult_staffID=customer_ID
            WHERE assessResult_isnew=1 AND assessResult_courseID=@cid 
            ORDER BY assessResult_modifiedOn DESC", conn);
        cmdR.Parameters.AddWithValue("@cid", model.CourseId);
        using var rdrR = await cmdR.ExecuteReaderAsync();
        while (await rdrR.ReadAsync())
        {
            model.Results.Add(new AssessmentResult
            {
                CustomerName = rdrR["customer_Name"]?.ToString() ?? "",
                IsPass = Convert.ToBoolean(rdrR["assessResult_ispass"]),
                ResultPercentage = Convert.ToDouble(rdrR["assessResult_resultPercentage"]),
                ModifiedOn = Convert.ToDateTime(rdrR["assessResult_modifiedOn"])
            });
        }

        return View("~/Views/CourseAssessment/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Assessment";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>Individual result mode: /courseresult/{assessmentID}</summary>
    [HttpGet("~/courseresult/{assessmentID}")]
    public async Task<IActionResult> ViewResult(string assessmentID)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0") return Redirect("/editbusinessinfo");

        var model = new CourseAssessmentViewModel { Mode = "result" };
        model.ShowSubmitButton = false;

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        using var cmdCourse = new SqlCommand(@"
            SELECT * FROM tbl_course 
            INNER JOIN tbl_assessResult ON assessResult_courseID=course_ID
            WHERE assessResult_ID=@rid", conn);
        cmdCourse.Parameters.AddWithValue("@rid", assessmentID);
        using var rdrCourse = await cmdCourse.ExecuteReaderAsync();
        if (!await rdrCourse.ReadAsync()) return NotFound();

        model.CourseId = rdrCourse["course_ID"]?.ToString() ?? "";
        var courseSeoUrl = rdrCourse["course_seoURL"]?.ToString() ?? "";
        model.BackUrl = "/course/" + courseSeoUrl;
        model.CourseName = rdrCourse["course_Name"]?.ToString() + " -  Assessment Result";
        ViewData["Title"] = "Course Assessment Result - " + rdrCourse["course_Name"]?.ToString();

        var resultPercentage = Convert.ToDouble(rdrCourse["assessResult_resultPercentage"]);
        var isPass = Convert.ToBoolean(rdrCourse["assessResult_ispass"]);
        var passPercentage = Convert.ToDecimal(rdrCourse["course_passPercentage"]);
        rdrCourse.Close();

        // Count questions
        using var cmdCount = new SqlCommand(
            "SELECT COUNT(1) FROM tbl_assessResultDet WHERE assessResultDet_resultID=@rid", conn);
        cmdCount.Parameters.AddWithValue("@rid", assessmentID);
        var totalQuestions = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());

        var marksCount = (decimal)(resultPercentage * totalQuestions / 100);
        model.ResultInfoHtml = "You have scored <span>" + (double)marksCount +
            "</span> [Out of <span>" + totalQuestions +
            "</span> questions] - <span>" + resultPercentage + "%</span>";
        model.ResultInfoCssClass = isPass ? "informationNotes st" : "informationNotes stf";
        model.ShowInfoDetail = isPass;
        model.ShowRestartButton = true;

        if (isPass)
        {
            model.RestartText = "Click here to continue...";
            model.RestartUrl = "/";
        }
        else
        {
            model.RestartText = "Start Re-Assessment";
            model.RestartUrl = "/course/assessment/" + courseSeoUrl;
        }

        return View("~/Views/CourseAssessment/Index.cshtml", model);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Course Assessment";
            ViewBag.MissingTable = "tbl_course";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }

    /// <summary>Submit assessment answers via AJAX</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAssessment([FromBody] SubmitAssessmentRequest request)
    {
        var userId = HttpContext.Items["BakeryUserId"]?.ToString() ?? "0";
        var result = new { data_ID = 0, data_str = "", data_optionalstr = "" };

        using var conn = new SqlConnection(CourseConnStr);
        await conn.OpenAsync();

        using var cmdQ = new SqlCommand(@"
            SELECT * FROM tbl_courseAssessment 
            INNER JOIN tbl_course ON course_ID=courseAssessment_courseID
            WHERE courseAssessment_courseID=@cid 
            ORDER BY courseAssessment_displayOrder", conn);
        cmdQ.Parameters.AddWithValue("@cid", request.CourseID);
        var dt = new DataTable();
        dt.Load(await cmdQ.ExecuteReaderAsync());

        if (dt.Rows.Count > 0)
        {
            int totalQuestions = dt.Rows.Count;
            decimal minPassingMarks = Convert.ToDecimal(dt.Rows[0]["course_passPercentage"]) * totalQuestions / 100;
            int marksCount = 0;

            // Insert initial result
            long resultId = await InsUpdAssessmentResult(conn, 0, request.CourseID, long.Parse(userId), 100, true, true);

            foreach (DataRow dr in dt.Rows)
            {
                long correctAnsID = Convert.ToInt64(dr["courseAssessment_AnswerID"]);
                long queID = Convert.ToInt64(dr["courseAssessment_ID"]);
                long ansID = 0;
                bool isPass = false;

                var match = request.Answers?.FirstOrDefault(a => a.QueID == queID);
                if (match != null)
                {
                    ansID = match.AnsID;
                    if (ansID == correctAnsID) { marksCount++; isPass = true; }
                }
                await InsUpdAssessmentResultDet(conn, resultId, queID, ansID, isPass, correctAnsID);
            }

            decimal finalPercentage = Convert.ToDecimal(marksCount * 100 / totalQuestions);
            await InsUpdAssessmentResult(conn, resultId, request.CourseID, long.Parse(userId),
                finalPercentage, marksCount >= minPassingMarks, true);

            return Json(new
            {
                d = new
                {
                    data_ID = 1,
                    data_str = "You scored " + marksCount + ", Out of " + totalQuestions + " questions",
                    data_optionalstr = resultId.ToString()
                }
            });
        }

        return Json(new { d = new { data_ID = 0, data_str = "Error", data_optionalstr = "" } });
    }

    private async Task<long> InsUpdAssessmentResult(SqlConnection conn, long id, long courseId, long staffId,
        decimal percentage, bool isPass, bool isNew)
    {
        using var cmd = new SqlCommand("insUpdAssessmentResult", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@assessResult_ID", id);
        cmd.Parameters.AddWithValue("@assessResult_courseID", courseId);
        cmd.Parameters.AddWithValue("@assessResult_staffID", staffId);
        cmd.Parameters.AddWithValue("@assessResult_resultPercentage", percentage);
        cmd.Parameters.AddWithValue("@assessResult_ispass", isPass);
        cmd.Parameters.AddWithValue("@assessResult_isnew", isNew);
        var retParam = new SqlParameter("@retID", SqlDbType.BigInt) { Direction = ParameterDirection.InputOutput, Value = id };
        cmd.Parameters.Add(retParam);
        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt64(retParam.Value);
    }

    private async Task InsUpdAssessmentResultDet(SqlConnection conn, long resultId, long questId,
        long ansId, bool isPass, long correctAnsId)
    {
        using var cmd = new SqlCommand("insUpdAssessmentResultDet", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@assessResultDet_resultID", resultId);
        cmd.Parameters.AddWithValue("@assessResultDet_questID", questId);
        cmd.Parameters.AddWithValue("@assessResultDet_ansID", ansId);
        cmd.Parameters.AddWithValue("@assessResultDet_ispass", isPass);
        cmd.Parameters.AddWithValue("@assessResultDet_correctAnsID", correctAnsId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public class SubmitAssessmentRequest
{
    public long CourseID { get; set; }
    public List<AnswerEntry>? Answers { get; set; }
}

public class AnswerEntry
{
    public long AnsID { get; set; }
    public long QueID { get; set; }
}

public class CourseAssessmentViewModel
{
    public string Mode { get; set; } = "assessment"; // assessment, resultlist, result
    public string CourseId { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string BackUrl { get; set; } = "";
    public bool ShowSubmitButton { get; set; } = true;
    public bool ShowRestartButton { get; set; }
    public string RestartUrl { get; set; } = "";
    public string RestartText { get; set; } = "Start Re-Assessment";
    public string ResultInfoHtml { get; set; } = "";
    public string ResultInfoCssClass { get; set; } = "informationNotes";
    public bool ShowInfoDetail { get; set; }
    public List<AssessmentQuestion> Questions { get; set; } = new();
    public List<AssessmentResult> Results { get; set; } = new();
}

public class AssessmentQuestion
{
    public string QuestionId { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public List<AssessmentAnswer> Answers { get; set; } = new();
}

public class AssessmentAnswer
{
    public string AnswerId { get; set; } = "";
    public string QuestionId { get; set; } = "";
    public string Title { get; set; } = "";
}

public class AssessmentResult
{
    public string CustomerName { get; set; } = "";
    public bool IsPass { get; set; }
    public double ResultPercentage { get; set; }
    public DateTime ModifiedOn { get; set; }
}
