var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<CakerStreet.Business.Filters.MigrationSafetyFilter>();
});
builder.Services.AddSingleton<CakerStreet.Business.Services.BakeryAuthHelper>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryMenuService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BusinessOrdersService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BusinessOrderDetailService>();
builder.Services.AddScoped<CakerStreet.Business.Services.MapCustomizedCakeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.AssignedTasksService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StaffRotaService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StaffRequestsService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StaffDashboardService>();
builder.Services.AddScoped<CakerStreet.Business.Services.OrderSpongeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.OrderManifestService>();
builder.Services.AddScoped<CakerStreet.Business.Services.DeliveryRoutesService>();
builder.Services.AddScoped<CakerStreet.Business.Services.PurchaseOrderService>();
builder.Services.AddScoped<CakerStreet.Business.Services.SupplyOrderService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryIngredientService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ManageSupplierService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ManageLocationService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryInventoryService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StockRequestService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryUserService>();
builder.Services.AddScoped<CakerStreet.Business.Services.SupplierUserService>();
builder.Services.AddScoped<CakerStreet.Business.Services.EditBusinessInfoService>();
builder.Services.AddScoped<CakerStreet.Business.Services.TradeAccountService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakerWorkTimeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.AccountBalanceService>();
builder.Services.AddScoped<CakerStreet.Business.Services.AccountBalanceBakingService>();
builder.Services.AddScoped<CakerStreet.Business.Services.SocialLinksService>();
builder.Services.AddScoped<CakerStreet.Business.Services.PaymentSettingsService>();
builder.Services.AddScoped<CakerStreet.Business.Services.DeliveryRoutePaymentsService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ModuleAssignmentService>();
builder.Services.AddScoped<CakerStreet.Business.Services.AllergenMatrixService>();
builder.Services.AddScoped<CakerStreet.Business.Services.RecipeMatrixService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ManageThemesService>();
builder.Services.AddScoped<CakerStreet.Business.Services.RecipeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ServiceService>();
builder.Services.AddScoped<CakerStreet.Business.Services.DailyChecklistService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StaffCertificatesService>();
builder.Services.AddScoped<CakerStreet.Business.Services.PackagingTypeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.VideoService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryAvailabilityService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ApcService>();
builder.Services.AddScoped<CakerStreet.Business.Services.TopperService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryFilesService>();
builder.Services.AddScoped<CakerStreet.Business.Services.FranchiseLinkingService>();
builder.Services.AddScoped<CakerStreet.Business.Services.KitchenPrintService>();
builder.Services.AddScoped<CakerStreet.Business.Services.StaffTrainingService>();
builder.Services.AddScoped<CakerStreet.Business.Services.PrintDocumentService>();
builder.Services.AddScoped<CakerStreet.Business.Services.BakeryQuotationService>();
builder.Services.AddScoped<CakerStreet.Business.Services.OrderImageService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ResetPasswordService>();
builder.Services.AddScoped<CakerStreet.Business.Services.PersonalisedCakeService>();
builder.Services.AddScoped<CakerStreet.Business.Services.ManageProductDocService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CakerStreet.Business.Services.RouteCalculationService>();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseMiddleware<CakerStreet.Business.Middleware.BakeryAuthMiddleware>();
app.MapControllers();
app.UseMiddleware<CakerStreet.Business.Middleware.LegacyProxyMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    runtime = ".NET 10",
    app = "CakerStreet.Business",
    mutationPhase = app.Configuration["MutationPhase"],
    timestamp = DateTime.UtcNow
}));

app.Run();
