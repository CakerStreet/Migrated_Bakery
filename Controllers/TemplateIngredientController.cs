using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Manages cake baking ingredient templates per sponge/dietary/shape/size combination.
/// Migrated from legacy managetemplateforingredients.aspx / managetemplateforingredients.aspx.cs.
/// Route: /managetemplateforingredients
/// </summary>
public class TemplateIngredientController : Controller
{
    private readonly IConfiguration _config;
    private readonly BakeryMenuService _menuService;

    public TemplateIngredientController(IConfiguration config, BakeryMenuService menuService)
    {
        _config = config;
        _menuService = menuService;
    }

    private string ConnStr => _config.GetConnectionString("aboraboraboraaboraaborab") ?? "";

    private async Task PopulateLayoutAsync()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        ViewBag.MenuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
    }

    [HttpGet("managetemplateforingredients")]
    [HttpGet("managetemplateforingredients.aspx")]
    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";

        if (userId == 0)
            return Redirect($"/businesslogin?returl={Uri.EscapeDataString(Request.Path)}");

        // CakerStreet-only check from legacy
        var csBakeryId = _config["CsBakeryId"] ?? "";
        if (webshopIdStr != csBakeryId && !string.IsNullOrEmpty(csBakeryId))
            return Redirect("/mywebstore");

        await PopulateLayoutAsync();

        // Load dropdowns from tbl_lnkflavourGrouping
        var sponges = new List<DropdownItem>();
        var dietaries = new List<DropdownItem>();
        var sizes = new List<DropdownItem>();
        var shapes = new List<DropdownItem>();

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        var sql = @"SELECT lnkflavourGrouping_grouptype, lnkflavourGrouping_groupID, lnkflavourGrouping_groupTitle
                    FROM tbl_lnkflavourGrouping
                    WHERE lnkflavourGrouping_webstoreID = @wid
                    GROUP BY lnkflavourGrouping_grouptype, lnkflavourGrouping_groupID, lnkflavourGrouping_groupTitle
                    ORDER BY lnkflavourGrouping_groupID";
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@wid", webshopIdStr);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var item = new DropdownItem
                {
                    Id = rdr.GetInt64(1).ToString(),
                    Title = rdr.GetString(2)
                };
                int groupType = rdr.GetInt32(0);
                switch (groupType)
                {
                    case 1: sponges.Add(item); break;
                    case 2: dietaries.Add(item); break;
                    case 3: sizes.Add(item); break;
                }
            }
        }

        if (sponges.Count == 0)
            return Redirect($"/editbusinessinfo?returl={Uri.EscapeDataString(Request.Path)}");

        // Load shapes
        var sqlShape = "SELECT CakeShapeID, CakeShapeTitle FROM tbl_CakeShape WHERE IsActive = 1";
        await using (var cmd = new SqlCommand(sqlShape, conn))
        {
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                shapes.Add(new DropdownItem { Id = rdr.GetInt32(0).ToString(), Title = rdr.GetString(1) });
        }

        ViewBag.Sponges = sponges;
        ViewBag.Dietaries = dietaries;
        ViewBag.Sizes = sizes;
        ViewBag.Shapes = shapes;
        ViewBag.WebstoreId = webshopIdStr;
        ViewBag.Ingredients = new DataTable(); // empty initially
        ViewBag.ShowIngredients = false;

        return View("~/Views/TemplateIngredient/Index.cshtml");
    }

    [HttpPost("managetemplateforingredients/load")]
    public async Task<IActionResult> LoadIngredients(
        [FromForm] string spongeId,
        [FromForm] string dietaryId,
        [FromForm] string shapeId,
        [FromForm] string sizeId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (userId == 0)
            return Redirect("/businesslogin");

        await PopulateLayoutAsync();

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        // Load dropdowns again
        var sponges = new List<DropdownItem>();
        var dietaries = new List<DropdownItem>();
        var sizes = new List<DropdownItem>();
        var shapes = new List<DropdownItem>();

        var sqlDd = @"SELECT lnkflavourGrouping_grouptype, lnkflavourGrouping_groupID, lnkflavourGrouping_groupTitle
                      FROM tbl_lnkflavourGrouping
                      WHERE lnkflavourGrouping_webstoreID = @wid
                      GROUP BY lnkflavourGrouping_grouptype, lnkflavourGrouping_groupID, lnkflavourGrouping_groupTitle
                      ORDER BY lnkflavourGrouping_groupID";
        await using (var cmd = new SqlCommand(sqlDd, conn))
        {
            cmd.Parameters.AddWithValue("@wid", webshopIdStr);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var item = new DropdownItem { Id = rdr.GetInt64(1).ToString(), Title = rdr.GetString(2) };
                switch (rdr.GetInt32(0))
                {
                    case 1: sponges.Add(item); break;
                    case 2: dietaries.Add(item); break;
                    case 3: sizes.Add(item); break;
                }
            }
        }

        var sqlShape = "SELECT CakeShapeID, CakeShapeTitle FROM tbl_CakeShape WHERE IsActive = 1";
        await using (var cmd = new SqlCommand(sqlShape, conn))
        {
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                shapes.Add(new DropdownItem { Id = rdr.GetInt32(0).ToString(), Title = rdr.GetString(1) });
        }

        // Load ingredients with template link data
        var dt = new DataTable();
        var sqlIng = string.Format(@"SELECT BakeryIngredient_ID, BakeryIngredient_title, BakeryIngredient_Unit, l.lnktemplateIngredirent_Qty,
            IsChecked = case when lnktemplateIngredirent_ID is null then 0 else 1 end, lnktemplateIngredirent_ID = ISNULL(lnktemplateIngredirent_ID, 0)
            FROM tbl_BakeryIngredient b
            LEFT OUTER JOIN tbl_lnktemplateIngredirent l ON b.BakeryIngredient_ID = l.lnktemplateIngredirent_ingredientID
                AND l.lnktemplateIngredirent_sizeID = @sizeId
                AND l.lnktemplateIngredirent_spongeID = @spongeId
                AND l.lnktemplateIngredirent_dietryID = @dietaryId
                AND l.lnktemplateIngredirent_shapeID = @shapeId
            WHERE BakeryIngredient_IsDeleted = 0 AND BakeryIngredient_webstoreID = @wid
            ORDER BY lnktemplateIngredirent_ID DESC");

        await using (var cmd = new SqlCommand(sqlIng, conn))
        {
            cmd.Parameters.AddWithValue("@sizeId", sizeId);
            cmd.Parameters.AddWithValue("@spongeId", spongeId);
            cmd.Parameters.AddWithValue("@dietaryId", dietaryId);
            cmd.Parameters.AddWithValue("@shapeId", shapeId);
            cmd.Parameters.AddWithValue("@wid", webshopIdStr);
            dt.Load(await cmd.ExecuteReaderAsync());
        }

        ViewBag.Sponges = sponges;
        ViewBag.Dietaries = dietaries;
        ViewBag.Sizes = sizes;
        ViewBag.Shapes = shapes;
        ViewBag.SelectedSponge = spongeId;
        ViewBag.SelectedDietary = dietaryId;
        ViewBag.SelectedShape = shapeId;
        ViewBag.SelectedSize = sizeId;
        ViewBag.WebstoreId = webshopIdStr;
        ViewBag.Ingredients = dt;
        ViewBag.ShowIngredients = dt.Rows.Count > 0;

        return View("~/Views/TemplateIngredient/Index.cshtml");
    }

    [HttpPost("managetemplateforingredients/save")]
    public async Task<IActionResult> SavePrices(
        [FromForm] string spongeId,
        [FromForm] string dietaryId,
        [FromForm] string shapeId,
        [FromForm] string sizeId)
    {
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;
        var webshopIdStr = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "0";
        if (userId == 0)
            return Redirect("/businesslogin");

        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();

        var delIds = new List<string>();

        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("chk_"))
            {
                string ingIdStr = key.Substring(4);
                long ingredientId = long.Parse(ingIdStr);
                string qtyStr = Request.Form[$"qty_{ingIdStr}"].ToString();

                if (!string.IsNullOrEmpty(qtyStr) && decimal.TryParse(qtyStr, out var qty))
                {
                    // Upsert
                    var sqlFind = @"SELECT lnktemplateIngredirent_ID FROM tbl_lnktemplateIngredirent
                        WHERE lnktemplateIngredirent_ingredientID=@iid AND lnktemplateIngredirent_sizeID=@sid
                        AND lnktemplateIngredirent_dietryID=@did AND lnktemplateIngredirent_shapeID=@shid
                        AND lnktemplateIngredirent_spongeID=@spid";
                    await using var cmdFind = new SqlCommand(sqlFind, conn);
                    cmdFind.Parameters.AddWithValue("@iid", ingredientId);
                    cmdFind.Parameters.AddWithValue("@sid", sizeId);
                    cmdFind.Parameters.AddWithValue("@did", dietaryId);
                    cmdFind.Parameters.AddWithValue("@shid", shapeId);
                    cmdFind.Parameters.AddWithValue("@spid", spongeId);
                    var existingId = await cmdFind.ExecuteScalarAsync();

                    if (existingId != null && existingId != DBNull.Value)
                    {
                        var sqlUpd = "UPDATE tbl_lnktemplateIngredirent SET lnktemplateIngredirent_Qty=@qty, lnktemplateIngredirent_modifiedOn=@now, lnktemplateIngredirent_modifiedBy=@uid WHERE lnktemplateIngredirent_ID=@id";
                        await using var cmdUpd = new SqlCommand(sqlUpd, conn);
                        cmdUpd.Parameters.AddWithValue("@qty", qty);
                        cmdUpd.Parameters.AddWithValue("@now", DateTime.Now);
                        cmdUpd.Parameters.AddWithValue("@uid", int.Parse(webshopIdStr));
                        cmdUpd.Parameters.AddWithValue("@id", existingId);
                        await cmdUpd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var sqlIns = @"INSERT INTO tbl_lnktemplateIngredirent
                            (lnktemplateIngredirent_ingredientID, lnktemplateIngredirent_sizeID, lnktemplateIngredirent_dietryID,
                             lnktemplateIngredirent_shapeID, lnktemplateIngredirent_spongeID, lnktemplateIngredirent_Qty,
                             lnktemplateIngredirent_modifiedOn, lnktemplateIngredirent_modifiedBy)
                            VALUES (@iid, @sid, @did, @shid, @spid, @qty, @now, @uid)";
                        await using var cmdIns = new SqlCommand(sqlIns, conn);
                        cmdIns.Parameters.AddWithValue("@iid", ingredientId);
                        cmdIns.Parameters.AddWithValue("@sid", sizeId);
                        cmdIns.Parameters.AddWithValue("@did", dietaryId);
                        cmdIns.Parameters.AddWithValue("@shid", shapeId);
                        cmdIns.Parameters.AddWithValue("@spid", spongeId);
                        cmdIns.Parameters.AddWithValue("@qty", qty);
                        cmdIns.Parameters.AddWithValue("@now", DateTime.Now);
                        cmdIns.Parameters.AddWithValue("@uid", int.Parse(webshopIdStr));
                        await cmdIns.ExecuteNonQueryAsync();
                    }
                }
            }
            else if (key.StartsWith("del_"))
            {
                string linkIdStr = Request.Form[key].ToString();
                if (long.TryParse(linkIdStr, out var linkId) && linkId > 0)
                    delIds.Add(linkId.ToString());
            }
        }

        // Delete unchecked items
        if (delIds.Count > 0)
        {
            var sqlDel = $"DELETE FROM tbl_lnktemplateIngredirent WHERE lnktemplateIngredirent_ID IN ({string.Join(",", delIds)})";
            await using var cmdDel = new SqlCommand(sqlDel, conn);
            await cmdDel.ExecuteNonQueryAsync();
        }

        TempData["Message"] = "Baking Ingredient(s) have been saved successfully";
        return Redirect($"/managetemplateforingredients");
    }

    public class DropdownItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
