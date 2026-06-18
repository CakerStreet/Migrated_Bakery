using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for printing a sponge order list.
/// Route: /printorderspongelist
/// Migrated from printorderspongelist.aspx.
/// Renders pre-generated HTML from the database utility function.
/// Standalone print page - no sidebar layout.
/// </summary>
[Route("printorderspongelist")]
[Route("printorderspongelist.aspx")]
public class PrintOrderSpongeListController : Controller
{
    private readonly IConfiguration _config;

    public PrintOrderSpongeListController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long? spongelistID)
    {
        if (!spongelistID.HasValue)
            return BadRequest("spongelistID is required.");

        var connectionString = _config.GetConnectionString("aboraboraboraaboraaborab");
        var printHtml = "";

        // Legacy called clsMail.GetspongeOrderdata_inmail(spongelistID)
        // which returns pre-formatted HTML. We replicate by calling the same function.
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT dbo.fn_GetspongeOrderdata_inmail(@id)", conn);
            cmd.Parameters.AddWithValue("@id", spongelistID.Value);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                printHtml = result.ToString() ?? "";
        }
        catch { }

        ViewBag.PrintHtml = printHtml;
        return View("~/Views/PrintOrderSpongeList/Index.cshtml");
    }
}
