using Knovault.Application.Parsing;

namespace Knovault.Application.Metadata;

public interface IIsbnMetadataProvider
{
    /// <summary>以 ISBN 查詢書目；查無/失敗回 null。</summary>
    Task<ParsedBookMetadata?> LookupAsync(string isbn, CancellationToken ct = default);
}
