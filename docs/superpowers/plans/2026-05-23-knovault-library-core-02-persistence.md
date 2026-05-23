# Knovault 書庫核心 — P2 持久層（EF Core + SQLite）實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 用 EF Core + SQLite 把 P1 的領域模型對應到資料庫（TPH 多型、owned 值物件、多對多），加上 WAL/busy_timeout 並發設定與 Migrations，並以臨時 SQLite 檔做整合測試驗證來回存取。

**Architecture:** 持久層全在 `Knovault.Infrastructure`；領域層不動。映射用 `IEntityTypeConfiguration` 分檔。並發設定（WAL + busy_timeout）用 DbConnection 攔截器。整合測試用真實的臨時 SQLite 檔（非 in-memory），驗證實際 SQLite 行為。

**Tech Stack:** EF Core 8 (Sqlite + Design)、Microsoft.Data.Sqlite、dotnet-ef（local tool）、xUnit + FluentAssertions。

> **執行前置**：從 `dev` 開分支 `feat/library-core-p2`（Task 0）。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §4 模型、§3 SQLite 並發（WAL+busy_timeout，不用 Cache=Shared）、§10 結構、§9 測試。
> **commit 風格**：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。

---

## 檔案結構（本計畫產出）

```
.config/dotnet-tools.json                      ← dotnet-ef 本地工具
src/Knovault.Infrastructure/
  Persistence/
    KnovaultDbContext.cs
    SqliteWalInterceptor.cs                     ← WAL + busy_timeout
    KnovaultDbContextFactory.cs                 ← 設計期工廠（給 dotnet ef）
    Configurations/
      BookConfiguration.cs
      BookCopyConfiguration.cs                  ← TPH discriminator
      DigitalCopyConfiguration.cs
      PhysicalCopyConfiguration.cs
      TagConfiguration.cs
      LibraryFolderConfiguration.cs
    Migrations/                                 ← dotnet ef 產生
tests/Knovault.Infrastructure.Tests/
  SqliteTestDb.cs                               ← 臨時檔測試夾具
  PersistenceIntegrationTests.cs
```

---

## Task 0: 建立功能分支

- [ ] **Step 1: 從 dev 開分支**

Run:
```bash
git switch dev
git switch -c feat/library-core-p2
```
Expected: `Switched to a new branch 'feat/library-core-p2'`

---

## Task 1: 套件與 dotnet-ef 工具

**Files:** `.config/dotnet-tools.json`（產生）、修改各 `.csproj`

- [ ] **Step 1: Infrastructure 加入 EF Core 套件**

Run:
```bash
dotnet add src/Knovault.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite --version "8.*"
dotnet add src/Knovault.Infrastructure package Microsoft.EntityFrameworkCore.Design --version "8.*"
```

- [ ] **Step 2: Infrastructure.Tests 加入 FluentAssertions**

Run:
```bash
dotnet add tests/Knovault.Infrastructure.Tests package FluentAssertions --version "7.*"
```
> EF Core 與 Microsoft.Data.Sqlite 經由專案參考由 Infrastructure 傳遞，測試專案不需另加。

- [ ] **Step 3: 建立本地工具資訊清單並安裝 dotnet-ef**

Run:
```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version "8.*"
```

- [ ] **Step 4: 驗證 ef 工具可用 + 建置**

Run:
```bash
dotnet ef --version
dotnet build src/Knovault.Infrastructure
```
Expected: 印出 EF Core 版本；`Build succeeded`。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "加入 EF Core 套件與 dotnet-ef 工具" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: DbContext 與實體映射

**Files:** Create `KnovaultDbContext.cs` 與 `Configurations/*.cs`

- [ ] **Step 1: 建立 `KnovaultDbContext.cs`**

Create `src/Knovault.Infrastructure/Persistence/KnovaultDbContext.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Persistence;

public class KnovaultDbContext : DbContext
{
    public KnovaultDbContext(DbContextOptions<KnovaultDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LibraryFolder> LibraryFolders => Set<LibraryFolder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnovaultDbContext).Assembly);
    }
}
```

- [ ] **Step 2: 建立 `BookConfiguration.cs`**

Create `src/Knovault.Infrastructure/Persistence/Configurations/BookConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Title).IsRequired();
        builder.Property(b => b.ReadingStatus).HasConversion<string>();

        // 閱讀進度：owned 值物件（必填；三欄皆可空）
        builder.OwnsOne(b => b.Progress, p =>
        {
            p.Property(x => x.Percent).HasColumnName("ProgressPercent");
            p.Property(x => x.CurrentPage).HasColumnName("ProgressCurrentPage");
            p.Property(x => x.TotalPages).HasColumnName("ProgressTotalPages");
        });
        builder.Navigation(b => b.Progress).IsRequired();

        // 作者：owned 有序集合，對應私有欄位 _authors
        builder.OwnsMany(b => b.Authors, a =>
        {
            a.ToTable("BookAuthors");
            a.WithOwner().HasForeignKey("BookId");
            a.Property(x => x.Order);
            a.Property(x => x.Name).IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(Book.Authors))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 版本：TPH 一對多，對應私有欄位 _copies
        builder.HasMany(b => b.Copies)
            .WithOne()
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Book.Copies))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 標籤：多對多，對應私有欄位 _tags
        builder.HasMany(b => b.Tags).WithMany();
        builder.Metadata.FindNavigation(nameof(Book.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 衍生旗標不入庫
        builder.Ignore(b => b.HasDigital);
        builder.Ignore(b => b.HasPhysical);
    }
}
```

