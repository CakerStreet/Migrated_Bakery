using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers;

[Route("linkkeywordwithgroup")]
public class LinkKeywordGroupController : Controller
{
    private readonly BakeryMenuService _menuService;
    private readonly IConfiguration _config;

    public LinkKeywordGroupController(BakeryMenuService menuService, IConfiguration config)
    {
        _menuService = menuService;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
        var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
        var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin?returl=/linkkeywordwithgroup");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var items = new List<KeywordGroupItem>();

        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT r.receipeBookIngredientGrp_ID, r.receipeBookIngredientGrp_ingredient, 
                               r.receipeBookIngredientGrp_Img, g.Keyword, g.RowId
                        FROM tbl_receipeBookIngredientGrp r 
                        INNER JOIN tbl_ReciepeReplaceGrp g ON r.receipeBookIngredientGrp_ID = g.GrpID
                        ORDER BY g.CreatedOn DESC";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new KeywordGroupItem
                {
                    IngredientGrpId = reader.GetInt64(0),
                    IngredientName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    IngredientImg = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Keyword = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    RowId = reader.GetInt64(4)
                });
            }
        }

        var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);
        ViewBag.MenuVisibility = menuVisibility;
        ViewBag.BusinessName = businessName;
        ViewBag.UserName = userName;
        ViewBag.Items = items;
        ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";

        if (TempData["SuccessMsg"] != null) ViewBag.SuccessMsg = TempData["SuccessMsg"].ToString();
        if (TempData["ErrorMsg"] != null) ViewBag.ErrorMsg = TempData["ErrorMsg"].ToString();

        return View("~/Views/LinkKeywordGroup/Index.cshtml");
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromForm] string txtKeyword, [FromForm] long hfTopperID)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        if (string.IsNullOrWhiteSpace(txtKeyword) || hfTopperID == 0)
        {
            TempData["ErrorMsg"] = "Please enter both keyword and select an ingredient.";
            return Redirect("/linkkeywordwithgroup");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                
                // Check check duplicate
                var checkSql = "SELECT COUNT(1) FROM tbl_ReciepeReplaceGrp WHERE GrpID = @grpId";
                await using var checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@grpId", hfTopperID);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                if (exists)
                {
                    TempData["ErrorMsg"] = "This ingredient group is already assigned to a keyword.";
                }
                else
                {
                    var insertSql = @"INSERT INTO tbl_ReciepeReplaceGrp (GrpID, Keyword, CreatedOn) 
                                      VALUES (@grpId, @keyword, GETDATE())";
                    await using var insCmd = new SqlCommand(insertSql, conn);
                    insCmd.Parameters.AddWithValue("@grpId", hfTopperID);
                    insCmd.Parameters.AddWithValue("@keyword", txtKeyword.Trim());
                    await insCmd.ExecuteNonQueryAsync();

                    TempData["SuccessMsg"] = "Keyword has been added successfully.";
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Failed to add keyword: " + ex.Message;
        }

        return Redirect("/linkkeywordwithgroup");
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromForm] long rowId)
    {
        var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
        var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

        if (string.IsNullOrEmpty(webshopId) || userId == 0)
            return Redirect("/businesslogin");

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        try
        {
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var deleteSql = "DELETE FROM tbl_ReciepeReplaceGrp WHERE RowId = @rowId";
                await using var cmd = new SqlCommand(deleteSql, conn);
                cmd.Parameters.AddWithValue("@rowId", rowId);
                await cmd.ExecuteNonQueryAsync();

                TempData["SuccessMsg"] = "Keyword has been deleted successfully.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = "Failed to delete keyword: " + ex.Message;
        }

        return Redirect("/linkkeywordwithgroup");
    }

    [HttpPost("GetIngredientGrp")]
    public async Task<IActionResult> GetIngredientGrp([FromBody] GetIngredientGrpRequest request)
    {
        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var list = new List<IngredientGrpAutocompleteItem>();

        await using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();
            var sql = @"SELECT TOP 20 receipeBookIngredientGrp_ID, receipeBookIngredientGrp_ingredient, receipeBookIngredientGrp_Img 
                        FROM tbl_receipeBookIngredientGrp 
                        WHERE receipeBookIngredientGrp_ID NOT IN (SELECT GrpID FROM tbl_ReciepeReplaceGrp) 
                          AND receipeBookIngredientGrp_ingredient LIKE '%' + @keyword + '%'";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@keyword", request.Keyword ?? "");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IngredientGrpAutocompleteItem
                {
                    receipeBookIngredientGrp_ID = reader.GetInt64(0),
                    receipeBookIngredientGrp_ingredient = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    receipeBookIngredientGrp_Img = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
        }

        return Json(new { d = list });
    }

    public class GetIngredientGrpRequest
    {
        public string? Keyword { get; set; }
    }

    public class KeywordGroupItem
    {
        public long IngredientGrpId { get; set; }
        public string IngredientName { get; set; } = "";
        public string IngredientImg { get; set; } = "";
        public string Keyword { get; set; } = "";
        public long RowId { get; set; }
    }

    public class IngredientGrpAutocompleteItem
    {
        public long receipeBookIngredientGrp_ID { get; set; }
        public string receipeBookIngredientGrp_ingredient { get; set; } = "";
        public string receipeBookIngredientGrp_Img { get; set; } = "";
    }
}
