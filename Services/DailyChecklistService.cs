using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class ChecklistCategoryItem
{
    public long ChecklistCatId { get; set; }
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
    public int StaffId { get; set; }
    public DateTime? ModifiedOn { get; set; }
}

public class UploadedFileItem
{
    public long ChecklistFileUploadedId { get; set; }
    public int CatId { get; set; }
    public int StaffId { get; set; }
    public long ById { get; set; }
    public string File { get; set; } = "";
    public DateTime FileDate { get; set; }
    public string FileTitle { get; set; } = "";
    public string Remarks { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string UploadByCustName { get; set; } = "";
    public string StaffName { get; set; } = "";
}

public class CleaningChecklistItem
{
    public long CleaningChecklistId { get; set; }
    public string Item { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Precautions { get; set; } = "";
    public string Methods { get; set; } = "";
    public int StaffId { get; set; }
    public int DisplayOrder { get; set; }
}

public class SavedCleaningChecklist
{
    public long CleaningChecklistDoneId { get; set; }
    public long ById { get; set; }
    public string Remarks { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class CleaningChecklistDetail
{
    public long ChecklistId { get; set; }
    public long StaffId { get; set; }
}

public class CleaningChecklistNote
{
    public int Id { get; set; }
    public long CleaningChecklistDoneNotesId { get; set; }
    public long DoneId { get; set; }
    public long ById { get; set; }
    public string Notes { get; set; } = "";
}

public class CleaningGridItem
{
    public string CustomerName { get; set; } = "";
    public string ProblemDuringChecklist { get; set; } = "";
    public DateTime DayDate { get; set; }
}

public class DailyChecklistGridItem
{
    public string CustomerNameOpening { get; set; } = "";
    public string ProblemDuringChecklistOpening { get; set; } = "";
    public string CustomerNameClosing { get; set; } = "";
    public string ProblemDuringChecklistClosing { get; set; } = "";
    public string ChecklistDataOpening { get; set; } = "";
    public string ChecklistDataClosing { get; set; } = "";
    public DateTime DayDate { get; set; }
}

public class BakeryStaffItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class DairyCheckItem
{
    public int CheckId { get; set; }
    public string CheckTitle { get; set; } = "";
    public int CheckType { get; set; }
    public int DisplayOrder { get; set; }
}

public class DairyTaskItem
{
    public long TaskId { get; set; }
    public string TaskTitle { get; set; } = "";
    public int TaskCheckType { get; set; }
    public int TaskDisplayOrder { get; set; }
}

public class SavedStaffDairyCheck
{
    public long StaffDairyCheckId { get; set; }
    public long CustomerId { get; set; }
    public int CheckType { get; set; }
    public bool ChecklistDone { get; set; }
    public string ProblemDuringChecklist { get; set; } = "";
    public string ChecklistData { get; set; } = "";
    public long CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class DailyChecklistService
{
    private readonly string _defaultConnection;
    private readonly string _staffAssessmentConnection;

    public DailyChecklistService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
        _staffAssessmentConnection = config.GetConnectionString("StaffAssessmentConnection") ?? "";
    }

    public async Task<List<DailyChecklistGridItem>> GetChecksListAsync(string startDateStr, string endDateStr)
    {
        var list = new List<DailyChecklistGridItem>();

        // Parse dates safely, falling back to current month start/end if invalid
        if (!DateTime.TryParseExact(startDateStr, "d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var sdate))
        {
            if (!DateTime.TryParse(startDateStr, out sdate))
            {
                sdate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
        }
        if (!DateTime.TryParseExact(endDateStr, "d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var edate))
        {
            if (!DateTime.TryParse(endDateStr, out edate))
            {
                edate = DateTime.Now;
            }
        }

        var sql = @"
            SELECT 
                ISNULL((SELECT customer_name FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu WHERE bu.customer_ID = O.customer_ID), '') AS customername_opening,
                ISNULL(O.ProblemDuringChecklist, '') AS ProblemDuringChecklist_opening,
                CASE WHEN C.customer_ID IS NULL THEN '' ELSE 
                    ISNULL((SELECT customer_name FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu WHERE bu.customer_ID = C.customer_ID), '') 
                END AS customername_closing,
                ISNULL(C.ProblemDuringChecklist, '') AS ProblemDuringChecklist_closing,
                ISNULL(O.ChecklistData, '') AS ChecklistData_opening,
                ISNULL(C.ChecklistData, '') AS ChecklistData_closing,
                O.CreatedOn AS daydate
            FROM tbl_Staff_DairyChecks O 
            LEFT JOIN tbl_Staff_DairyChecks C 
                ON (DATEPART(d, O.CreatedOn) = DATEPART(d, C.CreatedOn) 
                    AND DATEPART(m, O.CreatedOn) = DATEPART(m, C.CreatedOn) 
                    AND DATEPART(y, O.CreatedOn) = DATEPART(y, C.CreatedOn) 
                    AND C.CheckType = 2)
            WHERE O.CheckType = 1 
              AND (CAST(O.CreatedOn AS DATE) BETWEEN @sdate AND @edate)
            ORDER BY O.CreatedOn DESC";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sdate", sdate.Date);
        cmd.Parameters.AddWithValue("@edate", edate.Date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DailyChecklistGridItem
            {
                CustomerNameOpening = Convert.ToString(reader["customername_opening"]),
                ProblemDuringChecklistOpening = Convert.ToString(reader["ProblemDuringChecklist_opening"]),
                CustomerNameClosing = Convert.ToString(reader["customername_closing"]),
                ProblemDuringChecklistClosing = Convert.ToString(reader["ProblemDuringChecklist_closing"]),
                ChecklistDataOpening = Convert.ToString(reader["ChecklistData_opening"]),
                ChecklistDataClosing = Convert.ToString(reader["ChecklistData_closing"]),
                DayDate = Convert.ToDateTime(reader["daydate"])
            });
        }

        return list;
    }

    public async Task<List<BakeryStaffItem>> GetBakeryUsersAsync(long webshopId)
    {
        var list = new List<BakeryStaffItem>();
        // Using same query logic as legacy or StaffDashboardService.cs
        var sql = @"
            SELECT customer_ID, customer_Name 
            FROM tbl_bakeryuser 
            WHERE customer_isActive = 1 AND customer_webshopID = @webshopId
            ORDER BY customer_Name";

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@webshopId", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BakeryStaffItem
            {
                Id = Convert.ToInt64(reader["customer_ID"]),
                Name = Convert.ToString(reader["customer_Name"])
            });
        }
        return list;
    }

    public async Task<List<DairyCheckItem>> GetDairyChecksAsync(int checkType)
    {
        var list = new List<DairyCheckItem>();
        var sql = "SELECT CheckID, CheckTitle, CheckType, DisplayOrder FROM tbl_Dairy_Checks WHERE CheckType = @type ORDER BY DisplayOrder";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", checkType);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DairyCheckItem
            {
                CheckId = Convert.ToInt32(reader["CheckID"]),
                CheckTitle = Convert.ToString(reader["CheckTitle"]),
                CheckType = Convert.ToInt32(reader["CheckType"]),
                DisplayOrder = Convert.ToInt32(reader["DisplayOrder"])
            });
        }
        return list;
    }

    public async Task<List<DairyTaskItem>> GetDairyTasksAsync(int checkType)
    {
        var list = new List<DairyTaskItem>();
        var sql = "SELECT task_ID, task_title, task_checktype, task_displayorder FROM tbl_dairyTask WHERE task_checktype = @type ORDER BY task_displayorder";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", checkType);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DairyTaskItem
            {
                TaskId = Convert.ToInt64(reader["task_ID"]),
                TaskTitle = Convert.ToString(reader["task_title"]),
                TaskCheckType = Convert.ToInt32(reader["task_checktype"]),
                TaskDisplayOrder = Convert.ToInt32(reader["task_displayorder"])
            });
        }
        return list;
    }

