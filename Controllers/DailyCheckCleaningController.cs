using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays the Cleaning Checklists management page with date-range filtering
/// and a results list showing cleaning check records.
///
/// Migrated from legacy WebForms page: manageDailyCheck_cleaning.aspx / manageDailyCheck_cleaning.aspx.cs
///
/// Legacy behaviour:
///   - Uses BakeryMaster.master master page.
///   - Page_Load calls bindpagemethod() which validates the webstore ID and calls getresultdata().
///   - getresultdata() uses clsCustomGetValue with constr_staffAssessment connection string
///     to query tbl_CleaningChecklistDone with columns:
///       customername (from tbl_bakeryuser via subquery),
///       ProblemDuringChecklist (from CleaningChecklistDone_remarks),
///       daydate (from CleaningChecklistDone_createdOn).
///   - Date range defaults to 1/{month}/{year} through today, overridden by ?sdate/edate params.
///   - Search button redirects with sdate/edate query params.
///   - Repeater renders results with date, cleaning check details, and View|Edit action link.
///   - "Add New Checklist" link goes to ~/addupdcleaningchecks.
///
/// Modern version:
///   - GET with optional sdate/edate query params.
///   - Same SQL query via raw ADO.NET.
///   - Same HTML structure with date filters and results table.
/// </summary>
[Route("managedailycheck_cleaning")]
[Route("manageDailyCheck_cleaning.aspx")]
public class DailyCheckCleaningController : Controller
{
    private readonly IConfiguration _config;

    public DailyCheckCleaningController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Displays the cleaning checklists with date range filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string sdate = null, [FromQuery] string edate = null)
    {
        try
        {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (webshopId == "0")
            return Redirect("/editbusinessinfo");

        var connStr = _config.GetConnectionString("aboraboraboraaboraaborab");

        // Default dates matching legacy logic
        string fromDate = $"1/{DateTime.Now.Month}/{DateTime.Now.Year}";
        string toDate = DateTime.Now.ToString("dd/MM/yyyy");
        if (!string.IsNullOrEmpty(sdate) && !string.IsNullOrEmpty(edate))
        {
            fromDate = sdate;
            toDate = edate;
        }

        var results = new List<Dictionary<string, object>>();

        using (var con = new SqlConnection(connStr))
        {
            await con.OpenAsync();

            string query = @"SELECT 
isnull((SELECT customer_name FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu 
WHERE bu.customer_ID = O.CleaningChecklistDone_byID), '') AS customername,
isnull(O.CleaningChecklistDone_remarks, '') AS ProblemDuringChecklist,
O.CleaningChecklistDone_createdOn AS daydate
FROM tbl_CleaningChecklistDone O
WHERE (db_Cakerstreet_live.dbo.dateonly(O.CleaningChecklistDone_createdOn) 
BETWEEN @fromDate AND @toDate) 
ORDER BY O.CleaningChecklistDone_createdOn DESC";

            using (var cmd = new SqlCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@fromDate", fromDate);
                cmd.Parameters.AddWithValue("@toDate", toDate);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            ["customername"] = reader["customername"]?.ToString() ?? "",
                            ["ProblemDuringChecklist"] = reader["ProblemDuringChecklist"]?.ToString() ?? "",
                            ["daydate"] = reader["daydate"] != DBNull.Value ? Convert.ToDateTime(reader["daydate"]) : DateTime.MinValue
                        });
                    }
                }
            }
        }

        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Results = results;

        return View("~/Views/DailyCheckCleaning/Index.cshtml");
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Daily Check - Cleaning";
            ViewBag.MissingTable = "tbl_CleaningChecklistDone";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }
    }
}
