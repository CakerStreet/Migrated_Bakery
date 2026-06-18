using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class RecipePrintModel
{
    public long RecipeId { get; set; }
    public string Title { get; set; } = "";
    public string Serving { get; set; } = "";
    public string BookName { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public List<RecipeIngredientPrintModel> Ingredients { get; set; } = new();
    public List<string> Directions { get; set; } = new();
}

public class RecipeIngredientPrintModel
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public class KitchenPrintService
{
    private readonly string _defaultConnection;

    public KitchenPrintService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    private class ObjDirectionTags
    {
        public long ID { get; set; }
        public string Replacetext { get; set; } = "";
    }

    private class TagDetail
    {
        public long Id { get; set; }
        public string Ingredient { get; set; } = "";
        public bool Active { get; set; }
        public string Marking { get; set; } = "";
        public string UnitTitle { get; set; } = "";
    }

    public async Task<(DataTable Products, int TotalRecords)> GetCuttersProductsForPrintAsync(List<string> pids, string webshopId, int pageNo, int pageSize)
    {
        var dtTemp = new DataTable();
        dtTemp.Columns.Add(new DataColumn("prdID", typeof(long)));
        dtTemp.Columns.Add(new DataColumn("countprd", typeof(int)));

        foreach (string str in pids)
        {
            if (string.IsNullOrWhiteSpace(str)) continue;
            var parts = str.Split('-');
            long prdId = 0;
            int countPrd = 1;
            if (parts.Length > 0 && long.TryParse(parts[0], out var pidVal))
            {
                prdId = pidVal;
            }
            if (parts.Length > 1 && int.TryParse(parts[1], out var cntVal))
            {
                countPrd = cntVal;
            }

            if (prdId > 0)
            {
                var dr = dtTemp.NewRow();
                dr["prdID"] = prdId;
                dr["countprd"] = countPrd;
                dtTemp.Rows.Add(dr);
            }
        }

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("USPGetCuttersProductsForPrint_new", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        var tvpParam = cmd.Parameters.AddWithValue("@prdids", dtTemp);
        tvpParam.SqlDbType = SqlDbType.Structured;
        tvpParam.TypeName = "dbo.udt_prdcount";

        cmd.Parameters.AddWithValue("@webstoreID", string.IsNullOrEmpty(webshopId) ? 82 : Convert.ToInt32(webshopId));
        cmd.Parameters.AddWithValue("@PageNumber", pageNo);
        cmd.Parameters.AddWithValue("@ProductsPerPage", pageSize);

        var outputParam = new SqlParameter("@HowManyProducts", SqlDbType.Int);
        outputParam.Direction = ParameterDirection.Output;
        cmd.Parameters.Add(outputParam);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync();
        dt.Load(reader);

        int totalRecords = dt.Rows.Count;
        if (outputParam.Value != DBNull.Value && outputParam.Value != null)
        {
            totalRecords = Convert.ToInt32(outputParam.Value);
        }

        return (dt, totalRecords);
    }

    public async Task<List<RecipePrintModel>> GetRecipesForPrintAsync(
        int filterStatus,
        int cookingStatus,
        string webstoreId,
        string search,
        int catId,
        int receipeCatId,
        int receipeTagId,
        int recipeId,
        string searchString,
        string strIds)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("GetReceipeByWebstoreID_full", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        if (filterStatus == 1)
            cmd.Parameters.AddWithValue("@prd_isActive", true);
        else if (filterStatus == 2)
            cmd.Parameters.AddWithValue("@prd_isActive", false);
        else
            cmd.Parameters.AddWithValue("@prd_isActive", DBNull.Value);

        if (cookingStatus == 1)
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", true);
        else if (cookingStatus == 2)
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", false);
        else
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", DBNull.Value);

        cmd.Parameters.AddWithValue("@webstoreID", string.IsNullOrEmpty(webstoreId) ? 82 : Convert.ToInt64(webstoreId));
        cmd.Parameters.AddWithValue("@search", string.IsNullOrEmpty(search) ? DBNull.Value : search);
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@receipecatid", receipeCatId);
        cmd.Parameters.AddWithValue("@receipetagid", receipeTagId);
        cmd.Parameters.AddWithValue("@receipeId", recipeId);
        cmd.Parameters.AddWithValue("@searchstring", string.IsNullOrEmpty(searchString) ? "" : searchString);
        cmd.Parameters.AddWithValue("@IDs", string.IsNullOrEmpty(strIds) ? "0" : strIds);

        var ds = new DataSet();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            do
            {
                var dt = new DataTable();
                dt.Load(reader);
                ds.Tables.Add(dt);
            } while (!reader.IsClosed && reader.NextResult());
        }

        if (ds.Tables.Count < 3)
        {
            return new List<RecipePrintModel>();
        }

        var dtRecipes = ds.Tables[0];
        var dtIngredients = ds.Tables[1];
        var dtDirections = ds.Tables[2];

        // Gather all direction tag IDs to query them in one batch
        var allTagIds = new HashSet<long>();
        var recipeDirections = new Dictionary<long, List<(string raw, List<ObjDirectionTags> tags)>>();

        foreach (DataRow drDir in dtDirections.Rows)
        {
            long recipeIdVal = Convert.ToInt64(drDir["receipeBookIngredient_receipeID"]);
            string strDirection = Convert.ToString(drDir["receipeBookIngredient_Ingredient"]);
            var tagList = new List<ObjDirectionTags>();

            if (strDirection.Contains("#"))
            {
                var parts = strDirection.Split('#');
                for (int i = 1; i < parts.Length; i++)
                {
                    var str = parts[i];
                    int closingParenIndex = str.IndexOf(')');
                    if (closingParenIndex != -1)
                    {
                        string rawTag = str.Substring(0, closingParenIndex + 1);
                        string strTempReplacetext = "#" + rawTag;
                        var tildeParts = rawTag.Split('~');
                        if (tildeParts.Length > 1)
                        {
                            string idStr = tildeParts[1].Replace("(", "").Replace(")", "").Trim();
                            if (long.TryParse(idStr, out long tagId))
                            {
                                tagList.Add(new ObjDirectionTags { ID = tagId, Replacetext = strTempReplacetext });
                                allTagIds.Add(tagId);
                            }
                        }
                    }
                }
            }

            if (!recipeDirections.ContainsKey(recipeIdVal))
            {
                recipeDirections[recipeIdVal] = new List<(string, List<ObjDirectionTags>)>();
            }
            recipeDirections[recipeIdVal].Add((strDirection, tagList));
        }

        // Query tag details
        var tagDict = new Dictionary<long, TagDetail>();
        if (allTagIds.Any())
        {
            var idsJoined = string.Join(",", allTagIds);
            var query = $@"
                SELECT g.receipeBookIngredientGrp_ID, 
                       g.receipeBookIngredientGrp_ingredient, 
                       g.receipeBookIngredientGrp_active, 
                       g.receipeBookIngredientGrp_marking,
                       (SELECT TOP 1 u.IngredientUnit_title 
                        FROM tbl_lnkUnit2Grp lnk 
                        JOIN tbl_receipeIngredientUnit u ON lnk.lnkUnit2Grp_ingID = u.IngredientUnit_ID 
                        WHERE lnk.lnkUnit2Grp_GrpID = g.receipeBookIngredientGrp_ID) as UnitTitle
                FROM tbl_receipeBookIngredientGrp g
                WHERE g.receipeBookIngredientGrp_ID IN ({idsJoined})";

            await using var tagCmd = new SqlCommand(query, conn);
            await using var tagReader = await tagCmd.ExecuteReaderAsync();
            while (await tagReader.ReadAsync())
            {
                long id = tagReader.GetInt64(0);
                tagDict[id] = new TagDetail
                {
                    Id = id,
                    Ingredient = tagReader.IsDBNull(1) ? "" : tagReader.GetString(1),
                    Active = tagReader.IsDBNull(2) ? false : tagReader.GetBoolean(2),
                    Marking = tagReader.IsDBNull(3) ? "" : tagReader.GetString(3),
                    UnitTitle = tagReader.IsDBNull(4) ? "" : tagReader.GetString(4)
                };
            }
        }

        var recipes = new List<RecipePrintModel>();
        foreach (DataRow drRecipe in dtRecipes.Rows)
        {
            long recId = Convert.ToInt64(drRecipe["receipeBookReceipe_ID"]);
            var model = new RecipePrintModel
            {
                RecipeId = recId,
                Title = Convert.ToString(drRecipe["receipeBookReceipe_title"]),
                Serving = Convert.ToString(drRecipe["receipeBookReceipe_serving"]),
                BookName = Convert.ToString(drRecipe["receipeBook_bookname"]),
                ChapterName = Convert.ToString(drRecipe["receipeBookChapter_chaptername"])
            };

            // Populate ingredients
            var ingRows = dtIngredients.Select("receipeBookIngredient_receipeID=" + recId, "receipeBookIngredientGrp_active desc, receipeBookIngredient_displayorder");
            foreach (var ingRow in ingRows)
            {
                model.Ingredients.Add(new RecipeIngredientPrintModel
                {
                    Name = Convert.ToString(ingRow["receipeBookIngredientGrp_ingredient"]),
                    IsActive = ingRow["receipeBookIngredientGrp_active"] != DBNull.Value && Convert.ToBoolean(ingRow["receipeBookIngredientGrp_active"])
                });
            }

            // Populate directions with replaced tags
            if (recipeDirections.TryGetValue(recId, out var dirs))
            {
                foreach (var dirInfo in dirs)
                {
                    string cleaned = dirInfo.raw;
                    foreach (var tag in dirInfo.tags)
                    {
                        if (tagDict.TryGetValue(tag.ID, out var ep))
                        {
                            string activeClass = ep.Active ? "act" : "nact";
                            string unit = string.IsNullOrEmpty(ep.UnitTitle) ? "-" : ep.UnitTitle;
                            string replacement = $"<span class=\"{activeClass}\"><span style=\"color:#df3f42; font-weight:bold;\">#{ep.Ingredient}</span> [L{ep.Marking} | {unit}]</span>";
                            cleaned = cleaned.Replace(tag.Replacetext, replacement);
                        }
                    }
                    model.Directions.Add(cleaned);
                }
            }

            recipes.Add(model);
        }

        return recipes;
    }
}
