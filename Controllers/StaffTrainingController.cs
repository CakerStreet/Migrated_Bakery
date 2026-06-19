using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class TrainingSubmitAssessmentRequest
{
    public long CourseID { get; set; }
    public List<TrainingAnswerEntry> Arr_data_entry { get; set; } = new();
}

public class TrainingAnswerEntry
{
    public long AnsID { get; set; }
    public long QueID { get; set; }
}

[Route("staff-training-ops")]
public class StaffTrainingController : Controller
{
    private readonly StaffTrainingService _trainingService;

    public StaffTrainingController(StaffTrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    [HttpGet("course/{courseUrl}")]
    public async Task<IActionResult> CourseDetail(string courseUrl)
    {
        var course = await _trainingService.GetCourseBySeoUrlAsync(courseUrl);
        if (course == null) return NotFound("Course not found.");

        var modules = await _trainingService.GetModulesByCourseIdAsync(course.CourseId);
        
        ViewBag.Course = course;
        ViewBag.Modules = modules;
        ViewBag.Mode = "Navigation";
        
        return View("~/Views/StaffTraining/CourseDetail.cshtml");
    }

    [HttpGet("course/{courseUrl}/{moduleUrl}")]
    public async Task<IActionResult> ModuleDetail(string courseUrl, string moduleUrl)
    {
        var course = await _trainingService.GetCourseBySeoUrlAsync(courseUrl);
        if (course == null) return NotFound("Course not found.");

        var currentModule = await _trainingService.GetModuleBySeoUrlAsync(course.CourseId, moduleUrl);
        if (currentModule == null) return NotFound("Module not found.");

        var chapters = await _trainingService.GetChaptersByModuleIdAsync(currentModule.ModuleId);
        var modules = await _trainingService.GetModulesByCourseIdAsync(course.CourseId);

        ViewBag.Course = course;
        ViewBag.Modules = modules;
        ViewBag.CurrentModule = currentModule;
        ViewBag.Chapters = chapters;
        ViewBag.Mode = "Module";

        // Resolve navigation links
        var currentOrder = currentModule.DisplayOrder;
        if (currentOrder == 1)
        {
            ViewBag.PrevUrl = $"/course/{courseUrl}";
        }
        else
        {
            var prevModule = modules.FirstOrDefault(m => m.DisplayOrder == currentOrder - 1);
            if (prevModule != null)
            {
                var prevChapters = await _trainingService.GetChaptersByModuleIdAsync(prevModule.ModuleId);
                if (prevChapters.Any())
                {
                    ViewBag.PrevUrl = $"/course/{courseUrl}/{prevModule.ModuleSeoUrl}/{prevChapters.Last().ChapterSeoUrl}";
                }
                else
                {
                    ViewBag.PrevUrl = $"/course/{courseUrl}/{prevModule.ModuleSeoUrl}";
                }
            }
        }

        if (chapters.Any())
        {
            ViewBag.NextUrl = $"/course/{courseUrl}/{moduleUrl}/{chapters.First().ChapterSeoUrl}";
        }
        else
        {
            var nextModule = modules.FirstOrDefault(m => m.DisplayOrder == currentOrder + 1);
            if (nextModule != null)
            {
                ViewBag.NextUrl = $"/course/{courseUrl}/{nextModule.ModuleSeoUrl}";
            }
            else
            {
                ViewBag.NextUrl = $"/course/assessment/{courseUrl}";
            }
        }

        return View("~/Views/StaffTraining/CourseDetail.cshtml");
    }

    [HttpGet("course/{courseUrl}/{moduleUrl}/{chapterUrl}")]
    public async Task<IActionResult> ChapterDetail(string courseUrl, string moduleUrl, string chapterUrl)
    {
        var course = await _trainingService.GetCourseBySeoUrlAsync(courseUrl);
        if (course == null) return NotFound("Course not found.");

        var currentModule = await _trainingService.GetModuleBySeoUrlAsync(course.CourseId, moduleUrl);
        if (currentModule == null) return NotFound("Module not found.");

        var currentChapter = await _trainingService.GetChapterBySeoUrlAsync(currentModule.ModuleId, chapterUrl);
        if (currentChapter == null) return NotFound("Chapter not found.");

        var chapters = await _trainingService.GetChaptersByModuleIdAsync(currentModule.ModuleId);
        var modules = await _trainingService.GetModulesByCourseIdAsync(course.CourseId);

        ViewBag.Course = course;
        ViewBag.Modules = modules;
        ViewBag.CurrentModule = currentModule;
        ViewBag.CurrentChapter = currentChapter;
        ViewBag.Mode = "Chapter";

        // Navigation resolution
        var chapOrder = currentChapter.DisplayOrder;
        if (chapOrder == 1)
        {
            ViewBag.PrevUrl = $"/course/{courseUrl}/{moduleUrl}";
        }
        else
        {
            var prevChap = chapters.FirstOrDefault(c => c.DisplayOrder == chapOrder - 1);
            if (prevChap != null)
            {
                ViewBag.PrevUrl = $"/course/{courseUrl}/{moduleUrl}/{prevChap.ChapterSeoUrl}";
            }
        }

        var nextChap = chapters.FirstOrDefault(c => c.DisplayOrder == chapOrder + 1);
        if (nextChap != null)
        {
            ViewBag.NextUrl = $"/course/{courseUrl}/{moduleUrl}/{nextChap.ChapterSeoUrl}";
        }
        else
        {
            var nextModule = modules.FirstOrDefault(m => m.DisplayOrder == currentModule.DisplayOrder + 1);
            if (nextModule != null)
            {
                ViewBag.NextUrl = $"/course/{courseUrl}/{nextModule.ModuleSeoUrl}";
            }
            else
            {
                ViewBag.NextUrl = $"/course/assessment/{courseUrl}";
                ViewBag.NextText = "Start Assessment >>";
            }
        }

        return View("~/Views/StaffTraining/CourseDetail.cshtml");
    }

    [HttpGet("course/assessment/{courseUrl}")]
    public async Task<IActionResult> CourseAssessment(string courseUrl)
    {
        var course = await _trainingService.GetCourseBySeoUrlAsync(courseUrl);
        if (course == null) return NotFound("Course not found.");

        var questions = await _trainingService.GetQuestionsByCourseIdAsync(course.CourseId);

        ViewBag.Course = course;
        ViewBag.Mode = "Assessment";

        return View("~/Views/StaffTraining/CourseAssessment.cshtml", questions);
    }

    [HttpPost("course/assessment/submit")]
    [Consumes("application/json")]
    public async Task<IActionResult> SubmitAssessment([FromBody] TrainingSubmitAssessmentRequest request)
    {
        var staffId = Convert.ToInt64(HttpContext.Items["BakeryUserId"] ?? 248);

        var answers = request.Arr_data_entry.Select(a => (a.QueID, a.AnsID)).ToList();
        var resultId = await _trainingService.SubmitAssessmentAsync(request.CourseID, staffId, answers);

        if (resultId > 0)
        {
            var result = await _trainingService.GetAssessmentResultAsync(resultId);
            var totalQ = await _trainingService.GetAssessmentResultQuestionCountAsync(resultId);
            var markscount = (result.ResultPercentage * totalQ) / 100m;

            return Json(new {
                data_ID = 1,
                data_str = $"You scored {Math.Round(markscount, 1)}, Out of {totalQ} questions",
                data_optionalstr = resultId.ToString()
            });
        }

        return Json(new { data_ID = 0, data_str = "Error submitting assessment" });
    }

    [HttpGet("course/result/{courseUrl}")]
    public async Task<IActionResult> CourseResultList(string courseUrl)
    {
        var course = await _trainingService.GetCourseBySeoUrlAsync(courseUrl);
        if (course == null) return NotFound("Course not found.");

        var results = await _trainingService.GetAllAssessmentResultsAsync(course.CourseId);

        ViewBag.Course = course;
        ViewBag.Mode = "ResultList";

        return View("~/Views/StaffTraining/CourseAssessment.cshtml", results);
    }

    [HttpGet("course/result/detail/{assessmentId:long}")]
    public async Task<IActionResult> CourseResultDetail(long assessmentId)
    {
        var result = await _trainingService.GetAssessmentResultAsync(assessmentId);
        if (result == null) return NotFound("Result not found.");

        var totalQ = await _trainingService.GetAssessmentResultQuestionCountAsync(assessmentId);
        
        ViewBag.Result = result;
        ViewBag.TotalQuestions = totalQ;
        ViewBag.Mode = "ResultDetail";

        return View("~/Views/StaffTraining/CourseAssessment.cshtml");
    }
}
