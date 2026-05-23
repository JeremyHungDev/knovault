using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class TagEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public TagEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_list_assign_and_unassign_tag()
    {
        var client = _factory.CreateClient();

        var tagResp = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "哲學" });
        tagResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = (await tagResp.Content.ReadFromJsonAsync<TagDto>())!;

        var bookResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest { Title = "標籤書" });
        var book = (await bookResp.Content.ReadFromJsonAsync<BookDetailDto>())!;

        (await client.PostAsync($"/api/books/{book.Id}/tags/{tag.Id}", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterAssign = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterAssign!.Tags.Should().ContainSingle().Which.Should().Be("哲學");

        var tags = await client.GetFromJsonAsync<List<TagDto>>("/api/tags");
        tags!.Single(t => t.Id == tag.Id).BookCount.Should().Be(1);

        (await client.DeleteAsync($"/api/books/{book.Id}/tags/{tag.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}"))!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_tag_name_returns_409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "重複" });
        (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = "重複" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
