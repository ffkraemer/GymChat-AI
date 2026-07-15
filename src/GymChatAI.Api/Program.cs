using GymChatAI.Api.Endpoints;
using GymChatAI.Infrastructure.DependencyInjection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGymChatInfrastructure(builder.Configuration);

var app = builder.Build();

// Seed a demo gym + starter FAQ knowledge base so the POC can be exercised immediately.
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<InMemoryDataStore>();
    var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>();
    DemoDataSeeder.Seed(store, whatsAppOptions);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithTags("Health");

app.MapWhatsAppWebhookEndpoints();
app.MapFaqEndpoints();
app.MapConversationEndpoints();
app.MapGymEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{ }