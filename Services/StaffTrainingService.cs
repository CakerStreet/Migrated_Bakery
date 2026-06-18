using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class CourseModel
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string CourseSeoUrl { get; set; } = "";
    public decimal PassPercentage { get; set; }
}

public class CourseModuleModel
{
    public long ModuleId { get; set; }
    public long CourseId { get; set; }
    public string ModuleName { get; set; } = "";
    public string ModuleSeoUrl { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class ChapterModel
{
    public long ChapterId { get; set; }
    public long ModuleId { get; set; }
    public string ChapterName { get; set; } = "";
    public string ChapterSeoUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class QuestionModel
{
    public long QuestionId { get; set; }
    public long CourseId { get; set; }
    public string QuestionText { get; set; } = "";
    public long CorrectAnswerId { get; set; }
    public int DisplayOrder { get; set; }
    public List<AnswerModel> Answers { get; set; } = new();
}

public class AnswerModel
{
    public long AnswerId { get; set; }
    public long QuestionId { get; set; }
    public string AnswerText { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public class AssessmentResultModel
{
    public long ResultId { get; set; }
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string CourseSeoUrl { get; set; } = "";
    public long StaffId { get; set; }
    public string StaffName { get; set; } = "";
    public decimal ResultPercentage { get; set; }
    public bool IsPass { get; set; }
    public bool IsNew { get; set; }
    public DateTime ModifiedOn { get; set; }
    public decimal CoursePassPercentage { get; set; }
}

public class StaffTrainingService
{
    private readonly string _staffAssessmentConnection;

    public StaffTrainingService(IConfiguration config)
    {
        _staffAssessmentConnection = config.GetConnectionString("StaffAssessmentConnection") ?? "";
    }

    public async Task<CourseModel?> GetCourseBySeoUrlAsync(string courseUrl)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT course_ID, course_Name, course_seoURL, course_passPercentage FROM tbl_course WHERE course_seoURL = @courseUrl";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@courseUrl", courseUrl);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CourseModel
            {
                CourseId = reader.GetInt64(0),
                CourseName = reader.GetString(1),
                CourseSeoUrl = reader.GetString(2),
                PassPercentage = reader.GetDecimal(3)
            };
        }
        return null;
    }

    public async Task<List<CourseModuleModel>> GetModulesByCourseIdAsync(long courseId)
    {
        var list = new List<CourseModuleModel>();
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT courseModules_ID, courseModules_courseID, courseModules_ModuleName, courseModules_ModuleseoURL, courseModules_displayorder, courseModules_isActive FROM tbl_courseModules WHERE courseModules_courseID = @courseId AND courseModules_isActive = 1 ORDER BY courseModules_displayorder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@courseId", courseId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CourseModuleModel
            {
                ModuleId = reader.GetInt64(0),
                CourseId = reader.GetInt64(1),
                ModuleName = reader.GetString(2),
                ModuleSeoUrl = reader.GetString(3),
                DisplayOrder = reader.GetInt32(4),
                IsActive = reader.GetBoolean(5)
            });
        }
        return list;
    }

