// src/Knovault.Infrastructure/Reviews/GoodreadsScraper.cs
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Knovault.Application.Reviews;
using Knovault.Domain.Enums;

namespace Knovault.Infrastructure.Reviews;

public class GoodreadsScraper : IBookReviewScraper
{
    private const string ApiKey = "da2-xpgsdydkbregjhpr6ejzqdhuwy";
    private const string GraphQlEndpoint =
        "https://kxbwmqov6jgg3daaamb744ycu4.appsync-api.us-east-1.amazonaws.com/graphql";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36";

    private readonly HttpClient _http;

    public ReviewSource Source => ReviewSource.Goodreads;

    public GoodreadsScraper(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default)
    {
        var bookPageUrl = await GetBookPageUrlAsync(isbn, ct);
        if (bookPageUrl is null) return Array.Empty<ScrapedReview>();

        var workId = await GetWorkIdAsync(bookPageUrl, ct);
        if (workId is null) return Array.Empty<ScrapedReview>();

        return await FetchReviewsAsync(workId, ct);
    }

    private async Task<string?> GetBookPageUrlAsync(string isbn, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.goodreads.com/book/isbn/{isbn}");
        req.Headers.Add("User-Agent", UserAgent);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return resp.RequestMessage?.RequestUri?.ToString();
    }

    private async Task<string?> GetWorkIdAsync(string bookPageUrl, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, bookPageUrl);
        req.Headers.Add("User-Agent", UserAgent);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        return ExtractWorkId(html);
    }

    private async Task<IReadOnlyList<ScrapedReview>> FetchReviewsAsync(string workId, CancellationToken ct)
    {
        var payload = new
        {
            operationName = "getReviews",
            variables = new
            {
                filters = new { resourceType = "WORK", resourceId = workId },
                pagination = new { limit = 30 },
            },
            query = GetReviewsQuery,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
        req.Headers.Add("X-Api-Key", ApiKey);
        req.Headers.Add("User-Agent", UserAgent);
        req.Content = JsonContent.Create(payload);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return Array.Empty<ScrapedReview>();

        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseReviewsResponse(json);
    }

    internal static string? ExtractLegacyId(string url)
    {
        var m = Regex.Match(url, @"/book/show/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    internal static string? ExtractWorkId(string html)
    {
        var m = Regex.Match(html, @"kca://work/[^""\\]+");
        return m.Success ? m.Value : null;
    }

    internal static IReadOnlyList<ScrapedReview> ParseReviewsResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var edges = doc.RootElement
            .GetProperty("data")
            .GetProperty("getReviews")
            .GetProperty("edges");

        var result = new List<ScrapedReview>();
        foreach (var edge in edges.EnumerateArray())
        {
            var node = edge.GetProperty("node");

            var name = node.TryGetProperty("creator", out var creator) &&
                       creator.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null
                ? n.GetString() : null;

            float? rating = node.TryGetProperty("rating", out var r) && r.ValueKind != JsonValueKind.Null
                ? (float)r.GetDouble() : null;

            var text = node.TryGetProperty("text", out var t) && t.ValueKind != JsonValueKind.Null
                ? t.GetString() : null;

            var date = node.TryGetProperty("createdAt", out var d) && d.ValueKind != JsonValueKind.Null
                ? d.GetString() : null;

            int? likes = node.TryGetProperty("likeCount", out var l) && l.ValueKind != JsonValueKind.Null
                ? l.GetInt32() : null;

            result.Add(new ScrapedReview(name, rating, text, date, likes));
        }
        return result;
    }

    private const string GetReviewsQuery = """
        query getReviews($filters: BookReviewsFilterInput!, $pagination: PaginationInput) {
          getReviews(filters: $filters, pagination: $pagination) {
            edges {
              node {
                creator { name }
                rating
                text
                createdAt
                likeCount
              }
            }
          }
        }
        """;
}
