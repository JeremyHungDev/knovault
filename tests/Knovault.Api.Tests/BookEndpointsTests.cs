using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class BookEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public BookEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_physical_book_then_list_and_get()
    {
        var client = _factory.CreateClient();

        var create = new CreatePhysicalBookRequest
        {
            Title = "實體測試書",
            Authors = new() { "某作者" },
            Isbn = "9789999999999"
        };
        var createResp = await client.PostAsJsonAsync("/api/books", create);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();
        created!.Title.Should().Be("實體測試書");
        created.IsPhysical.Should().BeTrue();
        created.Copies.Should().BeEmpty();

        var list = await client.GetFromJsonAsync<PagedResult<BookSummaryDto>>("/api/books");
        list!.Total.Should().BeGreaterThanOrEqualTo(1);
        list.Items.Should().Contain(b => b.Title == "實體測試書" && b.HasPhysical);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{created.Id}");
        detail!.Isbn.Should().Be("9789999999999");
    }

    [Fact]
    public async Task Get_missing_book_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_book_with_blank_title_returns_400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_physical_sets_location_and_returns_detail()
    {
        var client = _factory.CreateClient();

        // 先建一本實體書
        var createResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest
        {
            Title = "DDD 紅書",
            Authors = new() { "Eric Evans" }
        });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        // PATCH physical
        var patchResp = await client.PatchAsJsonAsync(
            $"/api/books/{book!.Id}/physical",
            new UpdatePhysicalRequest { IsPhysical = true, Location = "書房 B 櫃", Notes = "精裝本" });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await patchResp.Content.ReadFromJsonAsync<BookDetailDto>();
        updated!.IsPhysical.Should().BeTrue();
        updated.PhysicalLocation.Should().Be("書房 B 櫃");
        updated.PhysicalNotes.Should().Be("精裝本");
    }

    [Fact]
    public async Task Patch_physical_false_clears_fields()
    {
        var client = _factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest
        {
            Title = "測試書",
            Authors = new() { "作者" }
        });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        // 先設位置
        await client.PatchAsJsonAsync(
            $"/api/books/{book!.Id}/physical",
            new UpdatePhysicalRequest { IsPhysical = true, Location = "書房", Notes = "備註" });

        // 再取消
        var clearResp = await client.PatchAsJsonAsync(
            $"/api/books/{book.Id}/physical",
            new UpdatePhysicalRequest { IsPhysical = false });
        clearResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var cleared = await clearResp.Content.ReadFromJsonAsync<BookDetailDto>();
        cleared!.IsPhysical.Should().BeFalse();
        cleared.PhysicalLocation.Should().BeNull();
        cleared.PhysicalNotes.Should().BeNull();
    }

    [Fact]
    public async Task Patch_physical_missing_book_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.PatchAsJsonAsync(
            $"/api/books/{Guid.NewGuid()}/physical",
            new UpdatePhysicalRequest { IsPhysical = true });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
