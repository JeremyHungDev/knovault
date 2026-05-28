# Reviews Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "評論" tab to BookDetailView that fetches and caches Goodreads reviews via their internal GraphQL API.

**Architecture:** Provider pattern — `GoodreadsScraper` implements `IBookReviewScraper`; `ExternalReviewService` (implements `IExternalReviewService`) coordinates scrapers and lazy-caches results in a new `ExternalReviews` SQLite table. `BooksComTwScraper` is a no-op stub registered for Phase 2.

**Tech Stack:** .NET 8, EF Core + SQLite, System.Text.Json, Vue 3, Naive UI (NSegmented, NSkeleton)

---

## File Map

| File | Action | Notes |
|---|---|---|
| `src/Knovault.Domain/Enums/ReviewSource.cs` | Create | enum stored as string |
| `src/Knovault.Domain/Entities/ExternalReview.cs` | Create | EF entity |
| `src/Knovault.Infrastructure/Persistence/Configurations/ExternalReviewConfiguration.cs` | Create | EF config |
| `src/Knovault.Infrastructure/Persistence/KnovaultDbContext.cs` | Modify | add DbSet |
| `src/Knovault.Application/Reviews/ScrapedReview.cs` | Create | record |
| `src/Knovault.Application/Reviews/IBookReviewScraper.cs` | Create | interface |
| `src/Knovault.Application/Reviews/IExternalReviewService.cs` | Create | interface + result records |
| `src/Knovault.Infrastructure/Reviews/GoodreadsScraper.cs` | Create | HttpClient scraper |
| `src/Knovault.Infrastructure/Reviews/BooksComTwScraper.cs` | Create | no-op stub |
| `src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs` | Create | aggregator + cache |
| `src/Knovault.Api/Contracts/ReviewsResultDto.cs` | Create | API response DTOs |
| `src/Knovault.Api/Endpoints/ReviewEndpoints.cs` | Create | Minimal API endpoints |
| `src/Knovault.Api/Program.cs` | Modify | register services + map endpoints |
| `tests/Knovault.Domain.Tests/ExternalReviewTests.cs` | Create | |
| `tests/Knovault.Infrastructure.Tests/GoodreadsScraperTests.cs` | Create | unit tests (static helpers) |
| `tests/Knovault.Infrastructure.Tests/ExternalReviewServiceTests.cs` | Create | integration with SQLite |
| `tests/Knovault.Api.Tests/ReviewEndpointsTests.cs` | Create | API integration tests |
| `web/src/api/types.ts` | Modify | add Review types |
| `web/src/api/reviews.ts` | Create | API client |
| `web/src/components/ReviewsSection.vue` | Create | reviews tab component |
| `web/src/views/BookDetailView.vue` | Modify | add 評論 tab |

---

## Task 1: Domain — ReviewSource enum + ExternalReview entity

**Files:**
- Create: `src/Knovault.Domain/Enums/ReviewSource.cs`
- Create: `src/Knovault.Domain/Entities/ExternalReview.cs`
- Create: `tests/Knovault.Domain.Tests/ExternalReviewTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Knovault.Domain.Tests/ExternalReviewTests.cs
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
```

- [ ] **Step 2: Run to confirm it fails**

```
dotnet test tests/Knovault.Domain.Tests --filter "ExternalReview" -v minimal
```

Expected: compile error — `ExternalReview` not found.

- [ ] **Step 3: Create ReviewSource enum**

```csharp
// src/Knovault.Domain/Enums/ReviewSource.cs
namespace Knovault.Domain.Enums;

public enum ReviewSource
{
    Goodreads,
    BooksComTw,
}
```

- [ ] **Step 4: Create ExternalReview entity**

```csharp
// src/Knovault.Domain/Entities/ExternalReview.cs
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
```

- [ ] **Step 5: Run tests to confirm they pass**

```
dotnet test tests/Knovault.Domain.Tests --filter "ExternalReview" -v minimal
```

Expected: 2 tests PASS.

- [ ] **Step 6: Commit**

```
git add src/Knovault.Domain/Enums/ReviewSource.cs src/Knovault.Domain/Entities/ExternalReview.cs tests/Knovault.Domain.Tests/ExternalReviewTests.cs
git commit -m "feat(domain): ExternalReview entity + ReviewSource enum"
```

---

## Task 2: EF Core — ExternalReview configuration + migration

**Files:**
- Create: `src/Knovault.Infrastructure/Persistence/Configurations/ExternalReviewConfiguration.cs`
- Modify: `src/Knovault.Infrastructure/Persistence/KnovaultDbContext.cs`
- Migration generated by EF CLI

- [ ] **Step 1: Write persistence test**