    public async Task<SavedStaffDairyCheck?> GetSavedChecklistAsync(DateTime date, int checkType)
    {
        var sql = "SELECT Staff_DairyCheckID, customer_ID, CheckType, ChecklistDone, ProblemDuringChecklist, ChecklistData, CreatedBy, CreatedOn FROM tbl_Staff_DairyChecks WHERE CAST(CreatedOn AS DATE) = @date AND CheckType = @type";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@date", date.Date);
        cmd.Parameters.AddWithValue("@type", checkType);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SavedStaffDairyCheck
            {
                StaffDairyCheckId = Convert.ToInt64(reader["Staff_DairyCheckID"]),
                CustomerId = Convert.ToInt64(reader["customer_ID"]),
                CheckType = Convert.ToInt32(reader["CheckType"]),
                ChecklistDone = Convert.ToBoolean(reader["ChecklistDone"]),
                ProblemDuringChecklist = Convert.ToString(reader["ProblemDuringChecklist"]),
                ChecklistData = Convert.ToString(reader["ChecklistData"]),
                CreatedBy = Convert.ToInt64(reader["CreatedBy"]),
                CreatedOn = Convert.ToDateTime(reader["CreatedOn"])
            };
        }
        return null;
    }

    public async Task<Dictionary<long, string>> GetSavedTasksDataAsync(long checklistId)
    {
        var dict = new Dictionary<long, string>();
        var sql = "SELECT stafftask_taskID, stafftask_input FROM tbl_staff_dairytask WHERE stafftask_checkID = @checkId";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@checkId", checklistId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dict[Convert.ToInt64(reader["stafftask_taskID"])] = Convert.ToString(reader["stafftask_input"]);
        }
        return dict;
    }

    public async Task SaveChecklistAsync(
        long customerId, 
        int checkType, 
        bool checklistDone, 
        string problem, 
        DateTime date, 
        Dictionary<long, string> taskInputs, 
        long createdBy)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        // 1. Check if check already exists for this date and checkType
        var findSql = "SELECT Staff_DairyCheckID FROM tbl_Staff_DairyChecks WHERE CAST(CreatedOn AS DATE) = @date AND CheckType = @type";
        long checkId = 0;
        await using (var cmd = new SqlCommand(findSql, conn))
        {
            cmd.Parameters.AddWithValue("@date", date.Date);
            cmd.Parameters.AddWithValue("@type", checkType);
            var val = await cmd.ExecuteScalarAsync();
            if (val != null)
            {
                checkId = Convert.ToInt64(val);
            }
        }

        // Build ChecklistData string from task titles and inputs
        var tasks = await GetDairyTasksAsync(checkType);
        var checklistDataList = new List<string>();
        foreach (var task in tasks)
        {
            string inputVal = taskInputs.TryGetValue(task.TaskId, out var taskVal) ? taskVal : "";
            checklistDataList.Add($"{task.TaskTitle}: {inputVal} °C");
        }
        string checklistData = string.Join(" <br/>", checklistDataList) + " <br/>";

        if (checkId > 0)
        {
            // Update
            var updSql = @"
                UPDATE tbl_Staff_DairyChecks 
                SET ChecklistDone = @done, 
                    ProblemDuringChecklist = @prob, 
                    customer_ID = @custId, 
                    ChecklistData = @chkData,
                    CreatedBy = @createdby
                WHERE Staff_DairyCheckID = @id";

            await using var cmd = new SqlCommand(updSql, conn);
            cmd.Parameters.AddWithValue("@done", checklistDone);
            cmd.Parameters.AddWithValue("@prob", problem ?? "");
            cmd.Parameters.AddWithValue("@custId", customerId);
            cmd.Parameters.AddWithValue("@chkData", checklistData);
            cmd.Parameters.AddWithValue("@createdby", createdBy);
            cmd.Parameters.AddWithValue("@id", checkId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Insert
            var insSql = @"
                INSERT INTO tbl_Staff_DairyChecks (customer_ID, CheckType, ChecklistDone, ProblemDuringChecklist, ChecklistData, CreatedBy, CreatedOn)
                VALUES (@custId, @type, @done, @prob, @chkData, @createdby, @date);
                SELECT SCOPE_IDENTITY();";

            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@custId", customerId);
            cmd.Parameters.AddWithValue("@type", checkType);
            cmd.Parameters.AddWithValue("@done", checklistDone);
            cmd.Parameters.AddWithValue("@prob", problem ?? "");
            cmd.Parameters.AddWithValue("@chkData", checklistData);
            cmd.Parameters.AddWithValue("@createdby", createdBy);
            cmd.Parameters.AddWithValue("@date", date.Date);
            checkId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        // 2. Save/Update individual task inputs in tbl_staff_dairytask
        foreach (var task in tasks)
        {
            string inputVal = taskInputs.TryGetValue(task.TaskId, out var inputValFromDict) ? inputValFromDict : "";

            var checkTaskSql = "SELECT stafftask_ID FROM tbl_staff_dairytask WHERE stafftask_checkID = @chkId AND stafftask_taskID = @taskId";
            long staffTaskId = 0;
            await using (var cmd = new SqlCommand(checkTaskSql, conn))
            {
                cmd.Parameters.AddWithValue("@chkId", checkId);
                cmd.Parameters.AddWithValue("@taskId", task.TaskId);
                var val = await cmd.ExecuteScalarAsync();
                if (val != null)
                {
                    staffTaskId = Convert.ToInt64(val);
                }
            }

            if (staffTaskId > 0)
            {
                // Update
                var updTaskSql = @"
                    UPDATE tbl_staff_dairytask 
                    SET stafftask_input = @input, 
                        stafftask_modifiedOn = GETDATE(), 
                        stafftask_modifiedBy = @modifiedby
                    WHERE stafftask_ID = @id";

                await using var cmd = new SqlCommand(updTaskSql, conn);
                cmd.Parameters.AddWithValue("@input", inputVal);
                cmd.Parameters.AddWithValue("@modifiedby", createdBy);
                cmd.Parameters.AddWithValue("@id", staffTaskId);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Insert
                var insTaskSql = @"
                    INSERT INTO tbl_staff_dairytask (stafftask_taskID, stafftask_checkID, stafftask_input, stafftask_createdOn, stafftask_createdby, stafftask_modifiedOn, stafftask_modifiedBy)
                    VALUES (@taskId, @chkId, @input, GETDATE(), @createdby, GETDATE(), @createdby)";

                await using var cmd = new SqlCommand(insTaskSql, conn);
                cmd.Parameters.AddWithValue("@taskId", task.TaskId);
                cmd.Parameters.AddWithValue("@chkId", checkId);
                cmd.Parameters.AddWithValue("@input", inputVal);
                cmd.Parameters.AddWithValue("@createdby", createdBy);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<List<CleaningGridItem>> GetCleaningChecksListAsync(string startDateStr, string endDateStr)
    {
        var list = new List<CleaningGridItem>();

        if (!DateTime.TryParseExact(startDateStr, "d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var sdate))
        {
            if (!DateTime.TryParse(startDateStr, out sdate))
            {
                sdate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
        }
        if (!DateTime.TryParseExact(endDateStr, "d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var edate))
        {
            if (!DateTime.TryParse(endDateStr, out edate))
            {
                edate = DateTime.Now;
            }
        }

        var sql = @"
            SELECT 
                ISNULL((SELECT customer_name FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu WHERE bu.customer_ID = O.CleaningChecklistDone_byID), '') AS customername,
                ISNULL(O.CleaningChecklistDone_remarks, '') AS ProblemDuringChecklist,
                O.CleaningChecklistDone_createdOn AS daydate
            FROM tbl_CleaningChecklistDone O 
            WHERE (CAST(O.CleaningChecklistDone_createdOn AS DATE) BETWEEN @sdate AND @edate)
            ORDER BY O.CleaningChecklistDone_createdOn DESC";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sdate", sdate.Date);
        cmd.Parameters.AddWithValue("@edate", edate.Date);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CleaningGridItem
            {
                CustomerName = Convert.ToString(reader["customername"]) ?? "",
                ProblemDuringChecklist = Convert.ToString(reader["ProblemDuringChecklist"]) ?? "",
                DayDate = Convert.ToDateTime(reader["daydate"])
            });
        }

        return list;
    }

    public async Task<List<CleaningChecklistItem>> GetCleaningChecklistItemsAsync()
    {
        var list = new List<CleaningChecklistItem>();
        var sql = @"
            SELECT CleaningChecklist_ID, CleaningChecklist_item, CleaningChecklist_frequency, 
                   CleaningChecklist_Precautions, CleaningChecklist_methods, CleaningChecklist_staffID, 
                   CleaningChecklist_displayOrder 
            FROM tbl_CleaningChecklist 
            ORDER BY CleaningChecklist_displayOrder";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CleaningChecklistItem
            {
                CleaningChecklistId = Convert.ToInt64(reader["CleaningChecklist_ID"]),
                Item = Convert.ToString(reader["CleaningChecklist_item"]) ?? "",
                Frequency = Convert.ToString(reader["CleaningChecklist_frequency"]) ?? "",
                Precautions = Convert.ToString(reader["CleaningChecklist_Precautions"]) ?? "",
                Methods = Convert.ToString(reader["CleaningChecklist_methods"]) ?? "",
                StaffId = reader["CleaningChecklist_staffID"] != DBNull.Value ? Convert.ToInt32(reader["CleaningChecklist_staffID"]) : 0,
                DisplayOrder = reader["CleaningChecklist_displayOrder"] != DBNull.Value ? Convert.ToInt32(reader["CleaningChecklist_displayOrder"]) : 0
            });
        }
        return list;
    }

    public async Task<SavedCleaningChecklist?> GetSavedCleaningChecklistAsync(DateTime date)
    {
        var sql = @"
            SELECT CleaningChecklistDone_ID, CleaningChecklistDone_byID, CleaningChecklistDone_remarks, 
                   CleaningChecklistDone_isDone, CleaningChecklistDone_createdOn 
            FROM tbl_CleaningChecklistDone 
            WHERE CAST(CleaningChecklistDone_createdOn AS DATE) = @date";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@date", date.Date);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SavedCleaningChecklist
            {
                CleaningChecklistDoneId = Convert.ToInt64(reader["CleaningChecklistDone_ID"]),
                ById = reader["CleaningChecklistDone_byID"] != DBNull.Value ? Convert.ToInt64(reader["CleaningChecklistDone_byID"]) : 0,
                Remarks = Convert.ToString(reader["CleaningChecklistDone_remarks"]) ?? "",
                IsDone = reader["CleaningChecklistDone_isDone"] != DBNull.Value && Convert.ToBoolean(reader["CleaningChecklistDone_isDone"]),
                CreatedOn = Convert.ToDateTime(reader["CleaningChecklistDone_createdOn"])
            };
        }
        return null;
    }

    public async Task<List<CleaningChecklistDetail>> GetSavedCleaningChecklistDetailsAsync(long doneId)
    {
        var list = new List<CleaningChecklistDetail>();
        var sql = @"
            SELECT CleaningChecklistDonedet_ChecklistID, CleaningChecklistDonedet_staffID 
            FROM tbl_CleaningChecklistDonedet 
            WHERE CleaningChecklistDonedet_doneID = @doneId";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@doneId", doneId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CleaningChecklistDetail
            {
                ChecklistId = Convert.ToInt64(reader["CleaningChecklistDonedet_ChecklistID"]),
                StaffId = reader["CleaningChecklistDonedet_staffID"] != DBNull.Value ? Convert.ToInt64(reader["CleaningChecklistDonedet_staffID"]) : 0
            });
        }
        return list;
    }

    public async Task<List<CleaningChecklistNote>> GetSavedCleaningChecklistNotesAsync(long doneId)
    {
        var list = new List<CleaningChecklistNote>();
        var sql = @"
            SELECT CleaningChecklistDoneNotes_ID, CleaningChecklistDoneNotes_doneID, 
                   CleaningChecklistDoneNotes_byID, CleaningChecklistDoneNotes_notes 
            FROM tbl_CleaningChecklistDoneNotes 
            WHERE CleaningChecklistDoneNotes_doneID = @doneId";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@doneId", doneId);

        int counter = 1;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CleaningChecklistNote
            {
                Id = counter++,
                CleaningChecklistDoneNotesId = Convert.ToInt64(reader["CleaningChecklistDoneNotes_ID"]),
                DoneId = Convert.ToInt64(reader["CleaningChecklistDoneNotes_doneID"]),
                ById = reader["CleaningChecklistDoneNotes_byID"] != DBNull.Value ? Convert.ToInt64(reader["CleaningChecklistDoneNotes_byID"]) : 0,
                Notes = Convert.ToString(reader["CleaningChecklistDoneNotes_notes"]) ?? ""
            });
        }
        return list;
    }

    public async Task SaveCleaningChecklistAsync(
        long customerId,
        bool isDone,
        string remarks,
        DateTime date,
        List<CleaningChecklistDetail> details,
        List<string> notes)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        var findSql = "SELECT CleaningChecklistDone_ID FROM tbl_CleaningChecklistDone WHERE CAST(CleaningChecklistDone_createdOn AS DATE) = @date";
        long doneId = 0;
        await using (var cmd = new SqlCommand(findSql, conn))
        {
            cmd.Parameters.AddWithValue("@date", date.Date);
            var val = await cmd.ExecuteScalarAsync();
            if (val != null)
            {
                doneId = Convert.ToInt64(val);
            }
        }

        if (doneId > 0)
        {
            var updSql = @"
                UPDATE tbl_CleaningChecklistDone 
                SET CleaningChecklistDone_byID = @byId, 
                    CleaningChecklistDone_isDone = @isDone, 
                    CleaningChecklistDone_remarks = @remarks, 
                    CleaningChecklistDone_modifiedOn = GETDATE()
                WHERE CleaningChecklistDone_ID = @doneId";

            await using var cmd = new SqlCommand(updSql, conn);
            cmd.Parameters.AddWithValue("@byId", customerId);
            cmd.Parameters.AddWithValue("@isDone", isDone);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmd.Parameters.AddWithValue("@doneId", doneId);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insSql = @"
                INSERT INTO tbl_CleaningChecklistDone (CleaningChecklistDone_byID, CleaningChecklistDone_isDone, CleaningChecklistDone_remarks, CleaningChecklistDone_createdOn, CleaningChecklistDone_modifiedOn)
                VALUES (@byId, @isDone, @remarks, @date, GETDATE());
                SELECT SCOPE_IDENTITY();";

            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@byId", customerId);
            cmd.Parameters.AddWithValue("@isDone", isDone);
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmd.Parameters.AddWithValue("@date", date.Date);
            doneId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var delDetailsSql = "DELETE FROM tbl_CleaningChecklistDonedet WHERE CleaningChecklistDonedet_doneID = @doneId";
        await using (var cmd = new SqlCommand(delDetailsSql, conn))
        {
            cmd.Parameters.AddWithValue("@doneId", doneId);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var detail in details)
        {
            var insDetailSql = @"
                INSERT INTO tbl_CleaningChecklistDonedet (CleaningChecklistDonedet_ChecklistID, CleaningChecklistDonedet_doneID, CleaningChecklistDonedet_staffID, CleaningChecklistDonedet_modifiedOn)
                VALUES (@checklistId, @doneId, @staffId, GETDATE())";

            await using var cmd = new SqlCommand(insDetailSql, conn);
            cmd.Parameters.AddWithValue("@checklistId", detail.ChecklistId);
            cmd.Parameters.AddWithValue("@doneId", doneId);
            cmd.Parameters.AddWithValue("@staffId", detail.StaffId);
            await cmd.ExecuteNonQueryAsync();
        }

        var delNotesSql = "DELETE FROM tbl_CleaningChecklistDoneNotes WHERE CleaningChecklistDoneNotes_doneID = @doneId AND CleaningChecklistDoneNotes_byID = @byId";
        await using (var cmd = new SqlCommand(delNotesSql, conn))
        {
            cmd.Parameters.AddWithValue("@doneId", doneId);
            cmd.Parameters.AddWithValue("@byId", customerId);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var note in notes)
        {
            if (string.IsNullOrWhiteSpace(note)) continue;

            var insNoteSql = @"
                INSERT INTO tbl_CleaningChecklistDoneNotes (CleaningChecklistDoneNotes_doneID, CleaningChecklistDoneNotes_byID, CleaningChecklistDoneNotes_notes, CleaningChecklistDoneNotes_createdOn, CleaningChecklistDoneNotes_modifiedOn)
                VALUES (@doneId, @byId, @notes, GETDATE(), GETDATE())";

            await using var cmd = new SqlCommand(insNoteSql, conn);
            cmd.Parameters.AddWithValue("@doneId", doneId);
            cmd.Parameters.AddWithValue("@byId", customerId);
            cmd.Parameters.AddWithValue("@notes", note.Trim());
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<ChecklistCategoryItem>> GetChecklistCategoriesAsync()
    {
        var list = new List<ChecklistCategoryItem>();
        var sql = "SELECT checklistCat_ID, checklistCat_title, checklistCat_file, checklistCat_staffID, checklistCat_modifiedOn FROM tbl_checklistCat ORDER BY checklistCat_ID";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ChecklistCategoryItem
            {
                ChecklistCatId = Convert.ToInt64(reader["checklistCat_ID"]),
                Title = Convert.ToString(reader["checklistCat_title"]) ?? "",
                File = Convert.ToString(reader["checklistCat_file"]) ?? "",
                StaffId = reader["checklistCat_staffID"] != DBNull.Value ? Convert.ToInt32(reader["checklistCat_staffID"]) : 0,
                ModifiedOn = reader["checklistCat_modifiedOn"] != DBNull.Value ? Convert.ToDateTime(reader["checklistCat_modifiedOn"]) : null
            });
        }
        return list;
    }

    public async Task<List<UploadedFileItem>> GetUploadedFilesByCatIdAsync(long catId)
    {
        var list = new List<UploadedFileItem>();
        var sql = @"
            SELECT f.checklistFileUploaded_ID, f.checklistFileUploaded_catID, f.checklistFileUploaded_staffID,
                   f.checklistFileUploaded_byID, f.checklistFileUploaded_file, f.checklistFileUploaded_filedate,
                   f.checklistFileUploaded_filetitle, f.checklistFileUploaded_remarks, f.checklistFileUploaded_createdon,
                   f.checklistFileUploaded_modifiedOn,
                   Uploadby_CustName = ISNULL(bu.customer_Name, ''),
                   StaffName = ISNULL(s.customer_Name, '')
            FROM tbl_checklistFileUploaded f
            LEFT OUTER JOIN db_Cakerstreet_live.dbo.tbl_bakeryuser bu ON f.checklistFileUploaded_byID = bu.customer_ID
            LEFT OUTER JOIN db_Cakerstreet_live.dbo.tbl_bakeryuser s ON f.checklistFileUploaded_staffID = s.customer_ID
            WHERE f.checklistFileUploaded_catID = @catId
            ORDER BY f.checklistFileUploaded_filedate DESC, f.checklistFileUploaded_filetitle";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@catId", catId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new UploadedFileItem
            {
                ChecklistFileUploadedId = Convert.ToInt64(reader["checklistFileUploaded_ID"]),
                CatId = Convert.ToInt32(reader["checklistFileUploaded_catID"]),
                StaffId = reader["checklistFileUploaded_staffID"] != DBNull.Value ? Convert.ToInt32(reader["checklistFileUploaded_staffID"]) : 0,
                ById = reader["checklistFileUploaded_byID"] != DBNull.Value ? Convert.ToInt64(reader["checklistFileUploaded_byID"]) : 0,
                File = Convert.ToString(reader["checklistFileUploaded_file"]) ?? "",
                FileDate = Convert.ToDateTime(reader["checklistFileUploaded_filedate"]),
                FileTitle = Convert.ToString(reader["checklistFileUploaded_filetitle"]) ?? "",
                Remarks = Convert.ToString(reader["checklistFileUploaded_remarks"]) ?? "",
                CreatedOn = Convert.ToDateTime(reader["checklistFileUploaded_createdon"]),
                ModifiedOn = Convert.ToDateTime(reader["checklistFileUploaded_modifiedOn"]),
                UploadByCustName = Convert.ToString(reader["Uploadby_CustName"]) ?? "",
                StaffName = Convert.ToString(reader["StaffName"]) ?? ""
            });
        }
        return list;
    }

    public async Task<UploadedFileItem?> GetUploadedFileByIdAsync(long id)
    {
        var sql = @"
            SELECT checklistFileUploaded_ID, checklistFileUploaded_catID, checklistFileUploaded_staffID,
                   checklistFileUploaded_byID, checklistFileUploaded_file, checklistFileUploaded_filedate,
                   checklistFileUploaded_filetitle, checklistFileUploaded_remarks, checklistFileUploaded_createdon,
                   checklistFileUploaded_modifiedOn
            FROM tbl_checklistFileUploaded
            WHERE checklistFileUploaded_ID = @id";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UploadedFileItem
            {
                ChecklistFileUploadedId = Convert.ToInt64(reader["checklistFileUploaded_ID"]),
                CatId = Convert.ToInt32(reader["checklistFileUploaded_catID"]),
                StaffId = reader["checklistFileUploaded_staffID"] != DBNull.Value ? Convert.ToInt32(reader["checklistFileUploaded_staffID"]) : 0,
                ById = reader["checklistFileUploaded_byID"] != DBNull.Value ? Convert.ToInt64(reader["checklistFileUploaded_byID"]) : 0,
                File = Convert.ToString(reader["checklistFileUploaded_file"]) ?? "",
                FileDate = Convert.ToDateTime(reader["checklistFileUploaded_filedate"]),
                FileTitle = Convert.ToString(reader["checklistFileUploaded_filetitle"]) ?? "",
                Remarks = Convert.ToString(reader["checklistFileUploaded_remarks"]) ?? "",
                CreatedOn = Convert.ToDateTime(reader["checklistFileUploaded_createdon"]),
                ModifiedOn = Convert.ToDateTime(reader["checklistFileUploaded_modifiedOn"])
            };
        }
        return null;
    }

    public async Task SaveUploadedFileAsync(
        long id,
        int catId,
        int staffId,
        long userId,
        string file,
        DateTime date,
        string title,
        string remarks)
    {
        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();

        if (id > 0)
        {
            var sql = @"
                UPDATE tbl_checklistFileUploaded
                SET checklistFileUploaded_staffID = @staffId,
                    checklistFileUploaded_byID = @byId,
                    checklistFileUploaded_file = @file,
                    checklistFileUploaded_filedate = @filedate,
                    checklistFileUploaded_filetitle = @filetitle,
                    checklistFileUploaded_remarks = @remarks,
                    checklistFileUploaded_modifiedOn = GETDATE()
                WHERE checklistFileUploaded_ID = @id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@staffId", staffId);
            cmd.Parameters.AddWithValue("@byId", userId);
            cmd.Parameters.AddWithValue("@file", file ?? "");
            cmd.Parameters.AddWithValue("@filedate", date);
            cmd.Parameters.AddWithValue("@filetitle", title ?? "");
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var sql = @"
                INSERT INTO tbl_checklistFileUploaded (checklistFileUploaded_catID, checklistFileUploaded_staffID, checklistFileUploaded_byID, checklistFileUploaded_file, checklistFileUploaded_filedate, checklistFileUploaded_filetitle, checklistFileUploaded_remarks, checklistFileUploaded_createdon, checklistFileUploaded_modifiedOn)
                VALUES (@catId, @staffId, @byId, @file, @filedate, @filetitle, @remarks, GETDATE(), GETDATE())";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@catId", catId);
            cmd.Parameters.AddWithValue("@staffId", staffId);
            cmd.Parameters.AddWithValue("@byId", userId);
            cmd.Parameters.AddWithValue("@file", file ?? "");
            cmd.Parameters.AddWithValue("@filedate", date);
            cmd.Parameters.AddWithValue("@filetitle", title ?? "");
            cmd.Parameters.AddWithValue("@remarks", remarks ?? "");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteUploadedFileAsync(long id)
    {
        var sql = "DELETE FROM tbl_checklistFileUploaded WHERE checklistFileUploaded_ID = @id";

        await using var conn = new SqlConnection(_staffAssessmentConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
