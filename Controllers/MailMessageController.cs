using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

[Route("mailmessage")]
public class MailMessageController : Controller
{
    [HttpGet("{messageMode}/{mesageCode?}")]
    public IActionResult Index(string messageMode, string? mesageCode = null)
    {
        ViewBag.MessageMode = messageMode;
        ViewBag.MessageCode = mesageCode;
        ViewBag.SiteUrl = "http://localhost:5000";
        ViewBag.WebsiteName = "Caker Street";

        return View("~/Views/MailMessage/Index.cshtml");
    }

    // Default route in case no parameters
    [HttpGet("")]
    public IActionResult DefaultIndex()
    {
        return Redirect("http://localhost:5000");
    }
}
