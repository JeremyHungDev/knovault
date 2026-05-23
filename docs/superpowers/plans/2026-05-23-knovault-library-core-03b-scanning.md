# Knovault 書庫核心 — P3b 掃描服務實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 做出資料夾掃描服務：走訪書庫資料夾 → 解析（P3a）→ 建立 Book/DigitalCopy 入庫，處理去重/移動/遺失、批次寫入、檔案鎖重試，並儲存封面+縮圖。產出「掃資料夾就長出書目」。

**Architecture:** 掃描服務在 `Infrastructure`，編排 P3a 解析器 + `IFileHasher` + `ICoverStore` + `KnovaultDbContext`。封面/縮圖用 ImageSharp。整合測試用臨時資料夾 + 臨時 SQLite + 程式產生的 fixture。

**Tech Stack:** .NET 8、SixLabors.ImageSharp、EF Core、xUnit + FluentAssertions。

> **執行前置**：續在 `feat/library-core-p3`（P3a 已在此，未 squash/未推）。commit 風格：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。逐 Task 本機 commit；最後一個 Task 把**整個 P3（a+b）squash 成少數 commit → 合併 dev → 推**。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §5.1、§5.6、§8。
> **範圍外（後續獨立小任務）**：PDF 第一頁算繪封面（D10；需 Skia/原生）。本計畫 PDF 暫無封面，由 UI 之後以佔位圖呈現。

---

## 檔案結構（本計畫產出）

```
src/Knovault.Application/
  Library/
    ScanReport.cs
    ILibraryScanService.cs
  Covers/
    ICoverStore.cs
src/Knovault.Infrastructure/
  Covers/CoverStorage.cs            ← ImageSharp 存原圖 + 縮圖
  Parsing/BookParsingService.cs     ← 解析器註冊表 + fallback
  Library/LibraryScanService.cs     ← 掃描編排
tests/Knovault.Infrastructure.Tests/
  CoverStorageTests.cs
  BookParsingServiceTests.cs
  LibraryScanServiceTests.cs        ← 整合（臨時資料夾 + SQLite）
```

---

## Task 1: ImageSharp + CoverStorage（TDD）

**Files:** Create `src/Knovault.Application/Covers/ICoverStore.cs`, `src/Knovault.Infrastructure/Covers/CoverStorage.cs`; Test `CoverStorageTests.cs`

- [ ] **Step 1: 加入 ImageSharp**

Run:
```bash
dotnet add src/Knovault.Infrastructure package SixLabors.ImageSharp --version "3.*"
```

- [ ] **Step 2: 建立 `ICoverStore.cs`**

Create `src/Knovault.Application/Covers/ICoverStore.cs`:
```csharp
namespace Knovault.Application.Covers;

public interface ICoverStore
{
    /// <summary>儲存原圖與縮圖，回傳原圖相對檔名（如 "{bookId}.png"）。</summary>
    Task<string> SaveAsync(Guid bookId, byte[] imageBytes, string? contentType, CancellationToken ct = default);
}
```

- [ ] **Step 3: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/CoverStorageTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Covers;
using SixLabors.ImageSharp;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class CoverStorageTests
{
    // 4x4 紅色 PNG
    private static byte[] SamplePng()
    {
        using var img = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task SaveAsync_writes_cover_and_thumbnail()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covers_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var store = new CoverStorage(dir);
            var bookId = Guid.NewGuid();

            var rel = await store.SaveAsync(bookId, SamplePng(), "image/png");

            rel.Should().Contain(bookId.ToString("N"));
            File.Exists(Path.Combine(dir, rel)).Should().BeTrue();
            // 縮圖檔存在
            var thumb = Path.Combine(dir, $"{bookId:N}_thumb.jpg");
            File.Exists(thumb).Should().BeTrue();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 4: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter CoverStorageTests`
Expected: 編譯失敗（`CoverStorage` 不存在）。

- [ ] **Step 5: 實作 `CoverStorage.cs`**

Create `src/Knovault.Infrastructure/Covers/CoverStorage.cs`:
```csharp
using Knovault.Application.Covers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Knovault.Infrastructure.Covers;

public sealed class CoverStorage : ICoverStore
{
    private const int ThumbnailMaxWidth = 400;
    private readonly string _root;

    public CoverStorage(string coversRootPath)
    {
        _root = coversRootPath;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Guid bookId, byte[] imageBytes, string? contentType, CancellationToken ct = default)
    {
        var ext = ExtensionFor(contentType);
        var coverName = $"{bookId:N}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(_root, coverName), imageBytes, ct);

        using var image = Image.Load(imageBytes);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(ThumbnailMaxWidth, 0)
        }));
        await image.SaveAsJpegAsync(Path.Combine(_root, $"{bookId:N}_thumb.jpg"), ct);

        return coverName;
    }

    private static string ExtensionFor(string? contentType) => contentType switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".png"
    };
}
```

- [ ] **Step 6: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter CoverStorageTests`
Expected: PASS（1 test）。

