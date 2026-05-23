# Knovault 書庫核心 — P3a 解析工具組實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 用 TDD 做出書檔解析工具組：快速雜湊、檔名清理、EPUB 元數據/封面/TOC 解析、PDF 元數據/頁數解析。皆為純函式級單元（無 DB、無 UI）。

**Architecture:** 解析結果 DTO 與介面放 `Application`；實作放 `Infrastructure`。EPUB 用內建 `System.IO.Compression` + `System.Xml.Linq`（無重依賴）；PDF 用 `UglyToad.PdfPig`。測試用「程式產生的」fixture（EPUB 以程式碼組 zip、PDF 以 PdfPig builder 產生），不放二進位檔進 git。

**Tech Stack:** .NET 8、UglyToad.PdfPig、System.Text.Json、xUnit + FluentAssertions。

> **執行前置**：從 `dev` 開分支 `feat/library-core-p3`（Task 0）。commit 風格：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。逐 Task 本機 commit，P3a 全完成後 squash 成少數 commit 再合併 `dev`（暫不推，等 P3b 也完成或使用者指示）。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §5.2–5.7。

---

## 檔案結構（本計畫產出）

```
src/Knovault.Application/
  Parsing/
    ParsedBookMetadata.cs         ← 解析結果 DTO
    TocEntry.cs                   ← TOC 節點
    IBookFileParser.cs            ← 解析器介面
  Files/
    IFileHasher.cs
src/Knovault.Infrastructure/
  Files/
    FileHasher.cs                 ← 大小 + 前 1MB SHA-256（ArrayPool/Span）
    FilenameTitleCleaner.cs
  Parsing/
    EpubMetadataParser.cs         ← IBookFileParser（.epub）
    PdfMetadataParser.cs          ← IBookFileParser（.pdf, PdfPig）
tests/Knovault.Infrastructure.Tests/
  Fixtures/EpubFixtureBuilder.cs  ← 程式組最小 EPUB
  Fixtures/PdfFixtureBuilder.cs   ← PdfPig 產生 PDF
  FileHasherTests.cs
  FilenameTitleCleanerTests.cs
  EpubMetadataParserTests.cs
  PdfMetadataParserTests.cs
```

---

## Task 0: 建立功能分支

- [ ] **Step 1:**
```bash
git switch dev
git switch -c feat/library-core-p3
```
Expected: `Switched to a new branch 'feat/library-core-p3'`

---

## Task 1: 套件與共用型別（Application）

**Files:** Create `ParsedBookMetadata.cs`, `TocEntry.cs`, `IBookFileParser.cs`, `IFileHasher.cs`；修改 csproj

- [ ] **Step 1: Infrastructure 加入 PdfPig**

Run:
```bash
dotnet add src/Knovault.Infrastructure package UglyToad.PdfPig --version "0.1.*"
```

- [ ] **Step 2: 建立 `TocEntry.cs`**

Create `src/Knovault.Application/Parsing/TocEntry.cs`:
```csharp
namespace Knovault.Application.Parsing;

public sealed class TocEntry
{
    public string Title { get; init; } = "";
    public string? Href { get; init; }
    public List<TocEntry> Children { get; init; } = new();
}
```

- [ ] **Step 3: 建立 `ParsedBookMetadata.cs`**

Create `src/Knovault.Application/Parsing/ParsedBookMetadata.cs`:
```csharp
namespace Knovault.Application.Parsing;

public sealed record ParsedBookMetadata
{
    public string? Title { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Isbn { get; init; }
    public string? Description { get; init; }
    public byte[]? CoverImage { get; init; }
    public string? CoverContentType { get; init; }
    public IReadOnlyList<TocEntry> Toc { get; init; } = Array.Empty<TocEntry>();
    public int? PageCount { get; init; }
}
```

- [ ] **Step 4: 建立 `IBookFileParser.cs`**

Create `src/Knovault.Application/Parsing/IBookFileParser.cs`:
```csharp
namespace Knovault.Application.Parsing;

public interface IBookFileParser
{
    /// <summary>是否能解析此副檔名（如 .epub / .pdf）。</summary>
    bool CanParse(string filePath);

    /// <summary>解析；失敗時擲例外，由呼叫端決定 fallback。</summary>
    Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default);
}
```

- [ ] **Step 5: 建立 `IFileHasher.cs`**

Create `src/Knovault.Application/Files/IFileHasher.cs`:
```csharp
namespace Knovault.Application.Files;

public interface IFileHasher
{
    /// <summary>快速雜湊：大小 + 前 1MB 的 SHA-256，足以去重/偵測移動。</summary>
    Task<string> ComputeQuickHashAsync(string filePath, CancellationToken ct = default);
}
```

