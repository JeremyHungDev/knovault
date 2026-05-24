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
        list!.Total.Should().Be(1);
        list.Items.Should().ContainSingle(b => b.Title == "實體測試書" && b.HasPhysical);

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
}
