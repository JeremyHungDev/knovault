using Knovault.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Tests;

/// <summary>每個測試一個獨立的臨時 SQLite 檔；用 EnsureCreated 建立 schema。</summary>
public sealed class SqliteTestDb : IDisposable
{
    public string Path { get; }
    private readonly DbContextOptions<KnovaultDbContext> _options;

    public SqliteTestDb()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"knovault_test_{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<KnovaultDbContext>()
            .UseSqlite($"Data Source={Path};Default Timeout=30")
            .AddInterceptors(new SqliteWalInterceptor())
            .Options;
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public KnovaultDbContext NewContext() => new(_options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Path)) File.Delete(Path);
    }
}
