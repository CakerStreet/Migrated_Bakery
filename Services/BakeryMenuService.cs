using Microsoft.Data.SqlClient;

namespace CakerStreet.Business.Services;

/// <summary>
/// Determines sidebar menu visibility based on user type, webshop ID, and module assignments.
/// Migrated from BakeryMaster.master.cs Page_Load logic.
/// </summary>
public class BakeryMenuService
{
    private readonly string _connectionString;
    private readonly IConfiguration _config;

    // Head office bakery ID (csBakeryId from legacy config)
    private const string CsBakeryId = "82";

    public BakeryMenuService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _config = config;
    }

    /// <summary>
    /// Gets menu visibility based on user type, webshop ID, and module assignments.
    /// Logic from BakeryMaster.master.cs Page_Load.
    /// </summary>
    public async Task<MenuVisibility> GetMenuVisibilityAsync(string userType, string webshopId, int userId)
    {
        var menu = new MenuVisibility();

        // Orders always visible
        menu.ShowOrders = true;

        // WhatsApp login = restricted (only staffrota/dashboard)
        // This is handled at middleware level; if we get here, user is not WhatsApp-only

        // userType "1" = owner, "2" = manager
        if (userType == "2" || userType == "1")
        {
            menu.ShowManageUsers = true;
            menu.ShowSupplierUsers = true;
            menu.ShowUsersTimeline = true;
            menu.ShowSocialLinks = true;
            menu.ShowTradeAccount = await CheckIsBakingAsync(webshopId);
        }

        if (userType == "1")
        {
            menu.ShowEditBusinessInfo = true;
            menu.ShowPaymentSettings = true;
            menu.ShowTemplates = true;
            menu.ShowTemplateSpec = true;
            menu.ShowTemplateFormula = true;
            menu.ShowTemplateBakingCost = true;
        }

        // Head office (webshopId == csBakeryId) gets all items
        if (webshopId == CsBakeryId)
        {
            menu.ShowUsersTimeline = true;
            menu.ShowBakeryIngredient = true;
            menu.ShowManageRota = true;
            menu.ShowBakeryInventory = true;
            menu.ShowPartyTheme = true;
            menu.ShowFoodStandards = true;
            menu.ShowSpongeOrder = true;
            menu.ShowDeliveryRoutes = true;
            menu.ShowDeliveryRoutesPayments = true;
            menu.ShowStockInventory = true;
            menu.ShowSandwichBar = true;
            menu.ShowEditBusinessInfo = true;
            menu.ShowOrderManifest = true;
            menu.ShowPurchaseOrder = true;
            menu.ShowSupplyOrder = true;
            menu.ShowSocialLinks = true;
            menu.ShowSupplier = true;
            menu.ShowSupplierLocation = true;
            menu.ShowSupervisor = true;
            menu.ShowAllergen = true;
            menu.ShowFranchiseProduct = true;

            if (userType == "1" || userType == "2")
            {
                menu.ShowModuleAssignment = true;
            }
            else
            {
                // Check module assignments for non-owner/manager at head office
                var allowedModules = await GetModuleAccessAsync(userId);

                menu.ShowEditBusinessInfo = allowedModules.Contains(1);
                menu.ShowBakeryInventory = allowedModules.Contains(4);
                menu.ShowFranchiseProduct = allowedModules.Contains(4);
                menu.ShowStockInventory = allowedModules.Contains(5);
                menu.ShowPartyTheme = allowedModules.Contains(10);
                menu.ShowFoodStandards = allowedModules.Contains(14);
                menu.ShowDeliveryRoutes = allowedModules.Contains(16);
                menu.ShowDeliveryRoutesPayments = allowedModules.Contains(16);
                menu.ShowManageRota = allowedModules.Contains(19);
                menu.ShowSpongeOrder = allowedModules.Contains(15);
                menu.ShowSupplier = allowedModules.Contains(7);
                menu.ShowSupplierLocation = allowedModules.Contains(7);
                menu.ShowManageUsers = allowedModules.Contains(8);
                menu.ShowUsersTimeline = allowedModules.Contains(8);
                menu.ShowPaymentSettings = allowedModules.Contains(2);
                menu.ShowTradeAccount = allowedModules.Contains(2);
                menu.ShowBakeryIngredient = allowedModules.Contains(9);
                menu.ShowSandwichBar = allowedModules.Contains(3);
                menu.ShowPurchaseOrder = allowedModules.Contains(20);
                menu.ShowSupplyOrder = allowedModules.Contains(21);
                menu.ShowSocialLinks = allowedModules.Contains(22);
                menu.ShowOrderManifest = allowedModules.Contains(24);
                menu.ShowTemplates = allowedModules.Contains(6);
                menu.ShowTemplateFormula = allowedModules.Contains(6);
                menu.ShowTemplateBakingCost = allowedModules.Contains(6);
            }

            // Get supervisor info for head office
            menu.SupervisorName = await GetSupervisorNameAsync(webshopId);
        }
        else
        {
            // Non-head-office: hide most items
            menu.ShowUsersTimeline = (userType == "1" || userType == "2") && menu.ShowUsersTimeline;
            menu.ShowBakeryIngredient = false;
            menu.ShowSandwichBar = false;
            menu.ShowPurchaseOrder = false;
            menu.ShowSupplyOrder = false;
            menu.ShowSocialLinks = (userType == "1" || userType == "2");
            menu.ShowSupplier = false;
            menu.ShowSupplierLocation = false;
            menu.ShowStockInventory = false;
            menu.ShowSupervisor = false;
            menu.ShowFoodStandards = false;
            menu.ShowAllergen = false;
            menu.ShowPartyTheme = false;
            menu.ShowBakeryInventory = false;
            menu.ShowSpongeOrder = false;
        }

        // Get pending order count
        menu.PendingOrderCount = await GetPendingOrderCountAsync(webshopId);

        return menu;
    }

    /// <summary>
    /// Checks if the bakery branch has baking enabled (for trade account visibility).
    /// </summary>
    private async Task<bool> CheckIsBakingAsync(string webshopId)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT ISNULL(WebstoreBranch_isBaking, 0) FROM tbl_WebstoreBranch WHERE WebstoreBranch_BranchID = @webshopId",
                conn);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value && Convert.ToBoolean(result);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the list of allowed module IDs for a user.
    /// Checks tbl_moduleAssignment for the specific module IDs used in BakeryMaster.
    /// </summary>
    private async Task<HashSet<int>> GetModuleAccessAsync(int userId)
    {
        var allowed = new HashSet<int>();
        var moduleIds = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 14, 15, 16, 19, 20, 21, 22, 24 };

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var inClause = string.Join(",", moduleIds);
            await using var cmd = new SqlCommand(
                $"SELECT moduleAssignment_moduleID FROM tbl_moduleAssignment WHERE moduleAssignment_userID = @userId AND moduleAssignment_moduleID IN ({inClause})",
                conn);
            cmd.Parameters.AddWithValue("@userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                allowed.Add(Convert.ToInt32(reader["moduleAssignment_moduleID"]));
            }
        }
        catch
        {
            // Return empty set on error
        }

        return allowed;
    }

    /// <summary>
    /// Gets the supervisor name for a bakery (head office display).
    /// </summary>
    private async Task<string?> GetSupervisorNameAsync(string webshopId)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT TOP 1 BakerySuperviser_FullName FROM BakerySuperviser WHERE BakerySuperviser_bakeryID = @bakeryId",
                conn);
            cmd.Parameters.AddWithValue("@bakeryId", long.Parse(webshopId));

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the count of pending orders for the bakery.
    /// From tbl_order WHERE order_branchID=webshopId AND order_status=0 AND order_isPurchased=1 AND order_isdeleted=0
    /// </summary>
    private async Task<int> GetPendingOrderCountAsync(string webshopId)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM tbl_order WHERE order_branchID = @webshopId AND order_status = 0 AND order_isPurchased = 1 AND order_isdeleted = 0 AND order_followingOrderid = 0",
                conn);
            cmd.Parameters.AddWithValue("@webshopId", webshopId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Model representing which menu items are visible for the current user.
/// </summary>
public class MenuVisibility
{
    public bool ShowOrders { get; set; } = true;
    public bool ShowSpongeOrder { get; set; }
    public bool ShowBakeryInventory { get; set; }
    public bool ShowStockInventory { get; set; }
    public bool ShowEditBusinessInfo { get; set; }
    public bool ShowPaymentSettings { get; set; }
    public bool ShowTradeAccount { get; set; }
    public bool ShowManageUsers { get; set; }
    public bool ShowSupplierUsers { get; set; }
    public bool ShowUsersTimeline { get; set; }
    public bool ShowBakeryIngredient { get; set; }
    public bool ShowFranchiseProduct { get; set; }
    public bool ShowSandwichBar { get; set; }
    public bool ShowPurchaseOrder { get; set; }
    public bool ShowSupplyOrder { get; set; }
    public bool ShowSocialLinks { get; set; }
    public bool ShowManageRota { get; set; }
    public bool ShowModuleAssignment { get; set; }
    public bool ShowOrderManifest { get; set; }
    public bool ShowFoodStandards { get; set; }
    public bool ShowAllergen { get; set; }
    public bool ShowDeliveryRoutes { get; set; }
    public bool ShowDeliveryRoutesPayments { get; set; }
    public bool ShowTemplates { get; set; }
    public bool ShowTemplateSpec { get; set; }
    public bool ShowTemplateFormula { get; set; }
    public bool ShowTemplateBakingCost { get; set; }
    public bool ShowSupervisor { get; set; }
    public bool ShowSupplier { get; set; }
    public bool ShowSupplierLocation { get; set; }
    public bool ShowPartyTheme { get; set; }
    public string? SupervisorName { get; set; }
    public int PendingOrderCount { get; set; }
}
