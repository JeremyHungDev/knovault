# Knovault 書庫核心 — P1 地基（Scaffolding + 領域模型）實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 Knovault 的 .NET 四層方案骨架，並用 TDD 完成書庫核心的領域模型（Book ↔ BookCopy、Tag、LibraryFolder、ReadingProgress）。

**Architecture:** 模組化單體、四層（Domain/Application/Infrastructure/Api）。本計畫只觸及 `Domain` 與其測試專案，並把整個方案骨架建好（後續計畫填 Application/Infrastructure/Api）。領域層**零外部相依**，純 C#。

**Tech Stack:** .NET 8 (LTS)、C#、xUnit、FluentAssertions（見下方授權註記）。

> **執行前置**：本計畫在 `dev` 之上的功能分支 `feat/library-core` 進行（Task 0 建立）。
> **設計依據**：[書庫核心設計 spec](../specs/2026-05-23-knovault-library-core-design.md) §4 領域模型、§10 專案結構、§9 測試策略。

---

## 檔案結構（本計畫產出）

```
Knovault.sln
Directory.Build.props                      ← 共用 TargetFramework/Nullable
src/
  Knovault.Domain/
    Enums/BookFormat.cs
    Enums/ReadingStatus.cs
    ValueObjects/ReadingProgress.cs
    Entities/Tag.cs
    Entities/BookAuthor.cs
    Entities/BookCopy.cs                    ← abstract 基底
    Entities/DigitalCopy.cs                 ← : BookCopy
    Entities/PhysicalCopy.cs                ← : BookCopy
    Entities/Book.cs                        ← 聚合根
    Entities/LibraryFolder.cs
  Knovault.Application/                     ← 本計畫僅建空專案
  Knovault.Infrastructure/                 ← 本計畫僅建空專案
  Knovault.Api/                            ← 本計畫僅建空專案（web）
tests/
  Knovault.Domain.Tests/
    ReadingProgressTests.cs
    TagTests.cs
    BookCopyTests.cs
    BookTests.cs
    LibraryFolderTests.cs
  Knovault.Infrastructure.Tests/           ← 本計畫僅建空專案
  Knovault.Api.Tests/                      ← 本計畫僅建空專案
```

每個領域單元一個檔、職責單一；測試與被測單元一一對應。

> **FluentAssertions 授權註記**：FluentAssertions v8+ 改為商業授權。本專案為 MIT 開源，請**鎖定最新 7.x（Apache-2.0，免費）**：套件版本用 `7.*`。若不想用浮動版本，可改用免費的 `Shouldly` 或 `AwesomeAssertions`（FA7 的免費 fork），語法近似，只需替換 `using` 與斷言寫法。

---

## Task 0: 建立功能分支

- [ ] **Step 1: 從 dev 建立並切換到 feat 分支**

Run:
```bash
git switch dev
git pull --ff-only
git switch -c feat/library-core
```
Expected: `Switched to a new branch 'feat/library-core'`

---

## Task 1: 方案與專案骨架

**Files:**
- Create: `Knovault.sln`, `Directory.Build.props`, 四個 `src/*` 專案、三個 `tests/*` 專案

- [ ] **Step 1: 建立方案與所有專案**

Run（於 repo 根目錄）:
```bash
dotnet new sln -n Knovault

dotnet new classlib -n Knovault.Domain         -o src/Knovault.Domain
dotnet new classlib -n Knovault.Application     -o src/Knovault.Application
dotnet new classlib -n Knovault.Infrastructure  -o src/Knovault.Infrastructure
dotnet new web      -n Knovault.Api             -o src/Knovault.Api

dotnet new xunit -n Knovault.Domain.Tests         -o tests/Knovault.Domain.Tests
dotnet new xunit -n Knovault.Infrastructure.Tests -o tests/Knovault.Infrastructure.Tests
dotnet new xunit -n Knovault.Api.Tests            -o tests/Knovault.Api.Tests
```

- [ ] **Step 2: 刪除範本佔位檔**

Run:
```bash
rm src/Knovault.Domain/Class1.cs src/Knovault.Application/Class1.cs src/Knovault.Infrastructure/Class1.cs
rm tests/Knovault.Domain.Tests/UnitTest1.cs tests/Knovault.Infrastructure.Tests/UnitTest1.cs tests/Knovault.Api.Tests/UnitTest1.cs
```
（PowerShell 用 `Remove-Item`。）

