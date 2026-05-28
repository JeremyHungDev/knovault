using Knovault.Domain.Enums;

namespace Knovault.Domain.Entities;

public class ExternalReview
{
    public Guid Id { get; private set; }
    public Guid BookId { get; private set; }
    public ReviewSource Source { get; private set; }
    public string? ReviewerName { get; private set; }
    public float? Rating { get; private set; }
    public string? ReviewText { get; private set; }
    public string? ReviewDate { get; private set; }
    public int? HelpfulCount { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }

    private ExternalReview() { } // EF

    public ExternalReview(
        Guid bookId, ReviewSource source,
        string? reviewerName, float? rating,
        string? reviewText, string? reviewDate,
        int? helpfulCount, DateTimeOffset fetchedAt)
    {
        Id = Guid.NewGuid();
        BookId = bookId;
        Source = source;
        ReviewerName = reviewerName;
        Rating = rating;
        ReviewText = reviewText;
        ReviewDate = reviewDate;
        HelpfulCount = helpfulCount;
        FetchedAt = fetchedAt;
    }
}
