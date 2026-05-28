using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Xunit;

namespace Knovault.Domain.Tests;

public class ExternalReviewTests
{
    [Fact]
    public void Constructor_sets_all_fields()
    {
        var bookId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow;

        var review = new ExternalReview(
            bookId, ReviewSource.Goodreads,
            "Alice", 4.5f, "Great book", "2024-01-15", 12, fetchedAt);

        review.Id.Should().NotBeEmpty();
        review.BookId.Should().Be(bookId);
        review.Source.Should().Be(ReviewSource.Goodreads);
        review.ReviewerName.Should().Be("Alice");
        review.Rating.Should().Be(4.5f);
        review.ReviewText.Should().Be("Great book");
        review.ReviewDate.Should().Be("2024-01-15");
        review.HelpfulCount.Should().Be(12);
        review.FetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public void Constructor_allows_null_optional_fields()
    {
        var review = new ExternalReview(
            Guid.NewGuid(), ReviewSource.Goodreads,
            null, null, null, null, null, DateTimeOffset.UtcNow);

        review.ReviewerName.Should().BeNull();
        review.Rating.Should().BeNull();
        review.ReviewText.Should().BeNull();
        review.ReviewDate.Should().BeNull();
        review.HelpfulCount.Should().BeNull();
    }
}
