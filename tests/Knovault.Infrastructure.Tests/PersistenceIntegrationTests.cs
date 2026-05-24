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
        Guid bookId;

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
            book.SetPhysical(true);
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
            loaded.Tags.Should().ContainSingle(t => t.Name == "設計");
            loaded.HasDigital.Should().BeTrue();
            loaded.IsPhysical.Should().BeTrue();
            loaded.HasPhysical.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Book_with_empty_progress_round_trips_as_non_null()
    {
        using var db = new SqliteTestDb();
        Guid id;

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
        Guid id;

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