- [ ] **Step 3: 建立 `BookCopyConfiguration.cs`（TPH discriminator）**

Create `src/Knovault.Infrastructure/Persistence/Configurations/BookCopyConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable("BookCopies");
        builder.HasKey(c => c.Id);
        builder.HasDiscriminator<string>("CopyKind")
            .HasValue<DigitalCopy>("Digital")
            .HasValue<PhysicalCopy>("Physical");
    }
}
```

- [ ] **Step 4: 建立 `DigitalCopyConfiguration.cs`**

Create `src/Knovault.Infrastructure/Persistence/Configurations/DigitalCopyConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class DigitalCopyConfiguration : IEntityTypeConfiguration<DigitalCopy>
{
    public void Configure(EntityTypeBuilder<DigitalCopy> builder)
    {
        // TPH 下子型別專屬欄位皆為可空欄，不可設 IsRequired（其他子型別沒有它們）
        builder.Property(c => c.Format).HasConversion<string>();
    }
}
```

- [ ] **Step 5: 建立 `PhysicalCopyConfiguration.cs`**

Create `src/Knovault.Infrastructure/Persistence/Configurations/PhysicalCopyConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class PhysicalCopyConfiguration : IEntityTypeConfiguration<PhysicalCopy>
{
    public void Configure(EntityTypeBuilder<PhysicalCopy> builder)
    {
        // Location/AcquiredDate 由慣例對應為可空欄，無需額外設定
    }
}
```

- [ ] **Step 6: 建立 `TagConfiguration.cs`**

Create `src/Knovault.Infrastructure/Persistence/Configurations/TagConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
```

- [ ] **Step 7: 建立 `LibraryFolderConfiguration.cs`**

Create `src/Knovault.Infrastructure/Persistence/Configurations/LibraryFolderConfiguration.cs`:
```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class LibraryFolderConfiguration : IEntityTypeConfiguration<LibraryFolder>
{
    public void Configure(EntityTypeBuilder<LibraryFolder> builder)
    {
        builder.ToTable("LibraryFolders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Path).IsRequired();
        builder.HasIndex(f => f.Path).IsUnique();
    }
}
```

- [ ] **Step 8: 建置確認映射可編譯**

Run: `dotnet build src/Knovault.Infrastructure`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "建立 DbContext 與實體映射" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: WAL/busy_timeout 攔截器與設計期工廠

**Files:** Create `SqliteWalInterceptor.cs`, `KnovaultDbContextFactory.cs`

- [ ] **Step 1: 建立 `SqliteWalInterceptor.cs`**

Create `src/Knovault.Infrastructure/Persistence/SqliteWalInterceptor.cs`:
```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Knovault.Infrastructure.Persistence;

/// <summary>每次連線開啟時套用 WAL 與 busy_timeout（見 spec §3 SQLite 並發設定）。</summary>
public sealed class SqliteWalInterceptor : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: 建立 `KnovaultDbContextFactory.cs`（設計期）**

Create `src/Knovault.Infrastructure/Persistence/KnovaultDbContextFactory.cs`:
```csharp
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
```

- [ ] **Step 3: 建置 + Commit**

Run: `dotnet build src/Knovault.Infrastructure`
Expected: `Build succeeded`
```bash
git add -A
git commit -m "加入 SQLite WAL 攔截器與設計期工廠" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: 初始 Migration

- [ ] **Step 1: 產生 InitialCreate migration**

Run:
```bash
dotnet ef migrations add InitialCreate --project src/Knovault.Infrastructure --startup-project src/Knovault.Infrastructure --output-dir Persistence/Migrations
```
Expected: 在 `Persistence/Migrations/` 產生 `*_InitialCreate.cs` 與 `KnovaultDbContextModelSnapshot.cs`。

- [ ] **Step 2: 建置確認 migration 可編譯**

Run: `dotnet build src/Knovault.Infrastructure`
Expected: `Build succeeded`。

- [ ] **Step 3: 檢視 migration 含預期資料表**

Run（PowerShell）: `Select-String -Path src/Knovault.Infrastructure/Persistence/Migrations/*_InitialCreate.cs -Pattern "Books","BookCopies","BookAuthors","Tags","LibraryFolders","CopyKind" | Select-Object -ExpandProperty Line`
Expected: 看到建立 `Books`、`BookCopies`（含 `CopyKind` 鑑別欄）、`BookAuthors`、`Tags`、`LibraryFolders` 與 Book↔Tag 連結表。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "加入初始 migration" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: 整合測試（臨時 SQLite 檔）

**Files:** Create `SqliteTestDb.cs`, `PersistenceIntegrationTests.cs`

- [ ] **Step 1: 建立測試夾具 `SqliteTestDb.cs`**