- [ ] **Step 3: 建立 `Directory.Build.props`（集中共用設定）**

Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```
> 各 `.csproj` 範本已含 `TargetFramework`；可保留（會被同名屬性覆蓋，無妨）或手動移除個別專案的 `<TargetFramework>` 改吃此處。

- [ ] **Step 4: 設定專案參考**

Run:
```bash
dotnet add src/Knovault.Application    reference src/Knovault.Domain
dotnet add src/Knovault.Infrastructure reference src/Knovault.Application src/Knovault.Domain
dotnet add src/Knovault.Api            reference src/Knovault.Application src/Knovault.Infrastructure
dotnet add tests/Knovault.Domain.Tests         reference src/Knovault.Domain
dotnet add tests/Knovault.Infrastructure.Tests reference src/Knovault.Infrastructure
dotnet add tests/Knovault.Api.Tests            reference src/Knovault.Api
```

- [ ] **Step 5: 測試專案加入 FluentAssertions（7.x 免費）**

Run:
```bash
dotnet add tests/Knovault.Domain.Tests package FluentAssertions --version "7.*"
```

- [ ] **Step 6: 把所有專案加進方案**

Run:
```bash
dotnet sln add (Get-ChildItem -Recurse *.csproj).FullName
```
> 上行為 PowerShell 寫法。bash 用：`dotnet sln add $(find . -name '*.csproj')`

- [ ] **Step 7: 建置確認骨架可編譯**

Run: `dotnet build`
Expected: `Build succeeded`，0 error。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: scaffold 4-layer solution + test projects"
```

---

## Task 2: 列舉 BookFormat 與 ReadingStatus

**Files:**
- Create: `src/Knovault.Domain/Enums/BookFormat.cs`, `src/Knovault.Domain/Enums/ReadingStatus.cs`

> 列舉無行為、不需測試；直接建立。

- [ ] **Step 1: 建立 `BookFormat.cs`**

```csharp
namespace Knovault.Domain.Enums;

public enum BookFormat
{
    Epub,
    Pdf
}
```

- [ ] **Step 2: 建立 `ReadingStatus.cs`**

```csharp
namespace Knovault.Domain.Enums;

public enum ReadingStatus
{
    None = 0,
    WantToRead = 1,
    Reading = 2,
    Finished = 3
}
```

- [ ] **Step 3: 建置 + Commit**

Run: `dotnet build src/Knovault.Domain`
Expected: `Build succeeded`
```bash
git add -A && git commit -m "feat(domain): add BookFormat and ReadingStatus enums"
```

---

## Task 3: ReadingProgress 值物件（TDD）

**Files:**
- Create: `src/Knovault.Domain/ValueObjects/ReadingProgress.cs`
- Test: `tests/Knovault.Domain.Tests/ReadingProgressTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Domain.Tests/ReadingProgressTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.ValueObjects;
using Xunit;

namespace Knovault.Domain.Tests;

public class ReadingProgressTests
{
    [Fact]
    public void Empty_has_all_nulls()
    {
        var p = ReadingProgress.Empty;
        p.Percent.Should().BeNull();
        p.CurrentPage.Should().BeNull();
        p.TotalPages.Should().BeNull();
    }

    [Fact]
    public void Create_with_valid_values_succeeds()
    {
        var p = ReadingProgress.Create(percent: 45, currentPage: 90, totalPages: 200);
        p.Percent.Should().Be(45);
        p.CurrentPage.Should().Be(90);
        p.TotalPages.Should().Be(200);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_out_of_range_percent(int percent)
    {
        var act = () => ReadingProgress.Create(percent: percent);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_rejects_current_page_greater_than_total()
    {
        var act = () => ReadingProgress.Create(currentPage: 300, totalPages: 200);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_negative_pages()
    {
        var act = () => ReadingProgress.Create(currentPage: -5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Domain.Tests --filter ReadingProgressTests`
Expected: 編譯失敗（`ReadingProgress` 不存在）。

- [ ] **Step 3: 實作 `ReadingProgress.cs`**

