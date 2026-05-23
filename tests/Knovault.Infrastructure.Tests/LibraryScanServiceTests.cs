using FluentAssertions;
using Knovault.Application.Parsing;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Files;
using Knovault.Infrastructure.Library;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Persistence;
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

    private LibraryScanService NewService(KnovaultDbContext ctx) =>
        new(ctx,
            new BookParsingService(new IBookFileParser[] { new EpubMetadataParser(), new PdfMetadataParser() }),
            new FileHasher(),
            new CoverStorage(_coversDir));

    private async Task AddFolderAsync()
    {
        await using var ctx = _db.NewContext();
        ctx.LibraryFolders.Add(new LibraryFolder(_libraryDir, "測試書庫"));
        await ctx.SaveChangesAsync();
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
            var report = await NewService(ctx).ScanAsync();
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
            await NewService(ctx).ScanAsync();
        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(ctx).ScanAsync();
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
            await NewService(ctx).ScanAsync();

        var moved = Path.Combine(_libraryDir, "renamed.epub");
        File.Move(original, moved);

        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(ctx).ScanAsync();
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
            await NewService(ctx).ScanAsync();

        File.Delete(path);

        await using (var ctx = _db.NewContext())
        {
            var report = await NewService(ctx).ScanAsync();
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
