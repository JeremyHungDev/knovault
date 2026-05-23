using Knovault.Api.Contracts;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", ListTags);
        app.MapPost("/api/tags", CreateTag);
        app.MapDelete("/api/tags/{id:guid}", DeleteTag);
        app.MapPost("/api/books/{bookId:guid}/tags/{tagId:guid}", AssignTag);
        app.MapDelete("/api/books/{bookId:guid}/tags/{tagId:guid}", UnassignTag);
    }

    private static async Task<IResult> ListTags(KnovaultDbContext db)
    {
        var tags = await db.Tags.ToListAsync();
        // client-side 計數（個人規模可接受，避免 skip-navigation 查詢轉譯問題）
        var books = await db.Books.Include(b => b.Tags).ToListAsync();
        var counts = books.SelectMany(b => b.Tags).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.Count());
        var dtos = tags.Select(t => new TagDto(t.Id, t.Name, t.Color, counts.GetValueOrDefault(t.Id))).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> CreateTag(KnovaultDbContext db, CreateTagRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.Problem(title: "標籤名為必填", statusCode: StatusCodes.Status400BadRequest);
        if (await db.Tags.AnyAsync(t => t.Name == req.Name.Trim()))
            return Results.Conflict(new { message = "標籤已存在" });
        var tag = new Tag(req.Name, req.Color);
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return Results.Created($"/api/tags/{tag.Id}", new TagDto(tag.Id, tag.Name, tag.Color, 0));
    }

    private static async Task<IResult> DeleteTag(KnovaultDbContext db, Guid id)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null) return Results.NotFound();
        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AssignTag(KnovaultDbContext db, Guid bookId, Guid tagId)
    {
        var book = await db.Books.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == bookId);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId);
        if (book is null || tag is null) return Results.NotFound();
        book.AddTag(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> UnassignTag(KnovaultDbContext db, Guid bookId, Guid tagId)
    {
        var book = await db.Books.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == bookId);
        var tag = book?.Tags.FirstOrDefault(t => t.Id == tagId);
        if (book is null || tag is null) return Results.NotFound();
        book.RemoveTag(tag);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
