using System.Text.Json;
using Knovault.Application.Covers;
using Knovault.Application.Files;
using Knovault.Application.Library;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Library;

public sealed class LibraryScanService : ILibraryScanService
{
    private static readonly string[] SupportedExtensions = { ".epub", ".pdf" };
    private const int BatchSize = 20;

    private readonly KnovaultDbContext _db;
    private readonly BookParsingService _parser;
    private readonly IFileHasher _hasher;
    private readonly ICoverStore _coverStore;

    public LibraryScanService(KnovaultDbContext db, BookParsingService parser,
        IFileHasher hasher, ICoverStore coverStore)
    {
        _db = db;
        _parser = parser;
        _hasher = hasher;
        _coverStore = coverStore;
    }

    public Task<ScanReport> ScanAsync(CancellationToken ct = default) => ScanAsync(null, ct);

    public async Task<ScanReport> ScanAsync(Func<ScanProgress, Task>? onProgress, CancellationToken ct = default)
    {
        var report = new ScanReport();
        var folders = await _db.LibraryFolders.Where(f => f.Enabled).ToListAsync(ct);
        var seenCopyIds = new HashSet<Guid>();

        // 先收集所有檔案以計算總數（供進度回報）
        var work = new List<(LibraryFolder Folder, string File)>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path))
            {
                report.Failures.Add(new ScanFailure(folder.Path, "資料夾無法存取"));
                continue;
            }
            foreach (var file in EnumerateBookFiles(folder.Path)) work.Add((folder, file));
        }

        var total = work.Count;
        var processed = 0;
        var sinceLastSave = 0;

        foreach (var (folder, file) in work)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var hash = await ComputeHashWithRetryAsync(file, ct);
                var existing = await _db.Set<DigitalCopy>().FirstOrDefaultAsync(c => c.FileHash == hash, ct);

                if (existing is not null)
                {
                    seenCopyIds.Add(existing.Id);
                    if (existing.FilePath != file) { existing.UpdatePath(file); report.Updated++; }
                    else report.Skipped++;
                }
                else
                {
                    var copy = await CreateBookFromFileAsync(file, hash, folder.Id, ct);
                    seenCopyIds.Add(copy.Id);
                    report.Added++;
                    if (++sinceLastSave >= BatchSize) { await _db.SaveChangesAsync(ct); sinceLastSave = 0; }
                }
            }
            catch (IOException)
            {
                report.Failures.Add(new ScanFailure(file, "檔案使用中"));
            }

            processed++;
            if (onProgress is not null) await onProgress(new ScanProgress(processed, total, file));
        }

        foreach (var folder in folders.Where(f => Directory.Exists(f.Path))) folder.MarkScanned();
        await _db.SaveChangesAsync(ct);

        // 遺失偵測：屬於啟用資料夾、未在本次掃描見到、且尚未標記遺失的數位版本
        var folderIds = folders.Select(f => f.Id).ToList();
        var tracked = await _db.Set<DigitalCopy>()
            .Where(c => c.LibraryFolderId != null && folderIds.Contains(c.LibraryFolderId.Value) && !c.IsMissing)
            .ToListAsync(ct);
        foreach (var copy in tracked.Where(c => !seenCopyIds.Contains(c.Id)))
        {
            copy.MarkMissing();
            report.MarkedMissing++;
        }
        await _db.SaveChangesAsync(ct);

        return report;
    }

    private static IEnumerable<string> EnumerateBookFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

    private async Task<string> ComputeHashWithRetryAsync(string file, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await _hasher.ComputeQuickHashAsync(file, ct); }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(500, ct);
            }
        }
    }

    private async Task<DigitalCopy> CreateBookFromFileAsync(string file, string hash, Guid folderId, CancellationToken ct)
    {
        var (meta, failed) = await _parser.ParseAsync(file, ct);
        var info = new FileInfo(file);
        var format = string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)
            ? BookFormat.Pdf : BookFormat.Epub;

        var title = string.IsNullOrWhiteSpace(meta.Title) ? Path.GetFileNameWithoutExtension(file) : meta.Title!;
        var book = new Book(title);
        book.SetAuthors(meta.Authors);
        book.UpdateMetadata(title, null, meta.Language, meta.Publisher, meta.PublishedDate, meta.Description, meta.Isbn);
        var copy = new DigitalCopy(file, format, info.Length, hash,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), folderId);
        if (failed) copy.MarkParseFailed();
        if (meta.Toc.Count > 0) copy.SetToc(JsonSerializer.Serialize(meta.Toc));
        book.AddCopy(copy);

        if (meta.CoverImage is { Length: > 0 })
        {
            var coverPath = await _coverStore.SaveAsync(book.Id, meta.CoverImage, meta.CoverContentType, ct);
            book.SetCoverPath(coverPath);
        }

        _db.Books.Add(book);
        return copy;
    }
}
