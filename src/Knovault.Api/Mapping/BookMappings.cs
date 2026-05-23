using Knovault.Api.Contracts;
using Knovault.Domain.Entities;

namespace Knovault.Api.Mapping;

public static class BookMappings
{
    public static BookSummaryDto ToSummaryDto(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Authors = b.Authors.Select(a => a.Name).ToList(),
        CoverPath = b.CoverPath,
        ReadingStatus = b.ReadingStatus.ToString(),
        ProgressPercent = b.Progress.Percent,
        HasDigital = b.HasDigital,
        HasPhysical = b.HasPhysical
    };

    public static BookDetailDto ToDetailDto(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        Authors = b.Authors.Select(a => a.Name).ToList(),
        Language = b.Language,
        Publisher = b.Publisher,
        PublishedDate = b.PublishedDate,
        Description = b.Description,
        Isbn = b.Isbn,
        CoverPath = b.CoverPath,
        ReadingStatus = b.ReadingStatus.ToString(),
        ProgressPercent = b.Progress.Percent,
        CurrentPage = b.Progress.CurrentPage,
        TotalPages = b.Progress.TotalPages,
        Tags = b.Tags.Select(t => t.Name).ToList(),
        Copies = b.Copies.Select(ToCopyDto).ToList()
    };

    private static CopyDto ToCopyDto(BookCopy c) => c switch
    {
        DigitalCopy d => new CopyDto
        {
            Id = d.Id,
            Type = "digital",
            Format = d.Format.ToString(),
            FileSizeBytes = d.FileSizeBytes,
            IsMissing = d.IsMissing,
            ParseFailed = d.ParseFailed
        },
        PhysicalCopy p => new CopyDto
        {
            Id = p.Id,
            Type = "physical",
            Location = p.Location
        },
        _ => new CopyDto { Id = c.Id, Type = "unknown" }
    };
}