Create `src/Knovault.Domain/ValueObjects/ReadingProgress.cs`:
```csharp
namespace Knovault.Domain.ValueObjects;

public sealed class ReadingProgress
{
    public int? Percent { get; }
    public int? CurrentPage { get; }
    public int? TotalPages { get; }

    private ReadingProgress(int? percent, int? currentPage, int? totalPages)
    {
        Percent = percent;
        CurrentPage = currentPage;
        TotalPages = totalPages;
    }

    public static readonly ReadingProgress Empty = new(null, null, null);

    public static ReadingProgress Create(int? percent = null, int? currentPage = null, int? totalPages = null)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 100.");
        if (currentPage is < 0)
            throw new ArgumentOutOfRangeException(nameof(currentPage), "CurrentPage cannot be negative.");
        if (totalPages is < 0)
            throw new ArgumentOutOfRangeException(nameof(totalPages), "TotalPages cannot be negative.");
        if (currentPage is not null && totalPages is not null && currentPage > totalPages)
            throw new ArgumentException("CurrentPage cannot exceed TotalPages.");

        return new ReadingProgress(percent, currentPage, totalPages);
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Domain.Tests --filter ReadingProgressTests`
Expected: PASS（6 tests）。

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): add ReadingProgress value object with validation"
```

---

## Task 4: Tag 實體（TDD）

**Files:**
- Create: `src/Knovault.Domain/Entities/Tag.cs`
- Test: `tests/Knovault.Domain.Tests/TagTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Domain.Tests/TagTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Xunit;

namespace Knovault.Domain.Tests;

public class TagTests
{
    [Fact]
    public void Constructor_trims_name_and_assigns_id()
    {
        var tag = new Tag("  哲學  ");
        tag.Name.Should().Be("哲學");
        tag.Id.Should().NotBe(Guid.Empty);
        tag.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name)
    {
        var act = () => new Tag(name);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Domain.Tests --filter TagTests`
Expected: 編譯失敗（`Tag` 不存在）。

- [ ] **Step 3: 實作 `Tag.cs`**

```csharp
namespace Knovault.Domain.Entities;

public class Tag
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Color { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tag() { Name = null!; } // EF

    public Tag(string name, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Color = color;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));
        Name = name.Trim();
    }

