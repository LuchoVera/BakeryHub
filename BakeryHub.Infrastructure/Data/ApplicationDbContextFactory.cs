using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BakeryHub.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string apiProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../BakeryHub.Api"));

        if (!Directory.Exists(apiProjectPath))
        {
            throw new DirectoryNotFoundException();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = LoadConnectionStringFromEnvFile(apiProjectPath);
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException();
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
    private string? LoadConnectionStringFromEnvFile(string apiProjectPath)
    {
        var envFilePath = Path.Combine(apiProjectPath, ".env");
        if (!File.Exists(envFilePath))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == "ConnectionStrings__DefaultConnection")
            {
                return parts[1].Trim().Trim('"');
            }
        }

        return null;
    }
}
