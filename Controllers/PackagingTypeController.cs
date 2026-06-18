using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("managepackagingtype")]
public class PackagingTypeController : Controller
{
    private readonly PackagingTypeService _packagingService;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public PackagingTypeController(
        PackagingTypeService packagingService,
        BakeryMenuService menuService,
        IConfiguration config)
    {
        _packagingService = packagingService;
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int? typeid = null,
        [FromQuery] string? search = null,
        [FromQuery] string? msg = null,
        [FromQuery] string? errorMsg = null,
        [FromQuery] int? editId = null)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId))
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        // Webshop ID must match csbakeryid (legacy rule, usually "82" or similar)
        var csBakeryId = "82"; // HQ Webshop ID constant
        if (webshopId != csBakeryId)
            return Redirect("/businessorders");

        var prdTypeFilter = typeid ?? 0;
        var searchTerm = search ?? "";

        var list = await _packagingService.GetPackagingTypesAsync(prdTypeFilter, searchTerm);

        // Handle edit modal popup binding if requested
        PackagingTypeItem? editItem = null;
        if (editId.HasValue && editId.Value > 0)
        {
            editItem = await _packagingService.GetByIdAsync(editId.Value);
        }

        // Layout ViewBag params
        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = cdnBase;

        ViewBag.List = list;
        ViewBag.ProductTypeFilter = prdTypeFilter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.SuccessMessage = msg;
        ViewBag.ErrorMessage = errorMsg;
        ViewBag.EditItem = editItem;

        return View("~/Views/PackagingType/Index.cshtml");
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAction(
        [FromForm] string bulkAction,
        [FromForm] List<int> checkedRows,
        [FromForm] List<int> allIds,
        [FromForm] IFormCollection form)
    {
        if (checkedRows == null || checkedRows.Count == 0)
        {
            return Redirect("/managepackagingtype?errorMsg=" + Uri.EscapeDataString("Please select at least one item."));
        }

        string msg = "";
        if (bulkAction == "active")
        {
            await _packagingService.SetActiveStatusAsync(checkedRows, true);
            msg = "Selected packaging types activated successfully.";
        }
        else if (bulkAction == "inactive")
        {
            await _packagingService.SetActiveStatusAsync(checkedRows, false);
            msg = "Selected packaging types deactivated successfully.";
        }
        else if (bulkAction == "delete")
        {
            await _packagingService.DeleteOnlyWithNoProductsAsync(checkedRows);
            msg = "Selected packaging types with no products assigned were deleted.";
        }
        else if (bulkAction == "update")
        {
            foreach (var id in checkedRows)
            {
                var displayOrderVal = form[$"displayOrder_{id}"].ToString();
                if (int.TryParse(displayOrderVal, out var displayOrder))
                {
                    await _packagingService.UpdateDisplayOrderAsync(id, displayOrder);
                }
            }
            msg = "Display orders updated successfully.";
        }

        return Redirect($"/managepackagingtype?msg={Uri.EscapeDataString(msg)}");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(
        [FromForm] int editId,
        [FromForm] string title,
        [FromForm] int productType)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Redirect("/managepackagingtype?errorMsg=" + Uri.EscapeDataString("Title is required."));
        }

        if (productType <= 0)
        {
            return Redirect("/managepackagingtype?errorMsg=" + Uri.EscapeDataString("Product type is required."));
        }

        bool exists = await _packagingService.CheckExistsAsync(editId, title);
        if (exists)
        {
            return Redirect($"/managepackagingtype?editId={editId}&errorMsg={Uri.EscapeDataString("Packaging Type already exists.")}");
        }

        await _packagingService.SaveAsync(editId, title, productType);

        string label = editId > 0 ? "updated" : "added";
        return Redirect($"/managepackagingtype?msg={Uri.EscapeDataString($"Packaging Type has been {label} successfully.")}");
    }
}
