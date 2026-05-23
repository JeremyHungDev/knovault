using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Knovault.Infrastructure.Persistence;

/// <summary>讓 `dotnet ef` 在設計期能建立 DbContext（連線字串僅供工具用，不影響執行期）。</summary>
public class KnovaultDbContextFactory : IDesignTimeDbContextFactory<KnovaultDbContext>
{
    public KnovaultDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KnovaultDbContext>()
            .UseSqlite("Data Source=knovault_design.db")
            .Options;
        return new KnovaultDbContext(options);
    }
}
