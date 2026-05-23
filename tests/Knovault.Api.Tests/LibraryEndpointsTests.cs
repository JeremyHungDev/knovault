using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Xunit;

namespace Knovault.Api.Tests;

public class LibraryEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public LibraryEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Add_list_and_delete_folder()
    {
        var client = _factory.CreateClient();
        var dir = Path.Combine(Path.GetTempPath(), $"libfolder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var add = await client.PostAsJsonAsync("/api/library/folders",
                new CreateFolderRequest { Path = dir, DisplayName = "測試" });
            add.StatusCode.Should().Be(HttpStatusCode.Created);
            var folder = (await add.Content.ReadFromJsonAsync<FolderDto>())!;

            var list = await client.GetFromJsonAsync<List<FolderDto>>("/api/library/folders");
            list!.Should().ContainSingle(f => f.Id == folder.Id);

            (await client.DeleteAsync($"/api/library/folders/{folder.Id}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Scan_returns_report()
    {
        var client = _factory.CreateClient();
        var report = await client.PostAsync("/api/library/scan", null);
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await report.Content.ReadFromJsonAsync<ScanReportDto>();
        dto.Should().NotBeNull();
    }

    [Fact]
    public async Task Authors_facet_lists_counts()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "甲", Authors = new() { "作者X" } });
        await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "乙", Authors = new() { "作者X" } });

        var authors = await client.GetFromJsonAsync<List<AuthorFacetDto>>("/api/authors");
        authors!.Single(a => a.Name == "作者X").BookCount.Should().Be(2);
    }
}
