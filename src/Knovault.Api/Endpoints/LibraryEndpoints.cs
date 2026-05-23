using Knovault.Api.Contracts;
using Knovault.Application.Library;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class LibraryEndpoints
{
    private static readonly SemaphoreSlim ScanGate = new(1, 1);

    public static void MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/authors", ListAuthors);
        app.MapGet("/api/library/folders", ListFolders);
        app.MapPost("/api/library/folders", AddFolder);
        app.MapDelete("/api/library/folders/{id:guid}", DeleteFolder);
        app.MapPost("/api/library/scan", Scan);
        app.MapGet("/api/library/scan/stream", ScanStream);
    }

    private static async Task<IResult> ListAuthors(KnovaultDbContext db)
    {
        // client-side 分組（個人規模可接受）
        var books = await db.Books.ToListAsync();
        var facets = books
            .SelectMany(b => b.Authors.Select(a => a.Name))
            .GroupBy(n => n)
            .Select(g => new AuthorFacetDto(g.Key, g.Count()))
            .OrderBy(a => a.Name)
            .ToList();
        return Results.Ok(facets);
    }

    private static async Task<IResult> ListFolders(KnovaultDbContext db)
    {
        var folders = await db.LibraryFolders.OrderBy(f => f.Path).ToListAsync();
        return Results.Ok(folders.Select(f =>
            new FolderDto(f.Id, f.Path, f.DisplayName, f.Enabled, f.LastScannedAt)).ToList());
    }

    private static async Task<IResult> AddFolder(KnovaultDbContext db, CreateFolderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
            return Results.Problem(title: "路徑為必填", statusCode: StatusCodes.Status400BadRequest);
        if (!Directory.Exists(req.Path))
            return Results.Problem(title: "資料夾不存在", statusCode: StatusCodes.Status400BadRequest);
        if (await db.LibraryFolders.AnyAsync(f => f.Path == req.Path))
            return Results.Conflict(new { message = "資料夾已存在" });
        var folder = new LibraryFolder(req.Path, req.DisplayName);
        db.LibraryFolders.Add(folder);
        await db.SaveChangesAsync();
        return Results.Created($"/api/library/folders/{folder.Id}",
            new FolderDto(folder.Id, folder.Path, folder.DisplayName, folder.Enabled, folder.LastScannedAt));
    }

    private static async Task<IResult> DeleteFolder(KnovaultDbContext db, Guid id)
    {
        var folder = await db.LibraryFolders.FirstOrDefaultAsync(f => f.Id == id);
        if (folder is null) return Results.NotFound();
        // 預設保留書，把該資料夾的數位版本標遺失
        var copies = await db.Set<DigitalCopy>().Where(c => c.LibraryFolderId == id && !c.IsMissing).ToListAsync();
        foreach (var c in copies) c.MarkMissing();
        db.LibraryFolders.Remove(folder);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Scan(ILibraryScanService scanner, CancellationToken ct)
    {
        if (!ScanGate.Wait(0)) return Results.Conflict(new { message = "掃描進行中" });
        try
        {
            var report = await scanner.ScanAsync(ct);
            return Results.Ok(ToDto(report));
        }
        finally { ScanGate.Release(); }
    }

    private static async Task ScanStream(HttpContext ctx, ILibraryScanService scanner)
    {
        if (!ScanGate.Wait(0))
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }
        try
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";

            var report = await scanner.ScanAsync(
                async p => await WriteSse(ctx, "progress", System.Text.Json.JsonSerializer.Serialize(p)),
                ctx.RequestAborted);

            await WriteSse(ctx, "done", System.Text.Json.JsonSerializer.Serialize(ToDto(report)));
        }
        finally { ScanGate.Release(); }
    }

    private static async Task WriteSse(HttpContext ctx, string evt, string data)
    {
        await ctx.Response.WriteAsync($"event: {evt}\ndata: {data}\n\n");
        await ctx.Response.Body.FlushAsync();
    }

    private static ScanReportDto ToDto(ScanReport report) => new(
        report.Added, report.Updated, report.Skipped, report.MarkedMissing,
        report.Failures.Select(f => $"{f.FilePath}: {f.Reason}").ToList());
}