Create `tests/Knovault.Infrastructure.Tests/SqliteTestDb.cs`:
```csharp
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
```

- [ ] **Step 2: 寫整合測試 `PersistenceIntegrationTests.cs`**

Create `tests/Knovault.Infrastructure.Tests/PersistenceIntegrationTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class PersistenceIntegrationTests
{
    [Fact]
    public async Task Book_with_full_graph_round_trips()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            var book = new Book("Domain-Driven Design");
            book.SetAuthors(new[] { "Eric Evans" });
            book.UpdateMetadata("Domain-Driven Design", "Tackling Complexity",
                "en", "Addison-Wesley", "2003", "經典", "9780321125217");
            book.SetReadingStatus(ReadingStatus.Reading);
            book.SetProgress(ReadingProgress.Create(percent: 40, currentPage: 200, totalPages: 500));
            book.AddCopy(new DigitalCopy("D:/books/ddd.epub", BookFormat.Epub, 2048, "hashA",
                DateTimeOffset.UtcNow, null));
            book.AddCopy(new PhysicalCopy("書房 B 櫃-第3層"));
            book.AddTag(new Tag("設計"));
            bookId = book.Id;

            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.Books
                .Include(b => b.Copies)
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == bookId);

            loaded.Title.Should().Be("Domain-Driven Design");
            loaded.Subtitle.Should().Be("Tackling Complexity");
            loaded.Isbn.Should().Be("9780321125217");
            loaded.ReadingStatus.Should().Be(ReadingStatus.Reading);
            loaded.Authors.Should().ContainSingle(a => a.Name == "Eric Evans");
            loaded.Progress.Percent.Should().Be(40);
            loaded.Progress.TotalPages.Should().Be(500);
            loaded.Copies.OfType<DigitalCopy>().Should().ContainSingle(c => c.FilePath == "D:/books/ddd.epub");
            loaded.Copies.OfType<PhysicalCopy>().Should().ContainSingle(c => c.Location == "書房 B 櫃-第3層");
            loaded.Tags.Should().ContainSingle(t => t.Name == "設計");
            loaded.HasDigital.Should().BeTrue();
            loaded.HasPhysical.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Book_with_empty_progress_round_trips_as_non_null()
    {
        using var db = new SqliteTestDb();
        var id = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            var book = new Book("No Progress Book");
            id = book.Id;
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.Books.SingleAsync(b => b.Id == id);
            loaded.Progress.Should().NotBeNull();
            loaded.Progress.Percent.Should().BeNull();
        }
    }

    [Fact]
    public async Task Tag_name_is_unique()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();

        ctx.Tags.Add(new Tag("哲學"));
        await ctx.SaveChangesAsync();

        ctx.Tags.Add(new Tag("哲學"));
        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task LibraryFolder_round_trips()
    {
        using var db = new SqliteTestDb();
        var id = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            var folder = new LibraryFolder(@"D:\Books", "主書庫");
            id = folder.Id;
            ctx.LibraryFolders.Add(folder);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.LibraryFolders.SingleAsync(f => f.Id == id);
            loaded.Path.Should().Be(@"D:\Books");
            loaded.Enabled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Wal_mode_is_enabled()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await ctx.Database.OpenConnectionAsync();

        var conn = (SqliteConnection)ctx.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (string)(await cmd.ExecuteScalarAsync())!;

        mode.Should().Be("wal");
    }
}
```

- [ ] **Step 3: 跑整合測試**

Run: `dotnet test tests/Knovault.Infrastructure.Tests`
Expected: PASS（5 tests）。

> **若 `Book_with_empty_progress_round_trips_as_non_null` 失敗**（EF 把全 null 的 owned 讀成 null）：在 `BookConfiguration` 的 `OwnsOne` 內為其中一欄加非空預設或保留 `Navigation(...).IsRequired()`；EF Core 8 對「必填 owned + 全 null」應會以建構子materialize 出實例。先跑測試確認實際行為再決定是否額外處理。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "加入持久層整合測試" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: 全量驗證與合併

- [ ] **Step 1: 全量測試**

Run: `dotnet test`
Expected: Domain 25 + Infrastructure 5 = 30 passed。

- [ ] **Step 2: 全量建置**

Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: 完成分支**（依 finishing-a-development-branch：合併回 dev、刪分支；推遠端依使用者意願）

---

## 完成定義 (Definition of Done)

- EF Core + SQLite 映射完成：Book（owned 進度/作者、多對多標籤、TPH 版本）、Tag、LibraryFolder。
- WAL + busy_timeout 經攔截器套用，整合測試驗證 `journal_mode=wal`。
- InitialCreate migration 產生且可編譯，含所有資料表與 `CopyKind` 鑑別欄。
- 整合測試以臨時 SQLite 檔驗證來回存取，全綠（含全 null 進度、唯一索引）。
- `dotnet test` 全綠（30）、`dotnet build` 0 警告 0 錯誤。

## 不在本計畫範圍（後續）

- 倉儲/查詢服務與 use-cases（隨 P4 API 一起，依實際用例設計）。
- 解析、掃描、封面（P3）。
