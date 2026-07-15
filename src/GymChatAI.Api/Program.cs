using GymChatAI.Api.Endpoints;
using GymChatAI.Infrastructure.DependencyInjection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGymChatInfrastructure(builder.Configuration);

var app = builder.Build();

var connectionString = builder.Configuration.GetConnectionString("GymChatDb");
var usingSqlServer = !string.IsNullOrWhiteSpace(connectionString);

// Provision the database schema (SQL Server mode) and seed a demo gym + starter FAQ
// knowledge base, so the app can be exercised immediately either way.
using (var scope = app.Services.CreateScope())
{
    var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>();

    if (usingSqlServer)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GymChatDbContext>();

        // EnsureCreated (rather than Migrate) for now: the schema is still evolving quickly
        // in the MVP phase. Once it stabilizes, run `dotnet ef migrations add InitialCreate`
        // from src/GymChatAI.Infrastructure and switch this to dbContext.Database.MigrateAsync().
        await dbContext.Database.EnsureCreatedAsync();
        await EfDemoDataSeeder.SeedAsync(dbContext, whatsAppOptions);

        app.Logger.LogInformation("GymChat AI started using SQL Server persistence.");
    }
    else
    {
        var store = scope.ServiceProvider.GetRequiredService<InMemoryDataStore>();
        DemoDataSeeder.Seed(store, whatsAppOptions);

        app.Logger.LogInformation("GymChat AI started using in-memory persistence (no ConnectionStrings:GymChatDb configured).");
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", persistence = usingSqlServer ? "sql-server" : "in-memory" }))
    .WithTags("Health");

app.MapWhatsAppWebhookEndpoints();
app.MapFaqEndpoints();
app.MapConversationEndpoints();
app.MapGymEndpoints();
app.MapCredentialHealthEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }