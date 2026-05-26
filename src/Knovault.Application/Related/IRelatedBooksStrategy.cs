using Knovault.Domain.Entities;

namespace Knovault.Application.Related;

public interface IRelatedBooksStrategy
{
    Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source,
        int limit,
        CancellationToken ct = default);
}
