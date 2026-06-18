using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

[Route("404")]
public class Error404Controller : Controller
{
    private readonly IConfiguration _config;

    public Error404Controller(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        Response.StatusCode = 404;
        ViewBag.SiteUrl = "http://localhost:5000";
        return View("~/Views/Error404/Index.cshtml");
    }
}