```csharp
// tests/Knovault.Infrastructure.Tests/ExternalReviewPersistenceTests.cs
using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class ExternalReviewPersistenceTests
{
    [Fact]
    public async Task ExternalReview_round_trips_through_sqlite()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow;

        await using (var ctx = db.NewContext())
        {
            var review = new ExternalReview(
                bookId, ReviewSource.Goodreads,
                "Alice", 4.5f, "Great book", "2024-01-15", 12, fetchedAt);
            ctx.ExternalReviews.Add(review);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.ExternalReviews
                .SingleAsync(r => r.BookId == bookId);

            loaded.Source.Should().Be(ReviewSource.Goodreads);
            loaded.ReviewerName.Should().Be("Alice");
            loaded.Rating.Should().BeApproximately(4.5f, 0.001f);
            loaded.ReviewText.Should().Be("Great book");
            loaded.ReviewDate.Should().Be("2024-01-15");
            loaded.HelpfulCount.Should().Be(12);
        }
    }

    [Fact]
    public async Task Source_is_stored_as_string_in_db()
    {
        using var db = new SqliteTestDb();

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                Guid.NewGuid(), ReviewSource.BooksComTw,
                null, null, null, null, null, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        // Query raw SQLite to confirm string storage
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db.Path}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Source FROM ExternalReviews LIMIT 1";
        var value = (string)(await cmd.ExecuteScalarAsync())!;
        value.Should().Be("BooksComTw");
    }
}
```

