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
