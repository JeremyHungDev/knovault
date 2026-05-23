using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class BookEditEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public BookEditEndpointsTests(TestApiFactory factory) => _factory = factory;

    private static async Task<BookDetailDto> CreateBookAsync(HttpClient client, string title)
    {
        var resp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = title });
        return (await resp.Content.ReadFromJsonAsync<BookDetailDto>())!;
    }

    [Fact]
    public async Task Update_book_changes_metadata()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "原書名");

        var resp = await client.PutAsJsonAsync($"/api/books/{book.Id}", new UpdateBookRequest
        {
            Title = "新書名",
            Authors = new() { "新作者" },
            Publisher = "新出版社"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.Title.Should().Be("新書名");
        detail.Authors.Should().ContainSingle().Which.Should().Be("新作者");
        detail.Publisher.Should().Be("新出版社");
    }

    [Fact]
    public async Task Patch_reading_updates_status_and_progress()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "進度書");

        var resp = await client.PatchAsJsonAsync($"/api/books/{book.Id}/reading", new UpdateReadingRequest
        {
            ReadingStatus = "Reading",
            Percent = 55
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.ReadingStatus.Should().Be("Reading");
        detail.ProgressPercent.Should().Be(55);
    }

    [Fact]
    public async Task Delete_book_removes_it()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client, "待刪書");

        (await client.DeleteAsync($"/api/books/{book.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/books/{book.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
