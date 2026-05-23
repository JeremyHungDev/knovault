using Knovault.Api.Contracts;
using Knovault.Api.Mapping;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class CopyEndpoints
{
    public static void MapCopyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/books/{bookId:guid}/copies", AddPhysicalCopy);
        app.MapPut("/api/copies/{copyId:guid}", UpdateCopy);
        app.MapDelete("/api/copies/{copyId:guid}", DeleteCopy);
        app.MapGet("/api/copies/{copyId:guid}/file", DownloadFile);
    }

    private static async Task<IResult> AddPhysicalCopy(KnovaultDbContext db, Guid bookId, AddPhysicalCopyRequest req)
    {
        var book = await db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == bookId);
        if (book is null) return Results.NotFound();
        var copy = new PhysicalCopy(req.Location);
        if (!string.IsNullOrWhiteSpace(req.Notes)) copy.SetNotes(req.Notes);
        book.AddCopy(copy);
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> UpdateCopy(KnovaultDbContext db, Guid copyId, UpdateCopyRequest req)
    {
        var copy = await db.Set<BookCopy>().FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null) return Results.NotFound();
        if (copy is PhysicalCopy p) p.UpdateLocation(req.Location);
        copy.SetNotes(req.Notes);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteCopy(KnovaultDbContext db, Guid copyId)
    {
        var copy = await db.Set<BookCopy>().FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null) return Results.NotFound();
        db.Remove(copy);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadFile(KnovaultDbContext db, Guid copyId, CancellationToken ct)
    {
        var copy = await db.Set<DigitalCopy>().FirstOrDefaultAsync(c => c.Id == copyId, ct);
        if (copy is null) return Results.NotFound();
        if (!File.Exists(copy.FilePath)) return Results.NotFound();
        var name = Path.GetFileName(copy.FilePath);
        return Results.File(copy.FilePath, "application/octet-stream", name);
    }
}
