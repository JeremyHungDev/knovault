using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class CopyEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public CopyEndpointsTests(TestApiFactory factory) => _factory = factory;

    private static async Task<BookDetailDto> CreateBookAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "版本書", Location = "A" }))
            .Content.ReadFromJsonAsync<BookDetailDto>())!;

    [Fact]
    public async Task Add_physical_copy_to_existing_book()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client);

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/copies",
            new AddPhysicalCopyRequest { Location = "B 櫃" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        detail!.Copies.Count(c => c.Type == "physical").Should().Be(2);
    }

    [Fact]
    public async Task Update_and_delete_copy()
    {
        var client = _factory.CreateClient();
        var book = await CreateBookAsync(client);
        var copyId = book.Copies.Single(c => c.Type == "physical").Id;

        var put = await client.PutAsJsonAsync($"/api/copies/{copyId}",
            new UpdateCopyRequest { Location = "新位置" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterPut = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterPut!.Copies.Single().Location.Should().Be("新位置");

        var del = await client.DeleteAsync($"/api/copies/{copyId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDel = await client.GetFromJsonAsync<BookDetailDto>($"/api/books/{book.Id}");
        afterDel!.Copies.Should().BeEmpty();
    }
}
