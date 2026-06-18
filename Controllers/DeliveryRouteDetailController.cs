using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Controller for the standalone Delivery Route Detail page (read-only, printable).
/// Route: /deliveryroutedetail
/// Migrated from deliveryRouteDetail.aspx.
/// No permission check — standalone printable page accessible via direct link.
/// </summary>
[Route("deliveryroutedetail")]
public class DeliveryRouteDetailController : Controller
{
    private readonly DeliveryRoutesService _routesService;
    private readonly IConfiguration _config;

    public DeliveryRouteDetailController(
        DeliveryRoutesService routesService,
        IConfiguration config)
    {
        _routesService = routesService;
        _config = config;
    }

    [HttpGet("r{routeId:long}")]
    public async Task<IActionResult> Detail(long routeId)
    {
        var model = await _routesService.GetRouteDetailAsync(routeId);

        if (model == null)
            return Redirect("/managedeliveryroutes");

        return View("Detail", model);
    }
}
