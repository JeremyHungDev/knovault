using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knovault.Api.Tests;

public class CopyEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public CopyEndpointsTests(TestApiFactory factory) => _factory = factory;

    // 數位版本由掃描產生；測試直接以 DbContext 植入一本含數位檔的書。
    private async Task<(Guid bookId, Guid copyId, string file)> SeedDigitalAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"copytest_{Guid.NewGuid():N}.epub");
        await File.WriteAllTextAsync(file, "dummy");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnovaultDbContext>();
        var book = new Book("數位書");
        var copy = new DigitalCopy(file, BookFormat.Epub, 5, "h", DateTimeOffset.UtcNow, null);
        book.AddCopy(copy);
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return (book.Id, copy.Id, file);
    }

    [Fact]
    public async Task Download_digital_copy_returns_file()
    {
        var (_, copyId, file) = await SeedDigitalAsync();
        try
        {
            var resp = await _factory.CreateClient().GetAsync($"/api/copies/{copyId}/file");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Delete_digital_copy_removes_it()
    {
        var (bookId, copyId, file) = await SeedDigitalAsync();
        try
        {
            var client = _factory.CreateClient();
            (await client.DeleteAsync($"/api/copies/{copyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{bookId}");
            detail!.Copies.Should().BeEmpty();
        }
        finally { File.Delete(file); }
    }
}
