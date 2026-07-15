using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GymChatAI.Infrastructure.Persistence.EfCore;

/// <summary>
/// Lets the EF Core CLI create a DbContext at design time (for `dotnet ef migrations add`, etc.)
/// without needing to spin up the whole Api host.
/// </summary>
public class GymChatDbContextFactory : IDesignTimeDbContextFactory<GymChatDbContext>
{
    public GymChatDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("GymChatDb");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new Exception("ConnectionString not found in the Configuration");

        var optionsBuilder = new DbContextOptionsBuilder<GymChatDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new GymChatDbContext(optionsBuilder.Options);
    }
}