- [ ] **Step 7: Commit**
```bash
git add -A
git commit -m "加入封面儲存與縮圖" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: BookParsingService（註冊表 + fallback）（TDD）

**Files:** Create `src/Knovault.Infrastructure/Parsing/BookParsingService.cs`; Test `BookParsingServiceTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/BookParsingServiceTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class BookParsingServiceTests
{
    private static BookParsingService NewService() =>
        new(new IBookFileParserList());

    [Fact]
    public async Task Parses_supported_epub()
    {
        var svc = new BookParsingService(new IBookFileParserList());
        var path = EpubFixtureBuilder.CreateMinimalEpub();
        try
        {
            var (meta, failed) = await svc.ParseAsync(path);
            failed.Should().BeFalse();
            meta.Title.Should().Be("測試書名");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Unsupported_extension_falls_back_to_filename()
    {
        var svc = new BookParsingService(new IBookFileParserList());
        var (meta, failed) = await svc.ParseAsync("C:/books/some_unknown_book.mobi");
        failed.Should().BeTrue();
        meta.Title.Should().Be("some unknown book");
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_filename()
    {
        var svc = new BookParsingService(new IBookFileParserList());
        var path = Path.Combine(Path.GetTempPath(), $"broken_{Guid.NewGuid():N}.epub");
        await File.WriteAllTextAsync(path, "not a real zip");
        try
        {
            var (meta, failed) = await svc.ParseAsync(path);
            failed.Should().BeTrue();
            meta.Title.Should().StartWith("broken");
        }
        finally { File.Delete(path); }
    }
}

// 測試用：實際的解析器清單
internal sealed class IBookFileParserList : List<Knovault.Application.Parsing.IBookFileParser>
{
    public IBookFileParserList()
    {
        Add(new EpubMetadataParser());
        Add(new PdfMetadataParser());
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter BookParsingServiceTests`
Expected: 編譯失敗（`BookParsingService` 不存在）。

- [ ] **Step 3: 實作 `BookParsingService.cs`**

Create `src/Knovault.Infrastructure/Parsing/BookParsingService.cs`:
```csharp
using Knovault.Application.Parsing;
using Knovault.Infrastructure.Files;

namespace Knovault.Infrastructure.Parsing;

public sealed class BookParsingService
{
    private readonly IReadOnlyList<IBookFileParser> _parsers;

    public BookParsingService(IEnumerable<IBookFileParser> parsers) => _parsers = parsers.ToList();

    public bool IsSupported(string path) => _parsers.Any(p => p.CanParse(path));

    /// <summary>解析並套用 fallback；回傳 (元數據, 是否解析失敗)。</summary>
    public async Task<(ParsedBookMetadata Metadata, bool Failed)> ParseAsync(string path, CancellationToken ct = default)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(path));
        if (parser is null)
            return (new ParsedBookMetadata { Title = FilenameTitleCleaner.Clean(path) }, true);

        try
        {
            var meta = await parser.ParseAsync(path, ct);
            if (string.IsNullOrWhiteSpace(meta.Title))
                meta = meta with { Title = FilenameTitleCleaner.Clean(path) };
            return (meta, false);
        }
        catch
        {
            return (new ParsedBookMetadata { Title = FilenameTitleCleaner.Clean(path) }, true);
        }
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter BookParsingServiceTests`
Expected: PASS（3 tests）。

- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "加入解析註冊表與 fallback" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: 掃描服務型別與實作

**Files:** Create `src/Knovault.Application/Library/ScanReport.cs`, `ILibraryScanService.cs`, `src/Knovault.Infrastructure/Library/LibraryScanService.cs`

- [ ] **Step 1: 建立 `ScanReport.cs`**

Create `src/Knovault.Application/Library/ScanReport.cs`:
```csharp
namespace Knovault.Application.Library;

public sealed record ScanFailure(string FilePath, string Reason);

public sealed class ScanReport
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int MarkedMissing { get; set; }
    public List<ScanFailure> Failures { get; } = new();
}
```

- [ ] **Step 2: 建立 `ILibraryScanService.cs`**

Create `src/Knovault.Application/Library/ILibraryScanService.cs`:
```csharp
namespace Knovault.Application.Library;

public interface ILibraryScanService
{
    /// <summary>掃描所有啟用的書庫資料夾。</summary>
    Task<ScanReport> ScanAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: 實作 `LibraryScanService.cs`**

Create `src/Knovault.Infrastructure/Library/LibraryScanService.cs`:
```csharp
using System.Text.Json;
using Knovault.Application.Covers;
using Knovault.Application.Files;
using Knovault.Application.Library;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Library;

public sealed class LibraryScanService : ILibraryScanService
{
    private static readonly string[] SupportedExtensions = { ".epub", ".pdf" };
    private const int BatchSize = 20;

    private readonly KnovaultDbContext _db;
    private readonly BookParsingService _parser;
    private readonly IFileHasher _hasher;
    private readonly ICoverStore _coverStore;

    public LibraryScanService(KnovaultDbContext db, BookParsingService parser,
        IFileHasher hasher, ICoverStore coverStore)
    {
        _db = db;
        _parser = parser;
        _hasher = hasher;
        _coverStore = coverStore;
    }

    public async Task<ScanReport> ScanAsync(CancellationToken ct = default)
    {
        var report = new ScanReport();
        var folders = await _db.LibraryFolders.Where(f => f.Enabled).ToListAsync(ct);
        var seenCopyIds = new HashSet<Guid>();
        var sinceLastSave = 0;

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path))
            {
                report.Failures.Add(new ScanFailure(folder.Path, "資料夾無法存取"));
                continue;
            }

            foreach (var file in EnumerateBookFiles(folder.Path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var hash = await ComputeHashWithRetryAsync(file, ct);
                    var existing = await _db.Set<DigitalCopy>().FirstOrDefaultAsync(c => c.FileHash == hash, ct);

                    if (existing is not null)
                    {
                        seenCopyIds.Add(existing.Id);
                        if (existing.FilePath != file) { existing.UpdatePath(file); report.Updated++; }
                        else report.Skipped++;
                        continue;
                    }

                    var copy = await CreateBookFromFileAsync(file, hash, folder.Id, ct);
                    seenCopyIds.Add(copy.Id);
                    report.Added++;

                    if (++sinceLastSave >= BatchSize) { await _db.SaveChangesAsync(ct); sinceLastSave = 0; }
                }
                catch (IOException)
                {
                    report.Failures.Add(new ScanFailure(file, "檔案使用中"));
                }
            }

            folder.MarkScanned();
        }

        await _db.SaveChangesAsync(ct);

        // 遺失偵測：屬於啟用資料夾、未在本次掃描見到、且尚未標記遺失的數位版本
        var folderIds = folders.Select(f => f.Id).ToList();
        var tracked = await _db.Set<DigitalCopy>()
            .Where(c => c.LibraryFolderId != null && folderIds.Contains(c.LibraryFolderId.Value) && !c.IsMissing)
            .ToListAsync(ct);
        foreach (var copy in tracked.Where(c => !seenCopyIds.Contains(c.Id)))
        {
            copy.MarkMissing();
            report.MarkedMissing++;
        }
        await _db.SaveChangesAsync(ct);

        return report;
    }

    private static IEnumerable<string> EnumerateBookFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

    private async Task<string> ComputeHashWithRetryAsync(string file, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await _hasher.ComputeQuickHashAsync(file, ct); }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(500, ct);
            }
        }
    }

    private async Task<DigitalCopy> CreateBookFromFileAsync(string file, string hash, Guid folderId, CancellationToken ct)
    {
        var (meta, failed) = await _parser.ParseAsync(file, ct);
        var info = new FileInfo(file);
        var format = string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)
            ? BookFormat.Pdf : BookFormat.Epub;

        var title = string.IsNullOrWhiteSpace(meta.Title) ? Path.GetFileNameWithoutExtension(file) : meta.Title!;
        var book = new Book(title);
        book.SetAuthors(meta.Authors);
        book.UpdateMetadata(title, null, meta.Language, meta.Publisher, meta.PublishedDate, meta.Description, meta.Isbn);
        if (meta.PageCount is int pages)
            book.SetProgress(ReadingProgress.Create(totalPages: pages));

        var copy = new DigitalCopy(file, format, info.Length, hash,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), folderId);
        if (failed) copy.MarkParseFailed();
        if (meta.Toc.Count > 0) copy.SetToc(JsonSerializer.Serialize(meta.Toc));
        book.AddCopy(copy);

        if (meta.CoverImage is { Length: > 0 })
        {
            var coverPath = await _coverStore.SaveAsync(book.Id, meta.CoverImage, meta.CoverContentType, ct);
            book.SetCoverPath(coverPath);
        }

        _db.Books.Add(book);
        return copy;
    }
}
```

- [ ] **Step 4: 建置 + Commit**

Run: `dotnet build src/Knovault.Infrastructure`
Expected: `Build succeeded`
```bash
git add -A
git commit -m "加入書庫掃描服務" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: 掃描整合測試

**Files:** Test `tests/Knovault.Infrastructure.Tests/LibraryScanServiceTests.cs`

- [ ] **Step 1: 寫整合測試**

Create `tests/Knovault.Infrastructure.Tests/LibraryScanServiceTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Application.Parsing;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Files;
using Knovault.Infrastructure.Library;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class LibraryScanServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly string _libraryDir = Path.Combine(Path.GetTempPath(), $"lib_{Guid.NewGuid():N}");
    private readonly string _coversDir = Path.Combine(Path.GetTempPath(), $"covers_{Guid.NewGuid():N}");

    public LibraryScanServiceTests()
    {
        Directory.CreateDirectory(_libraryDir);
        Directory.CreateDirectory(_coversDir);
    }

    private LibraryScanService NewService(KnovaultDbContextProvider ctx) =>
        new(ctx.Context,
            new BookParsingService(new IBookFileParser[] { new EpubMetadataParser(), new PdfMetadataParser() }),
            new FileHasher(),
            new CoverStorage(_coversDir));

    private sealed class KnovaultDbContextProvider
    {
        public required Knovault.Infrastructure.Persistence.KnovaultDbContext Context { get; init; }
    }

    private async Task<Guid> AddFolderAsync()
    {
        await using var ctx = _db.NewContext();
        var folder = new LibraryFolder(_libraryDir, "測試書庫");
        ctx.LibraryFolders.Add(folder);
        await ctx.SaveChangesAsync();
        return folder.Id;
    }

    private static string PlaceEpub(string dir, string fileName)
    {
        var tmp = EpubFixtureBuilder.CreateMinimalEpub();
        var dest = Path.Combine(dir, fileName);
        File.Move(tmp, dest, overwrite: true);
        return dest;
    }

    [Fact]
    public async Task Scan_creates_book_with_metadata_copy_and_cover()
    {
        await AddFolderAsync();
        PlaceEpub(_libraryDir, "book1.epub");

        await using (var ctx = _db.NewContext())
        {
            var svc = NewService(new KnovaultDbContextProvider { Context = ctx });
            var report = await svc.ScanAsync();
            report.Added.Should().Be(1);
        }

        await using (var ctx = _db.NewContext())
        {
            var book = await ctx.Books.Include(b => b.Copies).SingleAsync();
            book.Title.Should().Be("測試書名");
            book.HasDigital.Should().BeTrue();
            book.CoverPath.Should().NotBeNull();
            File.Exists(Path.Combine(_coversDir, book.CoverPath!)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Rescan_does_not_duplicate()
    {
        await AddFolderAsync();
        PlaceEpub(_libraryDir, "book1.epub");

        await using (var ctx = _db.NewContext())
            await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();
        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();
            report.Added.Should().Be(0);
            report.Skipped.Should().Be(1);
        }

        await using (var verify = _db.NewContext())
            (await verify.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Moved_file_updates_path()
    {
        await AddFolderAsync();
        var original = PlaceEpub(_libraryDir, "book1.epub");

        await using (var ctx = _db.NewContext())
            await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();

        var moved = Path.Combine(_libraryDir, "renamed.epub");
        File.Move(original, moved);

        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();
            report.Updated.Should().Be(1);
        }

        await using (var verify = _db.NewContext())
        {
            var copy = await verify.Set<DigitalCopy>().SingleAsync();
            copy.FilePath.Should().Be(moved);
            copy.IsMissing.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Deleted_file_is_marked_missing()
    {
        await AddFolderAsync();
        var path = PlaceEpub(_libraryDir, "book1.epub");

        await using (var ctx = _db.NewContext())
            await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();

        File.Delete(path);

        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(new KnovaultDbContextProvider { Context = ctx }).ScanAsync();
            report.MarkedMissing.Should().Be(1);
        }

        await using (var verify = _db.NewContext())
            (await verify.Set<DigitalCopy>().SingleAsync()).IsMissing.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_libraryDir)) Directory.Delete(_libraryDir, true);
        if (Directory.Exists(_coversDir)) Directory.Delete(_coversDir, true);
    }
}
```

- [ ] **Step 2: 跑整合測試**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter LibraryScanServiceTests`
Expected: PASS（4 tests）。若失敗依實際錯誤修正服務或測試。

- [ ] **Step 3: Commit**
```bash
git add -A
git commit -m "加入掃描整合測試" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: 全量驗證 + 整個 P3 squash + 合併 dev + 推

- [ ] **Step 1: 全量測試**
Run: `dotnet test`
Expected: Domain 25 + Infrastructure (16 + 1 + 3 + 4 = 24) = 49 passed。

- [ ] **Step 2: 全量建置**
Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: 把整個 P3（a+b）squash 成 2 個 commit（計畫 + 實作）**

於 `feat/library-core-p3`：
```bash
git reset --soft dev
git restore --staged .
git add docs/superpowers/plans/2026-05-23-knovault-library-core-03a-parsers.md docs/superpowers/plans/2026-05-23-knovault-library-core-03b-scanning.md
git commit -m "加入 P3 解析與掃描計畫" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
git add -A
git commit -m "實作 P3 解析與掃描：EPUB/PDF 解析、雜湊、掃描服務、封面縮圖" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

- [ ] **Step 4: 合併回 dev、刪分支、推**
```bash
git checkout dev
git merge feat/library-core-p3
dotnet test
git branch -d feat/library-core-p3
git push origin dev
```

---

## 完成定義 (Definition of Done)

- `CoverStorage`（原圖 + ImageSharp 縮圖）、`BookParsingService`（註冊表 + fallback）、`LibraryScanService`（去重/移動/遺失、批次寫入、檔案鎖重試、封面儲存）完成。
- 整合測試以臨時資料夾 + 臨時 SQLite 驗證：新增建書、重掃不重複、移動更新路徑、刪除標遺失。
- `dotnet test` 全綠（49）、`dotnet build` 0 警告 0 錯誤。
- 整個 P3 squash 後合併 `dev` 並推遠端。

## 範圍外（後續）

- PDF 第一頁算繪封面（D10；Skia/原生）。
- API 端點、SSE 進度、ISBN 查詢、主程式（P4）。前端（P5）。
