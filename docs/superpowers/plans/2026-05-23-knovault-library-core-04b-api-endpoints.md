# Knovault 書庫核心 — P4b 其餘 API 端點 + ISBN 查詢實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 補齊書庫核心 REST API：書籍編輯/刪除、閱讀狀態進度、版本(copies) 新增/更新/刪除/下載、封面服務、標籤、作者瀏覽、書庫資料夾、掃描觸發，以及 ISBN 查詢（OpenLibrary）。

**Architecture:** 端點依功能分檔（Minimal API group）。ISBN 查詢抽象 `IIsbnMetadataProvider`（Application）、OpenLibrary 實作（Infrastructure，用 HttpClient）。整合測試用既有 `TestApiFactory`；ISBN provider 以 stub HttpMessageHandler 單元測試。

**Tech Stack:** ASP.NET Core 8 Minimal API、EF Core、System.Text.Json、xUnit + FluentAssertions。

> **執行前置**：續在 `feat/library-core-p4`（P4a 已在此，未推）。commit 風格：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §6。
> **範圍外（P4c）**：SSE 掃描進度（本計畫掃描為同步呼叫回傳報告）、找空閒埠、開瀏覽器、託管 Vue。
> **小備註**：刪除書時封面檔暫不刪（可重生，留作後續清理）；作者 facet 以 client-side 分組（個人規模可接受）。

---

## 檔案結構（本計畫產出/修改）

```
src/Knovault.Api/
  Contracts/  UpdateBookRequest.cs, UpdateReadingRequest.cs, AddPhysicalCopyRequest.cs,
              UpdateCopyRequest.cs, TagDto.cs, CreateTagRequest.cs, AuthorFacetDto.cs,
              FolderDto.cs, CreateFolderRequest.cs, IsbnMetadataDto.cs, ScanReportDto.cs
  Endpoints/  BookEndpoints.cs(修改), CopyEndpoints.cs, TagEndpoints.cs,
              LibraryEndpoints.cs, MetadataEndpoints.cs
  Program.cs(修改：註冊新端點 + HttpClient/ISBN provider)
src/Knovault.Application/Metadata/IIsbnMetadataProvider.cs
src/Knovault.Infrastructure/Metadata/OpenLibraryIsbnProvider.cs
tests/Knovault.Api.Tests/  BookEditEndpointsTests.cs, CopyEndpointsTests.cs,
              TagEndpointsTests.cs, LibraryEndpointsTests.cs
tests/Knovault.Infrastructure.Tests/ OpenLibraryIsbnProviderTests.cs
```

---

## Task 1: 書籍編輯/刪除 + 閱讀狀態 + 封面服務

**Files:** Create `Contracts/UpdateBookRequest.cs`, `UpdateReadingRequest.cs`; modify `Endpoints/BookEndpoints.cs`; Test `BookEditEndpointsTests.cs`

- [ ] **Step 1: 建立 `UpdateBookRequest.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record UpdateBookRequest
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public List<string> Authors { get; init; } = new();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Description { get; init; }
    public string? Isbn { get; init; }
}
```

- [ ] **Step 2: 建立 `UpdateReadingRequest.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record UpdateReadingRequest
{
    public string ReadingStatus { get; init; } = "None";
    public int? Percent { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalPages { get; init; }
}
```