- [ ] **Step 2: Run to confirm compile error**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "ExternalReviewPersistence" -v minimal
```

Expected: compile error — `ctx.ExternalReviews` not found.

- [ ] **Step 3: Create EF configuration**

```csharp
// src/Knovault.Infrastructure/Persistence/Configurations/ExternalReviewConfiguration.cs
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class ExternalReviewConfiguration : IEntityTypeConfiguration<ExternalReview>
{
    public void Configure(EntityTypeBuilder<ExternalReview> builder)
    {
        builder.ToTable("ExternalReviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BookId).IsRequired();
        builder.Property(r => r.Source).HasConversion<string>().IsRequired();
        builder.Property(r => r.FetchedAt).IsRequired();
        builder.HasIndex(r => new { r.BookId, r.Source });
    }
}
```

- [ ] **Step 4: Add DbSet to KnovaultDbContext**

In `src/Knovault.Infrastructure/Persistence/KnovaultDbContext.cs`, add one line after `public DbSet<LibraryFolders>`:

```csharp
public DbSet<ExternalReview> ExternalReviews => Set<ExternalReview>();
```

Full file after edit:

```csharp
using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Persistence;

public class KnovaultDbContext : DbContext
{
    public KnovaultDbContext(DbContextOptions<KnovaultDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LibraryFolder> LibraryFolders => Set<LibraryFolder>();
    public DbSet<ExternalReview> ExternalReviews => Set<ExternalReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnovaultDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Run tests to confirm they pass**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "ExternalReviewPersistence" -v minimal
```

Expected: 2 tests PASS.

- [ ] **Step 6: Generate EF migration**

```
dotnet ef migrations add AddExternalReviews --project src/Knovault.Infrastructure --startup-project src/Knovault.Api
```

Expected: new migration files under `src/Knovault.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 7: Commit**

```
git add src/Knovault.Infrastructure/Persistence/Configurations/ExternalReviewConfiguration.cs
git add src/Knovault.Infrastructure/Persistence/KnovaultDbContext.cs
git add src/Knovault.Infrastructure/Persistence/Migrations/
git add tests/Knovault.Infrastructure.Tests/ExternalReviewPersistenceTests.cs
git commit -m "feat(infra): ExternalReviews table + EF migration"
```

---

## Task 3: Application layer — interfaces + ScrapedReview record

**Files:**
- Create: `src/Knovault.Application/Reviews/ScrapedReview.cs`
- Create: `src/Knovault.Application/Reviews/IBookReviewScraper.cs`
- Create: `src/Knovault.Application/Reviews/IExternalReviewService.cs`

No tests for pure interfaces/records — they're validated by the compiler when used.

- [ ] **Step 1: Create ScrapedReview record**

```csharp
// src/Knovault.Application/Reviews/ScrapedReview.cs
namespace Knovault.Application.Reviews;

public record ScrapedReview(
    string? ReviewerName,
    float? Rating,
    string? ReviewText,
    string? ReviewDate,
    int? HelpfulCount);
```

- [ ] **Step 2: Create IBookReviewScraper interface**

```csharp
// src/Knovault.Application/Reviews/IBookReviewScraper.cs
using Knovault.Domain.Enums;

namespace Knovault.Application.Reviews;

public interface IBookReviewScraper
{
    ReviewSource Source { get; }
    Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create IExternalReviewService interface + result types**

```csharp
// src/Knovault.Application/Reviews/IExternalReviewService.cs
using Knovault.Domain.Enums;

namespace Knovault.Application.Reviews;

public interface IExternalReviewService
{
    Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default);
    Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default);
}

public record ReviewsResult(IReadOnlyList<SourceResult> Sources);

public record SourceResult(
    ReviewSource Source,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<ScrapedReview> Reviews);
```

- [ ] **Step 4: Confirm project builds**

```
dotnet build src/Knovault.Application
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Knovault.Application/Reviews/
git commit -m "feat(application): IBookReviewScraper + IExternalReviewService interfaces"
```

---

## Task 4: Infrastructure — GoodreadsScraper

**Files:**
- Create: `src/Knovault.Infrastructure/Reviews/GoodreadsScraper.cs`
- Create: `tests/Knovault.Infrastructure.Tests/GoodreadsScraperTests.cs`

- [ ] **Step 1: Write unit tests for static parsing helpers**

```csharp
// tests/Knovault.Infrastructure.Tests/GoodreadsScraperTests.cs
using FluentAssertions;
using Knovault.Infrastructure.Reviews;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class GoodreadsScraperTests
{
    [Theory]
    [InlineData("https://www.goodreads.com/book/show/11468377-thinking-fast-and-slow", "11468377")]
    [InlineData("https://www.goodreads.com/en/book/show/99999-some-book", "99999")]
    public void ExtractLegacyId_returns_id_from_valid_url(string url, string expected)
    {
        GoodreadsScraper.ExtractLegacyId(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.goodreads.com/search?q=test")]
    [InlineData("https://www.goodreads.com/")]
    public void ExtractLegacyId_returns_null_for_invalid_url(string url)
    {
        GoodreadsScraper.ExtractLegacyId(url).Should().BeNull();
    }

    [Fact]
    public void ExtractWorkId_finds_kca_work_id_in_html()
    {
        var html = """
            <html><head></head><body>
            <script id="__NEXT_DATA__">
            {"props":{"pageProps":{"apolloState":{"Work:kca://work/amzn1.gr.work.v1.abc123":{"id":"kca://work/amzn1.gr.work.v1.abc123"}}}}}
            </script>
            </body></html>
            """;
        GoodreadsScraper.ExtractWorkId(html).Should().Be("kca://work/amzn1.gr.work.v1.abc123");
    }

    [Fact]
    public void ExtractWorkId_returns_null_when_not_found()
    {
        GoodreadsScraper.ExtractWorkId("<html><body>no work id here</body></html>").Should().BeNull();
    }

    [Fact]
    public void ParseReviewsResponse_maps_all_fields()
    {
        var json = """
            {
              "data": {
                "getReviews": {
                  "edges": [
                    {
                      "node": {
                        "creator": { "name": "Alice" },
                        "rating": 4,
                        "text": "Great book!",
                        "createdAt": "2024-01-15T00:00:00Z",
                        "likeCount": 7
                      }
                    },
                    {
                      "node": {
                        "creator": { "name": "Bob" },
                        "rating": null,
                        "text": null,
                        "createdAt": "2023-11-02T00:00:00Z",
                        "likeCount": 0
                      }
                    }
                  ]
                }
              }
            }
            """;

        var reviews = GoodreadsScraper.ParseReviewsResponse(json);

        reviews.Should().HaveCount(2);
        reviews[0].ReviewerName.Should().Be("Alice");
        reviews[0].Rating.Should().Be(4f);
        reviews[0].ReviewText.Should().Be("Great book!");
        reviews[0].ReviewDate.Should().Be("2024-01-15T00:00:00Z");
        reviews[0].HelpfulCount.Should().Be(7);
        reviews[1].ReviewerName.Should().Be("Bob");
        reviews[1].Rating.Should().BeNull();
        reviews[1].ReviewText.Should().BeNull();
    }

    [Fact]
    public void ParseReviewsResponse_returns_empty_on_missing_data()
    {
        var json = """{"data":{"getReviews":{"edges":[]}}}""";
        GoodreadsScraper.ParseReviewsResponse(json).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run to confirm compile error**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "GoodreadsScraper" -v minimal
```

Expected: compile error — `GoodreadsScraper` not found.

- [ ] **Step 3: Create GoodreadsScraper**

```csharp
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
```

- [ ] **Step 4: Run tests to confirm they pass**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "GoodreadsScraper" -v minimal
```

Expected: 7 tests PASS.

- [ ] **Step 5: Commit**

```
git add src/Knovault.Infrastructure/Reviews/GoodreadsScraper.cs tests/Knovault.Infrastructure.Tests/GoodreadsScraperTests.cs
git commit -m "feat(infra): GoodreadsScraper — ISBN→workId→GraphQL reviews"
```

---

## Task 5: Infrastructure — BooksComTwScraper stub

**Files:**
- Create: `src/Knovault.Infrastructure/Reviews/BooksComTwScraper.cs`

No tests — stub always returns empty list (Phase 2 will implement).

- [ ] **Step 1: Create stub**

```csharp
// src/Knovault.Infrastructure/Reviews/BooksComTwScraper.cs
using Knovault.Application.Reviews;
using Knovault.Domain.Enums;

namespace Knovault.Infrastructure.Reviews;

// Phase 2: 博客來 product pages are behind Cloudflare Managed Challenge.
// Requires Microsoft.Playwright (headless Chromium). Stub returns empty until implemented.
public class BooksComTwScraper : IBookReviewScraper
{
    public ReviewSource Source => ReviewSource.BooksComTw;

    public Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScrapedReview>>(Array.Empty<ScrapedReview>());
}
```

- [ ] **Step 2: Confirm project builds**

```
dotnet build src/Knovault.Infrastructure
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Knovault.Infrastructure/Reviews/BooksComTwScraper.cs
git commit -m "feat(infra): BooksComTwScraper stub (Phase 2 placeholder)"
```

---

## Task 6: Infrastructure — ExternalReviewService

**Files:**
- Create: `src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs`
- Create: `tests/Knovault.Infrastructure.Tests/ExternalReviewServiceTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Knovault.Infrastructure.Tests/ExternalReviewServiceTests.cs
using FluentAssertions;
using Knovault.Application.Reviews;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Reviews;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class ExternalReviewServiceTests
{
    private static Book MakeBook(string? isbn = "9780374275631")
    {
        var b = new Book("Test Book");
        b.UpdateMetadata("Test Book", null, null, null, null, null, isbn);
        return b;
    }

    [Fact]
    public async Task GetReviewsAsync_returns_empty_when_isbn_is_null()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        var svc = new ExternalReviewService(ctx, Array.Empty<IBookReviewScraper>());

        var result = await svc.GetReviewsAsync(Guid.NewGuid(), null);

        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviewsAsync_calls_scraper_when_no_cache_exists()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var scraper = new FakeScraper(ReviewSource.Goodreads,
            new ScrapedReview("Alice", 4f, "Good", "2024-01-01", 3));

        await using var ctx = db.NewContext();
        var svc = new ExternalReviewService(ctx, new[] { scraper });

        var result = await svc.GetReviewsAsync(bookId, "9780374275631");

        result.Sources.Should().HaveCount(1);
        result.Sources[0].Source.Should().Be(ReviewSource.Goodreads);
        result.Sources[0].Reviews.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Alice");
        scraper.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReviewsAsync_returns_cache_without_calling_scraper()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                bookId, ReviewSource.Goodreads, "Bob", 3f, "Okay", "2023-06-01", 0, fetchedAt));
            await ctx.SaveChangesAsync();
        }

        var scraper = new FakeScraper(ReviewSource.Goodreads);
        await using var ctx2 = db.NewContext();
        var svc = new ExternalReviewService(ctx2, new[] { scraper });

        var result = await svc.GetReviewsAsync(bookId, "9780374275631");

        scraper.CallCount.Should().Be(0);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Bob");
    }

    [Fact]
    public async Task RefreshReviewsAsync_overwrites_existing_cache()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                bookId, ReviewSource.Goodreads, "OldReviewer", 2f, "Old", "2020-01-01", 0, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        var scraper = new FakeScraper(ReviewSource.Goodreads,
            new ScrapedReview("NewReviewer", 5f, "Excellent", "2024-12-01", 20));

        await using var ctx2 = db.NewContext();
        var svc = new ExternalReviewService(ctx2, new[] { scraper });

        var result = await svc.RefreshReviewsAsync(bookId, "9780374275631");

        result.Sources[0].Reviews.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("NewReviewer");
    }

    private class FakeScraper : IBookReviewScraper
    {
        private readonly ScrapedReview[] _reviews;
        public int CallCount { get; private set; }
        public ReviewSource Source { get; }

        public FakeScraper(ReviewSource source, params ScrapedReview[] reviews)
        {
            Source = source;
            _reviews = reviews;
        }

        public Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<ScrapedReview>>(_reviews);
        }
    }
}
```

- [ ] **Step 2: Run to confirm compile error**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "ExternalReviewService" -v minimal
```

Expected: compile error — `ExternalReviewService` not found.

- [ ] **Step 3: Create ExternalReviewService**

```csharp
// src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs
using Knovault.Application.Reviews;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Reviews;

public class ExternalReviewService : IExternalReviewService
{
    private readonly KnovaultDbContext _db;
    private readonly IEnumerable<IBookReviewScraper> _scrapers;

    public ExternalReviewService(KnovaultDbContext db, IEnumerable<IBookReviewScraper> scrapers)
    {
        _db = db;
        _scrapers = scrapers;
    }

    public async Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
    {
        var cached = await _db.ExternalReviews
            .Where(r => r.BookId == bookId)
            .ToListAsync(ct);

        if (cached.Count > 0)
            return ToResult(cached);

        if (string.IsNullOrWhiteSpace(isbn))
            return new ReviewsResult(Array.Empty<SourceResult>());

        return await ScrapeAndCacheAsync(bookId, isbn, ct);
    }

    public async Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return new ReviewsResult(Array.Empty<SourceResult>());

        return await ScrapeAndCacheAsync(bookId, isbn, ct);
    }

    private async Task<ReviewsResult> ScrapeAndCacheAsync(Guid bookId, string isbn, CancellationToken ct)
    {
        var fetchedAt = DateTimeOffset.UtcNow;

        foreach (var scraper in _scrapers)
        {
            var existing = await _db.ExternalReviews
                .Where(r => r.BookId == bookId && r.Source == scraper.Source)
                .ToListAsync(ct);
            _db.ExternalReviews.RemoveRange(existing);

            var scraped = await scraper.ScrapeAsync(isbn, ct);
            foreach (var r in scraped)
                _db.ExternalReviews.Add(new ExternalReview(
                    bookId, scraper.Source,
                    r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount,
                    fetchedAt));
        }

        await _db.SaveChangesAsync(ct);

        var all = await _db.ExternalReviews
            .Where(r => r.BookId == bookId)
            .ToListAsync(ct);
        return ToResult(all);
    }

    private static ReviewsResult ToResult(IList<ExternalReview> reviews)
    {
        var groups = reviews
            .GroupBy(r => r.Source)
            .Select(g => new SourceResult(
                g.Key,
                g.Max(r => r.FetchedAt),
                g.Select(r => new ScrapedReview(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                 .ToList()))
            .ToList();
        return new ReviewsResult(groups);
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```
dotnet test tests/Knovault.Infrastructure.Tests --filter "ExternalReviewService" -v minimal
```

Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```
git add src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs tests/Knovault.Infrastructure.Tests/ExternalReviewServiceTests.cs
git commit -m "feat(infra): ExternalReviewService — lazy cache + refresh"
```

---

## Task 7: API — DTOs + ReviewEndpoints

**Files:**
- Create: `src/Knovault.Api/Contracts/ReviewsResultDto.cs`
- Create: `src/Knovault.Api/Endpoints/ReviewEndpoints.cs`
- Create: `tests/Knovault.Api.Tests/ReviewEndpointsTests.cs`

- [ ] **Step 1: Write API tests**

```csharp
// tests/Knovault.Api.Tests/ReviewEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Knovault.Application.Reviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Knovault.Api.Tests;

public class ReviewEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ReviewEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_reviews_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_reviews_returns_empty_sources_when_book_has_no_isbn()
    {
        var client = _factory.WithFakeScraper(new FakeReviewService()).CreateClient();

        // Create a book without ISBN
        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "No ISBN Book", Authors = new() { "Author" } });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_reviews_returns_reviews_from_service()
    {
        var fakeService = new FakeReviewService(
            new SourceResultDto("Goodreads", DateTimeOffset.UtcNow,
                new List<ReviewDto> { new("Alice", 4f, "Good book", "2024-01-01", 5) }));

        var client = _factory.WithFakeScraper(fakeService).CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "ISBN Book", Authors = new() { "Author" }, Isbn = "9780374275631" });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Post_refresh_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync($"/api/books/{Guid.NewGuid()}/reviews/refresh", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private class FakeReviewService : IExternalReviewService
    {
        private readonly SourceResultDto[] _sources;
        public FakeReviewService(params SourceResultDto[] sources) => _sources = sources;

        public Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(ToResult(isbn));

        public Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(ToResult(isbn));

        private ReviewsResult ToResult(string? isbn) =>
            string.IsNullOrWhiteSpace(isbn)
                ? new ReviewsResult(Array.Empty<SourceResult>())
                : new ReviewsResult(_sources.Select(s => new SourceResult(
                    Enum.Parse<Knovault.Domain.Enums.ReviewSource>(s.Source),
                    s.FetchedAt,
                    s.Reviews.Select(r => new ScrapedReview(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount)).ToList()
                )).ToList());
    }
}

public static class TestApiFactoryExtensions
{
    public static TestApiFactory WithFakeScraper(this TestApiFactory factory, IExternalReviewService fakeService)
    {
        // Return a new factory instance that overrides IExternalReviewService.
        // Simplest approach: use WithWebHostBuilder override on the existing factory client.
        // Since TestApiFactory is a fixture, use CreateClient with custom configuration instead.
        // This requires a small change in TestApiFactory — see Task 7 Step 2.
        return factory; // placeholder — see note in Step 2
    }
}
```

> **Note:** The `WithFakeScraper` helper requires modifying `TestApiFactory` to support service overrides. Step 2 adds this capability.

- [ ] **Step 2: Extend TestApiFactory to support service overrides**

In `tests/Knovault.Api.Tests/TestApiFactory.cs`, add a `CreateClientWith` method. Replace the full file:

```csharp
// tests/Knovault.Api.Tests/TestApiFactory.cs
using Knovault.Application.Covers;
using Knovault.Application.Reviews;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Knovault.Api.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"knovault_api_{Guid.NewGuid():N}");
    private Action<IServiceCollection>? _serviceOverrides;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "test.db");
        var coversDir = Path.Combine(_root, "covers");

        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<KnovaultDbContext>));
            services.AddDbContext<KnovaultDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath};Default Timeout=30")
                 .AddInterceptors(new SqliteWalInterceptor()));

            services.RemoveAll(typeof(ICoverStore));
            services.AddSingleton<ICoverStore>(new CoverStorage(coversDir));

            _serviceOverrides?.Invoke(services);
        });
    }

    public HttpClient CreateClientWith(Action<IServiceCollection> overrides)
    {
        _serviceOverrides = overrides;
        return CreateClient();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { /* 忽略清理失敗 */ }
    }
}
```

Now update `ReviewEndpointsTests.cs` to use `CreateClientWith` instead of `WithFakeScraper`. Replace the test file:

```csharp
// tests/Knovault.Api.Tests/ReviewEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Knovault.Application.Reviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Knovault.Api.Tests;

public class ReviewEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ReviewEndpointsTests(TestApiFactory factory) => _factory = factory;

    private HttpClient ClientWithFakeService(FakeReviewService svc) =>
        _factory.CreateClientWith(services =>
        {
            services.RemoveAll<IExternalReviewService>();
            services.AddSingleton<IExternalReviewService>(svc);
        });

    [Fact]
    public async Task Get_reviews_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_reviews_returns_empty_when_book_has_no_isbn()
    {
        var client = ClientWithFakeService(new FakeReviewService());

        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "No ISBN Book", Authors = new() { "Author" } });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_reviews_returns_reviews_from_service()
    {
        var svc = new FakeReviewService(
            new SourceResultDto("Goodreads", DateTimeOffset.UtcNow,
                new List<ReviewDto> { new("Alice", 4f, "Good book", "2024-01-01", 5) }));

        var client = ClientWithFakeService(svc);

        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "ISBN Book", Authors = new() { "Author" }, Isbn = "9780374275631" });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Post_refresh_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync($"/api/books/{Guid.NewGuid()}/reviews/refresh", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    internal class FakeReviewService : IExternalReviewService
    {
        private readonly SourceResultDto[] _sources;
        public FakeReviewService(params SourceResultDto[] sources) => _sources = sources;

        public Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(Build(isbn));

        public Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(Build(isbn));

        private ReviewsResult Build(string? isbn) =>
            string.IsNullOrWhiteSpace(isbn)
                ? new ReviewsResult(Array.Empty<SourceResult>())
                : new ReviewsResult(_sources.Select(s => new SourceResult(
                    Enum.Parse<Knovault.Domain.Enums.ReviewSource>(s.Source),
                    s.FetchedAt,
                    s.Reviews.Select(r => new ScrapedReview(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                             .ToList()
                )).ToList());
    }
}
```

- [ ] **Step 3: Run to confirm compile errors (DTOs missing)**

```
dotnet test tests/Knovault.Api.Tests --filter "ReviewEndpoints" -v minimal
```

Expected: compile errors — `ReviewsResultDto`, `SourceResultDto`, `ReviewDto` not found.

- [ ] **Step 4: Create DTOs**

```csharp
// src/Knovault.Api/Contracts/ReviewsResultDto.cs
namespace Knovault.Api.Contracts;

public record ReviewsResultDto(IReadOnlyList<SourceResultDto> Sources);

public record SourceResultDto(
    string Source,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<ReviewDto> Reviews);

public record ReviewDto(
    string? ReviewerName,
    float? Rating,
    string? ReviewText,
    string? ReviewDate,
    int? HelpfulCount);
```

- [ ] **Step 5: Create ReviewEndpoints**

```csharp
// src/Knovault.Api/Endpoints/ReviewEndpoints.cs
using Knovault.Api.Contracts;
using Knovault.Application.Reviews;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books/{id:guid}/reviews");
        group.MapGet("", GetReviews);
        group.MapPost("refresh", RefreshReviews);
    }

    private static async Task<IResult> GetReviews(
        KnovaultDbContext db,
        IExternalReviewService reviewService,
        Guid id,
        CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Results.NotFound();

        var result = await reviewService.GetReviewsAsync(id, book.Isbn, ct);
        return Results.Ok(ToDto(result));
    }

    private static async Task<IResult> RefreshReviews(
        KnovaultDbContext db,
        IExternalReviewService reviewService,
        Guid id,
        CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Results.NotFound();

        var result = await reviewService.RefreshReviewsAsync(id, book.Isbn, ct);
        return Results.Ok(ToDto(result));
    }

    private static ReviewsResultDto ToDto(ReviewsResult result) =>
        new(result.Sources.Select(s =>
            new SourceResultDto(
                s.Source.ToString(),
                s.FetchedAt,
                s.Reviews.Select(r =>
                    new ReviewDto(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                .ToList()))
            .ToList());
}
```

- [ ] **Step 6: Run tests (still fail — endpoints not registered)**

```
dotnet test tests/Knovault.Api.Tests --filter "ReviewEndpoints" -v minimal
```

Expected: tests fail with 404 (endpoints not wired yet — handled in Task 8).

- [ ] **Step 7: Commit**

```
git add src/Knovault.Api/Contracts/ReviewsResultDto.cs src/Knovault.Api/Endpoints/ReviewEndpoints.cs
git add tests/Knovault.Api.Tests/ReviewEndpointsTests.cs tests/Knovault.Api.Tests/TestApiFactory.cs
git commit -m "feat(api): ReviewEndpoints + ReviewsResultDto"
```

---

## Task 8: DI registration + endpoint wiring

**Files:**
- Modify: `src/Knovault.Api/Program.cs`

- [ ] **Step 1: Add service registrations and endpoint mapping to Program.cs**

Add after `builder.Services.AddScoped<IRelatedBooksStrategy, AttributeRelatedBooksStrategy>();`:

```csharp
builder.Services.AddHttpClient<GoodreadsScraper>(c =>
    c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IBookReviewScraper>(sp => sp.GetRequiredService<GoodreadsScraper>());
builder.Services.AddScoped<IBookReviewScraper, BooksComTwScraper>();
builder.Services.AddScoped<IExternalReviewService, ExternalReviewService>();
```

Add the required `using` statements at the top of Program.cs:

```csharp
using Knovault.Application.Reviews;
using Knovault.Infrastructure.Reviews;
```

Add `app.MapReviewEndpoints();` after `app.MapMetadataEndpoints();`.

Full relevant section of `Program.cs` after the edit:

```csharp
// ... existing usings ...
using Knovault.Application.Reviews;
using Knovault.Infrastructure.Reviews;

// ... existing registrations ...
builder.Services.AddScoped<IRelatedBooksStrategy, AttributeRelatedBooksStrategy>();

builder.Services.AddHttpClient<GoodreadsScraper>(c =>
    c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IBookReviewScraper>(sp => sp.GetRequiredService<GoodreadsScraper>());
builder.Services.AddScoped<IBookReviewScraper, BooksComTwScraper>();
builder.Services.AddScoped<IExternalReviewService, ExternalReviewService>();

// ... existing app configuration ...
app.MapMetadataEndpoints();
app.MapReviewEndpoints();   // ← add this line
app.MapFallbackToFile("index.html");
```

- [ ] **Step 2: Run all backend tests**

```
dotnet test -v minimal
```

Expected: all tests PASS including the 4 new `ReviewEndpoints` tests.

- [ ] **Step 3: Commit**

```
git add src/Knovault.Api/Program.cs
git commit -m "feat(api): wire review DI registrations + endpoints"
```

---

## Task 9: Frontend — types + API client

**Files:**
- Modify: `web/src/api/types.ts`
- Create: `web/src/api/reviews.ts`

- [ ] **Step 1: Add review types to types.ts**

Append to the end of `web/src/api/types.ts`:

```typescript
export interface Review {
  reviewerName: string | null
  rating: number | null
  reviewText: string | null
  reviewDate: string | null
  helpfulCount: number | null
}

export interface ReviewSourceResult {
  source: string
  fetchedAt: string | null
  reviews: Review[]
}

export interface ReviewsResult {
  sources: ReviewSourceResult[]
}
```

- [ ] **Step 2: Create reviews API client**

```typescript
// web/src/api/reviews.ts
import { http } from './http'
import type { ReviewsResult } from './types'

export const reviewsApi = {
  get: (bookId: string): Promise<ReviewsResult> =>
    http.get<ReviewsResult>(`/books/${bookId}/reviews`),

  refresh: (bookId: string): Promise<ReviewsResult> =>
    http.post<ReviewsResult>(`/books/${bookId}/reviews/refresh`),
}
```

- [ ] **Step 3: Confirm TypeScript compiles**

```
cd web && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 4: Commit**

```
git add web/src/api/types.ts web/src/api/reviews.ts
git commit -m "feat(web): review API types + client"
```

---

## Task 10: Frontend — ReviewsSection.vue component

**Files:**
- Create: `web/src/components/ReviewsSection.vue`

- [ ] **Step 1: Create the component**

```vue
<!-- web/src/components/ReviewsSection.vue -->
<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  NSegmented,
  NSpin,
  NAlert,
  NEmpty,
  NButton,
  NSpace,
} from 'naive-ui'
import { reviewsApi } from '@/api/reviews'
import type { ReviewsResult, ReviewSourceResult } from '@/api/types'

const props = defineProps<{ bookId: string; isbn: string | null }>()

const loading = ref(false)
const error = ref<string | null>(null)
const result = ref<ReviewsResult | null>(null)
const refreshing = ref(false)

const platformOptions = [
  { label: 'Goodreads', value: 'Goodreads' },
  { label: '博客來', value: 'BooksComTw' },
]
const selectedPlatform = ref<string>('Goodreads')

const currentSource = computed<ReviewSourceResult | null>(() => {
  if (!result.value) return null
  return result.value.sources.find(s => s.source === selectedPlatform.value) ?? null
})

const isBooksComTw = computed(() => selectedPlatform.value === 'BooksComTw')

async function load() {
  if (!props.isbn) return
  loading.value = true
  error.value = null
  try {
    result.value = await reviewsApi.get(props.bookId)
  } catch (e) {
    error.value = e instanceof Error ? e.message : '載入失敗'
  } finally {
    loading.value = false
  }
}

async function refresh() {
  if (!props.isbn) return
  refreshing.value = true
  error.value = null
  try {
    result.value = await reviewsApi.refresh(props.bookId)
  } catch (e) {
    error.value = e instanceof Error ? e.message : '重新整理失敗'
  } finally {
    refreshing.value = false
  }
}

function formatFetchedAt(iso: string | null): string {
  if (!iso) return '尚未抓取'
  return new Date(iso).toLocaleString('zh-TW', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

function stars(rating: number | null): string {
  if (rating == null) return ''
  const full = Math.round(rating)
  return '★'.repeat(full) + '☆'.repeat(Math.max(0, 5 - full))
}

onMounted(load)
watch(() => props.bookId, load)
</script>

<template>
  <div class="reviews-section">
    <!-- 無 ISBN -->
    <n-empty v-if="!isbn" description="此書無 ISBN，無法查詢外部評論" />

    <template v-else>
      <!-- 平台切換 -->
      <div class="reviews-toolbar">
        <n-segmented
          v-model:value="selectedPlatform"
          :options="platformOptions"
          size="small"
        />
        <n-space v-if="currentSource?.fetchedAt" align="center" class="fetch-meta">
          <span class="fetch-time">資料更新：{{ formatFetchedAt(currentSource.fetchedAt) }}</span>
          <n-button size="tiny" :loading="refreshing" @click="refresh">重新整理</n-button>
        </n-space>
      </div>

      <!-- 載入中 -->
      <n-spin v-if="loading" style="margin-top: 24px" />

      <!-- 錯誤 -->
      <n-alert v-else-if="error" type="error" :title="error" style="margin-top: 12px">
        <n-button size="small" @click="load">重試</n-button>
      </n-alert>

      <!-- 博客來佔位 -->
      <div v-else-if="isBooksComTw" class="placeholder-box">
        <p>博客來評論功能開發中</p>
        <a
          :href="`https://search.books.com.tw/search/query/key/${isbn}/cat/BKall`"
          target="_blank"
          rel="noopener"
        >
          前往博客來查詢 ↗
        </a>
      </div>

      <!-- 無評論 -->
      <n-empty
        v-else-if="!currentSource || currentSource.reviews.length === 0"
        description="尚無評論"
        style="margin-top: 24px"
      >
        <template #extra>
          <n-button size="small" :loading="refreshing" @click="refresh">從網路抓取</n-button>
        </template>
      </n-empty>

      <!-- 評論列表 -->
      <div v-else class="reviews-list">
        <div v-for="(review, i) in currentSource.reviews" :key="i" class="review-card">
          <div class="review-header">
            <span class="reviewer">{{ review.reviewerName ?? '匿名' }}</span>
            <span v-if="review.rating" class="stars">{{ stars(review.rating) }}</span>
            <span class="review-date">{{ review.reviewDate?.slice(0, 10) ?? '' }}</span>
            <span v-if="review.helpfulCount" class="helpful">👍 {{ review.helpfulCount }}</span>
          </div>
          <p v-if="review.reviewText" class="review-text">{{ review.reviewText }}</p>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reviews-section {
  padding-top: 8px;
}
.reviews-toolbar {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}
.fetch-meta {
  font-size: 12px;
  opacity: 0.65;
}
.fetch-time {
  font-size: 12px;
}
.placeholder-box {
  margin-top: 16px;
  padding: 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
  text-align: center;
}
.placeholder-box a {
  color: var(--n-color);
  text-decoration: none;
  opacity: 0.8;
}
.reviews-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.review-card {
  padding: 12px 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
}
.review-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 6px;
  font-size: 13px;
}
.reviewer {
  font-weight: 600;
}
.stars {
  color: #f0a500;
  letter-spacing: 1px;
}
.review-date {
  opacity: 0.55;
}
.helpful {
  opacity: 0.6;
}
.review-text {
  margin: 0;
  font-size: 14px;
  line-height: 1.65;
  white-space: pre-wrap;
  opacity: 0.88;
}
</style>
```

- [ ] **Step 2: Confirm TypeScript compiles**

```
cd web && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 3: Commit**

```
git add web/src/components/ReviewsSection.vue
git commit -m "feat(web): ReviewsSection component"
```

---

## Task 11: Frontend — add 評論 tab to BookDetailView

**Files:**
- Modify: `web/src/views/BookDetailView.vue`

- [ ] **Step 1: Add import + tab to BookDetailView.vue**

At the top of `<script setup>`, add import after the existing `RelatedBooksSection` import:

```typescript
import ReviewsSection from '@/components/ReviewsSection.vue'
```

In the `<template>`, add a 4th `<n-tab-pane>` after the `相關書籍` tab pane (around line 416):

```html
<n-tab-pane name="reviews" tab="評論">
  <reviews-section :book-id="id" :isbn="book.isbn" />
</n-tab-pane>
```

- [ ] **Step 2: Confirm TypeScript compiles**

```
cd web && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 3: Run full backend test suite**

```
cd .. && dotnet test -v minimal
```

Expected: all tests PASS.

- [ ] **Step 4: Commit**

```
git add web/src/views/BookDetailView.vue
git commit -m "feat(web): add 評論 tab to BookDetailView"
```

---

## Self-Review Checklist

- [x] **Spec coverage:**
  - Provider pattern with `IBookReviewScraper` → Task 3 ✓
  - `ExternalReviews` table with all fields → Task 1 + 2 ✓
  - Source stored as string (enum + `HasConversion<string>`) → Task 2 ✓
  - Cache strategy (lazy fetch, batch overwrite, manual refresh) → Task 6 ✓
  - Goodreads scraper flow (ISBN → redirect → workId → GraphQL) → Task 4 ✓
  - `BooksComTwScraper` stub → Task 5 ✓
  - `GET /books/{id}/reviews` + `POST /books/{id}/reviews/refresh` → Task 7 ✓
  - `FetchedAt` returned to frontend → SourceResultDto + ReviewSourceResult ✓
  - Vue tab 評論 → Task 11 ✓
  - NSegmented platform switcher → Task 10 ✓
  - 博客來 placeholder with link → Task 10 ✓
  - No ISBN → "此書無 ISBN" message → Task 10 ✓

- [x] **Type consistency:**
  - `ScrapedReview` record used in `IBookReviewScraper`, `ExternalReviewService`, and `ReviewEndpoints.ToDto()` — matches everywhere
  - `ReviewSource` enum imported correctly in all files
  - `ReviewsResult` / `SourceResult` defined in `IExternalReviewService.cs` and used consistently

- [x] **No placeholders:** All steps contain complete code.
