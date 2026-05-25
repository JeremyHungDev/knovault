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
        HasDigital = b.HasDigital,
        IsPhysical = b.IsPhysical,
        PhysicalLocation = b.PhysicalLocation,
        PhysicalNotes = b.PhysicalNotes,
        Tags = b.Tags.Select(t => t.Name).ToList(),
        Copies = b.Copies.OfType<DigitalCopy>().Select(ToCopyDto).ToList()
    };

    private static CopyDto ToCopyDto(DigitalCopy d) => new()
    {
        Id = d.Id,
        Format = d.Format.ToString(),
        FileSizeBytes = d.FileSizeBytes,
        IsMissing = d.IsMissing,
        ParseFailed = d.ParseFailed
    };
}