- [ ] **Step 3: 寫整合測試 `BookEditEndpointsTests.cs`**
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class BookEditEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public BookEditEndpointsTests(TestApiFactory factory) => _factory = factory;

    private async Task<BookDetailDto> CreateBookAsync(System.Net.Http.HttpClient client, string title)
    {
        var resp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = title });
        return (await resp.Content.ReadFromJsonAsync<BookDetailDto>())!;
    }

    [Fact]
    public async Task Update_book_changes_metadata()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "原書名");

        var resp = await client.PutAsJsonAsync($"/api/books/{book.Id}", new UpdateBookRequest
        {
            Title = "新書名",
            Authors = new() { "新作者" },
            Publisher = "新出版社"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.Title.Should().Be("新書名");
        detail.Authors.Should().ContainSingle().Which.Should().Be("新作者");
        detail.Publisher.Should().Be("新出版社");
    }

    [Fact]
    public async Task Patch_reading_updates_status_and_progress()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "進度書");

        var resp = await client.PatchAsJsonAsync($"/api/books/{book.Id}/reading", new UpdateReadingRequest
        {
            ReadingStatus = "Reading",
            Percent = 55
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.ReadingStatus.Should().Be("Reading");
        detail.ProgressPercent.Should().Be(55);
    }

    [Fact]
    public async Task Delete_book_removes_it()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "待刪書");

        (await client.DeleteAsync($"/api/books/{book.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/books/{book.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 4: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Api.Tests --filter BookEditEndpointsTests`
Expected: 失敗（端點未實作）。

- [ ] **Step 5: 擴充 `BookEndpoints.cs`（在 MapBookEndpoints 內新增）**

在 `var group = app.MapGroup("/api/books");` 之後、現有 GET/POST 之外，新增對應與處理方法。完整檔案改為：
```csharp
using Knovault.Api;
using Knovault.Api.Contracts;
using Knovault.Api.Mapping;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
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
        group.MapPut("/{id:guid}", UpdateBook);
        group.MapPatch("/{id:guid}/reading", UpdateReading);
        group.MapDelete("/{id:guid}", DeleteBook);
        group.MapGet("/{id:guid}/cover", (Guid id, KnovaultDbContext db, AppPaths paths, CancellationToken ct) =>
            ServeCover(id, db, paths, thumb: false, ct));
        group.MapGet("/{id:guid}/cover/thumb", (Guid id, KnovaultDbContext db, AppPaths paths, CancellationToken ct) =>
            ServeCover(id, db, paths, thumb: true, ct));
    }

    private static async Task<IResult> ListBooks(KnovaultDbContext db, string? search, int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Books.Include(b => b.Copies).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Title.Contains(search));
        var total = await query.CountAsync();
        var books = await query.OrderBy(b => b.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Results.Ok(new PagedResult<BookSummaryDto>(books.Select(b => b.ToSummaryDto()).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetBook(KnovaultDbContext db, Guid id)
    {
        var book = await db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == id);
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

    private static async Task<IResult> UpdateBook(KnovaultDbContext db, Guid id, UpdateBookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.Problem(title: "書名為必填", statusCode: StatusCodes.Status400BadRequest);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        book.SetAuthors(req.Authors);
        book.UpdateMetadata(req.Title, req.Subtitle, req.Language, req.Publisher, req.PublishedDate, req.Description, req.Isbn);
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> UpdateReading(KnovaultDbContext db, Guid id, UpdateReadingRequest req)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        if (!Enum.TryParse<ReadingStatus>(req.ReadingStatus, out var status))
            return Results.Problem(title: "閱讀狀態無效", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            book.SetReadingStatus(status);
            book.SetProgress(ReadingProgress.Create(req.Percent, req.CurrentPage, req.TotalPages));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> DeleteBook(KnovaultDbContext db, Guid id)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        db.Books.Remove(book); // copies 由 cascade 刪除；硬碟書檔與封面檔不動
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ServeCover(Guid id, KnovaultDbContext db, AppPaths paths, bool thumb, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book?.CoverPath is null) return Results.NotFound();
        var file = thumb
            ? Path.Combine(paths.CoversDir, $"{id:N}_thumb.jpg")
            : Path.Combine(paths.CoversDir, book.CoverPath);
        if (!File.Exists(file)) return Results.NotFound();
        var contentType = thumb ? "image/jpeg" : "application/octet-stream";
        return Results.File(file, contentType);
    }
}
```

- [ ] **Step 6: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Api.Tests --filter BookEditEndpointsTests`
Expected: PASS（3 tests）。

- [ ] **Step 7: Commit**
```bash
git add -A
git commit -m "加入書籍編輯/刪除/閱讀狀態/封面端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: 版本 Copy 端點（新增/更新/刪除/下載）

**Files:** Create `Contracts/AddPhysicalCopyRequest.cs`, `UpdateCopyRequest.cs`, `Endpoints/CopyEndpoints.cs`; modify `Program.cs`; Test `CopyEndpointsTests.cs`

- [ ] **Step 1: 建立 `AddPhysicalCopyRequest.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record AddPhysicalCopyRequest
{
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
```

- [ ] **Step 2: 建立 `UpdateCopyRequest.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record UpdateCopyRequest
{
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
```

- [ ] **Step 3: 寫整合測試 `CopyEndpointsTests.cs`**
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class CopyEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public CopyEndpointsTests(TestApiFactory factory) => _factory = factory;

    private async Task<BookDetailDto> CreateBookAsync(System.Net.Http.HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "版本書", Location = "A" }))
            .Content.ReadFromJsonAsync<BookDetailDto>())!;

    [Fact]
    public async Task Add_physical_copy_to_existing_book()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client);

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/copies",
            new AddPhysicalCopyRequest { Location = "B 櫃" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.Copies.Count(c => c.Type == "physical").Should().Be(2);
    }

    [Fact]
    public async Task Update_and_delete_copy()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client);
        var copyId = book.Copies.Single(c => c.Type == "physical").Id;

        var put = await client.PutAsJsonAsync($"/api/copies/{copyId}",
            new UpdateCopyRequest { Location = "新位置" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterPut = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterPut!.Copies.Single().Location.Should().Be("新位置");

        var del = await client.DeleteAsync($"/api/copies/{copyId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDel = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterDel!.Copies.Should().BeEmpty();
    }
}
```

- [ ] **Step 4: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Api.Tests --filter CopyEndpointsTests`
Expected: 失敗。

- [ ] **Step 5: 建立 `Endpoints/CopyEndpoints.cs`**
```csharp
using Knovault.Api.Contracts;
using Knovault.Api.Mapping;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class CopyEndpoints
{
    public static void MapCopyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/books/{bookId:guid}/copies", AddPhysicalCopy);
        app.MapPut("/api/copies/{copyId:guid}", UpdateCopy);
        app.MapDelete("/api/copies/{copyId:guid}", DeleteCopy);
        app.MapGet("/api/copies/{copyId:guid}/file", DownloadFile);
    }

    private static async Task<IResult> AddPhysicalCopy(KnovaultDbContext db, Guid bookId, AddPhysicalCopyRequest req)
    {
        var book = await db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == bookId);
        if (book is null) return Results.NotFound();
        var copy = new PhysicalCopy(req.Location);
        if (!string.IsNullOrWhiteSpace(req.Notes)) copy.SetNotes(req.Notes);
        book.AddCopy(copy);
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> UpdateCopy(KnovaultDbContext db, Guid copyId, UpdateCopyRequest req)
    {
        var copy = await db.Set<BookCopy>().FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null) return Results.NotFound();
        if (copy is PhysicalCopy p) p.UpdateLocation(req.Location);
        copy.SetNotes(req.Notes);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteCopy(KnovaultDbContext db, Guid copyId)
    {
        var copy = await db.Set<BookCopy>().FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null) return Results.NotFound();
        db.Remove(copy);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadFile(KnovaultDbContext db, Guid copyId, CancellationToken ct)
    {
        var copy = await db.Set<DigitalCopy>().FirstOrDefaultAsync(c => c.Id == copyId, ct);
        if (copy is null) return Results.NotFound();
        if (!File.Exists(copy.FilePath)) return Results.NotFound();
        var name = Path.GetFileName(copy.FilePath);
        return Results.File(copy.FilePath, "application/octet-stream", name);
    }
}
```

- [ ] **Step 6: 在 `Program.cs` 註冊**：於 `app.MapBookEndpoints();` 後加 `app.MapCopyEndpoints();`（並確保 `using Knovault.Api.Endpoints;` 已存在）。

- [ ] **Step 7: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Api.Tests --filter CopyEndpointsTests`
Expected: PASS（2 tests）。

- [ ] **Step 8: Commit**
```bash
git add -A
git commit -m "加入版本 copy 端點（新增/更新/刪除/下載）" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: 標籤端點

**Files:** Create `Contracts/TagDto.cs`, `CreateTagRequest.cs`, `Endpoints/TagEndpoints.cs`; modify `Program.cs`; Test `TagEndpointsTests.cs`

- [ ] **Step 1: 建立 `TagDto.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record TagDto(Guid Id, string Name, string? Color, int BookCount);
```

- [ ] **Step 2: 建立 `CreateTagRequest.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record CreateTagRequest
{
    public string Name { get; init; } = "";
    public string? Color { get; init; }
}
```

- [ ] **Step 3: 寫整合測試 `TagEndpointsTests.cs`**
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class TagEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public TagEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_list_assign_and_unassign_tag()
    {
        var client = _factory.CreateClient();

        var tagResp = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "哲學" });
        tagResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = (await tagResp.Content.ReadFromJsonAsync<TagDto>())!;

        var bookResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = "標籤書" });
        var book = (await bookResp.Content.ReadFromJsonAsync<BookDetailDto>())!;

        (await client.PostAsync($"/api/books/{book.Id}/tags/{tag.Id}", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterAssign = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterAssign!.Tags.Should().ContainSingle().Which.Should().Be("哲學");

        var tags = await client.GetFromJsonAsync<List<TagDto>>("/api/tags");
        tags!.Single(t => t.Id == tag.Id).BookCount.Should().Be(1);

        (await client.DeleteAsync($"/api/books/{book.Id}/tags/{tag.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}"))!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_tag_name_returns_409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "重複" });
        (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "重複" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 4: 跑測試確認失敗** — `dotnet test tests/Knovault.Api.Tests --filter TagEndpointsTests`，預期失敗。

- [ ] **Step 5: 建立 `Endpoints/TagEndpoints.cs`**
```csharp
using Knovault.Api.Contracts;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", ListTags);
        app.MapPost("/api/tags", CreateTag);
        app.MapDelete("/api/tags/{id:guid}", DeleteTag);
        app.MapPost("/api/books/{bookId:guid}/tags/{tagId:guid}", AssignTag);
        app.MapDelete("/api/books/{bookId:guid}/tags/{tagId:guid}", UnassignTag);
    }

    private static async Task<IResult> ListTags(KnovaultDbContext db)
    {
        var tags = await db.Tags.ToListAsync();
        var counts = await db.Books
            .SelectMany(b => b.Tags, (b, t) => t.Id)
            .GroupBy(id => id)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToListAsync();
        var map = counts.ToDictionary(c => c.TagId, c => c.Count);
        var dtos = tags.Select(t => new TagDto(t.Id, t.Name, t.Color, map.GetValueOrDefault(t.Id))).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> CreateTag(KnovaultDbContext db, CreateTagRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.Problem(title: "標籤名為必填", statusCode: StatusCodes.Status400BadRequest);
        if (await db.Tags.AnyAsync(t => t.Name == req.Name.Trim()))
            return Results.Conflict(new { message = "標籤已存在" });
        var tag = new Tag(req.Name, req.Color);
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return Results.Created($"/api/tags/{tag.Id}", new TagDto(tag.Id, tag.Name, tag.Color, 0));
    }

    private static async Task<IResult> DeleteTag(KnovaultDbContext db, Guid id)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null) return Results.NotFound();
        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AssignTag(KnovaultDbContext db, Guid bookId, Guid tagId)
    {
        var book = await db.Books.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == bookId);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId);
        if (book is null || tag is null) return Results.NotFound();
        book.AddTag(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> UnassignTag(KnovaultDbContext db, Guid bookId, Guid tagId)
    {
        var book = await db.Books.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == bookId);
        var tag = book?.Tags.FirstOrDefault(t => t.Id == tagId);
        if (book is null || tag is null) return Results.NotFound();
        book.RemoveTag(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
```

- [ ] **Step 6: 在 `Program.cs` 註冊** `app.MapTagEndpoints();`

- [ ] **Step 7: 跑測試確認通過** — 預期 PASS（2 tests）。

- [ ] **Step 8: Commit**
```bash
git add -A
git commit -m "加入標籤端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: 作者瀏覽 + 書庫資料夾 + 掃描觸發

**Files:** Create `Contracts/AuthorFacetDto.cs`, `FolderDto.cs`, `CreateFolderRequest.cs`, `ScanReportDto.cs`, `Endpoints/LibraryEndpoints.cs`; modify `Program.cs`; Test `LibraryEndpointsTests.cs`

- [ ] **Step 1: 建立 DTOs**

`Contracts/AuthorFacetDto.cs`:
```csharp
namespace Knovault.Api.Contracts;
public sealed record AuthorFacetDto(string Name, int BookCount);
```
`Contracts/FolderDto.cs`:
```csharp
namespace Knovault.Api.Contracts;
public sealed record FolderDto(Guid Id, string Path, string? DisplayName, bool Enabled, DateTimeOffset? LastScannedAt);
```
`Contracts/CreateFolderRequest.cs`:
```csharp
namespace Knovault.Api.Contracts;
public sealed record CreateFolderRequest { public string Path { get; init; } = ""; public string? DisplayName { get; init; } }
```
`Contracts/ScanReportDto.cs`:
```csharp
namespace Knovault.Api.Contracts;
public sealed record ScanReportDto(int Added, int Updated, int Skipped, int MarkedMissing, IReadOnlyList<string> Failures);
```

- [ ] **Step 2: 寫整合測試 `LibraryEndpointsTests.cs`**
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class LibraryEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public LibraryEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Add_list_and_delete_folder()
    {
        var client = _factory.CreateClient();
        var dir = Path.Combine(Path.GetTempPath(), $"libfolder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var add = await client.PostAsJsonAsync("/api/library/folders",
                new CreateFolderRequest { Path = dir, DisplayName = "測試" });
            add.StatusCode.Should().Be(HttpStatusCode.Created);
            var folder = (await add.Content.ReadFromJsonAsync<FolderDto>())!;

            var list = await client.GetFromJsonAsync<List<FolderDto>>("/api/library/folders");
            list!.Should().ContainSingle(f => f.Id == folder.Id);

            (await client.DeleteAsync($"/api/library/folders/{folder.Id}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Scan_returns_report()
    {
        var client = _factory.CreateClient();
        var report = await client.PostAsync("/api/library/scan", null);
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await report.Content.ReadFromJsonAsync<ScanReportDto>();
        dto.Should().NotBeNull();
    }

    [Fact]
    public async Task Authors_facet_lists_counts()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "甲", Authors = new() { "作者X" } });
        await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "乙", Authors = new() { "作者X" } });

        var authors = await client.GetFromJsonAsync<List<AuthorFacetDto>>("/api/authors");
        authors!.Single(a => a.Name == "作者X").BookCount.Should().Be(2);
    }
}
```

- [ ] **Step 3: 跑測試確認失敗** — `dotnet test tests/Knovault.Api.Tests --filter LibraryEndpointsTests`，預期失敗。

- [ ] **Step 4: 建立 `Endpoints/LibraryEndpoints.cs`**
```csharp
using Knovault.Api.Contracts;
using Knovault.Application.Library;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class LibraryEndpoints
{
    public static void MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/authors", ListAuthors);
        app.MapGet("/api/library/folders", ListFolders);
        app.MapPost("/api/library/folders", AddFolder);
        app.MapDelete("/api/library/folders/{id:guid}", DeleteFolder);
        app.MapPost("/api/library/scan", Scan);
    }

    private static async Task<IResult> ListAuthors(KnovaultDbContext db)
    {
        // client-side 分組（個人規模可接受）
        var books = await db.Books.ToListAsync();
        var facets = books
            .SelectMany(b => b.Authors.Select(a => a.Name))
            .GroupBy(n => n)
            .Select(g => new AuthorFacetDto(g.Key, g.Count()))
            .OrderBy(a => a.Name)
            .ToList();
        return Results.Ok(facets);
    }

    private static async Task<IResult> ListFolders(KnovaultDbContext db)
    {
        var folders = await db.LibraryFolders.OrderBy(f => f.Path).ToListAsync();
        return Results.Ok(folders.Select(f => new FolderDto(f.Id, f.Path, f.DisplayName, f.Enabled, f.LastScannedAt)).ToList());
    }

    private static async Task<IResult> AddFolder(KnovaultDbContext db, CreateFolderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
            return Results.Problem(title: "路徑為必填", statusCode: StatusCodes.Status400BadRequest);
        if (!Directory.Exists(req.Path))
            return Results.Problem(title: "資料夾不存在", statusCode: StatusCodes.Status400BadRequest);
        if (await db.LibraryFolders.AnyAsync(f => f.Path == req.Path))
            return Results.Conflict(new { message = "資料夾已存在" });
        var folder = new LibraryFolder(req.Path, req.DisplayName);
        db.LibraryFolders.Add(folder);
        await db.SaveChangesAsync();
        return Results.Created($"/api/library/folders/{folder.Id}",
            new FolderDto(folder.Id, folder.Path, folder.DisplayName, folder.Enabled, folder.LastScannedAt));
    }

    private static async Task<IResult> DeleteFolder(KnovaultDbContext db, Guid id)
    {
        var folder = await db.LibraryFolders.FirstOrDefaultAsync(f => f.Id == id);
        if (folder is null) return Results.NotFound();
        // 預設保留書，把該資料夾的數位版本標遺失
        var copies = await db.Set<DigitalCopy>().Where(c => c.LibraryFolderId == id && !c.IsMissing).ToListAsync();
        foreach (var c in copies) c.MarkMissing();
        db.LibraryFolders.Remove(folder);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Scan(ILibraryScanService scanner, CancellationToken ct)
    {
        var report = await scanner.ScanAsync(ct);
        var dto = new ScanReportDto(report.Added, report.Updated, report.Skipped, report.MarkedMissing,
            report.Failures.Select(f => $"{f.FilePath}: {f.Reason}").ToList());
        return Results.Ok(dto);
    }
}
```

- [ ] **Step 5: 在 `Program.cs` 註冊** `app.MapLibraryEndpoints();`

- [ ] **Step 6: 跑測試確認通過** — 預期 PASS（3 tests）。

- [ ] **Step 7: Commit**
```bash
git add -A
git commit -m "加入作者/書庫資料夾/掃描端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: ISBN 查詢（OpenLibrary）

**Files:** Create `src/Knovault.Application/Metadata/IIsbnMetadataProvider.cs`, `src/Knovault.Infrastructure/Metadata/OpenLibraryIsbnProvider.cs`, `Contracts/IsbnMetadataDto.cs`, `Endpoints/MetadataEndpoints.cs`; modify `Program.cs`; Test `OpenLibraryIsbnProviderTests.cs`

- [ ] **Step 1: 建立 `IIsbnMetadataProvider.cs`**
```csharp
using Knovault.Application.Parsing;

namespace Knovault.Application.Metadata;

public interface IIsbnMetadataProvider
{
    /// <summary>以 ISBN 查詢書目；查無/失敗回 null。</summary>
    Task<ParsedBookMetadata?> LookupAsync(string isbn, CancellationToken ct = default);
}
```

- [ ] **Step 2: 建立 `OpenLibraryIsbnProvider.cs`**
```csharp
using System.Text.Json;
using Knovault.Application.Metadata;
using Knovault.Application.Parsing;

namespace Knovault.Infrastructure.Metadata;

public sealed class OpenLibraryIsbnProvider : IIsbnMetadataProvider
{
    private readonly HttpClient _http;
    public OpenLibraryIsbnProvider(HttpClient http) => _http = http;

    public async Task<ParsedBookMetadata?> LookupAsync(string isbn, CancellationToken ct = default)
    {
        var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn)}&format=json&jscmd=data";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty($"ISBN:{isbn}", out var book)) return null;

        var authors = book.TryGetProperty("authors", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                .Where(s => s.Length > 0).ToList()
            : new List<string>();

        string? publisher = book.TryGetProperty("publishers", out var p) &&
                            p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0 &&
                            p[0].TryGetProperty("name", out var pn) ? pn.GetString() : null;

        int? pages = book.TryGetProperty("number_of_pages", out var np) && np.TryGetInt32(out var pc) ? pc : null;

        return new ParsedBookMetadata
        {
            Title = book.TryGetProperty("title", out var t) ? t.GetString() : null,
            Authors = authors,
            Publisher = publisher,
            PublishedDate = book.TryGetProperty("publish_date", out var d) ? d.GetString() : null,
            Isbn = isbn,
            PageCount = pages
        };
    }
}
```

- [ ] **Step 3: 寫單元測試 `OpenLibraryIsbnProviderTests.cs`（stub HTTP）**

Create `tests/Knovault.Infrastructure.Tests/OpenLibraryIsbnProviderTests.cs`:
```csharp
using System.Net;
using System.Text;
using FluentAssertions;
using Knovault.Infrastructure.Metadata;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class OpenLibraryIsbnProviderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public StubHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Lookup_parses_openlibrary_response()
    {
        const string isbn = "9780321125217";
        var json = $$"""
            {
              "ISBN:{{isbn}}": {
                "title": "Domain-Driven Design",
                "authors": [{ "name": "Eric Evans" }],
                "publishers": [{ "name": "Addison-Wesley" }],
                "publish_date": "2003",
                "number_of_pages": 560
              }
            }
            """;
        var provider = new OpenLibraryIsbnProvider(new HttpClient(new StubHandler(json)));

        var meta = await provider.LookupAsync(isbn);

        meta.Should().NotBeNull();
        meta!.Title.Should().Be("Domain-Driven Design");
        meta.Authors.Should().ContainSingle().Which.Should().Be("Eric Evans");
        meta.Publisher.Should().Be("Addison-Wesley");
        meta.PageCount.Should().Be(560);
        meta.Isbn.Should().Be(isbn);
    }

    [Fact]
    public async Task Lookup_returns_null_when_not_found()
    {
        var provider = new OpenLibraryIsbnProvider(new HttpClient(new StubHandler("{}")));
        (await provider.LookupAsync("0000000000")).Should().BeNull();
    }
}
```

- [ ] **Step 4: 跑測試確認失敗** — `dotnet test tests/Knovault.Infrastructure.Tests --filter OpenLibraryIsbnProviderTests`，預期失敗。

- [ ] **Step 5: 建立 `Contracts/IsbnMetadataDto.cs`**
```csharp
namespace Knovault.Api.Contracts;

