using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays the Daily Dairy navigation page listing three checklist links:
///   1. Opening &amp; Closing Checklist
///   2. Cleaning Checklist
///   3. Upload other checklists
///
/// Migrated from legacy WebForms page: DailyDairy_checklists.aspx / DailyDairy_checklists.aspx.cs
///
/// Legacy behaviour:
///   - Uses BakeryMaster.master master page.
///   - Page_Load sets title and checks for ?msg query parameter (commented out).
///   - No database queries – purely a navigation/menu page.
///   - Links to addupdopeningnclosingchecks, addupdcleaningchecks, addupduploadchecks.
///   - "Back" link goes to haccp page.
///
/// Modern version:
///   - Same static navigation structure with three checklist links.
///   - Uses sidebar layout.
/// </summary>
[Route("dailydairychecklists")]
[Route("dailydairy_checklists")]
[Route("DailyDairy_checklists.aspx")]
public class DailyChecklistsController : Controller
{
    private readonly IConfiguration _config;

    public DailyChecklistsController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Displays the Daily Dairy checklist navigation page.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View("~/Views/DailyChecklists/Index.cshtml");
    }
}
