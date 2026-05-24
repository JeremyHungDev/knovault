using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

// 形式重構後 copy 僅代表數位檔（實體已改為 Book.IsPhysical 旗標）：只保留刪除與下載。
public static class CopyEndpoints
{
    public static void MapCopyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/copies/{copyId:guid}", DeleteCopy);
        app.MapGet("/api/copies/{copyId:guid}/file", DownloadFile);
    }

    private static async Task<IResult> DeleteCopy(KnovaultDbContext db, Guid copyId)
    {
        var copy = await db.Set<DigitalCopy>().FirstOrDefaultAsync(c => c.Id == copyId);
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