public sealed record IsbnMetadataDto
{
    public string? Title { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Isbn { get; init; }
    public int? PageCount { get; init; }
}
```

- [ ] **Step 6: 建立 `Endpoints/MetadataEndpoints.cs`**
```csharp
using Knovault.Api.Contracts;
using Knovault.Application.Metadata;

namespace Knovault.Api.Endpoints;

public static class MetadataEndpoints
{
    public static void MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/metadata/isbn/{isbn}", async (IIsbnMetadataProvider provider, string isbn, CancellationToken ct) =>
        {
            var meta = await provider.LookupAsync(isbn, ct);
            if (meta is null) return Results.NotFound();
            return Results.Ok(new IsbnMetadataDto
            {
                Title = meta.Title,
                Authors = meta.Authors,
                Publisher = meta.Publisher,
                PublishedDate = meta.PublishedDate,
                Isbn = meta.Isbn,
                PageCount = meta.PageCount
            });
        });
    }
}
```

- [ ] **Step 7: 在 `Program.cs` 註冊 HttpClient + provider + 端點**

新增：
```csharp
builder.Services.AddHttpClient<IIsbnMetadataProvider, OpenLibraryIsbnProvider>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));
```
（using `Knovault.Application.Metadata;`、`Knovault.Infrastructure.Metadata;`）
並於端點對應區加 `app.MapMetadataEndpoints();`。

- [ ] **Step 8: 跑測試確認通過** — `dotnet test tests/Knovault.Infrastructure.Tests --filter OpenLibraryIsbnProviderTests`，預期 PASS（2 tests）。

- [ ] **Step 9: Commit**
```bash
git add -A
git commit -m "加入 ISBN 查詢（OpenLibrary）" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: 全量驗證

- [ ] **Step 1: 全量測試**
Run: `dotnet test`
Expected: 全綠。Domain 25 + Infrastructure (24 + 2 = 26) + Api (4 + 3 + 2 + 2 + 3 = 14) = 65 passed。

- [ ] **Step 2: 全量建置**
Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: 留在 `feat/library-core-p4`**，不合併不推（等 P4c）。

---

## 完成定義 (Definition of Done)

- 書籍 PUT/PATCH(reading)/DELETE、封面/縮圖服務。
- 版本 copy 新增/更新/刪除/下載。
- 標籤 CRUD + 指派/取消；作者 facet；書庫資料夾 CRUD（刪除時標遺失）；掃描觸發回報告。
- ISBN 查詢（OpenLibrary，HttpClient，10s 逾時）+ 端點，stub HTTP 單元測試。
- `dotnet test` 全綠（約 65）、`dotnet build` 0 警告 0 錯誤。

## 不在本計畫範圍（P4c / P5）

- SSE 掃描進度、找空閒埠、開瀏覽器、託管 Vue 靜態檔（P4c）。
- 前端（P5）。
