using Knovault.Api.Contracts;
using Knovault.Application.Metadata;

namespace Knovault.Api.Endpoints;

public static class MetadataEndpoints
{
    public static void MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/metadata/isbn/{isbn}", async (IIsbnMetadataProvider provider, string isbn, CancellationToken ct) =>
        {
            var meta = await provider.LookupAsync(isbn, ct);
            if (meta is null) return Results.NotFound();
            return Results.Ok(new IsbnMetadataDto
            {
                Title = meta.Title,
                Authors = meta.Authors,
                Publisher = meta.Publisher,
                PublishedDate = meta.PublishedDate,
                Isbn = meta.Isbn,
                PageCount = meta.PageCount,
                CoverUrl = meta.CoverUrl
            });
        });
    }
}
