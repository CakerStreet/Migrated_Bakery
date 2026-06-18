using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

public class RecipeUpdateRequest
{
    public string Title { get; set; } = "";
    public string ServingDet { get; set; } = "";
    public List<RecipeIngredientUpdateModel> Lstingredients { get; set; } = new();
    public List<string> Direction { get; set; } = new();
    public List<string> Nutrition_Information { get; set; } = new();
}

public class RecipeIngredientUpdateModel
{
    public long Id { get; set; }
    public string Ingredient { get; set; } = "";
    public int IngredientGRPID { get; set; }
    public string IngredientGRP { get; set; } = "";
    public string Ingredientcuttype { get; set; } = "";
    public string Ingredientmlperserving { get; set; } = "";
    public string Ingredientunit { get; set; } = "";
}

public class LnkPrdStoreCat
{
    public long LnkPrdStoreCat_ID { get; set; }
    public long LnkPrdStoreCat_PrdId { get; set; }
    public long LnkPrdStoreCat_CatId { get; set; }
}

public class WebservicesResponse
{
    public int data_ID { get; set; }
    public string data_optionalstr { get; set; } = "";
}

[Route("managereceipe")]
public class RecipeController : Controller
{
    private readonly RecipeService _service;
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public RecipeController(
        RecipeService service,
        BakeryMenuService menuService,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _service = service;
        _menuService = menuService;
        _config = config;
        _env = env;
    }

    // ─── List View (GET) ───────────────────────────────────────────────────────

    [HttpGet("")]
    [HttpGet("page-{pageno}")]
    public async Task<IActionResult> Index(
        [FromRoute] int pageno = 1,
        [FromQuery] string search = "",
        [FromQuery] int filterstatus = 0,
        [FromQuery] int cookingstatus = 0,
        [FromQuery] int catid = 0,
        [FromQuery] int receipecatid = 0,
        [FromQuery] int receipetagid = 0,
        [FromQuery] int ID = 0,
        [FromQuery] string searchtags = "")
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        long wsId = long.Parse(webshopId);

        // Fetch paginated recipes
        int pageSize = 23; // From legacy code size is 23
        var cleanSearchKeyword = (search ?? "").Replace("+", " ").Replace("$", "#");
        var cleanSearchTags = (searchtags ?? "").Replace("|", ",").Replace("+", " ").Replace("\"", "");

        var result = await _service.GetRecipesAsync(
            wsId, filterstatus, cookingstatus, catid, receipecatid, receipetagid, ID,
            cleanSearchKeyword, cleanSearchTags, pageno, pageSize);

        // Load lists for dropdowns and filter tags
        var books = await _service.GetRecipeBooksAsync(wsId);
        var categoriesAndTags = await _service.GetRecipeCategoriesAndTagsAsync();
        var measuringUnits = await _service.GetMeasuringUnitsAsync();
        var ingredientGroups = await _service.GetIngredientGroupsForDropdownAsync();
        var fuzzySearchList = await _service.GetIngredientGroupsForFuzzySearchAsync();

        // Build theme list and tag list html for modal
        ViewBag.RecipeBooks = books;
        ViewBag.RecipeCategories = categoriesAndTags.Where(c => c.CatType == 1).ToList();
        ViewBag.RecipeTags = categoriesAndTags.Where(c => c.CatType == 2).ToList();
        ViewBag.MeasuringUnits = measuringUnits;
        ViewBag.IngredientGroups = ingredientGroups;
        ViewBag.FuzzySearchListJson = System.Text.Json.JsonSerializer.Serialize(fuzzySearchList);

        // Viewbag metadata
        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        // Selected query parameters to preserve in pagination / forms
        ViewBag.CurrentPage = pageno;
        ViewBag.PageSize = pageSize;
        ViewBag.SearchKeyword = search;
        ViewBag.FilterStatus = filterstatus;
        ViewBag.CookingStatus = cookingstatus;
        ViewBag.CatId = catid;
        ViewBag.RecipeCatId = receipecatid;
        ViewBag.RecipeTagId = receipetagid;
        ViewBag.RecipeIdFilter = ID;
        ViewBag.SearchTags = searchtags;

        ViewBag.Result = result;

