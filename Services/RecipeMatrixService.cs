using System.Data;
using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

public class RecipeCategoryItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
}

public class RecipeMatrixHeader
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

public class RecipeMatrixDataResponse
{
    public List<RecipeMatrixHeader> Columns { get; set; } = new();
    public List<Dictionary<string, object>> Rows { get; set; } = new();
    public int TotalRecords { get; set; }
    public int TotalColumns { get; set; }
}

public class RecipeMatrixService
{
    private readonly string _defaultConnection;

    public RecipeMatrixService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Gets all recipe ingredient categories.
    /// </summary>
    public async Task<List<RecipeCategoryItem>> GetCategoriesAsync()
    {
        var items = new List<RecipeCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "SELECT category_ID, category_name FROM tbl_receipeIngredient_category ORDER BY category_name";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new RecipeCategoryItem
            {
                CategoryId = Convert.ToInt32(reader.GetValue(0)),
                CategoryName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return items;
    }

    /// <summary>
    /// Gets the recipe matrix by calling getreceipeMAtrix stored procedure.
    /// </summary>
    public async Task<RecipeMatrixDataResponse> GetRecipeMatrixAsync(int bookId, int catId)
    {
        var response = new RecipeMatrixDataResponse();
        
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("getreceipeMAtrix", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        
        // Stored procedure expects @catid (param 0) and @bookID (param 1) based on parameters definition
        cmd.Parameters.AddWithValue("@catid", catId);
        cmd.Parameters.AddWithValue("@bookID", bookId);

        using var adapter = new SqlDataAdapter(cmd);
        var dataset = new DataSet();
        adapter.Fill(dataset);

        if (dataset.Tables.Count > 0)
        {
            var dt = dataset.Tables[0];
            response.TotalRecords = dt.Rows.Count;
            response.TotalColumns = dt.Columns.Count - 2;

            // Extract dynamic column headers starting from column index 2
            for (int colIndex = 2; colIndex < dt.Columns.Count; colIndex++)
            {
                var colName = dt.Columns[colIndex].ColumnName;
                if (colName.Contains("|"))
                {
                    var parts = colName.Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var id))
                    {
                        response.Columns.Add(new RecipeMatrixHeader
                        {
                            Id = id,
                            Title = parts[0]
                        });
                    }
                }
            }

            // Convert DataTable rows into dictionary format for easy JSON serialization
            foreach (DataRow row in dt.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    rowDict[col.ColumnName] = row[col] == DBNull.Value ? 0 : row[col];
                }
                response.Rows.Add(rowDict);
            }
        }

        return response;
    }

    /// <summary>
    /// Soft deletes selected recipes by setting receipeBookReceipe_isDeleted = 1.
    /// </summary>
    public async Task<bool> RemoveRecipesAsync(List<long> recipeIds)
    {
        if (recipeIds == null || recipeIds.Count == 0)
            return false;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_receipeBookReceipe SET receipeBookReceipe_isDeleted = 1 WHERE receipeBookReceipe_ID IN ({string.Join(",", recipeIds)})";
        await using var cmd = new SqlCommand(sql, conn);
        
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <summary>
    /// Finds recipe IDs containing specified ingredient group IDs.
    /// </summary>
    public async Task<List<long>> GetRecipeIdsByIngredientsAsync(List<long> ingredientGroupIds)
    {
        var recipeIds = new List<long>();
        if (ingredientGroupIds == null || ingredientGroupIds.Count == 0)
            return recipeIds;

        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT DISTINCT receipeBookReceipe_ID 
            FROM tbl_receipeBookReceipe 
            WHERE receipeBookReceipe_ID IN (
                SELECT receipeBookIngredient_receipeID 
                FROM tbl_receipeBookIngredient 
                INNER JOIN tbl_lnkIngredient2Grp ON lnkIngredient2Grp_ingID = receipeBookIngredient_ID 
                WHERE lnkIngredient2Grp_GrpID IN (" + string.Join(",", ingredientGroupIds) + @")
            )";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            recipeIds.Add(reader.GetInt64(0));
        }

        return recipeIds;
    }
}
