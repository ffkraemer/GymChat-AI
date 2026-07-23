using GymChatAI.Api.Endpoints;
using GymChatAI.Infrastructure.DependencyInjection;
using GymChatAI.Infrastructure.Identity;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.Persistence.EfCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GymChatDb");
var usingSqlServer = !string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddGymChatInfrastructure(builder.Configuration);

// Lets the Administration Portal (a separate origin, e.g. localhost:5173 in dev) call this
// API from the browser. Without this, the frontend's login and every other request fail
// with a CORS error before ever reaching our endpoints.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminPortal", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

if (usingSqlServer)
{
    // Authentication requires a real, durable user store - only available with SQL Server.
    builder.Services.AddGymChatIdentity();
}

var app = builder.Build();

app.UseCors("AdminPortal");

// Apply pending EF Core migrations (SQL Server mode) and seed a demo gym + starter FAQ
// knowledge base (and, in SQL Server mode, a demo admin account), so the app can be
// exercised immediately either way.
using (var scope = app.Services.CreateScope())
{
    var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>();

    if (usingSqlServer)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GymChatDbContext>();
        await dbContext.Database.MigrateAsync();

        var gym = await EfDemoDataSeeder.SeedAsync(dbContext, whatsAppOptions);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DemoAdminSeeder.SeedAsync(roleManager, userManager, gym.Id, app.Logger);

        app.Logger.LogInformation("GymChat AI started using SQL Server persistence (authentication enabled).");
    }
    else
    {
        var store = scope.ServiceProvider.GetRequiredService<InMemoryDataStore>();
        DemoDataSeeder.Seed(store, whatsAppOptions);

        app.Logger.LogWarning(
            "ConnectionStrings:GymChatDb is not configured - falling back to in-memory persistence. " +
            "Data will NOT survive a restart, and authentication is disabled (Identity needs a real store). " +
            "Configure a connection string for real usage.");
    }
}

if (usingSqlServer)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapGroup("/api/auth").WithTags("Auth").MapIdentityApi<ApplicationUser>();
    app.MapCurrentUserEndpoint();
    app.MapRegisterOperatorEndpoint();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", persistence = usingSqlServer ? "sql-server" : "in-memory", auth = usingSqlServer }))
    .WithTags("Health");

app.MapWhatsAppWebhookEndpoints();
app.MapFlowDataExchangeEndpoints();
app.MapFaqEndpoints(usingSqlServer);
app.MapConversationEndpoints(usingSqlServer);
app.MapGymEndpoints(usingSqlServer);
app.MapCampaignEndpoints(usingSqlServer);
app.MapMemberEndpoints(usingSqlServer);
app.MapClassTypeEndpoints(usingSqlServer);
app.MapComplianceEndpoints(usingSqlServer);
app.MapTemplateEndpoints(usingSqlServer);
app.MapFlowEndpoints(usingSqlServer);
app.MapCredentialHealthEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{ }