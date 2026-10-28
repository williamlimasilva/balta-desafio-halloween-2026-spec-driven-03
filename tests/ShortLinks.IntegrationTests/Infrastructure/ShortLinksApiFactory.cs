using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShortLinks.Api.Options;
using ShortLinks.Infrastructure.Persistence;

namespace ShortLinks.IntegrationTests.Infrastructure;

public sealed class ShortLinksApiFactory : WebApplicationFactory<Program>
{
    private const string DeterministicTestKey =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private readonly bool _deleteDatabaseOnDispose;

    public ShortLinksApiFactory(
        string? databasePath = null,
        bool deleteDatabaseOnDispose = true,
        TimeProvider? timeProvider = null)
    {
        DatabasePath = databasePath ?? Path.Combine(
            Path.GetTempPath(),
            $"short-links-tests-{Guid.NewGuid():N}.db");
        _deleteDatabaseOnDispose = deleteDatabaseOnDispose;
        TimeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DatabasePath { get; }

    public TimeProvider TimeProvider { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(TimeProvider);
            services.RemoveAll<LinkOptions>();
            services.AddSingleton(new LinkOptions
            {
                DatabasePath = DatabasePath,
                PublicBaseUrl = "https://short.test"
            });
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Pooling = false
            }.ToString();

            services.AddDbContext<LinkDbContext>(options =>
                options.UseSqlite(connectionString));
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Links:DatabasePath"] = DatabasePath,
                ["Links:PublicBaseUrl"] = "https://short.test",
                ["LinkSecurity:ClientIpFingerprintKey"] = DeterministicTestKey
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _deleteDatabaseOnDispose)
        {
            DeleteDatabaseFile();
        }
    }

    private void DeleteDatabaseFile()
    {
        foreach (var path in new[] { DatabasePath, $"{DatabasePath}-shm", $"{DatabasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return application =>
        {
            application.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= System.Net.IPAddress.Loopback;
                await nextMiddleware();
            });

            next(application);
        };
    }
}