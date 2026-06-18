using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class RecipeItem
{
    public long RecipeId { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public int Serving { get; set; }
    public string ServingDet { get; set; } = "";
    public bool IsCooking { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string Image { get; set; } = "";
    public long ProductId { get; set; }
    public string BookName { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public DateTime ModifiedOn { get; set; }
    public string RecipeCatIds { get; set; } = "";
}

public class RecipeListResult
{
    public List<RecipeItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class BookItem
{
    public long BookId { get; set; }
    public string BookName { get; set; } = "";
}

public class ChapterItem
{
    public long ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
}

public class RecipeCategoryItem2
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int CatType { get; set; }
}

public class MeasuringUnitItem
{
    public int UnitId { get; set; }
    public string Title { get; set; } = "";
    public decimal Ml { get; set; }
}

public class IngredientGroupDropdownItem
{
    public string Text { get; set; } = "";
    public string Value { get; set; } = "";
}

public class FuzzySearchItem
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
}

public class RecipeIngredientItem
{
    public long IngredientId { get; set; }
    public string Ingredient { get; set; } = "";
    public string IngredientGrp { get; set; } = "";
    public string IngredientCutType { get; set; } = "";
    public decimal UnitMlPerServing { get; set; }
    public int UnitTypeId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string GrpIngredientName { get; set; } = "";
    public string GrpMarking { get; set; } = "";
    public string MeasureDet { get; set; } = "";
}

public class RecipeDetailResult
{
    public long RecipeId { get; set; }
    public string Title { get; set; } = "";
    public string ServingDet { get; set; } = "";
    public List<RecipeIngredientItem> Ingredients { get; set; } = new();
    public List<string> Directions { get; set; } = new();
    public List<string> Nutritions { get; set; } = new();
}

public class RecipeCreateModel
{
    public long BookId { get; set; }
    public long ChapterId { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public int Servings { get; set; }
    public bool IsCooking { get; set; }
}

public class RecipeUpdateItem
{
    public long RecipeId { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
}

public class RecipeService
{
    private readonly string _defaultConnection;
    private readonly IConfiguration _config;

    public RecipeService(IConfiguration config)
    {
        _config = config;
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<RecipeListResult> GetRecipesAsync(
        long webstoreId,
        int statusFilter,
        int cookingFilter,
        int bookId,
        int categoryId,
        int tagId,
        int recipeId,
        string searchKeyword,
        string searchTags,
        int page,
        int pageSize)
    {
        var result = new RecipeListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("GetReceipeByWebstoreID", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        // Add parameters
        if (statusFilter == 1)
            cmd.Parameters.AddWithValue("@prd_isActive", true);
        else if (statusFilter == 2)
            cmd.Parameters.AddWithValue("@prd_isActive", false);
        else
            cmd.Parameters.AddWithValue("@prd_isActive", DBNull.Value);

        if (cookingFilter == 1)
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", true);
        else if (cookingFilter == 2)
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", false);
        else
            cmd.Parameters.AddWithValue("@receipeBookReceipe_isCooking", DBNull.Value);

        cmd.Parameters.AddWithValue("@webstoreID", webstoreId);

        if (!string.IsNullOrEmpty(searchKeyword))
            cmd.Parameters.AddWithValue("@search", searchKeyword.Trim());
        else
            cmd.Parameters.AddWithValue("@search", DBNull.Value);

        cmd.Parameters.AddWithValue("@PageNumber", page);
        cmd.Parameters.AddWithValue("@ProductsPerPage", pageSize);
        cmd.Parameters.AddWithValue("@catid", bookId);
        cmd.Parameters.AddWithValue("@receipecatid", categoryId);
        cmd.Parameters.AddWithValue("@receipetagid", tagId);
        cmd.Parameters.AddWithValue("@receipeId", recipeId);
        cmd.Parameters.AddWithValue("@searchstring", searchTags ?? "");

        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        if (ds.Tables.Count > 1)
        {
            // Table 0: Count
            if (ds.Tables[0].Rows.Count > 0)
            {
                result.TotalCount = Convert.ToInt32(ds.Tables[0].Rows[0][0]);
                result.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            }

            // Table 1: Recipes list
            foreach (DataRow r in ds.Tables[1].Rows)
            {
                result.Items.Add(new RecipeItem
                {
                    RecipeId = Convert.ToInt64(r["receipeBookReceipe_ID"]),
                    Title = Convert.ToString(r["receipeBookReceipe_title"]),
                    Price = r["receipeBookReceipe_price"] == DBNull.Value ? 0 : Convert.ToDecimal(r["receipeBookReceipe_price"]),
                    Serving = r["receipeBookReceipe_serving"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookReceipe_serving"]),
                    IsCooking = r["receipeBookReceipe_isCooking"] != DBNull.Value && Convert.ToBoolean(r["receipeBookReceipe_isCooking"]),
                    IsActive = r["receipeBookReceipe_isActive"] != DBNull.Value && Convert.ToBoolean(r["receipeBookReceipe_isActive"]),
                    Image = Convert.ToString(r["receipeBookReceipe_image"]),
                    ProductId = r["receipeBookReceipe_productID"] == DBNull.Value ? 0 : Convert.ToInt64(r["receipeBookReceipe_productID"]),
                    BookName = Convert.ToString(r["receipeBook_bookname"]),
                    ChapterName = Convert.ToString(r["receipeBookChapter_chaptername"]),
                    ModifiedOn = r["receipeBookReceipe_modifiedOn"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["receipeBookReceipe_modifiedOn"]),
                    RecipeCatIds = Convert.ToString(r["receipeCatIDs"])
                });
            }
        }

        return result;
    }

    public async Task<List<BookItem>> GetRecipeBooksAsync(long webstoreId)
    {
        var list = new List<BookItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT receipeBook_ID, receipeBook_bookname FROM tbl_receipeBook WHERE receipeBook_wsID = @wid ORDER BY receipeBook_bookname";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@wid", webstoreId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new BookItem
            {
                BookId = reader.GetInt64(0),
                BookName = reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<ChapterItem>> GetChaptersByBookIdAsync(long bookId)
    {
        var list = new List<ChapterItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT receipeBookChapter_ID, receipeBookChapter_chaptername FROM tbl_receipeBookChapter WHERE receipeBookChapter_bookID = @bid ORDER BY receipeBookChapter_chaptername";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@bid", bookId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ChapterItem
            {
                ChapterId = reader.GetInt64(0),
                ChapterName = reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<RecipeCategoryItem2>> GetRecipeCategoriesAndTagsAsync()
    {
        var list = new List<RecipeCategoryItem2>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT receipeCat_ID, receipeCat_categoryName, receipe_catType 
                    FROM tbl_receipeCat 
                    WHERE receipeCat_isActive = 1 AND receipeCat_isDeleted = 0 
                    ORDER BY receipe_catType, receipeCat_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RecipeCategoryItem2
            {
                CategoryId = Convert.ToInt32(reader.GetValue(0)),
                CategoryName = reader.GetString(1),
                CatType = reader.GetInt32(2)
            });
        }
        return list;
    }

    public async Task<List<MeasuringUnitItem>> GetMeasuringUnitsAsync()
    {
        var list = new List<MeasuringUnitItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT IngredientUnit_ID, IngredientUnit_title, IngredientUnit_ml FROM tbl_receipeIngredientUnit ORDER BY IngredientUnit_ml DESC";
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new MeasuringUnitItem
            {
                UnitId = reader.GetInt32(0),
                Title = reader.GetString(1),
                Ml = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2))
            });
        }
        return list;
    }

    public async Task<List<IngredientGroupDropdownItem>> GetIngredientGroupsForDropdownAsync()
    {
        var list = new List<IngredientGroupDropdownItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT g.receipeBookIngredientGrp_ingredient, 
                   CONCAT(g.receipeBookIngredientGrp_ID, ',', COALESCE(last_ing.receipeBookIngredient_IngredientCutType, '')) AS value
            FROM tbl_receipeBookIngredientGrp g
            LEFT JOIN (
                SELECT lnk.lnkIngredient2Grp_GrpID, MAX(lnk.lnkIngredient2Grp_ingID) as max_ing_id
                FROM tbl_lnkIngredient2Grp lnk
                GROUP BY lnk.lnkIngredient2Grp_GrpID
            ) grp_max ON g.receipeBookIngredientGrp_ID = grp_max.lnkIngredient2Grp_GrpID
            LEFT JOIN tbl_receipeBookIngredient last_ing ON grp_max.max_ing_id = last_ing.receipeBookIngredient_ID 
                                                        AND last_ing.receipeBookIngredient_isActive = 1 
                                                        AND last_ing.receipeBookIngredient_isdeleted = 0
            WHERE g.receipeBookIngredientGrp_active = 1
            ORDER BY g.receipeBookIngredientGrp_ingredient";

        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new IngredientGroupDropdownItem
            {
                Text = reader.GetString(0),
                Value = reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<FuzzySearchItem>> GetIngredientGroupsForFuzzySearchAsync()
    {
        var list = new List<FuzzySearchItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"SELECT receipeBookIngredientGrp_ingredient, 
                           CONCAT('#', receipeBookIngredientGrp_ingredient, '(', receipeBookIngredientGrp_marking, ')') 
                    FROM tbl_receipeBookIngredientGrp 
                    WHERE receipeBookIngredientGrp_ingredient <> '' 
                    ORDER BY receipeBookIngredientGrp_ingredient";
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FuzzySearchItem
            {
                Text = reader.GetString(0),
                Value = reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<RecipeDetailResult> GetRecipeDetailsAsync(long recipeId)
    {
        var result = new RecipeDetailResult { RecipeId = recipeId };
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // 1. Fetch recipe basic info
        var basicSql = "SELECT receipeBookReceipe_title, receipeBookReceipe_servingDet FROM tbl_receipeBookReceipe WHERE receipeBookReceipe_ID = @id";
        await using (var basicCmd = new SqlCommand(basicSql, conn))
        {
            basicCmd.Parameters.AddWithValue("@id", recipeId);
            await using var reader = await basicCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.Title = reader.GetString(0);
                result.ServingDet = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }
        }

        // 2. Fetch Ingredients (type 1) via Stored Procedure GetIngredientsByReceipeID
        await using (var cmd = new SqlCommand("GetIngredientsByReceipeID", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@receipeID", recipeId);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            adapter.Fill(dt);

            foreach (DataRow r in dt.Rows)
            {
                result.Ingredients.Add(new RecipeIngredientItem
                {
                    IngredientId = Convert.ToInt64(r["receipeBookIngredient_ID"]),
                    Ingredient = Convert.ToString(r["receipeBookIngredient_Ingredient"]),
                    IngredientGrp = Convert.ToString(r["receipeBookIngredient_IngredientGRP"]),
                    IngredientCutType = Convert.ToString(r["receipeBookIngredient_IngredientCutType"]),
                    UnitMlPerServing = r["receipeBookIngredient_unitMlPerServing"] == DBNull.Value ? 0 : Convert.ToDecimal(r["receipeBookIngredient_unitMlPerServing"]),
                    UnitTypeId = r["receipeBookIngredient_unitTypeID"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookIngredient_unitTypeID"]),
                    DisplayOrder = r["receipeBookIngredient_displayorder"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookIngredient_displayorder"]),
                    IsActive = r["receipeBookIngredient_isActive"] != DBNull.Value && Convert.ToBoolean(r["receipeBookIngredient_isActive"]),
                    GrpIngredientName = Convert.ToString(r["receipeBookIngredientGrp_ingredient"]),
                    GrpMarking = Convert.ToString(r["receipeBookIngredientGrp_marking"]),
                    MeasureDet = Convert.ToString(r["Measuredet"])
                });
            }
        }

        // 3. Fetch Directions (type 2) and Nutritions (type 3)
        var listSql = @"SELECT receipeBookIngredient_Ingredient, receipeBookIngredient_typeID 
                        FROM tbl_receipeBookIngredient 
                        WHERE receipeBookIngredient_receipeID = @id AND receipeBookIngredient_typeID IN (2, 3) 
                        ORDER BY receipeBookIngredient_typeID, receipeBookIngredient_displayorder";
        await using (var listCmd = new SqlCommand(listSql, conn))
        {
            listCmd.Parameters.AddWithValue("@id", recipeId);
            await using var reader = await listCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var text = reader.GetString(0);
                var type = reader.GetInt32(1);
                if (type == 2)
                    result.Directions.Add(text);
                else
                    result.Nutritions.Add(text);
            }
        }

        return result;
    }

    public async Task<List<FuzzySearchItem>> GetLinkedGroupFuzzySearchListAsync(long recipeId)
    {
        var list = new List<FuzzySearchItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT DISTINCT att.receipeBookIngredientGrp_ingredient, att.receipeBookIngredientGrp_ID
            FROM tbl_receipeBookIngredientGrp att
            INNER JOIN tbl_lnkIngredient2Grp lnk ON att.receipeBookIngredientGrp_ID = lnk.lnkIngredient2Grp_GrpID
            INNER JOIN tbl_receipeBookIngredient ing ON lnk.lnkIngredient2Grp_ingID = ing.receipeBookIngredient_ID
            WHERE ing.receipeBookIngredient_typeID = 1 AND ing.receipeBookIngredient_receipeID = @id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", recipeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var text = reader.GetString(0);
            var id = reader.GetInt64(1);
            list.Add(new FuzzySearchItem
            {
                Text = text,
                Value = $"#{text}~({id})"
            });
        }

        return list;
    }

    public async Task<long> CreateRecipeAsync(RecipeCreateModel model)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO tbl_receipeBookReceipe (
                receipeBookReceipe_bookID, receipeBookReceipe_chapterID, receipeBookReceipe_createdOn, 
                receipeBookReceipe_image, receipeBookReceipe_isActive, receipeBookReceipe_isCooking, 
                receipeBookReceipe_isDeleted, receipeBookReceipe_modifiedOn, receipeBookReceipe_No, 
                receipeBookReceipe_price, receipeBookReceipe_productID, receipeBookReceipe_serving, 
                receipeBookReceipe_servingDet, receipeBookReceipe_title
            ) VALUES (
                @bookID, @chapterID, GETDATE(), 
                '', 1, @isCooking, 
                0, GETDATE(), 0, 
                @price, 0, @serving, 
                @servingDet, @title
            );
            SELECT SCOPE_IDENTITY();";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@bookID", model.BookId);
        cmd.Parameters.AddWithValue("@chapterID", model.ChapterId);
        cmd.Parameters.AddWithValue("@isCooking", model.IsCooking);
        cmd.Parameters.AddWithValue("@price", model.Price);
        cmd.Parameters.AddWithValue("@serving", model.Servings);
        cmd.Parameters.AddWithValue("@servingDet", $"Serving: {model.Servings}");
        cmd.Parameters.AddWithValue("@title", model.Title.Trim());

        var newId = Convert.ToInt64(await cmd.ExecuteScalarAsync());

        // Update No equal to ID
        var updateSql = "UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_No = @id WHERE receipeBookReceipe_ID = @id";
        await using var updateCmd = new SqlCommand(updateSql, conn);
        updateCmd.Parameters.AddWithValue("@id", newId);
        await updateCmd.ExecuteNonQueryAsync();

        return newId;
    }

    public async Task<bool> BulkSetActiveAsync(List<long> ids, bool isActive)
    {
        if (ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isActive = @active, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@active", isActive);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> BulkDeleteAsync(List<long> ids)
    {
        if (ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isDeleted = 1, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> UpdateRecipeInlineAsync(long recipeId, string title, decimal price)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"UPDATE tbl_receipeBookReceipe 
                    SET receipeBookReceipe_title = @title, 
                        receipeBookReceipe_price = @price, 
                        receipeBookReceipe_modifiedOn = GETDATE() 
                    WHERE receipeBookReceipe_ID = @id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@id", recipeId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> AssignCategoriesToRecipesAsync(List<long> recipeIds, List<long> categoryIds)
    {
        if (recipeIds.Count == 0 || categoryIds.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // MERGE logic for tbl_lnkreceipe2cat matching recipe ID and category/tag ID
        foreach (var recipeId in recipeIds)
        {
            foreach (var catId in categoryIds)
            {
                var sql = @"
                    IF NOT EXISTS (SELECT 1 FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_receipeID = @rid AND lnkreceipe2cat_catId = @cid)
                    BEGIN
                        INSERT INTO tbl_lnkreceipe2cat (lnkreceipe2cat_receipeID, lnkreceipe2cat_catId) VALUES (@rid, @cid)
                    END";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@rid", recipeId);
                cmd.Parameters.AddWithValue("@cid", catId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        return true;
    }

    public async Task<bool> UnassignCategoryFromRecipeAsync(long recipeId, long catId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "DELETE FROM tbl_lnkreceipe2cat WHERE lnkreceipe2cat_receipeID = @rid AND lnkreceipe2cat_catId = @cid";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@rid", recipeId);
        cmd.Parameters.AddWithValue("@cid", catId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> UpdateRecipeImageAsync(long recipeId, string imageName)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_image = @img, receipeBookReceipe_modifiedOn = GETDATE() WHERE receipeBookReceipe_ID = @id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@img", imageName);
        cmd.Parameters.AddWithValue("@id", recipeId);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task UpdateRecipeDetailDataAsync(long recipeId, string title, string servingDet, List<RecipeIngredientItem> ingredients, List<string> directions, List<string> nutritions)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // 1. Update basic recipe fields
        var sql = @"UPDATE tbl_receipeBookReceipe 
                    SET receipeBookReceipe_title = @title, 
                        receipeBookReceipe_servingDet = @servingDet, 
                        receipeBookReceipe_modifiedOn = GETDATE() 
                    WHERE receipeBookReceipe_ID = @id";
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@title", title.Trim());
            cmd.Parameters.AddWithValue("@servingDet", servingDet ?? "");
            cmd.Parameters.AddWithValue("@id", recipeId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Save Ingredients (type 1)
        var existingIngIds = new List<long>();
        int displayOrder = 1;
        foreach (var ing in ingredients)
        {
            long ingId = ing.IngredientId;
            bool isNew = ingId <= 0;

            if (isNew)
            {
                var insSql = @"
                    INSERT INTO tbl_receipeBookIngredient (
                        receipeBookIngredient_receipeID, receipeBookIngredient_typeID, receipeBookIngredient_Ingredient, 
                        receipeBookIngredient_IngredientGRP, receipeBookIngredient_IngredientCutType, 
                        receipeBookIngredient_unitMlPerServing, receipeBookIngredient_unitTypeID, 
                        receipeBookIngredient_displayorder, receipeBookIngredient_isActive, 
                        receipeBookIngredient_isdeleted, receipeBookIngredient_createdOn, 
                        receipeBookIngredient_IngredientQtyStr
                    ) VALUES (
                        @rid, 1, @ingredient, 
                        @ingredientGRP, @cutType, 
                        @mlPerServing, @unitType, 
                        @displayOrder, 1, 
                        0, GETDATE(), 
                        ''
                    );
                    SELECT SCOPE_IDENTITY();";

                await using var cmd = new SqlCommand(insSql, conn);
                cmd.Parameters.AddWithValue("@rid", recipeId);
                cmd.Parameters.AddWithValue("@ingredient", ing.Ingredient ?? "");
                cmd.Parameters.AddWithValue("@ingredientGRP", ing.IngredientGrp ?? "");
                cmd.Parameters.AddWithValue("@cutType", ing.IngredientCutType ?? "");
                cmd.Parameters.AddWithValue("@mlPerServing", ing.UnitMlPerServing);
                cmd.Parameters.AddWithValue("@unitType", ing.UnitTypeId);
                cmd.Parameters.AddWithValue("@displayOrder", displayOrder);

                ingId = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                // Find or add link from ingredient to ingredient group tag
                // Since the UI passes the ingredient name/value which contains group ID, we get it
                // and link it. Let's check if the caller passed the Group ID.
                if (int.TryParse(ing.MeasureDet, out var groupId) && groupId > 0) // MeasureDet carries the GroupID in UI payload
                {
                    var lnkSql = @"
                        IF NOT EXISTS (SELECT 1 FROM tbl_lnkIngredient2Grp WHERE lnkIngredient2Grp_ingID = @ingId AND lnkIngredient2Grp_GrpID = @grpId)
                        BEGIN
                            INSERT INTO tbl_lnkIngredient2Grp (lnkIngredient2Grp_ingID, lnkIngredient2Grp_GrpID) VALUES (@ingId, @grpId)
                        END";
                    await using var lnkCmd = new SqlCommand(lnkSql, conn);
                    lnkCmd.Parameters.AddWithValue("@ingId", ingId);
                    lnkCmd.Parameters.AddWithValue("@grpId", groupId);
                    await lnkCmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                var updSql = @"
                    UPDATE tbl_receipeBookIngredient 
                    SET receipeBookIngredient_Ingredient = @ingredient,
                        receipeBookIngredient_IngredientGRP = @ingredientGRP,
                        receipeBookIngredient_IngredientCutType = @cutType,
                        receipeBookIngredient_unitMlPerServing = @mlPerServing,
                        receipeBookIngredient_unitTypeID = @unitType,
                        receipeBookIngredient_displayorder = @displayOrder,
                        receipeBookIngredient_createdOn = GETDATE()
                    WHERE receipeBookIngredient_ID = @ingId";

                await using var cmd = new SqlCommand(updSql, conn);
                cmd.Parameters.AddWithValue("@ingredient", ing.Ingredient ?? "");
                cmd.Parameters.AddWithValue("@ingredientGRP", ing.IngredientGrp ?? "");
                cmd.Parameters.AddWithValue("@cutType", ing.IngredientCutType ?? "");
                cmd.Parameters.AddWithValue("@mlPerServing", ing.UnitMlPerServing);
                cmd.Parameters.AddWithValue("@unitType", ing.UnitTypeId);
                cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
                cmd.Parameters.AddWithValue("@ingId", ingId);
                await cmd.ExecuteNonQueryAsync();
            }

            existingIngIds.Add(ingId);
            displayOrder++;
        }

        // Delete removed ingredients
        if (existingIngIds.Count > 0)
        {
            var delLnkSql = $"DELETE FROM tbl_lnkIngredient2Grp WHERE lnkIngredient2Grp_ingID IN (SELECT receipeBookIngredient_ID FROM tbl_receipeBookIngredient WHERE receipeBookIngredient_typeID = 1 AND receipeBookIngredient_receipeID = @rid AND receipeBookIngredient_ID NOT IN ({string.Join(",", existingIngIds)}))";
            await using (var delLnkCmd = new SqlCommand(delLnkSql, conn))
            {
                delLnkCmd.Parameters.AddWithValue("@rid", recipeId);
                await delLnkCmd.ExecuteNonQueryAsync();
            }

            var delSql = $"DELETE FROM tbl_receipeBookIngredient WHERE receipeBookIngredient_typeID = 1 AND receipeBookIngredient_receipeID = @rid AND receipeBookIngredient_ID NOT IN ({string.Join(",", existingIngIds)})";
            await using (var delCmd = new SqlCommand(delSql, conn))
            {
                delCmd.Parameters.AddWithValue("@rid", recipeId);
                await delCmd.ExecuteNonQueryAsync();
            }
        }

        // 3. Save Directions (type 2)
        // Clean out existing directions first
        var delDirsSql = "DELETE FROM tbl_receipeBookIngredient WHERE receipeBookIngredient_typeID = 2 AND receipeBookIngredient_receipeID = @rid";
        await using (var cmd = new SqlCommand(delDirsSql, conn))
        {
            cmd.Parameters.AddWithValue("@rid", recipeId);
            await cmd.ExecuteNonQueryAsync();
        }

        int dirOrder = 1;
        foreach (var dir in directions)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;

            var insSql = @"
                INSERT INTO tbl_receipeBookIngredient (
                    receipeBookIngredient_receipeID, receipeBookIngredient_typeID, receipeBookIngredient_Ingredient, 
                    receipeBookIngredient_IngredientGRP, receipeBookIngredient_IngredientCutType, 
                    receipeBookIngredient_unitMlPerServing, receipeBookIngredient_unitTypeID, 
                    receipeBookIngredient_displayorder, receipeBookIngredient_isActive, 
                    receipeBookIngredient_isdeleted, receipeBookIngredient_createdOn, 
                    receipeBookIngredient_IngredientQtyStr
                ) VALUES (
                    @rid, 2, @ingredient, 
                    @ingredient, '', 
                    0, 0, 
                    @displayOrder, 1, 
                    0, GETDATE(), 
                    ''
                )";

            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@rid", recipeId);
            cmd.Parameters.AddWithValue("@ingredient", dir.Trim());
            cmd.Parameters.AddWithValue("@displayOrder", dirOrder);
            await cmd.ExecuteNonQueryAsync();
            dirOrder++;
        }

        // 4. Save Nutritions (type 3)
        var delNutsSql = "DELETE FROM tbl_receipeBookIngredient WHERE receipeBookIngredient_typeID = 3 AND receipeBookIngredient_receipeID = @rid";
        await using (var cmd = new SqlCommand(delNutsSql, conn))
        {
            cmd.Parameters.AddWithValue("@rid", recipeId);
            await cmd.ExecuteNonQueryAsync();
        }

        int nutOrder = 1;
        foreach (var nut in nutritions)
        {
            if (string.IsNullOrWhiteSpace(nut)) continue;

            var insSql = @"
                INSERT INTO tbl_receipeBookIngredient (
                    receipeBookIngredient_receipeID, receipeBookIngredient_typeID, receipeBookIngredient_Ingredient, 
                    receipeBookIngredient_IngredientGRP, receipeBookIngredient_IngredientCutType, 
                    receipeBookIngredient_unitMlPerServing, receipeBookIngredient_unitTypeID, 
                    receipeBookIngredient_displayorder, receipeBookIngredient_isActive, 
                    receipeBookIngredient_isdeleted, receipeBookIngredient_createdOn, 
                    receipeBookIngredient_IngredientQtyStr
                ) VALUES (
                    @rid, 3, @ingredient, 
                    @ingredient, '', 
                    0, 0, 
                    @displayOrder, 1, 
                    0, GETDATE(), 
                    ''
                )";

            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@rid", recipeId);
            cmd.Parameters.AddWithValue("@ingredient", nut.Trim());
            cmd.Parameters.AddWithValue("@displayOrder", nutOrder);
            await cmd.ExecuteNonQueryAsync();
            nutOrder++;
        }
    }

    public async Task<int> CloneRecipeToProductAsync(long recipeId, long csBakeryId, int userId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        // 1. Fetch recipe details
        var fetchSql = "SELECT receipeBookReceipe_title, receipeBookReceipe_price, receipeBookReceipe_serving, receipeBookReceipe_image FROM tbl_receipeBookReceipe WHERE receipeBookReceipe_ID = @id";
        string title = "";
        decimal price = 0;
        int serving = 0;
        string image = "";

        await using (var cmd = new SqlCommand(fetchSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", recipeId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                title = r.GetString(0);
                price = r.GetDecimal(1);
                serving = r.GetInt32(2);
                image = r.IsDBNull(3) ? "" : r.GetString(3);
            }
        }

        if (string.IsNullOrEmpty(title)) return 0;

        // Get max display order
        int displayOrder = 1;
        var dispSql = "SELECT COALESCE(MAX(product_displayOrder), 0) + 1 FROM tbl_products WHERE product_WebstoreID = @wid";
        await using (var cmd = new SqlCommand(dispSql, conn))
        {
            cmd.Parameters.AddWithValue("@wid", csBakeryId);
            displayOrder = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // Fetch clone source product largeDesc (cloneprd = 6425)
        string largeDesc = "";
        var cloneSql = "SELECT product_largeDesc FROM tbl_products WHERE product_ID = 6425";
        await using (var cmd = new SqlCommand(cloneSql, conn))
        {
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                largeDesc = r.IsDBNull(0) ? "" : r.GetString(0);
            }
        }

        // Insert new product
        var productCode = "CS-" + Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10).ToLower();
        var seoUrl = FormatTitleAsUrl(title);

        var insPrdSql = @"
            INSERT INTO tbl_products (
                product_createdOn, product_custID, product_WebstoreID, product_displayOrder, 
                product_type, product_isActive, product_isdeleted, product_isexpired, 
                product_code, Product_CDNSts, product_modifiedOn, product_desc, product_largeDesc, 
                product_marketPrice, product_startingtPrice, product_Name, product_SEOURL, 
                product_quantity, product_refID, product_catID, product_Weight, product_preparationday, 
                Product_image1isURL, product_isWSP, product_ispostal, product_saletype
            ) VALUES (
                GETDATE(), @custID, @webstoreID, @displayOrder, 
                3, 1, 0, 0, 
                @code, 0, GETDATE(), '', @largeDesc, 
                @price, @price, @name, @seoUrl, 
                0, @refID, 271, 0, 2, 
                0, 0, 0, 2
            );
            SELECT SCOPE_IDENTITY();";

        long newPrdId = 0;
        await using (var cmd = new SqlCommand(insPrdSql, conn))
        {
            cmd.Parameters.AddWithValue("@custID", userId);
            cmd.Parameters.AddWithValue("@webstoreID", csBakeryId);
            cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
            cmd.Parameters.AddWithValue("@code", productCode);
            cmd.Parameters.AddWithValue("@largeDesc", largeDesc);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@name", title);
            cmd.Parameters.AddWithValue("@seoUrl", seoUrl);
            cmd.Parameters.AddWithValue("@refID", recipeId);

            newPrdId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        // Update code
        var updPrdCodeSql = "UPDATE tbl_products SET product_code = @code WHERE product_ID = @id";
        await using (var cmd = new SqlCommand(updPrdCodeSql, conn))
        {
            cmd.Parameters.AddWithValue("@code", $"CS-{newPrdId}");
            cmd.Parameters.AddWithValue("@id", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update recipe link
        var updRecPrdSql = "UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_productID = @pid WHERE receipeBookReceipe_ID = @rid";
        await using (var cmd = new SqlCommand(updRecPrdSql, conn))
        {
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            cmd.Parameters.AddWithValue("@rid", recipeId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert category mapping
        var insCatLnkSql = "INSERT INTO tbl_refPrdCatLink (refPrdCat_catID, refPrdCat_prdID) VALUES (271, @pid)";
        await using (var cmd = new SqlCommand(insCatLnkSql, conn))
        {
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Handle image if exists
        if (!string.IsNullOrEmpty(image))
        {
            // Note: Upload image to S3 or copy physically as per legacy logic, we can at least copy DB fields
            var imgRes = "300_300"; // dummy resolution metadata
            var updImgSql = "UPDATE tbl_products SET product_image1 = @img, product_image1Resolution = @res WHERE product_ID = @pid";
            await using (var cmd = new SqlCommand(updImgSql, conn))
            {
                cmd.Parameters.AddWithValue("@img", image);
                cmd.Parameters.AddWithValue("@res", imgRes);
                cmd.Parameters.AddWithValue("@pid", newPrdId);
                await cmd.ExecuteNonQueryAsync();
            }

            var insImgSql = @"
                INSERT INTO tbl_productImage (
                    productImage_createdOn, productImage_imagename, productImage_imageResolution, 
                    productImage_prdID, productImage_imgNo, productImage_isdefaultimage, 
                    productImage_imagetype, productImage_CDNSts, productImage_isURL
                ) VALUES (
                    GETDATE(), @img, @res, 
                    @pid, 1, 1, 
                    1, 0, 0
                )";
            await using (var cmd = new SqlCommand(insImgSql, conn))
            {
                cmd.Parameters.AddWithValue("@img", image);
                cmd.Parameters.AddWithValue("@res", imgRes);
                cmd.Parameters.AddWithValue("@pid", newPrdId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // SEO info
        var insSeoSql = @"
            INSERT INTO tbl_productSEO (
                productSEO_createdOn, productSEO_description, productSEO_keyword, 
                productSEO_Pagedescription, productSEO_prdID, productSEO_title, 
                productSEO_nofollow, productSEO_modifiedOn
            ) VALUES (
                GETDATE(), @title, @title, 
                @title, @pid, @title, 
                0, GETDATE()
            )";
        await using (var cmd = new SqlCommand(insSeoSql, conn))
        {
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Shipping settings
        var insShipSql = @"
            INSERT INTO tbl_prdShiping (
                prdShiping_prdID, prdShiping_iscollectable, prdShiping_deliverMiles, 
                prdShiping_deliverytype, prdShiping_deliverytype1Price, prdShiping_deliverytype2Price, 
                prdShiping_isdeliverable, prdShiping_modifiedOn
            ) VALUES (
                @pid, 1, 1000, 
                1, 7.49, 7.49, 
                1, GETDATE()
            )";
        await using (var cmd = new SqlCommand(insShipSql, conn))
        {
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Link shape (12) and type (3)
        var countShapeSql = "SELECT COUNT(1) FROM tbl_lnkPrdShape WHERE CakeShapeID = 12 AND product_ID = @pid";
        bool hasShape = false;
        await using (var cmd = new SqlCommand(countShapeSql, conn))
        {
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            hasShape = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }
        if (!hasShape)
        {
            var insShapeSql = "INSERT INTO tbl_lnkPrdShape (product_ID, CakeShapeID) VALUES (@pid, 12)";
            await using var cmd = new SqlCommand(insShapeSql, conn);
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        var countTypeSql = "SELECT COUNT(1) FROM tbl_CakeShape_CakeType WHERE CakeShapeID = 12 AND product_ID = @pid AND CakeTypeID = 3";
        bool hasType = false;
        await using (var cmd = new SqlCommand(countTypeSql, conn))
        {
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            hasType = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }
        if (!hasType)
        {
            var insTypeSql = "INSERT INTO tbl_CakeShape_CakeType (product_ID, CakeShapeID, CakeTypeID) VALUES (@pid, 12, 3)";
            await using var cmd = new SqlCommand(insTypeSql, conn);
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Specifications (USP_UpdateSpecification)
        try
        {
            // First select specifications from clone template (6425)
            var specs = new DataTable();
            specs.Columns.Add("typeID", typeof(int));
            specs.Columns.Add("Value", typeof(string));

            var getSpecsSql = "SELECT typeID, Value FROM tbl_specification WHERE product_ID = 6425";
            await using (var cmd = new SqlCommand(getSpecsSql, conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    specs.Rows.Add(r.GetInt32(0), r.IsDBNull(1) ? "" : r.GetString(1));
                }
            }

            await using (var cmd = new SqlCommand("USP_UpdateSpecification", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@SpecificationType", specs);
                cmd.Parameters.AddWithValue("@pid", newPrdId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error USP_UpdateSpecification: " + ex.Message);
        }

        // Cake price
        var insPriceSql = @"
            INSERT INTO tbl_CakePrice (
                CakeMaxPortion, CakeMinPortion, CakePortion, CakePrice, 
                cakeprice_displayorder, CakeShapeID, CakeTypeID, modifiedby, 
                modifiedOn, product_ID, SizeID, wsPrice
            ) VALUES (
                @serving, @serving, @portion, @price, 
                1, 12, 3, @uid, 
                GETDATE(), @pid, 984, 0
            )";
        await using (var cmd = new SqlCommand(insPriceSql, conn))
        {
            cmd.Parameters.AddWithValue("@serving", serving);
            cmd.Parameters.AddWithValue("@portion", $"{serving}-{serving}");
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@pid", newPrdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Flavour mapping
        var flavourSql = @"
            SELECT category_ID, category_name 
            FROM tbl_receipeIngredient_category 
            WHERE category_ID IN (
                SELECT grp.receipeBookIngredientGrp_catID 
                FROM tbl_receipeBookReceipe r 
                INNER JOIN tbl_receipeBookIngredient i ON r.receipeBookReceipe_ID = i.receipeBookIngredient_receipeID 
                INNER JOIN tbl_lnkIngredient2Grp lnk ON lnk.lnkIngredient2Grp_ingID = i.receipeBookIngredient_ID 
                INNER JOIN tbl_receipeBookIngredientGrp grp ON lnk.lnkIngredient2Grp_GrpID = grp.receipeBookIngredientGrp_ID 
                WHERE r.receipeBookReceipe_ID = @rid
            )";

        var flavourCats = new List<(int id, string name)>();
        await using (var cmd = new SqlCommand(flavourSql, conn))
        {
            cmd.Parameters.AddWithValue("@rid", recipeId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                flavourCats.Add((r.GetInt32(0), r.GetString(1)));
            }
        }

        int flvDisplayOrder = 1;
        foreach (var fc in flavourCats)
        {
            long flavourID = await GetFlavourIdByNameAsync(conn, fc.name, 0, 6, csBakeryId);
            int mandatoryType = fc.name.ToLower() == "bread" ? 8 : 5;

            await AddLnkAttAsync(conn, flvDisplayOrder, 0, 0, flavourID, mandatoryType, newPrdId, 0, 0);

            var childIngSql = @"
                SELECT grp.receipeBookIngredientGrp_ingredient 
                FROM tbl_receipeBookReceipe r 
                INNER JOIN tbl_receipeBookIngredient i ON r.receipeBookReceipe_ID = i.receipeBookIngredient_receipeID 
                INNER JOIN tbl_lnkIngredient2Grp lnk ON lnk.lnkIngredient2Grp_ingID = i.receipeBookIngredient_ID 
                INNER JOIN tbl_receipeBookIngredientGrp grp ON lnk.lnkIngredient2Grp_GrpID = grp.receipeBookIngredientGrp_ID 
                WHERE r.receipeBookReceipe_ID = @rid AND grp.receipeBookIngredientGrp_catID = @catid";

            var childIngs = new List<string>();
            await using (var cmd = new SqlCommand(childIngSql, conn))
            {
                cmd.Parameters.AddWithValue("@rid", recipeId);
                cmd.Parameters.AddWithValue("@catid", fc.id);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    childIngs.Add(r.GetString(0));
                }
            }

            int innerCounter = 1;
            foreach (var ci in childIngs)
            {
                long flavourChildID = await GetFlavourIdByNameAsync(conn, ci, flavourID, 1, csBakeryId);
                await AddLnkAttAsync(conn, innerCounter, 12, 3, flavourChildID, 9, newPrdId, 984, 0);
                innerCounter++;
            }

            flvDisplayOrder++;
        }

        return (int)newPrdId;
    }

    private async Task<long> GetFlavourIdByNameAsync(SqlConnection conn, string name, long parentId, int viewtype, long webstoreId)
    {
        // Try find existing flavor
        var sql = "SELECT FlavourID FROM tbl_flavour WHERE FlavourName = @name AND FlavourParentID = @pid AND flavour_viewtype = @vt AND flavour_wsID = @wid";
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@name", name.Trim());
            cmd.Parameters.AddWithValue("@pid", parentId);
            cmd.Parameters.AddWithValue("@vt", viewtype);
            cmd.Parameters.AddWithValue("@wid", webstoreId);

            var id = await cmd.ExecuteScalarAsync();
            if (id != null) return Convert.ToInt64(id);
        }

        // If not exists, insert new flavor
        var insFlvSql = @"
            INSERT INTO tbl_flavour (
                FlavourName, FlavourParentID, flavour_viewtype, flavour_wsID, 
                flavour_SEOURL, flavour_isActive, flavour_createdOn, flavour_modifiedOn
            ) VALUES (
                @name, @pid, @vt, @wid, 
                @seo, 1, GETDATE(), GETDATE()
            );
            SELECT SCOPE_IDENTITY();";

        await using (var cmd = new SqlCommand(insFlvSql, conn))
        {
            cmd.Parameters.AddWithValue("@name", name.Trim());
            cmd.Parameters.AddWithValue("@pid", parentId);
            cmd.Parameters.AddWithValue("@vt", viewtype);
            cmd.Parameters.AddWithValue("@wid", webstoreId);
            cmd.Parameters.AddWithValue("@seo", FormatTitleAsUrl(name));

            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
    }

    private async Task AddLnkAttAsync(SqlConnection conn, int displayorder, int shapeid, int typeid, long flavourID, int Mandatorytype, long Prd_id, int sizeID, decimal lnkAtt_ExtraPrice)
    {
        var sql = "SELECT lnkAtt_ID FROM tbl_lnkAtt WHERE CakeShapeID = @shape AND SizeID = @size AND CakeTypeID = @type AND FlavourID = @flv AND product_ID = @pid";
        long? lnkId = null;

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@shape", shapeid);
            cmd.Parameters.AddWithValue("@size", sizeID);
            cmd.Parameters.AddWithValue("@type", typeid);
            cmd.Parameters.AddWithValue("@flv", flavourID);
            cmd.Parameters.AddWithValue("@pid", Prd_id);

            var val = await cmd.ExecuteScalarAsync();
            if (val != null) lnkId = Convert.ToInt64(val);
        }

        if (lnkId.HasValue)
        {
            var updSql = @"UPDATE tbl_lnkAtt 
                           SET displayorder = @disp, lnkAtt_ExtraPrice = @price, lnkAtt_Mandatorytype = @mandatory, lnkAtt_modifiedOn = GETDATE() 
                           WHERE lnkAtt_ID = @id";
            await using var cmd = new SqlCommand(updSql, conn);
            cmd.Parameters.AddWithValue("@disp", displayorder);
            cmd.Parameters.AddWithValue("@price", lnkAtt_ExtraPrice);
            cmd.Parameters.AddWithValue("@mandatory", Mandatorytype);
            cmd.Parameters.AddWithValue("@id", lnkId.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insSql = @"
                INSERT INTO tbl_lnkAtt (
                    CakeShapeID, CakeTypeID, FlavourID, lnkAtt_createdOn, lnkAtt_isActive, 
                    lnkAtt_modifiedOn, product_ID, SizeID, displayorder, lnkAtt_ExtraPrice, lnkAtt_Mandatorytype
                ) VALUES (
                    @shape, @type, @flv, GETDATE(), 1, 
                    GETDATE(), @pid, @size, @disp, @price, @mandatory
                )";
            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@shape", shapeid);
            cmd.Parameters.AddWithValue("@type", typeid);
            cmd.Parameters.AddWithValue("@flv", flavourID);
            cmd.Parameters.AddWithValue("@pid", Prd_id);
            cmd.Parameters.AddWithValue("@size", sizeID);
            cmd.Parameters.AddWithValue("@disp", displayorder);
            cmd.Parameters.AddWithValue("@price", lnkAtt_ExtraPrice);
            cmd.Parameters.AddWithValue("@mandatory", Mandatorytype);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FormatTitleAsUrl(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        var slug = title.Trim().ToLower();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug;
    }

    public async Task<ManageIngredientListResult> GetManageIngredientsAsync(
        int typeId,
        int activeStatus,
        int cookingStatus,
        int taggrpStatus,
        long webstoreId,
        string search,
        int page,
        int pageSize,
        int bookId,
        int grpCatId,
        string searchTags)
    {
        var result = new ManageIngredientListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("GetIngredientsByWebstoreID", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.AddWithValue("@typeID", typeId);
        cmd.Parameters.AddWithValue("@prd_isActive", activeStatus);
        cmd.Parameters.AddWithValue("@prd_icooking", cookingStatus);
        cmd.Parameters.AddWithValue("@prd_istaggrp", taggrpStatus);
        cmd.Parameters.AddWithValue("@webstoreID", webstoreId);

        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("@search", search.Trim());
        else
            cmd.Parameters.AddWithValue("@search", DBNull.Value);

        cmd.Parameters.AddWithValue("@PageNumber", page);
        cmd.Parameters.AddWithValue("@ProductsPerPage", pageSize);
        cmd.Parameters.AddWithValue("@catid", bookId);
        cmd.Parameters.AddWithValue("@grpcatid", grpCatId);
        cmd.Parameters.AddWithValue("@searchstring", searchTags ?? "");

        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        if (ds.Tables.Count > 1)
        {
            if (ds.Tables[0].Rows.Count > 0)
            {
                result.TotalCount = Convert.ToInt32(ds.Tables[0].Rows[0][0]);
                result.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            }

            foreach (DataRow r in ds.Tables[1].Rows)
            {
                result.Items.Add(new ManageIngredientItem
                {
                    IngredientId = Convert.ToInt64(r["receipeBookIngredient_ID"]),
                    UnitIsMl = r["IngredientUnit_isMl"] != DBNull.Value && Convert.ToBoolean(r["IngredientUnit_isMl"]),
                    UnitMlPerServing = r["receipeBookIngredient_unitMlPerServing"] == DBNull.Value ? 0 : Convert.ToDecimal(r["receipeBookIngredient_unitMlPerServing"]),
                    UnitTitle = Convert.ToString(r["IngredientUnit_title"]),
                    RecipeServing = r["receipeBookReceipe_serving"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookReceipe_serving"]),
                    IngredientGrpCatId = r["receipeBookIngredientGrp_catID"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookIngredientGrp_catID"]),
                    IsActive = r["receipeBookIngredient_isActive"] != DBNull.Value && Convert.ToBoolean(r["receipeBookIngredient_isActive"]),
                    Ingredient = Convert.ToString(r["receipeBookIngredient_Ingredient"]),
                    TypeId = Convert.ToInt32(r["receipeBookIngredient_typeID"]),
                    UnitTypeId = r["receipeBookIngredient_unitTypeID"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookIngredient_unitTypeID"]),
                    RecipeTitle = Convert.ToString(r["receipeBookReceipe_title"]),
                    BookName = Convert.ToString(r["receipeBook_bookname"]),
                    IngredientGrp = Convert.ToString(r["receipeBookIngredient_IngredientGRP"]),
                    IngredientCutType = Convert.ToString(r["receipeBookIngredient_IngredientCutType"]),
                    CreatedOn = r["receipeBookIngredient_createdOn"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["receipeBookIngredient_createdOn"]),
                    DisplayOrder = r["receipeBookIngredient_displayorder"] == DBNull.Value ? 0 : Convert.ToInt32(r["receipeBookIngredient_displayorder"]),
                    GrpIngredientName = Convert.ToString(r["receipeBookIngredientGrp_ingredient"]),
                    RecipeId = r["receipeBookReceipe_ID"] == DBNull.Value ? 0 : Convert.ToInt64(r["receipeBookReceipe_ID"]),
                    GrpMarking = Convert.ToString(r["receipeBookIngredientGrp_marking"]),
                    MeasureDet = Convert.ToString(r["Measuredet"])
                });
            }
        }

        return result;
    }

    public async Task<List<RecipeIngredientCategory>> GetRecipeIngredientCategoriesAsync()
    {
        var list = new List<RecipeIngredientCategory>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT category_ID, category_name, category_displayOrder FROM tbl_receipeIngredient_category ORDER BY category_displayOrder";
        await using var cmd = new SqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RecipeIngredientCategory
            {
                CategoryId = reader.GetInt64(0),
                CategoryName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                DisplayOrder = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
            });
        }
        return list;
    }

    public async Task<bool> SaveIngredientInlineAsync(long id, int typeId, string title, string grp, string cutType, decimal ml, int unitId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "";
        if (typeId == 1)
        {
            sql = @"UPDATE tbl_receipeBookIngredient 
                    SET receipeBookIngredient_Ingredient = @title,
                        receipeBookIngredient_IngredientGRP = @grp,
                        receipeBookIngredient_IngredientCutType = @cutType,
                        receipeBookIngredient_unitMlPerServing = @ml,
                        receipeBookIngredient_unitTypeID = @unitId,
                        receipeBookIngredient_createdOn = GETDATE()
                    WHERE receipeBookIngredient_ID = @id";
        }
        else
        {
            sql = @"UPDATE tbl_receipeBookIngredient 
                    SET receipeBookIngredient_Ingredient = @title,
                        receipeBookIngredient_IngredientGRP = @title,
                        receipeBookIngredient_createdOn = GETDATE()
                    WHERE receipeBookIngredient_ID = @id";
        }

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@title", title ?? "");
        cmd.Parameters.AddWithValue("@grp", grp ?? "");
        cmd.Parameters.AddWithValue("@cutType", cutType ?? "");
        cmd.Parameters.AddWithValue("@ml", ml);
        cmd.Parameters.AddWithValue("@unitId", unitId);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> BulkDeleteIngredientsAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_receipeBookIngredient SET receipeBookIngredient_isdeleted = 1 WHERE receipeBookIngredient_ID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> FindAndReplaceIngredientsAsync(
        List<long> ids,
        string fromText,
        string toText,
        bool replaceGrp,
        bool replaceCut,
        bool replaceIng,
        string userId)
    {
        if (ids == null || ids.Count == 0 || string.IsNullOrEmpty(fromText)) return 0;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var setParts = new List<string>();
        if (replaceGrp)
        {
            setParts.Add("receipeBookIngredient_IngredientGRP = REPLACE(LOWER(receipeBookIngredient_IngredientGRP), @from, @to)");
        }
        if (replaceCut)
        {
            setParts.Add("receipeBookIngredient_IngredientCutType = REPLACE(LOWER(receipeBookIngredient_IngredientCutType), @from, @to)");
        }
        if (replaceIng)
        {
            setParts.Add("receipeBookIngredient_Ingredient = REPLACE(LOWER(receipeBookIngredient_Ingredient), @from, @to)");
        }

        if (setParts.Count == 0) return 0;

        var sql = $@"
            UPDATE tbl_receipeBookIngredient 
            SET {string.Join(", ", setParts)}
            WHERE (receipeBookIngredient_Ingredient LIKE '%' + @from + '%' OR receipeBookIngredient_IngredientGRP LIKE '%' + @from + '%')
              AND receipeBookIngredient_ID IN ({string.Join(",", ids)})";

        int rows = 0;
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@from", fromText.ToLower().Trim());
            cmd.Parameters.AddWithValue("@to", toText ?? "");
            rows = await cmd.ExecuteNonQueryAsync();
        }

        // Insert into tbl_receipeReplace
        var insSql = @"
            INSERT INTO tbl_receipeReplace (SourceUrl, DestinationUrl, CreatedOn, CreatedBy)
            VALUES (@from, @to, GETDATE(), @uid)";
        await using (var cmd = new SqlCommand(insSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", fromText.ToLower().Trim());
            cmd.Parameters.AddWithValue("@to", toText ?? "");
            cmd.Parameters.AddWithValue("@uid", userId ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        return rows;
    }

    public async Task<List<RecipeReplaceLog>> GetReplaceLogsAsync(string search)
    {
        var list = new List<RecipeReplaceLog>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT RedirectID, SourceUrl, DestinationUrl, CreatedOn, CreatedBy FROM tbl_receipeReplace";
        if (!string.IsNullOrEmpty(search))
        {
            sql += " WHERE SourceUrl LIKE '%' + @search + '%' OR DestinationUrl LIKE '%' + @search + '%'";
        }
        sql += " ORDER BY CreatedOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(search))
        {
            cmd.Parameters.AddWithValue("@search", search.Trim());
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RecipeReplaceLog
            {
                RedirectId = reader.GetInt32(0),
                SourceUrl = reader.IsDBNull(1) ? "" : reader.GetString(1),
                DestinationUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
                CreatedOn = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                CreatedBy = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return list;
    }

    public async Task<bool> DeleteReplaceLogsAsync(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"DELETE FROM tbl_receipeReplace WHERE RedirectID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> GlobalFindAndReplaceIngredientsAsync(string fromText, string toText, string userId)
    {
        if (string.IsNullOrEmpty(fromText)) return 0;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            UPDATE tbl_receipeBookIngredient 
            SET receipeBookIngredient_Ingredient = REPLACE(LOWER(receipeBookIngredient_Ingredient), @from, @to),
                receipeBookIngredient_IngredientGRP = REPLACE(LOWER(receipeBookIngredient_IngredientGRP), @from, @to),
                receipeBookIngredient_IngredientCutType = REPLACE(LOWER(receipeBookIngredient_IngredientCutType), @from, @to)
            WHERE (receipeBookIngredient_Ingredient LIKE '%' + @from + '%' OR receipeBookIngredient_IngredientGRP LIKE '%' + @from + '%')";

        int rows = 0;
        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@from", fromText.ToLower().Trim());
            cmd.Parameters.AddWithValue("@to", toText ?? "");
            rows = await cmd.ExecuteNonQueryAsync();
        }

        var insSql = @"
            INSERT INTO tbl_receipeReplace (SourceUrl, DestinationUrl, CreatedOn, CreatedBy)
            VALUES (@from, @to, GETDATE(), @uid)";
        await using (var cmd = new SqlCommand(insSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", fromText.ToLower().Trim());
            cmd.Parameters.AddWithValue("@to", toText ?? "");
            cmd.Parameters.AddWithValue("@uid", userId ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        return rows;
    }
}

public class ManageIngredientItem
{
    public long IngredientId { get; set; }
    public bool UnitIsMl { get; set; }
    public decimal UnitMlPerServing { get; set; }
    public string UnitTitle { get; set; } = "";
    public int RecipeServing { get; set; }
    public int IngredientGrpCatId { get; set; }
    public bool IsActive { get; set; }
    public string Ingredient { get; set; } = "";
    public int TypeId { get; set; }
    public int UnitTypeId { get; set; }
    public string RecipeTitle { get; set; } = "";
    public string BookName { get; set; } = "";
    public string IngredientGrp { get; set; } = "";
    public string IngredientCutType { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public int DisplayOrder { get; set; }
    public string GrpIngredientName { get; set; } = "";
    public long RecipeId { get; set; }
    public string GrpMarking { get; set; } = "";
    public string MeasureDet { get; set; } = "";
}

public class ManageIngredientListResult
{
    public List<ManageIngredientItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class RecipeIngredientCategory
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public class RecipeReplaceLog
{
    public int RedirectId { get; set; }
    public string SourceUrl { get; set; } = "";
    public string DestinationUrl { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}


