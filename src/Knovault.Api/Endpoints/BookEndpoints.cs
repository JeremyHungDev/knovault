using Knovault.Api;
using Knovault.Api.Contracts;
using Knovault.Api.Mapping;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books");
        group.MapGet("", ListBooks);
        group.MapGet("/{id:guid}", GetBook);
        group.MapPost("", CreatePhysicalBook);
        group.MapPut("/{id:guid}", UpdateBook);
        group.MapPatch("/{id:guid}/reading", UpdateReading);
        group.MapDelete("/{id:guid}", DeleteBook);
        group.MapGet("/{id:guid}/cover", (Guid id, KnovaultDbContext db, AppPaths paths, CancellationToken ct) =>
            ServeCover(id, db, paths, thumb: false, ct));
        group.MapGet("/{id:guid}/cover/thumb", (Guid id, KnovaultDbContext db, AppPaths paths, CancellationToken ct) =>
            ServeCover(id, db, paths, thumb: true, ct));
    }

    private static async Task<IResult> ListBooks(KnovaultDbContext db, string? search, int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Books.Include(b => b.Copies).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Title.Contains(search));
        var total = await query.CountAsync();
        var books = await query.OrderBy(b => b.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Results.Ok(new PagedResult<BookSummaryDto>(books.Select(b => b.ToSummaryDto()).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetBook(KnovaultDbContext db, Guid id)
    {
        var book = await db.Books
            .Include(b => b.Copies)
            .Include(b => b.Tags)
            .FirstOrDefaultAsync(b => b.Id == id);
        return book is null ? Results.NotFound() : Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> CreatePhysicalBook(KnovaultDbContext db, CreatePhysicalBookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.Problem(title: "書名為必填", statusCode: StatusCodes.Status400BadRequest);
        var book = new Book(req.Title);
        book.SetAuthors(req.Authors);
        book.UpdateMetadata(req.Title, null, req.Language, req.Publisher, req.PublishedDate, req.Description, req.Isbn);
        book.AddCopy(new PhysicalCopy(req.Location));
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return Results.Created($"/api/books/{book.Id}", book.ToDetailDto());
    }

    private static async Task<IResult> UpdateBook(KnovaultDbContext db, Guid id, UpdateBookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.Problem(title: "書名為必填", statusCode: StatusCodes.Status400BadRequest);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        book.SetAuthors(req.Authors);
        book.UpdateMetadata(req.Title, req.Subtitle, req.Language, req.Publisher, req.PublishedDate, req.Description, req.Isbn);
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> UpdateReading(KnovaultDbContext db, Guid id, UpdateReadingRequest req)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        if (!Enum.TryParse<ReadingStatus>(req.ReadingStatus, out var status))
            return Results.Problem(title: "閱讀狀態無效", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            book.SetReadingStatus(status);
            book.SetProgress(ReadingProgress.Create(req.Percent, req.CurrentPage, req.TotalPages));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        await db.SaveChangesAsync();
        return Results.Ok(book.ToDetailDto());
    }

    private static async Task<IResult> DeleteBook(KnovaultDbContext db, Guid id)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return Results.NotFound();
        db.Books.Remove(book); // copies 由 cascade 刪除；硬碟書檔與封面檔不動
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ServeCover(Guid id, KnovaultDbContext db, AppPaths paths, bool thumb, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book?.CoverPath is null) return Results.NotFound();
        var file = thumb
            ? Path.Combine(paths.CoversDir, $"{id:N}_thumb.jpg")
            : Path.Combine(paths.CoversDir, book.CoverPath);
        if (!File.Exists(file)) return Results.NotFound();
        var contentType = thumb ? "image/jpeg" : "application/octet-stream";
        return Results.File(file, contentType);
    }
}