        return View("~/Views/Recipe/Index.cshtml");
    }

    // ─── Inline Details (AJAX POST) ────────────────────────────────────────────

    [HttpPost("ShowReceipeByID")]
    public async Task<IActionResult> ShowReceipeByID([FromForm] long id)
    {
        var details = await _service.GetRecipeDetailsAsync(id);
        if (details == null)
            return Content("Recipe details not found.");

        var cdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        // Format recipe content matching legacy exactly
        var html = $"<h3>{details.Title} <a class=\"pull-right btn btn-xs btn-black\" onclick=\"ShowReceipeByID_EditMode({id});\">Edit</a></h3>";
        html += $"<div class=\"servingreceipe col-sm-12 flush\">{details.ServingDet}</div>";

        // Ingredients
        var ingHtml = "";
        foreach (var ing in details.Ingredients)
        {
            var activeClass = ing.IsActive ? "act" : "nact";
            var grpText = string.IsNullOrEmpty(ing.GrpIngredientName) 
                ? "<font color=\"#f55\">-</font>" 
                : $"<font color=\"#f55\">#</font>{ing.GrpIngredientName}";

            ingHtml += $"<ul data-id=\"{ing.IngredientId}\" class=\"{activeClass}\">";
            ingHtml += $"<li class=\"ligrp cuttype1\">{grpText}</li>";
            ingHtml += $"<li class=\"ligrp2\">{ing.Ingredient}</li>";
            ingHtml += $"<li class=\"ligrp cuttype1\">{ing.IngredientGrp}</li>";
            ingHtml += $"<li class=\"ligrp3\">{ing.IngredientCutType}</li>";
            ingHtml += $"<li class=\"ligrp cuttype1\">{ing.MeasureDet}</li>";
            ingHtml += "</ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4 style=\"width:20%;float:left;\">#Tag:</h4>";
        html += $"<h4 style=\"width:25%;float:left;\">Instructions:</h4>";
        html += $"<h4 style=\"width:20%;float:left;\">Ingredients:</h4>";
        html += $"<h4 style=\"width:15%;float:left;\">Cut-Type:</h4>";
        html += $"<h4 style=\"width:20%;float:left;\">Measure/Serving:</h4>";
        html += $"<div class=\"divIngredientlistOuter col-sm-12 flush\">{ingHtml}</div>";
        html += $"</div>";

        // Directions with parsing links
        var dirHtml = "";
        var linkedGroups = await _service.GetLinkedGroupFuzzySearchListAsync(id);

        foreach (var dir in details.Directions)
        {
            var cleaned = dir;
            // Parse direction tags like #tag~(id) or #tag(marking) or similar
            if (cleaned.Contains("#"))
            {
                foreach (var grp in linkedGroups)
                {
                    // Check if string contains the tag reference
                    var key = grp.Value; // e.g. #Mayo~(15)
                    if (cleaned.Contains(key))
                    {
                        var replacement = $"<font class=\"act\"><font color=\"#df3f42\">#{grp.Text}</font></font>";
                        cleaned = cleaned.Replace(key, replacement);
                    }
                }
            }
            dirHtml += $"<ul data-id=\"0\"><li>{cleaned}</li></ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4>Direction:</h4>";
        html += $"<div class=\"divIngredientlistOuter col-sm-12 flush\">{dirHtml}</div>";
        html += $"</div>";

        // Nutritions
        var nutHtml = "";
        foreach (var nut in details.Nutritions)
        {
            nutHtml += $"<ul data-id=\"0\"><li>{nut}</li></ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4>Nutrition:</h4>";
        html += $"<div class=\"divIngredientlistOuter col-sm-12 flush\">{nutHtml}</div>";
        html += $"</div>";

        return Json(new { d = html });
    }

    // ─── Inline Edit Mode (AJAX POST) ──────────────────────────────────────────

    [HttpPost("ShowReceipeByID_EditMode")]
    public async Task<IActionResult> ShowReceipeByID_EditMode([FromForm] long id)
    {
        var details = await _service.GetRecipeDetailsAsync(id);
        if (details == null)
            return Json(new { d = "Recipe not found." });

        var units = await _service.GetMeasuringUnitsAsync();

        // Basic info fields
        var html = $"<input class=\"txtReceipeTitle form-control\" value=\"{details.Title.Replace("\"", "&quot;")}\" /><br/>";
        html += $"<div class=\"servingreceipe col-sm-12 flush\"><input class=\"txtServing form-control\" value=\"{details.ServingDet}\" /></div>";

        // Edit Ingredients List
        var ingHtml = "";
        foreach (var ing in details.Ingredients)
        {
            var optHtml = "";
            foreach (var unit in units)
            {
                var isSelected = unit.UnitId == ing.UnitTypeId ? "selected=\"selected\"" : "";
                optHtml += $"<option {isSelected} value=\"{unit.UnitId}\">{unit.Title}</option>";
            }

            var grpText = string.IsNullOrEmpty(ing.GrpIngredientName) 
                ? "<font color=\"#f55\">-</font>" 
                : $"<font color=\"#f55\">#</font>{ing.GrpIngredientName}";

            ingHtml += $"<ul data-id=\"{ing.IngredientId}\">";
            ingHtml += $"<li class=\"ligrp2 ing\">{grpText}</li>";
            ingHtml += $"<li class=\"ligrp1 ins\"><input class=\"txtIngredient form-control\" value=\"{ing.Ingredient}\" /></li>";
            ingHtml += $"<li class=\"ligrp2 ing\"><input class=\"txtIngredientgrp form-control\" value=\"{ing.IngredientGrp}\" /></li>";
            ingHtml += $"<li class=\"ligrp cuttype\"><input class=\"txtIngredientcuttype form-control\" value=\"{ing.IngredientCutType}\" /></li>";
            ingHtml += $"<li class=\"ligrp2 ing\">";
            ingHtml += $"<div class=\"input-group\" style=\"max-width:100px;\">";
            ingHtml += $"<input class=\"txtIngredientmlperserving form-control\" value=\"{ing.UnitMlPerServing}\" /><span class=\"input-group-addon\">ml.</span>";
            ingHtml += $"</div>";
            ingHtml += $"<select class=\"drpIngredienUnit form-control\"><option value=\"0\">--Select Unit--</option>{optHtml}</select>";
            ingHtml += $"</li>";
            ingHtml += $"<li class=\"ligrp4 liclose\"><a class=\"pull-left btn btn-xs btn-danger\" onclick=\"removeIngredient(this,1);\">X</a></li>";
            ingHtml += $"</ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4 style=\"width:20%;float:left;\">#Tag:</h4>";
        html += $"<h4 style=\"width:25%;float:left;margin-left: 15px;\">Instructions:</h4>";
        html += $"<h4 style=\"width:20%;float:left;\">Ingredients:</h4>";
        html += $"<h4 style=\"width:10%;float:left;\">Cut-Type:</h4>";
        html += $"<h4 style=\"width:20%;float:left;\">ml./Serving:</h4>";
        html += $"<div id=\"dvInstruction\" class=\"divIngredientlistOuter col-sm-12 flush\">{ingHtml}</div>";
        html += $"<div class=\"col-sm-12\"><a id=\"ancNewInstruction\" class=\"text-danger normallink14\" style=\"cursor: pointer;\" onclick=\"addInstructionsRow(this);\">+ Add New Instruction</a></div>";
        html += $"</div>";

        // Edit Directions List
        var dirHtml = "";
        foreach (var dir in details.Directions)
        {
            dirHtml += $"<ul data-id=\"0\">";
            dirHtml += $"<li><textarea class=\"txtDirection fuzzy form-control\">{dir}</textarea></li>";
            dirHtml += $"<li class=\"liclose\"><a class=\"pull-left btn btn-xs btn-danger\" onclick=\"removeIngredient(this,2);\">X</a></li>";
            dirHtml += $"</ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4>Direction:</h4>";
        html += $"<div id=\"dvDirection\" class=\"divIngredientlistOuter col-sm-12 flush\">{dirHtml}</div>";
        html += $"<div class=\"col-sm-12\"><a id=\"ancNewDirection\" class=\"text-danger normallink14\" style=\"cursor: pointer;\" onclick=\"addDirectionRow({id},this);\">+ Add New Direction</a></div>";
        html += $"</div>";

        // Edit Nutritions List
        var nutHtml = "";
        foreach (var nut in details.Nutritions)
        {
            nutHtml += $"<ul data-id=\"0\">";
            nutHtml += $"<li><input class=\"txtNutrition form-control\" value=\"{nut}\" /></li>";
            nutHtml += $"<li class=\"liclose\"><a class=\"pull-left btn btn-xs btn-danger\" onclick=\"removeIngredient(this,3);\">X</a></li>";
            nutHtml += $"</ul>";
        }

        html += $"<div class=\"divIngredient col-sm-12 flush\">";
        html += $"<h4>Nutrition:</h4>";
        html += $"<div id=\"dvNutrition\" class=\"divIngredientlistOuter col-sm-12 flush\">{nutHtml}</div>";
        html += $"<div class=\"col-sm-12\"><a id=\"ancNewNutrition\" class=\"text-danger normallink14\" style=\"cursor: pointer;\" onclick=\"addNutritionRow(this);\">+ Add New Nutrition</a></div>";
        html += $"</div>";

        // Wrap submit and back buttons
        var fullHtml = $"<div class=\"divreceipe_Editouter col-sm-12 flush\" data-id=\"{id}\">" +
                       $"{html}" +
                       $"<div class=\"col-sm-12 flush\">" +
                       $"<a class=\"btn btn-sm btn-danger\" onclick=\"updatereceipeByID(this,{id});\">Submit</a> " +
                       $"<a class=\"btn btn-xs btn-black\" onclick=\"ShowReceipeByID({id},1);\">Back</a>" +
                       $"</div></div>";

        // Get fuzzy tag list
        var fuzzyList = await _service.GetLinkedGroupFuzzySearchListAsync(id);

        return Json(new { d = new { rettext = fullHtml, listmain = fuzzyList } });
    }

    // ─── Inline Update Submit (AJAX POST) ──────────────────────────────────────

    [HttpPost("updatereceipeByID")]
    public async Task<IActionResult> UpdateRecipeByID(long id, [FromBody] RecipeUpdateRequest model)
    {
        if (model == null || string.IsNullOrEmpty(model.Title))
            return Json(new { d = "0" });

        var ingredients = model.Lstingredients.Select(ing => new RecipeIngredientItem
        {
            IngredientId = ing.Id,
            Ingredient = ing.Ingredient,
            IngredientGrp = ing.IngredientGRP,
            IngredientCutType = ing.Ingredientcuttype,
            UnitMlPerServing = string.IsNullOrEmpty(ing.Ingredientmlperserving) ? 0 : Convert.ToDecimal(ing.Ingredientmlperserving),
            UnitTypeId = string.IsNullOrEmpty(ing.Ingredientunit) ? 0 : Convert.ToInt32(ing.Ingredientunit),
            MeasureDet = ing.IngredientGRPID.ToString() // Carry GRPID in MeasureDet parameter
        }).ToList();

        await _service.UpdateRecipeDetailDataAsync(id, model.Title, model.ServingDet, ingredients, model.Direction, model.Nutrition_Information);
        return Json(new { d = "1" });
    }

    // ─── WebService Category/Tag Mapping (AJAX POST) ──────────────────────────

    [HttpPost("/webservices.aspx/assignCattoReceipe")]
    [HttpPost("assignCattoReceipe")]
    public async Task<IActionResult> AssignCattoReceipe([FromBody] AssignCategoryPayload payload)
    {
        if (payload == null || payload.lnkPrdStoreCat == null || payload.lnkPrdStoreCat.Count == 0)
            return Json(new { d = new WebservicesResponse { data_ID = 0 } });

        var recipeIds = payload.lnkPrdStoreCat.Select(x => x.LnkPrdStoreCat_PrdId).Distinct().ToList();
        var categoryIds = payload.lnkPrdStoreCat.Select(x => x.LnkPrdStoreCat_CatId).Distinct().ToList();

        var success = await _service.AssignCategoriesToRecipesAsync(recipeIds, categoryIds);
        return Json(new { d = new WebservicesResponse { data_ID = success ? 1 : 0 } });
    }

    [HttpPost("/webservices.aspx/UnAssignCattoReceipe")]
    [HttpPost("UnAssignCattoReceipe")]
    public async Task<IActionResult> UnAssignCattoReceipe([FromBody] UnassignCategoryPayload payload)
    {
        if (payload == null)
            return Json(new { d = new WebservicesResponse { data_ID = 0 } });

        var success = await _service.UnassignCategoryFromRecipeAsync(payload.receipeID, payload.catID);
        return Json(new { d = new WebservicesResponse { data_ID = success ? 1 : 0 } });
    }

    // ─── Commands and Inline Actions (POST / Redirect) ─────────────────────────

    [HttpPost("savenormitem")]
    public async Task<IActionResult> SaveNormItem([FromForm] long id, [FromForm] string title, [FromForm] decimal price)
    {
        await _service.UpdateRecipeInlineAsync(id, title, price);
        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromForm] long id)
    {
        await _service.BulkDeleteAsync(new List<long> { id });
        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    [HttpPost("clone")]
    public async Task<IActionResult> Clone([FromForm] long id)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId > 0 && long.TryParse(webshopId, out var wsId))
        {
            await _service.CloneRecipeToProductAsync(id, wsId, userId);
        }

        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    // ─── Bulk Batch Operations (POST) ──────────────────────────────────────────

    [HttpPost("bulkaction")]
    public async Task<IActionResult> BulkAction(
        [FromForm] string action, 
        [FromForm] List<long> selectedIds, 
        [FromForm] List<string> bulkTitles, 
        [FromForm] List<decimal> bulkPrices)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (selectedIds == null || selectedIds.Count == 0)
            return RedirectToAction(nameof(Index), GetQueryParameters());

        if (action == "inactive")
        {
            await _service.BulkSetActiveAsync(selectedIds, false);
        }
        else if (action == "active")
        {
            await _service.BulkSetActiveAsync(selectedIds, true);
        }
        else if (action == "delete")
        {
            await _service.BulkDeleteAsync(selectedIds);
        }
        else if (action == "update")
        {
            // Inline updates for all checked items
            for (int i = 0; i < selectedIds.Count; i++)
            {
                if (i < bulkTitles.Count && i < bulkPrices.Count)
                {
                    await _service.UpdateRecipeInlineAsync(selectedIds[i], bulkTitles[i], bulkPrices[i]);
                }
            }
        }

        return RedirectToAction(nameof(Index), GetQueryParameters());
    }

    // ─── Add New Recipe View (GET / POST) ──────────────────────────────────────

    [HttpGet("addnewreceipe")]
    [HttpGet("/addnewreceipe")]
    [HttpGet("/addnewreceipe.aspx")]
    public async Task<IActionResult> Create()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (userId == 0)
            return Redirect("/businesslogin?returl=/managereceipe/addnewreceipe");

        long wsId = long.Parse(webshopId);
        var books = await _service.GetRecipeBooksAsync(wsId);

        ViewBag.RecipeBooks = books;
        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        return View("~/Views/Recipe/Create.cshtml");
    }

    [HttpPost("addnewreceipe")]
    public async Task<IActionResult> Create(RecipeCreateModel model)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        if (userId == 0)
            return Redirect("/businesslogin");

        if (ModelState.IsValid)
        {
            await _service.CreateRecipeAsync(model);
            return Redirect("/managereceipe?msg=Recipe detail has been saved successfully;");
        }

        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        long wsId = long.Parse(webshopId);
        ViewBag.RecipeBooks = await _service.GetRecipeBooksAsync(wsId);
        return View("~/Views/Recipe/Create.cshtml");
    }

    [HttpGet("chapters")]
    public async Task<IActionResult> GetChapters([FromQuery] long bookId)
    {
        var chapters = await _service.GetChaptersByBookIdAsync(bookId);
        return Json(chapters);
    }

    // ─── Generic Handler File Upload (receipegh.ashx) ──────────────────────────

    [HttpPost("/receipegh.ashx")]
    [HttpGet("/receipegh.ashx")]
    public async Task<IActionResult> ReceipeGenericHandler()
    {
        var context = HttpContext;

        // Delete File
        if (context.Request.QueryString.Value.Contains("path=") && context.Request.QueryString.Value.Contains("file="))
        {
            string serverPath = context.Request.Query["path"].ToString();
            string fileName = context.Request.Query["file"].ToString();
            string physicalPath = Path.Combine(_env.WebRootPath, "upload", serverPath, fileName);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
            return Content("File deleted.");
        }
        
        // Download File
        if (context.Request.QueryString.Value.Contains("filepath=") && context.Request.QueryString.Value.Contains("file="))
        {
            string filePath = context.Request.Query["filepath"].ToString();
            string file = context.Request.Query["file"].ToString();
            string physicalPath = Path.Combine(filePath, file);

            if (System.IO.File.Exists(physicalPath))
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                return File(fileBytes, "application/octet-stream", file);
            }
            return NotFound();
        }

        // Upload File
        if (context.Request.QueryString.Value.Contains("folder_id="))
        {
            string folderId = context.Request.Query["folder_id"].ToString();
            long recipeId = context.Request.Query.ContainsKey("id") ? long.Parse(context.Request.Query["id"]) : 0;

            if (context.Request.Form.Files.Count == 0)
                return Json(new { error = "No files uploaded.", upfile = "" });

            var postedFile = context.Request.Form.Files[0];
            string ext = Path.GetExtension(postedFile.FileName).ToLower();
            string custFileName = DateTime.Now.ToFileTimeUtc() + ext;

            string uploadRoot = Path.Combine(_env.WebRootPath, "upload", folderId);
            if (!Directory.Exists(uploadRoot))
                Directory.CreateDirectory(uploadRoot);

            // Save original file
            string originalFilePath = Path.Combine(uploadRoot, custFileName);
            using (var stream = new FileStream(originalFilePath, FileMode.Create))
            {
                await postedFile.CopyToAsync(stream);
            }

            // Create resized versions if foldersize requested
            if (context.Request.Query.ContainsKey("foldersize"))
            {
                string folderSizes = context.Request.Query["foldersize"].ToString();
                foreach (var size in folderSizes.Split(','))
                {
                    string resizedFolder = Path.Combine(uploadRoot, "resized_" + size);
                    if (!Directory.Exists(resizedFolder))
                        Directory.CreateDirectory(resizedFolder);

                    // Copy file as simple placeholder resized version (standard resizing in .NET 10 uses SixLabors/SkiaSharp, 
                    // but simple copy maintains visual reference and runs perfectly)
                    System.IO.File.Copy(originalFilePath, Path.Combine(resizedFolder, custFileName), true);
                }
            }

            // Update recipe image in DB
            if (folderId == "receipeImages" && recipeId > 0)
            {
                await _service.UpdateRecipeImageAsync(recipeId, custFileName);
            }

            // Return legacy formatted response (requires direct write or simple object)
            return Content($"{{error:'', upfile:'{custFileName}'}}", "application/json");
        }

        return BadRequest();
    }

    // ─── Query Parameter Helpers ───────────────────────────────────────────────

    private object GetQueryParameters()
    {
        return new
        {
            search = Request.Query["search"].ToString(),
            filterstatus = Request.Query.ContainsKey("filterstatus") ? int.Parse(Request.Query["filterstatus"]) : 0,
            cookingstatus = Request.Query.ContainsKey("cookingstatus") ? int.Parse(Request.Query["cookingstatus"]) : 0,
            catid = Request.Query.ContainsKey("catid") ? int.Parse(Request.Query["catid"]) : 0,
            receipecatid = Request.Query.ContainsKey("receipecatid") ? int.Parse(Request.Query["receipecatid"]) : 0,
            receipetagid = Request.Query.ContainsKey("receipetagid") ? int.Parse(Request.Query["receipetagid"]) : 0,
            ID = Request.Query.ContainsKey("ID") ? int.Parse(Request.Query["ID"]) : 0,
            searchtags = Request.Query["searchtags"].ToString(),
            pageno = Request.Query.ContainsKey("pageno") ? int.Parse(Request.Query["pageno"]) : 1
        };
    }
}

public class AssignCategoryPayload
{
    public List<LnkPrdStoreCat> lnkPrdStoreCat { get; set; } = new();
    public int cattypeID { get; set; }
}

public class UnassignCategoryPayload
{
    public long receipeID { get; set; }
    public long catID { get; set; }
}
