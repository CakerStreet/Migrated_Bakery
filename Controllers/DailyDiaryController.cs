using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Displays the Daily Diary form with fields for Name, Business, Address, Start Date, End Date.
/// Migrated from legacy WebForms page: addupdatedairy.aspx / addupdatedairy.aspx.cs
///
/// Legacy behaviour:
///   - Page_Load is empty – no database queries, no postback handling.
///   - Renders a standalone HTML page (no master page) with bootstrap CSS
///     and a custom dairycss.css stylesheet.
///   - Contains a form with five TextBox controls: Name, Business, Address (multiline),
///     Start Date, End Date.
///   - No submit button or save logic existed in legacy code.
///
/// Modern version:
///   - Renders the same form structure inside the sidebar layout.
///   - No database interaction (matches legacy).
/// </summary>
[Route("addupdatedairy")]
[Route("addupdatedairy.aspx")]
public class DailyDiaryController : Controller
{
    private readonly IConfiguration _config;

    public DailyDiaryController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Displays the Daily Diary form.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View("~/Views/DailyDiary/Index.cshtml");
    }
}
