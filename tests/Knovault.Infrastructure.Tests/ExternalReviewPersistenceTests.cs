using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class ExternalReviewPersistenceTests
{
    [Fact]
    public async Task ExternalReview_round_trips_through_sqlite()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow;

        await using (var ctx = db.NewContext())
        {
            var review = new ExternalReview(
                bookId, ReviewSource.Goodreads,
                "Alice", 4.5f, "Great book", "2024-01-15", 12, fetchedAt);
            ctx.ExternalReviews.Add(review);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.ExternalReviews
                .SingleAsync(r => r.BookId == bookId);

            loaded.Source.Should().Be(ReviewSource.Goodreads);
            loaded.ReviewerName.Should().Be("Alice");
            loaded.Rating.Should().BeApproximately(4.5f, 0.001f);
            loaded.ReviewText.Should().Be("Great book");
            loaded.ReviewDate.Should().Be("2024-01-15");
            loaded.HelpfulCount.Should().Be(12);
        }
    }

    [Fact]
    public async Task Source_is_stored_as_string_in_db()
    {
        using var db = new SqliteTestDb();

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                Guid.NewGuid(), ReviewSource.BooksComTw,
                null, null, null, null, null, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        // Query raw SQLite to confirm string storage
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db.Path}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Source FROM ExternalReviews LIMIT 1";
        var value = (string)(await cmd.ExecuteScalarAsync())!;
        value.Should().Be("BooksComTw");
    }
}
