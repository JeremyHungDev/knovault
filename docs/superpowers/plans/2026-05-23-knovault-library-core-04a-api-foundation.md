# Knovault 書庫核心 — P4a API 地基 + 書籍端點實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 建立可運行的 REST API 地基：Program.cs DI/資料路徑/SQLite(WAL) 接線、開機套用 migration、ProblemDetails、DTO（含 discriminated copy DTO）與對應、書籍端點（列表/詳情/手動新增實體書）與 health，並用 `WebApplicationFactory` + 臨時 SQLite 做整合測試。

**Architecture:** API 在 `Knovault.Api`（Minimal API，依功能分組）。DTO 與對應放 Api 層（API 專屬）。測試用 `WebApplicationFactory<Program>`，以 `ConfigureTestServices` 把 DbContext 與 ICoverStore 換成臨時實作（race-free，不碰 %AppData%）。

**Tech Stack:** ASP.NET Core 8 Minimal API、EF Core、Microsoft.AspNetCore.Mvc.Testing、xUnit + FluentAssertions。

> **執行前置**：從 `dev` 開分支 `feat/library-core-p4`（Task 0）。commit 風格：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。逐 Task 本機 commit；整個 P4（a+b+c）完成後 squash → 合併 dev → 推。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §3、§6。

---

## 檔案結構（本計畫產出）

```
src/Knovault.Api/
  Program.cs                         ← 重寫：DI、資料路徑、migrate、端點對應
  AppPaths.cs                        ← 資料路徑（可被 KNOVAULT_DATA 覆蓋）
  Contracts/
    PagedResult.cs
    BookSummaryDto.cs
    BookDetailDto.cs
    CopyDto.cs                       ← discriminated（type: digital/physical）
    CreatePhysicalBookRequest.cs
  Mapping/BookMappings.cs
  Endpoints/
    HealthEndpoints.cs
    BookEndpoints.cs
tests/Knovault.Api.Tests/
  TestApiFactory.cs
  HealthEndpointsTests.cs
  BookEndpointsTests.cs
```

---

## Task 0: 建立功能分支

- [ ] **Step 1:**
```bash
git switch dev
git switch -c feat/library-core-p4
```

---

## Task 1: 套件 + 主程式 DI 接線 + Health

**Files:** Create `AppPaths.cs`, rewrite `Program.cs`, create `Endpoints/HealthEndpoints.cs`, `tests/.../TestApiFactory.cs`, `HealthEndpointsTests.cs`

- [ ] **Step 1: Api.Tests 加入測試套件**

Run:
```bash
dotnet add tests/Knovault.Api.Tests package Microsoft.AspNetCore.Mvc.Testing --version "8.*"
dotnet add tests/Knovault.Api.Tests package FluentAssertions --version "7.*"
```

- [ ] **Step 2: 建立 `AppPaths.cs`**

Create `src/Knovault.Api/AppPaths.cs`:
```csharp
namespace Knovault.Api;

public sealed class AppPaths
{
    public string DataRoot { get; }
    public string DbPath => Path.Combine(DataRoot, "knovault.db");
    public string CoversDir => Path.Combine(DataRoot, "covers");

    public AppPaths()
    {
        DataRoot = Environment.GetEnvironmentVariable("KNOVAULT_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Knovault");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(CoversDir);
    }
}
```

- [ ] **Step 3: 重寫 `Program.cs`**

Replace `src/Knovault.Api/Program.cs` with:
```csharp
using Knovault.Api;
using Knovault.Api.Endpoints;
using Knovault.Application.Covers;
using Knovault.Application.Files;
using Knovault.Application.Library;
using Knovault.Application.Parsing;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Files;
using Knovault.Infrastructure.Library;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var paths = new AppPaths();
builder.Services.AddSingleton(paths);

builder.Services.AddDbContext<KnovaultDbContext>(o =>
    o.UseSqlite($"Data Source={paths.DbPath};Default Timeout=30")
     .AddInterceptors(new SqliteWalInterceptor()));

builder.Services.AddScoped<IFileHasher, FileHasher>();
builder.Services.AddSingleton<ICoverStore>(_ => new CoverStorage(paths.CoversDir));
builder.Services.AddScoped<IBookFileParser, EpubMetadataParser>();
builder.Services.AddScoped<IBookFileParser, PdfMetadataParser>();
builder.Services.AddScoped<BookParsingService>();
builder.Services.AddScoped<ILibraryScanService, LibraryScanService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<KnovaultDbContext>().Database.Migrate();

app.MapHealthEndpoints();
app.MapBookEndpoints();

app.Run();

public partial class Program; // 供 WebApplicationFactory 使用
```

- [ ] **Step 4: 建立 `Endpoints/HealthEndpoints.cs`**

Create `src/Knovault.Api/Endpoints/HealthEndpoints.cs`:
```csharp
namespace Knovault.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
    }
}
```

> 註：`MapBookEndpoints` 在 Task 3 建立；本 Task 先以空殼讓編譯通過——在 Task 3 前，暫時於 Program.cs 註解掉 `app.MapBookEndpoints();`，或先建立空的 `BookEndpoints`。**採後者**：Step 5 先建空殼。

