using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Related;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Tests;

public class AttributeRelatedBooksStrategyTests
{
    [Fact]
    public async Task Returns_books_ordered_by_score_shared_tags()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tagDesign = new Tag("設計");
            var tagDev = new Tag("開發");
            ctx.Tags.AddRange(tagDesign, tagDev);

            var source = new Book("Clean Code");
            source.AddTag(tagDesign);
            source.AddTag(tagDev);

            var twoTags = new Book("Clean Architecture"); // 2 tags → score 4
            twoTags.AddTag(tagDesign);
            twoTags.AddTag(tagDev);

            var oneTag = new Book("Design Patterns"); // 1 tag → score 2
            oneTag.AddTag(tagDesign);

            var noMatch = new Book("Cooking Book"); // score 0 → excluded

            ctx.Books.AddRange(source, twoTags, oneTag, noMatch);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().HaveCount(2);
            result[0].Title.Should().Be("Clean Architecture"); // score 4
            result[1].Title.Should().Be("Design Patterns");    // score 2
        }
    }

    [Fact]
    public async Task Author_weight_3_beats_single_tag_weight_2()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Clean Code");
            source.SetAuthors(new[] { "Robert Martin" });
            source.AddTag(tag);

            var sameAuthor = new Book("Clean Architecture"); // author → score 3
            sameAuthor.SetAuthors(new[] { "Robert Martin" });

            var sameTag = new Book("Refactoring"); // 1 tag → score 2
            sameTag.AddTag(tag);

            ctx.Books.AddRange(source, sameAuthor, sameTag);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().HaveCount(2);
            result[0].Title.Should().Be("Clean Architecture"); // score 3
            result[1].Title.Should().Be("Refactoring");        // score 2
        }
    }

    [Fact]
    public async Task Publisher_match_scores_1_point()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var source = new Book("Book A");
            source.UpdateMetadata("Book A", null, null, "Pearson", null, null, null);

            var samePublisher = new Book("Book B");
            samePublisher.UpdateMetadata("Book B", null, null, "Pearson", null, null, null);

            var diffPublisher = new Book("Book C");
            diffPublisher.UpdateMetadata("Book C", null, null, "O'Reilly", null, null, null);

            ctx.Books.AddRange(source, samePublisher, diffPublisher);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().ContainSingle(b => b.Title == "Book B");
            result.Should().NotContain(b => b.Title == "Book C");
        }
    }

    [Fact]
    public async Task Does_not_return_source_book_itself()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Source Book");
            source.AddTag(tag);

            var other = new Book("Other Book");
            other.AddTag(tag);

            ctx.Books.AddRange(source, other);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().NotContain(b => b.Id == sourceId);
            result.Should().ContainSingle(b => b.Title == "Other Book");
        }
    }

    [Fact]
    public async Task Respects_limit_parameter()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Source");
            source.AddTag(tag);

            for (var i = 0; i < 5; i++)
            {
                var b = new Book($"Book {i}");
                b.AddTag(tag);
                ctx.Books.Add(b);
            }
            ctx.Books.Add(source);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 3);

            result.Should().HaveCount(3);
        }
    }

    [Fact]
    public async Task Returns_empty_when_no_match()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var source = new Book("Lone Book");
            source.SetAuthors(new[] { "Author A" });

            var other = new Book("Other Lone");
            other.SetAuthors(new[] { "Author B" });

            ctx.Books.AddRange(source, other);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().BeEmpty();
        }
    }
}
