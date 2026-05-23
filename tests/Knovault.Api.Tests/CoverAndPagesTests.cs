using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Knovault.Api.Tests;

public class CoverAndPagesTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public CoverAndPagesTests(TestApiFactory factory) => _factory = factory;

    private static byte[] Png()
    {
        using var img = new Image<Rgba32>(4, 4);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Upload_cover_sets_cover_path_and_serves()
    {
        var client = _factory.CreateClient();
        var book = (await (await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "封面書" })).Content.ReadFromJsonAsync<BookDetailDto>())!;

        using var form = new MultipartFormDataContent();
        var img = new ByteArrayContent(Png());
        img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(img, "file", "cover.png");

        var resp = await client.PostAsync($"/api/books/{book.Id}/cover", form);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<BookDetailDto>();
        detail!.CoverPath.Should().NotBeNull();

        (await client.GetAsync($"/api/books/{book.Id}/cover")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/books/{book.Id}/cover/thumb")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_non_image_returns_400()
    {
        var client = _factory.CreateClient();
        var book = (await (await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "壞檔書" })).Content.ReadFromJsonAsync<BookDetailDto>())!;

        using var form = new MultipartFormDataContent();
        var txt = new ByteArrayContent(new byte[] { 1, 2, 3 });
        txt.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(txt, "file", "x.txt");

        (await client.PostAsync($"/api/books/{book.Id}/cover", form))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_total_pages_sets_progress_total()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "頁數書", TotalPages = 300 });
        var detail = await resp.Content.ReadFromJsonAsync<BookDetailDto>();
        detail!.TotalPages.Should().Be(300);
    }
}