- [ ] **Step 6: 建置 + Commit**

Run: `dotnet build src/Knovault.Application`
Expected: `Build succeeded`
```bash
git add -A
git commit -m "加入解析共用型別與 PdfPig 套件" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: FileHasher（TDD）

**Files:** Create `src/Knovault.Infrastructure/Files/FileHasher.cs`; Test `tests/.../FileHasherTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/FileHasherTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Files;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class FileHasherTests
{
    private static async Task<string> WriteTempAsync(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hash_{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    [Fact]
    public async Task Same_content_yields_same_hash()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3, 4, 5 });
        var b = await WriteTempAsync(new byte[] { 1, 2, 3, 4, 5 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().Be(await hasher.ComputeQuickHashAsync(b));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public async Task Different_content_yields_different_hash()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3 });
        var b = await WriteTempAsync(new byte[] { 9, 9, 9 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().NotBe(await hasher.ComputeQuickHashAsync(b));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public async Task Hash_includes_size_prefix()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().StartWith("3-");
        }
        finally { File.Delete(a); }
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter FileHasherTests`
Expected: 編譯失敗（`FileHasher` 不存在）。

- [ ] **Step 3: 實作 `FileHasher.cs`**

Create `src/Knovault.Infrastructure/Files/FileHasher.cs`:
```csharp
using System.Buffers;
using System.Security.Cryptography;
using Knovault.Application.Files;

namespace Knovault.Infrastructure.Files;

public sealed class FileHasher : IFileHasher
{
    private const int SampleSize = 1024 * 1024; // 1 MB

    public async Task<string> ComputeQuickHashAsync(string filePath, CancellationToken ct = default)
    {
        var size = new FileInfo(filePath).Length;

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);

        var buffer = ArrayPool<byte>.Shared.Rent(SampleSize);
        try
        {
            var total = 0;
            int read;
            while (total < SampleSize &&
                   (read = await stream.ReadAsync(buffer.AsMemory(total, SampleSize - total), ct)) > 0)
                total += read;

            var hash = SHA256.HashData(buffer.AsSpan(0, total));
            return $"{size}-{Convert.ToHexString(hash)}";
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter FileHasherTests`
Expected: PASS（3 tests）。

- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "加入 FileHasher 快速雜湊" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: FilenameTitleCleaner（TDD）

**Files:** Create `src/Knovault.Infrastructure/Files/FilenameTitleCleaner.cs`; Test `tests/.../FilenameTitleCleanerTests.cs`

- [ ] **Step 1: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/FilenameTitleCleanerTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Files;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class FilenameTitleCleanerTests
{
    [Theory]
    [InlineData("C:/books/the_great_book.epub", "the great book")]
    [InlineData("D:/x/Clean.Architecture.pdf", "Clean Architecture")]
    [InlineData("/tmp/  spaced   name .epub", "spaced name")]
    public void Clean_strips_extension_and_normalizes(string path, string expected)
    {
        FilenameTitleCleaner.Clean(path).Should().Be(expected);
    }

    [Fact]
    public void Clean_returns_untitled_for_empty_name()
    {
        FilenameTitleCleaner.Clean("C:/books/.epub").Should().Be("Untitled");
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter FilenameTitleCleanerTests`
Expected: 編譯失敗。

- [ ] **Step 3: 實作 `FilenameTitleCleaner.cs`**

Create `src/Knovault.Infrastructure/Files/FilenameTitleCleaner.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Knovault.Infrastructure.Files;

public static class FilenameTitleCleaner
{
    public static string Clean(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        name = name.Replace('_', ' ').Replace('.', ' ');
        name = Regex.Replace(name, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter FilenameTitleCleanerTests`
Expected: PASS（4 tests）。

- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "加入檔名標題清理" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: EpubMetadataParser（TDD）

**Files:** Create `Fixtures/EpubFixtureBuilder.cs`, `src/Knovault.Infrastructure/Parsing/EpubMetadataParser.cs`; Test `EpubMetadataParserTests.cs`

- [ ] **Step 1: 建立測試夾具 `EpubFixtureBuilder.cs`**

Create `tests/Knovault.Infrastructure.Tests/Fixtures/EpubFixtureBuilder.cs`:
```csharp
using System.IO.Compression;
using System.Text;

namespace Knovault.Infrastructure.Tests.Fixtures;

/// <summary>以程式組出最小但合法的 EPUB3，回傳暫存檔路徑。</summary>
public static class EpubFixtureBuilder
{
    public static string CreateMinimalEpub()
    {
        var path = Path.Combine(Path.GetTempPath(), $"epub_{Guid.NewGuid():N}.epub");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        AddEntry(zip, "mimetype", "application/epub+zip");
        AddEntry(zip, "META-INF/container.xml", """
            <?xml version="1.0"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);
        AddEntry(zip, "OEBPS/content.opf", """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>測試書名</dc:title>
                <dc:creator>作者一</dc:creator>
                <dc:creator>作者二</dc:creator>
                <dc:language>zh-TW</dc:language>
                <dc:publisher>測試出版社</dc:publisher>
                <dc:date>2021-05-01</dc:date>
                <dc:identifier id="bookid">9781234567890</dc:identifier>
                <dc:description>一本測試書。</dc:description>
              </metadata>
              <manifest>
                <item id="cover" href="cover.png" media-type="image/png" properties="cover-image"/>
                <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                <item id="c1" href="c1.xhtml" media-type="application/xhtml+xml"/>
              </manifest>
              <spine>
                <itemref idref="c1"/>
              </spine>
            </package>
            """);
        AddEntry(zip, "OEBPS/nav.xhtml", """
            <?xml version="1.0" encoding="utf-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
              <body>
                <nav epub:type="toc">
                  <ol>
                    <li><a href="c1.xhtml#ch1">第一章</a></li>
                    <li><a href="c1.xhtml#ch2">第二章</a></li>
                  </ol>
                </nav>
              </body>
            </html>
            """);
        AddEntry(zip, "OEBPS/c1.xhtml", "<html><body><p>內容</p></body></html>");
        AddBinaryEntry(zip, "OEBPS/cover.png", MinimalPng());

        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content.TrimStart('\n'));
        s.Write(bytes, 0, bytes.Length);
    }

    private static void AddBinaryEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        s.Write(content, 0, content.Length);
    }

    // 1x1 透明 PNG
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
}
```

- [ ] **Step 2: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/EpubMetadataParserTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class EpubMetadataParserTests
{
    [Fact]
    public void CanParse_matches_epub_extension()
    {
        var parser = new EpubMetadataParser();
        parser.CanParse("a.epub").Should().BeTrue();
        parser.CanParse("a.EPUB").Should().BeTrue();
        parser.CanParse("a.pdf").Should().BeFalse();
    }

    [Fact]
    public async Task Parses_metadata_cover_and_toc()
    {
        var parser = new EpubMetadataParser();
        var path = EpubFixtureBuilder.CreateMinimalEpub();
        try
        {
            var meta = await parser.ParseAsync(path);

            meta.Title.Should().Be("測試書名");
            meta.Authors.Should().BeEquivalentTo(new[] { "作者一", "作者二" }, o => o.WithStrictOrdering());
            meta.Language.Should().Be("zh-TW");
            meta.Publisher.Should().Be("測試出版社");
            meta.PublishedDate.Should().Be("2021-05-01");
            meta.Isbn.Should().Be("9781234567890");
            meta.Description.Should().Be("一本測試書。");
            meta.CoverImage.Should().NotBeNullOrEmpty();
            meta.CoverContentType.Should().Be("image/png");
            meta.Toc.Should().HaveCount(2);
            meta.Toc[0].Title.Should().Be("第一章");
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 3: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter EpubMetadataParserTests`
Expected: 編譯失敗（`EpubMetadataParser` 不存在）。

- [ ] **Step 4: 實作 `EpubMetadataParser.cs`**

Create `src/Knovault.Infrastructure/Parsing/EpubMetadataParser.cs`:
```csharp
using System.IO.Compression;
using System.Xml.Linq;
using Knovault.Application.Parsing;

namespace Knovault.Infrastructure.Parsing;

public sealed class EpubMetadataParser : IBookFileParser
{
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Cnt = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".epub", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(filePath);

        var opfPath = GetOpfPath(zip);
        var opfDir = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? "";
        var opf = LoadXml(zip, opfPath);

        var metadata = opf.Root!.Element(Opf + "metadata") ?? opf.Root.Elements()
            .First(e => e.Name.LocalName == "metadata");

        var authors = metadata.Elements(Dc + "creator").Select(e => e.Value.Trim())
            .Where(s => s.Length > 0).ToList();

        var manifest = opf.Root.Element(Opf + "manifest")!;
        var (coverBytes, coverType) = ReadCover(zip, manifest, metadata, opfDir);
        var toc = ReadToc(zip, opf.Root, manifest, opfDir);

        return new ParsedBookMetadata
        {
            Title = metadata.Element(Dc + "title")?.Value.Trim(),
            Authors = authors,
            Language = metadata.Element(Dc + "language")?.Value.Trim(),
            Publisher = metadata.Element(Dc + "publisher")?.Value.Trim(),
            PublishedDate = metadata.Element(Dc + "date")?.Value.Trim(),
            Isbn = metadata.Elements(Dc + "identifier").Select(e => e.Value.Trim())
                .FirstOrDefault(v => LooksLikeIsbn(v)),
            Description = metadata.Element(Dc + "description")?.Value.Trim(),
            CoverImage = coverBytes,
            CoverContentType = coverType,
            Toc = toc
        };
    }

    private static string GetOpfPath(ZipArchive zip)
    {
        var container = LoadXml(zip, "META-INF/container.xml");
        var rootfile = container.Descendants(Cnt + "rootfile").First();
        return rootfile.Attribute("full-path")!.Value;
    }

    private static (byte[]? bytes, string? type) ReadCover(
        ZipArchive zip, XElement manifest, XElement metadata, string opfDir)
    {
        // EPUB3: properties 含 cover-image
        var coverItem = manifest.Elements(Opf + "item")
            .FirstOrDefault(i => (i.Attribute("properties")?.Value ?? "").Split(' ').Contains("cover-image"));

        // EPUB2: <meta name="cover" content="id"/>
        if (coverItem is null)
        {
            var coverId = metadata.Elements(Opf + "meta")
                .FirstOrDefault(m => (string?)m.Attribute("name") == "cover")?.Attribute("content")?.Value;
            if (coverId is not null)
                coverItem = manifest.Elements(Opf + "item")
                    .FirstOrDefault(i => (string?)i.Attribute("id") == coverId);
        }

        if (coverItem is null) return (null, null);

        var href = coverItem.Attribute("href")!.Value;
        var entryPath = CombineZipPath(opfDir, href);
        var entry = zip.GetEntry(entryPath);
        if (entry is null) return (null, null);

        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return (ms.ToArray(), (string?)coverItem.Attribute("media-type"));
    }

    private static List<TocEntry> ReadToc(ZipArchive zip, XElement package, XElement manifest, string opfDir)
    {
        // EPUB3 nav.xhtml（properties=nav）
        var navItem = manifest.Elements(Opf + "item")
            .FirstOrDefault(i => (i.Attribute("properties")?.Value ?? "").Split(' ').Contains("nav"));
        if (navItem is not null)
        {
            var navDoc = LoadXml(zip, CombineZipPath(opfDir, navItem.Attribute("href")!.Value));
            var nav = navDoc.Descendants(Xhtml + "nav").FirstOrDefault()
                      ?? navDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "nav");
            var ol = nav?.Descendants(Xhtml + "ol").FirstOrDefault()
                     ?? nav?.Descendants().FirstOrDefault(e => e.Name.LocalName == "ol");
            return ol is null ? new() : ParseNavList(ol);
        }
        return new();
    }

    private static List<TocEntry> ParseNavList(XElement ol)
    {
        var result = new List<TocEntry>();
        foreach (var li in ol.Elements().Where(e => e.Name.LocalName == "li"))
        {
            var a = li.Elements().FirstOrDefault(e => e.Name.LocalName == "a");
            var childOl = li.Elements().FirstOrDefault(e => e.Name.LocalName == "ol");
            result.Add(new TocEntry
            {
                Title = a?.Value.Trim() ?? "",
                Href = (string?)a?.Attribute("href"),
                Children = childOl is null ? new() : ParseNavList(childOl)
            });
        }
        return result;
    }

    private static bool LooksLikeIsbn(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is 10 or 13;
    }

    private static string CombineZipPath(string dir, string href) =>
        (string.IsNullOrEmpty(dir) ? href : $"{dir}/{href}").Replace('\\', '/');

    private static XDocument LoadXml(ZipArchive zip, string entryPath)
    {
        var entry = zip.GetEntry(entryPath) ?? throw new InvalidDataException($"EPUB 缺少 {entryPath}");
        using var s = entry.Open();
        return XDocument.Load(s);
    }
}
```

- [ ] **Step 5: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter EpubMetadataParserTests`
Expected: PASS（2 tests）。若 TOC/封面斷言失敗，依實際解析結果修正 parser（fixture 為準）。

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "加入 EPUB 元數據解析器" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: PdfMetadataParser（TDD）

**Files:** Create `Fixtures/PdfFixtureBuilder.cs`, `src/Knovault.Infrastructure/Parsing/PdfMetadataParser.cs`; Test `PdfMetadataParserTests.cs`

- [ ] **Step 1: 建立 fixture `PdfFixtureBuilder.cs`（用 PdfPig builder）**

Create `tests/Knovault.Infrastructure.Tests/Fixtures/PdfFixtureBuilder.cs`:
```csharp
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;

namespace Knovault.Infrastructure.Tests.Fixtures;

/// <summary>用 PdfPig builder 產生含標題/作者與 N 頁的 PDF，回傳暫存檔路徑。</summary>
public static class PdfFixtureBuilder
{
    public static string CreatePdf(string title, string author, int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;
        for (var i = 0; i < pageCount; i++)
            builder.AddPage(PageSize.A4);

        var bytes = builder.Build();
        var path = Path.Combine(Path.GetTempPath(), $"pdf_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
```

- [ ] **Step 2: 寫失敗測試**

Create `tests/Knovault.Infrastructure.Tests/PdfMetadataParserTests.cs`:
```csharp
using FluentAssertions;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class PdfMetadataParserTests
{
    [Fact]
    public void CanParse_matches_pdf_extension()
    {
        var parser = new PdfMetadataParser();
        parser.CanParse("a.pdf").Should().BeTrue();
        parser.CanParse("a.PDF").Should().BeTrue();
        parser.CanParse("a.epub").Should().BeFalse();
    }

    [Fact]
    public async Task Parses_title_author_and_page_count()
    {
        var parser = new PdfMetadataParser();
        var path = PdfFixtureBuilder.CreatePdf("PDF 測試", "PDF 作者", pageCount: 3);
        try
        {
            var meta = await parser.ParseAsync(path);
            meta.Title.Should().Be("PDF 測試");
            meta.Authors.Should().ContainSingle().Which.Should().Be("PDF 作者");
            meta.PageCount.Should().Be(3);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 3: 跑測試確認失敗**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter PdfMetadataParserTests`
Expected: 編譯失敗（`PdfMetadataParser` 不存在）。

- [ ] **Step 4: 實作 `PdfMetadataParser.cs`**

Create `src/Knovault.Infrastructure/Parsing/PdfMetadataParser.cs`:
```csharp
using Knovault.Application.Parsing;
using UglyToad.PdfPig;

namespace Knovault.Infrastructure.Parsing;

public sealed class PdfMetadataParser : IBookFileParser
{
    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var doc = PdfDocument.Open(filePath);
        var info = doc.Information;

        var authors = string.IsNullOrWhiteSpace(info.Author)
            ? Array.Empty<string>()
            : new[] { info.Author.Trim() };

        var meta = new ParsedBookMetadata
        {
            Title = string.IsNullOrWhiteSpace(info.Title) ? null : info.Title.Trim(),
            Authors = authors,
            PageCount = doc.NumberOfPages
        };
        return Task.FromResult(meta);
    }
}
```

- [ ] **Step 5: 跑測試確認通過**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter PdfMetadataParserTests`
Expected: PASS（2 tests）。

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "加入 PDF 元數據解析器" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: 全量驗證 + squash

- [ ] **Step 1: 全量測試**
Run: `dotnet test`
Expected: Domain 25 + Infrastructure (5 + 3 + 4 + 2 + 2 = 16) = 41 passed。

- [ ] **Step 2: 全量建置**
Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: squash 成少數 commit（計畫 + 實作）**，留在 `feat/library-core-p3`，**先不合併、不推**（等 P3b 完成一起處理，或依使用者指示）。

---

## 完成定義 (Definition of Done)

- `FileHasher`（大小+前1MB SHA-256）、`FilenameTitleCleaner`、`EpubMetadataParser`（元數據/封面/TOC）、`PdfMetadataParser`（元數據/頁數）完成並有 fixture 單元測試全綠。
- 解析介面/DTO 在 Application；實作在 Infrastructure；無 DB 依賴。
- `dotnet test` 全綠（41）、`dotnet build` 0 警告 0 錯誤。

## 不在本計畫範圍（P3b）

- 資料夾掃描編排、雜湊比對去重/移動/遺失、批次寫入、檔案鎖重試。
- 封面檔儲存 + 縮圖（ImageSharp）、PDF 第一頁算繪（PDFium）。
- DB 整合（建 Book/DigitalCopy）。