    public void SetColor(string? color) => Color = color;
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Domain.Tests --filter TagTests`
Expected: PASS（3 tests）。

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): add Tag entity"
```

---

## Task 5: BookAuthor 與 BookCopy 階層（TDD）

**Files:**
- Create: `src/Knovault.Domain/Entities/BookAuthor.cs`, `BookCopy.cs`, `DigitalCopy.cs`, `PhysicalCopy.cs`
- Test: `tests/Knovault.Domain.Tests/BookCopyTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Domain.Tests/BookCopyTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Xunit;

namespace Knovault.Domain.Tests;

public class BookCopyTests
{
    [Fact]
    public void BookAuthor_trims_name_and_keeps_order()
    {
        var a = new BookAuthor(2, "  村上春樹 ");
        a.Order.Should().Be(2);
        a.Name.Should().Be("村上春樹");
    }

    [Fact]
    public void DigitalCopy_stores_file_info_and_sets_scanned()
    {
        var folderId = Guid.NewGuid();
        var copy = new DigitalCopy("C:/books/a.epub", BookFormat.Epub, 1024, "hash123",
            DateTimeOffset.UtcNow, folderId);

        copy.FilePath.Should().Be("C:/books/a.epub");
        copy.Format.Should().Be(BookFormat.Epub);
        copy.FileSizeBytes.Should().Be(1024);
        copy.FileHash.Should().Be("hash123");
        copy.LibraryFolderId.Should().Be(folderId);
        copy.LastScannedAt.Should().NotBeNull();
        copy.IsMissing.Should().BeFalse();
    }

    [Fact]
    public void DigitalCopy_UpdatePath_clears_missing()
    {
        var copy = new DigitalCopy("C:/old.epub", BookFormat.Epub, 1, "h", DateTimeOffset.UtcNow, null);
        copy.MarkMissing();
        copy.IsMissing.Should().BeTrue();

        copy.UpdatePath("C:/new.epub");
        copy.FilePath.Should().Be("C:/new.epub");
        copy.IsMissing.Should().BeFalse();
    }

    [Fact]
    public void DigitalCopy_rejects_blank_path_or_hash()
    {
        var act1 = () => new DigitalCopy("", BookFormat.Pdf, 1, "h", DateTimeOffset.UtcNow, null);
        var act2 = () => new DigitalCopy("C:/a.pdf", BookFormat.Pdf, 1, "", DateTimeOffset.UtcNow, null);
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhysicalCopy_allows_null_location()
    {
        var copy = new PhysicalCopy();
        copy.Location.Should().BeNull();

        copy.UpdateLocation("書房 B 櫃-第3層");
        copy.Location.Should().Be("書房 B 櫃-第3層");
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Domain.Tests --filter BookCopyTests`
Expected: 編譯失敗（型別不存在）。

- [ ] **Step 3: 實作 `BookAuthor.cs`**

```csharp
namespace Knovault.Domain.Entities;

public class BookAuthor
{
    public int Order { get; private set; }
    public string Name { get; private set; }

    private BookAuthor() { Name = null!; } // EF

    public BookAuthor(int order, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Author name is required.", nameof(name));
        Order = order;
        Name = name.Trim();
    }
}
```

- [ ] **Step 4: 實作 `BookCopy.cs`（abstract 基底）**

```csharp
namespace Knovault.Domain.Entities;

public abstract class BookCopy
{
    public Guid Id { get; protected set; }
    public Guid BookId { get; internal set; }
    public DateTimeOffset AddedAt { get; protected set; }
    public string? Notes { get; protected set; }

    protected BookCopy()
    {
        Id = Guid.NewGuid();
        AddedAt = DateTimeOffset.UtcNow;
    }

    public void SetNotes(string? notes) => Notes = notes;
}
```

- [ ] **Step 5: 實作 `DigitalCopy.cs`**

```csharp
using Knovault.Domain.Enums;

namespace Knovault.Domain.Entities;

public class DigitalCopy : BookCopy
{
    public string FilePath { get; private set; }
    public BookFormat Format { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string FileHash { get; private set; }
    public DateTimeOffset FileLastModified { get; private set; }
    public string? TocJson { get; private set; }
    public Guid? LibraryFolderId { get; private set; }
    public DateTimeOffset? LastScannedAt { get; private set; }
    public bool IsMissing { get; private set; }
    public bool ParseFailed { get; private set; }

    private DigitalCopy() { FilePath = null!; FileHash = null!; } // EF

    public DigitalCopy(string filePath, BookFormat format, long fileSizeBytes, string fileHash,
        DateTimeOffset fileLastModified, Guid? libraryFolderId)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("FilePath is required.", nameof(filePath));
        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("FileHash is required.", nameof(fileHash));

        FilePath = filePath;
        Format = format;
        FileSizeBytes = fileSizeBytes;
        FileHash = fileHash;
        FileLastModified = fileLastModified;
        LibraryFolderId = libraryFolderId;
        LastScannedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePath(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentException("FilePath is required.", nameof(newPath));
        FilePath = newPath;
        IsMissing = false;
        LastScannedAt = DateTimeOffset.UtcNow;
    }

    public void MarkMissing() => IsMissing = true;
    public void MarkParseFailed() => ParseFailed = true;
    public void SetToc(string? tocJson) => TocJson = tocJson;
}
```

- [ ] **Step 6: 實作 `PhysicalCopy.cs`**

```csharp
namespace Knovault.Domain.Entities;

public class PhysicalCopy : BookCopy
{
    public string? Location { get; private set; }
    public DateOnly? AcquiredDate { get; private set; }

    private PhysicalCopy() { } // EF (also default public for blank physical copy)

    public PhysicalCopy(string? location = null, DateOnly? acquiredDate = null)
    {
        Location = location;
        AcquiredDate = acquiredDate;
    }

    public void UpdateLocation(string? location) => Location = location;
    public void SetAcquiredDate(DateOnly? date) => AcquiredDate = date;
}
```

- [ ] **Step 7: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Domain.Tests --filter BookCopyTests`
Expected: PASS（5 tests）。

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat(domain): add BookAuthor and BookCopy hierarchy (Digital/Physical)"
```

---

## Task 6: Book 聚合根（TDD）

**Files:**
- Create: `src/Knovault.Domain/Entities/Book.cs`
- Test: `tests/Knovault.Domain.Tests/BookTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Domain.Tests/BookTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
using Xunit;

namespace Knovault.Domain.Tests;

public class BookTests
{
    private static Book NewBook() => new("Clean Architecture");

    [Fact]
    public void Constructor_sets_defaults()
    {
        var book = NewBook();
        book.Id.Should().NotBe(Guid.Empty);
        book.Title.Should().Be("Clean Architecture");
        book.ReadingStatus.Should().Be(ReadingStatus.None);
        book.Progress.Should().BeSameAs(ReadingProgress.Empty);
        book.HasDigital.Should().BeFalse();
        book.HasPhysical.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_title(string title)
    {
        var act = () => new Book(title);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCopy_sets_bookId_and_flags()
    {
        var book = NewBook();
        var digital = new DigitalCopy("C:/a.epub", BookFormat.Epub, 1, "h", DateTimeOffset.UtcNow, null);
        var physical = new PhysicalCopy("書房");

        book.AddCopy(digital);
        book.AddCopy(physical);

        digital.BookId.Should().Be(book.Id);
        book.Copies.Should().HaveCount(2);
        book.HasDigital.Should().BeTrue();
        book.HasPhysical.Should().BeTrue();
    }

    [Fact]
    public void SetAuthors_orders_and_skips_blanks()
    {
        var book = NewBook();
        book.SetAuthors(new[] { "Robert C. Martin", "  ", "Second Author" });

        book.Authors.Should().HaveCount(2);
        book.Authors[0].Order.Should().Be(0);
        book.Authors[0].Name.Should().Be("Robert C. Martin");
        book.Authors[1].Name.Should().Be("Second Author");
    }

    [Fact]
    public void AddTag_is_idempotent_by_id()
    {
        var book = NewBook();
        var tag = new Tag("哲學");
        book.AddTag(tag);
        book.AddTag(tag);
        book.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void SetProgress_and_status_update_timestamp()
    {
        var book = NewBook();
        var before = book.UpdatedAt;
        book.SetReadingStatus(ReadingStatus.Reading);
        book.SetProgress(ReadingProgress.Create(percent: 30));

        book.ReadingStatus.Should().Be(ReadingStatus.Reading);
        book.Progress.Percent.Should().Be(30);
        book.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateMetadata_rejects_blank_title()
    {
        var book = NewBook();
        var act = () => book.UpdateMetadata("", null, null, null, null, null, null);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Domain.Tests --filter BookTests`
Expected: 編譯失敗（`Book` 缺方法/型別）。

- [ ] **Step 3: 實作 `Book.cs`**

```csharp
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;

namespace Knovault.Domain.Entities;

public class Book
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string? Subtitle { get; private set; }
    public string? Language { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublishedDate { get; private set; }
    public string? Description { get; private set; }
    public string? Isbn { get; private set; }
    public string? CoverPath { get; private set; }
    public ReadingStatus ReadingStatus { get; private set; }
    public ReadingProgress Progress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<BookAuthor> _authors = new();
    public IReadOnlyList<BookAuthor> Authors => _authors.OrderBy(a => a.Order).ToList();

    private readonly List<BookCopy> _copies = new();
    public IReadOnlyList<BookCopy> Copies => _copies;

    private readonly List<Tag> _tags = new();
    public IReadOnlyList<Tag> Tags => _tags;

    public bool HasDigital => _copies.Any(c => c is DigitalCopy);
    public bool HasPhysical => _copies.Any(c => c is PhysicalCopy);

    private Book() { Title = null!; Progress = ReadingProgress.Empty; } // EF

    public Book(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Id = Guid.NewGuid();
        Title = title.Trim();
        ReadingStatus = ReadingStatus.None;
        Progress = ReadingProgress.Empty;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SetAuthors(IEnumerable<string> names)
    {
        _authors.Clear();
        var order = 0;
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            _authors.Add(new BookAuthor(order++, name));
        Touch();
    }

    public void AddCopy(BookCopy copy)
    {
        copy.BookId = Id;
        _copies.Add(copy);
        Touch();
    }

    public void RemoveCopy(BookCopy copy)
    {
        _copies.Remove(copy);
        Touch();
    }

    public void AddTag(Tag tag)
    {
        if (_tags.Any(t => t.Id == tag.Id)) return;
        _tags.Add(tag);
        Touch();
    }

    public void RemoveTag(Tag tag)
    {
        _tags.RemoveAll(t => t.Id == tag.Id);
        Touch();
    }

    public void SetReadingStatus(ReadingStatus status)
    {
        ReadingStatus = status;
        Touch();
    }

    public void SetProgress(ReadingProgress progress)
    {
        Progress = progress ?? ReadingProgress.Empty;
        Touch();
    }

    public void SetCoverPath(string? coverPath)
    {
        CoverPath = coverPath;
        Touch();
    }

    public void UpdateMetadata(string title, string? subtitle, string? language, string? publisher,
        string? publishedDate, string? description, string? isbn)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Title = title.Trim();
        Subtitle = subtitle;
        Language = language;
        Publisher = publisher;
        PublishedDate = publishedDate;
        Description = description;
        Isbn = isbn;
        Touch();
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Domain.Tests --filter BookTests`
Expected: PASS（8 tests）。

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): add Book aggregate root"
```

---

## Task 7: LibraryFolder 實體（TDD）

**Files:**
- Create: `src/Knovault.Domain/Entities/LibraryFolder.cs`
- Test: `tests/Knovault.Domain.Tests/LibraryFolderTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Domain.Tests/LibraryFolderTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Xunit;

