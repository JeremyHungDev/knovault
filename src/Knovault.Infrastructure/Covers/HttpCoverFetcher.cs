using Knovault.Application.Covers;

namespace Knovault.Infrastructure.Covers;

public sealed class HttpCoverFetcher : ICoverFetcher
{
    private readonly HttpClient _http;
    public HttpCoverFetcher(HttpClient http) => _http = http;

    public async Task<(byte[] Bytes, string? ContentType)?> FetchAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return null;
            return (bytes, resp.Content.Headers.ContentType?.MediaType);
        }
        catch
        {
            return null; // 網路/逾時/非圖片：不擋住建立流程
        }
    }
}
