using Knovault.Application.Related;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Related;

public sealed class AttributeRelatedBooksStrategy(KnovaultDbContext db) : IRelatedBooksStrategy
{
    public async Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source,
        int limit,
        CancellationToken ct = default)
    {
        var sourceTags = source.Tags.Select(t => t.Id).ToHashSet();
        var sourceAuthors = source.Authors
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Authors 是 OwnsMany，EF Core 查詢時自動載入；Tags 需要明確 Include
        var candidates = await db.Books
            .Include(b => b.Tags)
            .Where(b => b.Id != source.Id)
            .ToListAsync(ct);

        return candidates
            .Select(b => new
            {
                Book  = b,
                Score = b.Tags.Count(t => sourceTags.Contains(t.Id)) * 2
                      + b.Authors.Count(a => sourceAuthors.Contains(a.Name)) * 3
                      + (!string.IsNullOrWhiteSpace(source.Publisher)
                         && string.Equals(b.Publisher, source.Publisher,
                            StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Book.Title)
            .Take(limit)
            .Select(x => x.Book)
            .ToList();
    }
}
