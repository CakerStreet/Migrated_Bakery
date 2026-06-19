using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays the Opening and Closing Checklists management page with date-range filtering
/// and a results list showing opening/closing check records.
///
/// Migrated from legacy WebForms page: manageDailyCheck_openingnClosing.aspx / manageDailyCheck_openingnClosing.aspx.cs
///
/// Legacy behaviour:
///   - Uses BakeryMaster.master master page.
///   - Page_Load calls bindpagemethod() which validates webstore ID and calls getresultdata().
///   - getresultdata() uses clsCustomGetValue with constr_staffAssessment to query
///     tbl_Staff_DairyChecks with a self-join:
///       O (Opening, CheckType=1) LEFT JOIN C (Closing, CheckType=2) on same date.
///   - Columns: customername_opening, ProblemDuringChecklist_opening,
///     customername_closing, ProblemDuringChecklist_closing, daydate.
///   - Date range defaults to 1/{month}/{year} to today, overridden by ?sdate/edate.
///   - Search button redirects with sdate/edate query params.
///   - Repeater renders 4-column rows: Date, Opening Check, Closing Check, Action.
///   - "Add New Checklist" link goes to ~/addupdopeningnclosingchecks.
///
/// Modern version:
///   - GET with optional sdate/edate query params.
///   - Same SQL query via raw ADO.NET.
///   - Same HTML structure.
/// </summary>
[Route("managedailycheck_openingnclosing")]
[Route("manageDailyCheck_openingnClosing.aspx")]
public class DailyCheckOpenCloseController : Controller
{
    private readonly IConfiguration _config;

    public DailyCheckOpenCloseController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Displays the opening/closing checklists with date range filtering.
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
WHERE bu.customer_ID = O.customer_ID), '') AS customername_opening,
isnull(O.ProblemDuringChecklist, '') AS ProblemDuringChecklist_opening,
CASE WHEN C.customer_ID IS NULL THEN '' ELSE 
isnull((SELECT customer_name FROM db_Cakerstreet_live.dbo.tbl_bakeryuser bu 
WHERE bu.customer_ID = C.customer_ID), '') END AS customername_closing,
isnull(C.ProblemDuringChecklist, '') AS ProblemDuringChecklist_closing,
O.CreatedOn AS daydate
FROM tbl_Staff_DairyChecks O 
LEFT JOIN tbl_Staff_DairyChecks C ON (
datepart(d, O.CreatedOn) = datepart(d, C.CreatedOn) 
AND datepart(m, O.CreatedOn) = datepart(m, C.CreatedOn) 
AND datepart(y, O.CreatedOn) = datepart(y, C.CreatedOn)) 
AND C.CheckType = 2
WHERE O.CheckType = 1 
AND (db_Cakerstreet_live.dbo.dateonly(O.CreatedOn) BETWEEN @fromDate AND @toDate) 
ORDER BY O.CreatedOn DESC";

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
                            ["customername_opening"] = reader["customername_opening"]?.ToString() ?? "",
                            ["ProblemDuringChecklist_opening"] = reader["ProblemDuringChecklist_opening"]?.ToString() ?? "",
                            ["customername_closing"] = reader["customername_closing"]?.ToString() ?? "",
                            ["ProblemDuringChecklist_closing"] = reader["ProblemDuringChecklist_closing"]?.ToString() ?? "",
                            ["daydate"] = reader["daydate"] != DBNull.Value ? Convert.ToDateTime(reader["daydate"]) : DateTime.MinValue
                        });
                    }
                }
            }
        }

        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Results = results;

        return View("~/Views/DailyCheckOpenClose/Index.cshtml");
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
        {
            ViewBag.PageTitle = "Daily Check - Opening & Closing";
            ViewBag.MissingTable = "tbl_Staff_DairyChecks";
            return View("~/Views/Shared/ModuleUnavailable.cshtml");
        }

    }
}