    public async Task<List<ChapterModel>> GetChaptersByModuleIdAsync(long moduleId)
    {
        var list = new List<ChapterModel>();
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT PostID, CategoryID, PostName, PostSEOUrl, Description, PostIsActive, DisplayOrder FROM tbl_Post WHERE CategoryID = @moduleId AND PostIsActive = 1 ORDER BY DisplayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@moduleId", moduleId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ChapterModel
            {
                ChapterId = reader.GetInt64(0),
                ModuleId = reader.GetInt64(1),
                ChapterName = reader.GetString(2),
                ChapterSeoUrl = reader.GetString(3),
                Description = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                DisplayOrder = reader.GetInt32(6)
            });
        }
        return list;
    }

    public async Task<CourseModuleModel?> GetModuleBySeoUrlAsync(long courseId, string moduleUrl)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT courseModules_ID, courseModules_courseID, courseModules_ModuleName, courseModules_ModuleseoURL, courseModules_displayorder, courseModules_isActive FROM tbl_courseModules WHERE courseModules_courseID = @courseId AND courseModules_ModuleseoURL = @moduleUrl AND courseModules_isActive = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@courseId", courseId);
        cmd.Parameters.AddWithValue("@moduleUrl", moduleUrl);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CourseModuleModel
            {
                ModuleId = reader.GetInt64(0),
                CourseId = reader.GetInt64(1),
                ModuleName = reader.GetString(2),
                ModuleSeoUrl = reader.GetString(3),
                DisplayOrder = reader.GetInt32(4),
                IsActive = reader.GetBoolean(5)
            };
        }
        return null;
    }

    public async Task<ChapterModel?> GetChapterBySeoUrlAsync(long moduleId, string chapterUrl)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT PostID, CategoryID, PostName, PostSEOUrl, Description, PostIsActive, DisplayOrder FROM tbl_Post WHERE CategoryID = @moduleId AND PostSEOUrl = @chapterUrl AND PostIsActive = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@moduleId", moduleId);
        cmd.Parameters.AddWithValue("@chapterUrl", chapterUrl);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ChapterModel
            {
                ChapterId = reader.GetInt64(0),
                ModuleId = reader.GetInt64(1),
                ChapterName = reader.GetString(2),
                ChapterSeoUrl = reader.GetString(3),
                Description = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                DisplayOrder = reader.GetInt32(6)
            };
        }
        return null;
    }

    public async Task<List<QuestionModel>> GetQuestionsByCourseIdAsync(long courseId)
    {
        var list = new List<QuestionModel>();
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT courseAssessment_ID, courseAssessment_courseID, courseAssessment_Question, courseAssessment_AnswerID, courseAssessment_displayOrder FROM tbl_courseAssessment WHERE courseAssessment_courseID = @courseId ORDER BY courseAssessment_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@courseId", courseId);

        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new QuestionModel
                {
                    QuestionId = reader.GetInt64(0),
                    CourseId = reader.GetInt64(1),
                    QuestionText = reader.GetString(2),
                    CorrectAnswerId = reader.GetInt64(3),
                    DisplayOrder = reader.GetInt32(4)
                });
            }
        }

        // Fetch answers for each question
        foreach (var q in list)
        {
            var answersSql = "SELECT assessAnsList_ID, assessAnsList_quesID, assessAnsList_title, assessAnsList_displaOrder FROM tbl_assessAnsList WHERE assessAnsList_quesID = @questionId ORDER BY assessAnsList_displaOrder";
            await using var ansCmd = new SqlCommand(answersSql, conn);
            ansCmd.Parameters.AddWithValue("@questionId", q.QuestionId);
            await using var ansReader = await ansCmd.ExecuteReaderAsync();
            while (await ansReader.ReadAsync())
            {
                q.Answers.Add(new AnswerModel
                {
                    AnswerId = ansReader.GetInt64(0),
                    QuestionId = ansReader.GetInt64(1),
                    AnswerText = ansReader.GetString(2),
                    DisplayOrder = ansReader.GetInt32(3)
                });
            }
        }

        return list;
    }

    public async Task<long> SubmitAssessmentAsync(long courseId, long staffId, List<(long questionId, long answerId)> answers)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        // 1. Fetch questions and correct answers
        var questions = new List<(long qId, long correctAnsId)>();
        var sqlQ = "SELECT courseAssessment_ID, courseAssessment_AnswerID, c.course_passPercentage FROM tbl_courseAssessment q JOIN tbl_course c ON q.courseAssessment_courseID = c.course_ID WHERE q.courseAssessment_courseID = @courseId";
        decimal passPercent = 70.00m;
        await using (var cmdQ = new SqlCommand(sqlQ, conn))
        {
            cmdQ.Parameters.AddWithValue("@courseId", courseId);
            await using var readerQ = await cmdQ.ExecuteReaderAsync();
            while (await readerQ.ReadAsync())
            {
                questions.Add((readerQ.GetInt64(0), readerQ.GetInt64(1)));
                passPercent = readerQ.GetDecimal(2);
            }
        }

        if (!questions.Any()) return 0;

        // 2. Insert initial Assessment Result via SP
        await using var cmdResult = new SqlCommand("insUpdAssessmentResult", conn);
        cmdResult.CommandType = CommandType.StoredProcedure;
        cmdResult.Parameters.AddWithValue("@assessResult_ID", 0);
        cmdResult.Parameters.AddWithValue("@assessResult_courseID", courseId);
        cmdResult.Parameters.AddWithValue("@assessResult_staffID", staffId);
        cmdResult.Parameters.AddWithValue("@assessResult_resultPercentage", 0.00m);
        cmdResult.Parameters.AddWithValue("@assessResult_ispass", false);
        cmdResult.Parameters.AddWithValue("@assessResult_isnew", true);
        
        var retIdParam = new SqlParameter("@retID", SqlDbType.BigInt);
        retIdParam.Direction = ParameterDirection.InputOutput;
        retIdParam.Value = 0;
        cmdResult.Parameters.Add(retIdParam);

        await cmdResult.ExecuteNonQueryAsync();
        long resultId = Convert.ToInt64(retIdParam.Value);

        // 3. Score and insert detail records
        int correctCount = 0;
        foreach (var q in questions)
        {
            var userAns = answers.FirstOrDefault(a => a.questionId == q.qId);
            long ansId = userAns.answerId;
            bool isPass = (ansId == q.correctAnsId);
            if (isPass) correctCount++;

            await using var cmdDet = new SqlCommand("insUpdAssessmentResultDet", conn);
            cmdDet.CommandType = CommandType.StoredProcedure;
            cmdDet.Parameters.AddWithValue("@assessResultDet_resultID", resultId);
            cmdDet.Parameters.AddWithValue("@assessResultDet_questID", q.qId);
            cmdDet.Parameters.AddWithValue("@assessResultDet_ansID", ansId);
            cmdDet.Parameters.AddWithValue("@assessResultDet_ispass", isPass);
            cmdDet.Parameters.AddWithValue("@assessResultDet_correctAnsID", q.correctAnsId);
            await cmdDet.ExecuteNonQueryAsync();
        }

        // 4. Update final assessment result with actual percentage and pass status
        decimal scorePercent = ((decimal)correctCount / questions.Count) * 100m;
        bool finalPass = (scorePercent >= passPercent);

        await using var cmdUpdate = new SqlCommand("insUpdAssessmentResult", conn);
        cmdUpdate.CommandType = CommandType.StoredProcedure;
        cmdUpdate.Parameters.AddWithValue("@assessResult_ID", resultId);
        cmdUpdate.Parameters.AddWithValue("@assessResult_courseID", courseId);
        cmdUpdate.Parameters.AddWithValue("@assessResult_staffID", staffId);
        cmdUpdate.Parameters.AddWithValue("@assessResult_resultPercentage", scorePercent);
        cmdUpdate.Parameters.AddWithValue("@assessResult_ispass", finalPass);
        cmdUpdate.Parameters.AddWithValue("@assessResult_isnew", true);
        
        var retIdParam2 = new SqlParameter("@retID", SqlDbType.BigInt);
        retIdParam2.Direction = ParameterDirection.InputOutput;
        retIdParam2.Value = resultId;
        cmdUpdate.Parameters.Add(retIdParam2);

        await cmdUpdate.ExecuteNonQueryAsync();

        return resultId;
    }

    public async Task<AssessmentResultModel?> GetAssessmentResultAsync(long resultId)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT r.assessResult_ID, 
                   r.assessResult_courseID, 
                   c.course_Name, 
                   c.course_seoURL, 
                   r.assessResult_staffID, 
                   u.customer_Name, 
                   r.assessResult_resultPercentage, 
                   r.assessResult_ispass, 
                   r.assessResult_isnew, 
                   r.assessResult_modifiedOn,
                   c.course_passPercentage
            FROM tbl_assessResult r
            JOIN tbl_course c ON r.assessResult_courseID = c.course_ID
            LEFT JOIN db_cakerstreet_live.dbo.tbl_bakeryuser u ON r.assessResult_staffID = u.customer_ID
            WHERE r.assessResult_ID = @resultId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@resultId", resultId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AssessmentResultModel
            {
                ResultId = reader.GetInt64(0),
                CourseId = reader.GetInt64(1),
                CourseName = reader.GetString(2),
                CourseSeoUrl = reader.GetString(3),
                StaffId = reader.GetInt64(4),
                StaffName = reader.IsDBNull(5) ? "Staff Member" : reader.GetString(5),
                ResultPercentage = reader.GetDecimal(6),
                IsPass = reader.GetBoolean(7),
                IsNew = reader.GetBoolean(8),
                ModifiedOn = reader.GetDateTime(9),
                CoursePassPercentage = reader.GetDecimal(10)
            };
        }
        return null;
    }

    public async Task<int> GetAssessmentResultQuestionCountAsync(long resultId)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = "SELECT COUNT(1) FROM tbl_assessResultDet WHERE assessResultDet_resultID = @resultId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@resultId", resultId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<AssessmentResultModel>> GetAllAssessmentResultsAsync(long courseId)
    {
        var list = new List<AssessmentResultModel>();
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT r.assessResult_ID, 
                   r.assessResult_courseID, 
                   r.assessResult_staffID, 
                   u.customer_Name, 
                   r.assessResult_resultPercentage, 
                   r.assessResult_ispass, 
                   r.assessResult_isnew, 
                   r.assessResult_modifiedOn
            FROM tbl_assessResult r
            LEFT JOIN db_cakerstreet_live.dbo.tbl_bakeryuser u ON r.assessResult_staffID = u.customer_ID
            WHERE r.assessResult_isnew = 1 AND r.assessResult_courseID = @courseId 
            ORDER BY r.assessResult_modifiedOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@courseId", courseId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AssessmentResultModel
            {
                ResultId = reader.GetInt64(0),
                CourseId = reader.GetInt64(1),
                StaffId = reader.GetInt64(2),
                StaffName = reader.IsDBNull(3) ? "Staff Member" : reader.GetString(3),
                ResultPercentage = reader.GetDecimal(4),
                IsPass = reader.GetBoolean(5),
                IsNew = reader.GetBoolean(6),
                ModifiedOn = reader.GetDateTime(7)
            });
        }
        return list;
    }
}