namespace Knovault.Domain.Tests;

public class LibraryFolderTests
{
    [Fact]
    public void Constructor_defaults_enabled_and_sets_path()
    {
        var folder = new LibraryFolder(@"D:\Books", "我的書庫");
        folder.Id.Should().NotBe(Guid.Empty);
        folder.Path.Should().Be(@"D:\Books");
        folder.DisplayName.Should().Be("我的書庫");
        folder.Enabled.Should().BeTrue();
        folder.LastScannedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_rejects_blank_path()
    {
        var act = () => new LibraryFolder("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkScanned_sets_timestamp()
    {
        var folder = new LibraryFolder(@"D:\Books");
        folder.MarkScanned();
        folder.LastScannedAt.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Domain.Tests --filter LibraryFolderTests`
Expected: 編譯失敗（`LibraryFolder` 不存在）。

- [ ] **Step 3: 實作 `LibraryFolder.cs`**

```csharp
namespace Knovault.Domain.Entities;

public class LibraryFolder
{
    public Guid Id { get; private set; }
    public string Path { get; private set; }
    public string? DisplayName { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
    public DateTimeOffset? LastScannedAt { get; private set; }

    private LibraryFolder() { Path = null!; } // EF

    public LibraryFolder(string path, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        Id = Guid.NewGuid();
        Path = path;
        DisplayName = displayName;
        Enabled = true;
        AddedAt = DateTimeOffset.UtcNow;
    }

    public void MarkScanned() => LastScannedAt = DateTimeOffset.UtcNow;
    public void SetEnabled(bool enabled) => Enabled = enabled;
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Domain.Tests --filter LibraryFolderTests`
Expected: PASS（3 tests）。

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): add LibraryFolder entity"
```

---

## Task 8: 全量驗證與收尾

- [ ] **Step 1: 跑整個方案的測試**

Run: `dotnet test`
Expected: 全綠（ReadingProgress 6 + Tag 3 + BookCopy 5 + Book 8 + LibraryFolder 3 = 25 passed），其他空測試專案 0 tests。

- [ ] **Step 2: 全量建置（含警告視為錯誤）**

Run: `dotnet build`
Expected: `Build succeeded`，0 warning / 0 error。

- [ ] **Step 3: 推送功能分支**

```bash
git push -u origin feat/library-core
```

---

## 完成定義 (Definition of Done)

- 四層方案骨架建立，`dotnet build` 0 警告 0 錯誤。
- 領域模型（Book、BookCopy/Digital/Physical、Tag、BookAuthor、LibraryFolder、ReadingProgress、兩個 enum）完成，全部關鍵不變式有單元測試覆蓋且全綠。
- 領域層零外部相依。
- 所有變更已提交，`feat/library-core` 已推送。

## 不在本計畫範圍（後續計畫）

- EF Core/SQLite 映射、Migrations、倉儲（P2）。
- 解析、掃描、封面（P3）。API、ISBN、SSE、主程式（P4）。前端（P5）。