- [ ] **Step 5: 建立空殼 `Endpoints/BookEndpoints.cs`（Task 3 補內容）**

Create `src/Knovault.Api/Endpoints/BookEndpoints.cs`:
```csharp
namespace Knovault.Api.Endpoints;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        // 端點於 Task 3 加入
    }
}
```

- [ ] **Step 6: 建立測試夾具 `TestApiFactory.cs`**

Create `tests/Knovault.Api.Tests/TestApiFactory.cs`:
```csharp
using Knovault.Application.Covers;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Knovault.Api.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"knovault_api_{Guid.NewGuid():N}");

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
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { /* 忽略清理失敗 */ }
    }
}
```

- [ ] **Step 7: 寫 Health 整合測試**

Create `tests/Knovault.Api.Tests/HealthEndpointsTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class HealthEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public HealthEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("ok");
    }
}
```

- [ ] **Step 8: 跑測試 + 建置**

Run: `dotnet test tests/Knovault.Api.Tests --filter HealthEndpointsTests`
Expected: PASS（1 test）。啟動時會對臨時 db 套用 migration。

- [ ] **Step 9: Commit**
```bash
git add -A
git commit -m "加入 API 主程式接線與 health 端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: DTO 與對應

**Files:** Create `Contracts/*.cs`, `Mapping/BookMappings.cs`

- [ ] **Step 1: 建立 `Contracts/PagedResult.cs`**

```csharp
namespace Knovault.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
```

- [ ] **Step 2: 建立 `Contracts/CopyDto.cs`**

```csharp
namespace Knovault.Api.Contracts;

public sealed record CopyDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";        // "digital" | "physical"
    public string? Format { get; init; }            // digital
    public long? FileSizeBytes { get; init; }       // digital
    public bool? IsMissing { get; init; }           // digital
    public bool? ParseFailed { get; init; }         // digital
    public string? Location { get; init; }          // physical
}
```

- [ ] **Step 3: 建立 `Contracts/BookSummaryDto.cs`**

```csharp
namespace Knovault.Api.Contracts;

public sealed record BookSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? CoverPath { get; init; }
    public string ReadingStatus { get; init; } = "";
    public int? ProgressPercent { get; init; }
    public bool HasDigital { get; init; }
    public bool HasPhysical { get; init; }
}
```

- [ ] **Step 4: 建立 `Contracts/BookDetailDto.cs`**

```csharp
namespace Knovault.Api.Contracts;

public sealed record BookDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Description { get; init; }
    public string? Isbn { get; init; }
    public string? CoverPath { get; init; }
    public string ReadingStatus { get; init; } = "";
    public int? ProgressPercent { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalPages { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CopyDto> Copies { get; init; } = Array.Empty<CopyDto>();
}
```

- [ ] **Step 5: 建立 `Contracts/CreatePhysicalBookRequest.cs`**

```csharp
namespace Knovault.Api.Contracts;

public sealed record CreatePhysicalBookRequest
{
    public string Title { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Language { get; init; }
    public string? Description { get; init; }
    public string? Location { get; init; }
}
```

- [ ] **Step 6: 建立 `Mapping/BookMappings.cs`**

```csharp
using Knovault.Api.Contracts;
using Knovault.Domain.Entities;

namespace Knovault.Api.Mapping;

public static class BookMappings
{
    public static BookSummaryDto ToSummaryDto(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Authors = b.Authors.Select(a => a.Name).ToList(),
        CoverPath = b.CoverPath,
        ReadingStatus = b.ReadingStatus.ToString(),
        ProgressPercent = b.Progress.Percent,
        HasDigital = b.HasDigital,
        HasPhysical = b.HasPhysical
    };

    public static BookDetailDto ToDetailDto(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        Authors = b.Authors.Select(a => a.Name).ToList(),
        Language = b.Language,
        Publisher = b.Publisher,
        PublishedDate = b.PublishedDate,
        Description = b.Description,
        Isbn = b.Isbn,
        CoverPath = b.CoverPath,
        ReadingStatus = b.ReadingStatus.ToString(),
        ProgressPercent = b.Progress.Percent,
        CurrentPage = b.Progress.CurrentPage,
        TotalPages = b.Progress.TotalPages,
        Tags = b.Tags.Select(t => t.Name).ToList(),
        Copies = b.Copies.Select(ToCopyDto).ToList()
    };

    private static CopyDto ToCopyDto(BookCopy c) => c switch
    {
        DigitalCopy d => new CopyDto
        {
            Id = d.Id,
            Type = "digital",
            Format = d.Format.ToString(),
            FileSizeBytes = d.FileSizeBytes,
            IsMissing = d.IsMissing,
            ParseFailed = d.ParseFailed
        },
        PhysicalCopy p => new CopyDto
        {
            Id = p.Id,
            Type = "physical",
            Location = p.Location
        },
        _ => new CopyDto { Id = c.Id, Type = "unknown" }
    };
}
```

- [ ] **Step 7: 建置 + Commit**

Run: `dotnet build src/Knovault.Api`
Expected: `Build succeeded`
```bash
git add -A
git commit -m "加入 API DTO 與對應" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: 書籍端點（列表 / 詳情 / 手動新增實體書）

**Files:** Rewrite `Endpoints/BookEndpoints.cs`; Test `BookEndpointsTests.cs`

- [ ] **Step 1: 寫整合測試**

Create `tests/Knovault.Api.Tests/BookEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class BookEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public BookEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_physical_book_then_list_and_get()
    {
        var client = _factory.CreateClient();

        var create = new CreatePhysicalBookRequest
        {
            Title = "實體測試書",
            Authors = new() { "某作者" },
            Location = "書房 A-1",
            Isbn = "9789999999999"
        };
        var createResp = await client.PostAsJsonAsync("/api/books", create);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();
        created!.Title.Should().Be("實體測試書");
        created.HasPhysical.Should().BeFalse(); // detail 無 HasPhysical 欄；改驗 copies
        created.Copies.Should().ContainSingle(c => c.Type == "physical" && c.Location == "書房 A-1");

        var list = await client.GetFromJsonAsync<PagedResult<BookSummaryDto>>("/api/books");
        list!.Total.Should().Be(1);
        list.Items.Should().ContainSingle(b => b.Title == "實體測試書" && b.HasPhysical);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{created.Id}");
        detail!.Isbn.Should().Be("9789999999999");
    }

    [Fact]
    public async Task Get_missing_book_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_book_with_blank_title_returns_400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

> 註：`BookDetailDto` 無 `HasPhysical` 欄位，測試改以 `Copies` 驗證實體版本；上面測試已如此。移除誤植的 `created.HasPhysical` 那行。

- [ ] **Step 2: 修正測試（移除 HasPhysical 誤植行）**

把 `created.HasPhysical.Should().BeFalse();` 整行刪除（`BookDetailDto` 沒有該屬性）。保留 `Copies` 斷言。

- [ ] **Step 3: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Api.Tests --filter BookEndpointsTests`
Expected: 失敗（端點未實作 → 404/405 或反序列化失敗）。

- [ ] **Step 4: 實作 `Endpoints/BookEndpoints.cs`**

Replace `src/Knovault.Api/Endpoints/BookEndpoints.cs` with:
```csharp
using Knovault.Api.Contracts;
using Knovault.Api.Mapping;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books");

        group.MapGet("", ListBooks);
        group.MapGet("/{id:guid}", GetBook);
        group.MapPost("", CreatePhysicalBook);
    }

    private static async Task<IResult> ListBooks(KnovaultDbContext db, string? search, int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Books
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Title.Contains(search));

        var total = await query.CountAsync();
        var books = await query
            .OrderBy(b => b.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = books.Select(b => b.ToSummaryDto()).ToList();
        return Results.Ok(new PagedResult<BookSummaryDto>(items, total, page, pageSize));
    }

    private static async Task<IResult> GetBook(KnovaultDbContext db, Guid id)
    {
        var book = await db.Books
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == id);

        return book is null ? Results.NotFound() : Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> CreatePhysicalBook(KnovaultDbContext db, CreatePhysicalBookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.Problem(title: "書名為必填", statusCode: StatusCodes.Status400BadRequest);

        var book = new Book(req.Title);
        book.SetAuthors(req.Authors);
        book.UpdateMetadata(req.Title, null, req.Language, req.Publisher, req.PublishedDate, req.Description, req.Isbn);
        book.AddCopy(new PhysicalCopy(req.Location));

        db.Books.Add(book);
        await db.SaveChangesAsync();

        return Results.Created($"/api/books/{book.Id}", book.ToDetailDto());
    }
}
```

- [ ] **Step 5: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Api.Tests --filter BookEndpointsTests`
Expected: PASS（3 tests）。

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "加入書籍列表/詳情/新增實體書端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: 全量驗證

- [ ] **Step 1: 全量測試**
Run: `dotnet test`
Expected: Domain 25 + Infrastructure 24 + Api (1 + 3 = 4) = 53 passed。

- [ ] **Step 2: 全量建置**
Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: 留在 `feat/library-core-p4`**，不合併不推（等 P4b/P4c）。

---

## 完成定義 (Definition of Done)

- API 主程式接線完成：DI、資料路徑（可被 `KNOVAULT_DATA` 覆蓋）、SQLite+WAL、開機 migrate、ProblemDetails。
- DTO（含 discriminated copy DTO）與對應完成。
- 書籍端點：`GET /api/books`（search/page）、`GET /api/books/{id}`、`POST /api/books`（手動實體書）。
- `WebApplicationFactory` 整合測試（臨時 SQLite，race-free）全綠。
- `dotnet test` 全綠（53）、`dotnet build` 0 警告 0 錯誤。

## 不在本計畫範圍（P4b / P4c）

- 書籍 PUT/PATCH(reading)/DELETE、copies、tags、authors facet、folders、scan 觸發、封面/檔案下載端點、ISBN 查詢（P4b）。
- SSE 掃描進度、找空閒埠、開瀏覽器、託管 Vue 靜態檔（P4c）。
