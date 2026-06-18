using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class TempPrintCutter
{
    public List<string> pid { get; set; } = new();
    public string pagesize { get; set; } = "16";
    public string width { get; set; } = "300";
    public string height { get; set; } = "176";
    public string itemperrow { get; set; } = "4";
}

public class KitchenPrintController : Controller
{
    private readonly KitchenPrintService _printService;
    private readonly IConfiguration _config;

    public KitchenPrintController(KitchenPrintService printService, IConfiguration config)
    {
        _printService = printService;
        _config = config;
    }

    [HttpGet("printcutters")]
    public async Task<IActionResult> PrintCutters([FromQuery] string? pids = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        TempPrintCutter cutter;
        var sessionData = HttpContext.Session.GetString("TempPrintCutter");
        if (!string.IsNullOrEmpty(sessionData))
        {
            cutter = JsonSerializer.Deserialize<TempPrintCutter>(sessionData) ?? new TempPrintCutter();
        }
        else
        {
            cutter = new TempPrintCutter();
        }

        if (!string.IsNullOrEmpty(pids))
        {
            cutter.pid = pids.Replace(" ", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            HttpContext.Session.SetString("TempPrintCutter", JsonSerializer.Serialize(cutter));
        }

        var pageNo = 1;
        var pageSize = 16;
        if (int.TryParse(cutter.pagesize, out var ps))
        {
            pageSize = ps;
        }

        var productsResult = new System.Data.DataTable();
        var totalRecords = 0;

        if (cutter.pid.Any())
        {
            var result = await _printService.GetCuttersProductsForPrintAsync(cutter.pid, webshopId, pageNo, pageSize);
            productsResult = result.Products;
            totalRecords = result.TotalRecords;
        }

        ViewBag.Cutter = cutter;
        ViewBag.TotalRecords = totalRecords;

        return View("~/Views/KitchenPrint/PrintCutters.cshtml", productsResult);
    }

    [HttpPost("printcutters")]
    public IActionResult PrintCuttersPost(
        [FromForm] string txtPRdIDs,
        [FromForm] string txtNoOfItems,
        [FromForm] string txtItemsperRow,
        [FromForm] string txtWidth,
        [FromForm] string txtHeight)
    {
        var cutter = new TempPrintCutter
        {
            pagesize = txtNoOfItems ?? "16",
            itemperrow = txtItemsperRow ?? "4",
            width = txtWidth ?? "300",
            height = txtHeight ?? "176",
            pid = (txtPRdIDs ?? "").Replace(" ", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
        };
        HttpContext.Session.SetString("TempPrintCutter", JsonSerializer.Serialize(cutter));
        return RedirectToAction("PrintCutters");
    }

    [HttpGet("printcutters/remove")]
    public IActionResult RemoveProduct([FromQuery] string excludePid)
    {
        var sessionData = HttpContext.Session.GetString("TempPrintCutter");
        if (!string.IsNullOrEmpty(sessionData))
        {
            var cutter = JsonSerializer.Deserialize<TempPrintCutter>(sessionData);
            if (cutter != null)
            {
                cutter.pid = cutter.pid.Where(w => w != excludePid).ToList();
                HttpContext.Session.SetString("TempPrintCutter", JsonSerializer.Serialize(cutter));
            }
        }
        return RedirectToAction("PrintCutters");
    }

    [HttpGet("printcutters/updatecount")]
    public IActionResult UpdateCount([FromQuery] string productId, [FromQuery] string count)
    {
        var sessionData = HttpContext.Session.GetString("TempPrintCutter");
        if (!string.IsNullOrEmpty(sessionData))
        {
            var cutter = JsonSerializer.Deserialize<TempPrintCutter>(sessionData);
            if (cutter != null)
            {
                var newList = new List<string>();
                foreach (var item in cutter.pid)
                {
                    var parts = item.Split('-');
                    if (parts[0] == productId)
                    {
                        newList.Add($"{productId}-{count}");
                    }
                    else
                    {
                        newList.Add(item);
                    }
                }
                cutter.pid = newList;
                HttpContext.Session.SetString("TempPrintCutter", JsonSerializer.Serialize(cutter));
            }
        }
        return RedirectToAction("PrintCutters");
    }

    [HttpGet("managerecipe-print")]
    [HttpGet("managereceipe_print")]
    [HttpGet("ManageReceipe_Print.aspx")]
    public async Task<IActionResult> ManageRecipePrint(
        [FromQuery] string? search = null,
        [FromQuery] int filterstatus = 0,
        [FromQuery] int cookingstatus = 0,
        [FromQuery] int catid = 0,
        [FromQuery] int receipecatid = 0,
        [FromQuery] int receipetagid = 0,
        [FromQuery] int id = 0,
        [FromQuery] string? searchtags = null,
        [FromQuery] string? strIDs = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "82";

        string strsearchtag = "";
        if (!string.IsNullOrEmpty(searchtags))
        {
            strsearchtag = searchtags.Replace("|", ",").Replace("+", " ").Replace("\"", "");
        }

        var resolvedSearch = search;
        if (!string.IsNullOrEmpty(resolvedSearch))
        {
            resolvedSearch = resolvedSearch.Replace("$", "#");
        }

        var recipes = await _printService.GetRecipesForPrintAsync(
            filterstatus,
            cookingstatus,
            webshopId,
            resolvedSearch,
            catid,
            receipecatid,
            receipetagid,
            id,
            strsearchtag,
            strIDs
        );

        ViewBag.RecordCount = recipes.Count;
        return View("~/Views/KitchenPrint/ManageRecipePrint.cshtml", recipes);
    }
}
