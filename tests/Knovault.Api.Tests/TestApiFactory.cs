using Knovault.Application.Covers;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Knovault.Api.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"knovault_api_{Guid.NewGuid():N}");
    private Action<IServiceCollection>? _serviceOverrides;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "test.db");
        var coversDir = Path.Combine(_root, "covers");

        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<KnovaultDbContext>));
            services.AddDbContext<KnovaultDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath};Default Timeout=30")
                 .AddInterceptors(new SqliteWalInterceptor()));

            services.RemoveAll(typeof(ICoverStore));
            services.AddSingleton<ICoverStore>(new CoverStorage(coversDir));

            _serviceOverrides?.Invoke(services);
        });
    }

    public HttpClient CreateClientWith(Action<IServiceCollection> overrides)
    {
        _serviceOverrides = overrides;
        return CreateClient();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { /* 忽略清理失敗 */ }
    }
}
