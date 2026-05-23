# Knovault 書庫核心 — P4c 主程式體驗實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 完成「雙擊執行的桌面 App」體驗：SSE 即時掃描進度、啟動時找空閒埠、自動開瀏覽器、託管 Vue 靜態檔（含 SPA fallback）。

**Architecture:** SSE 端點「就地」執行掃描並串流進度（請求範圍內，DbContext scope 有效，免背景排程複雜度），用 static gate 防併發。找埠/開瀏覽器在 `Program` 啟動時做、以環境守衛（Testing 不執行）。靜態檔以 `UseStaticFiles` + `MapFallbackToFile("index.html")` 託管，先放佔位 index.html（P5 換成 Vue build）。

**Tech Stack:** ASP.NET Core 8、System.Text.Json、System.Net.Sockets、xUnit + FluentAssertions。

> **執行前置**：續在 `feat/library-core-p4`。commit 風格：簡短中文一行 + `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>` trailer。
> **設計依據**：[spec](../specs/2026-05-23-knovault-library-core-design.md) §3、§6、§8（埠號被占用）。

---

## 檔案結構（本計畫產出/修改）

```
src/Knovault.Application/Library/ScanProgress.cs              ← 新
src/Knovault.Application/Library/ILibraryScanService.cs       ← 加 onProgress 多載
src/Knovault.Infrastructure/Library/LibraryScanService.cs    ← 重構：收集檔案算總數 + 回報進度
src/Knovault.Api/Hosting/NetworkPorts.cs                     ← 找空閒埠
src/Knovault.Api/Hosting/BrowserLauncher.cs                  ← 開瀏覽器（best-effort）
src/Knovault.Api/Endpoints/LibraryEndpoints.cs               ← 加 SSE 串流 + 併發 gate
src/Knovault.Api/Program.cs                                  ← UseUrls(找埠)、靜態檔、SPA fallback、開瀏覽器
src/Knovault.Api/wwwroot/index.html                          ← 佔位（P5 換 Vue build）
tests/Knovault.Infrastructure.Tests/LibraryScanServiceTests.cs ← 加進度回報測試
tests/Knovault.Api.Tests/  NetworkPortsTests.cs, StaticFilesTests.cs, ScanStreamTests.cs
```

---

## Task 1: 掃描進度回報（重構掃描服務）

**Files:** Create `ScanProgress.cs`; modify `ILibraryScanService.cs`, `LibraryScanService.cs`; add test to `LibraryScanServiceTests.cs`

- [ ] **Step 1: 建立 `ScanProgress.cs`**

Create `src/Knovault.Application/Library/ScanProgress.cs`:
```csharp
namespace Knovault.Application.Library;

public sealed record ScanProgress(int Processed, int Total, string? CurrentFile);
```

- [ ] **Step 2: 修改 `ILibraryScanService.cs`（加 onProgress 多載）**
```csharp
namespace Knovault.Application.Library;

public interface ILibraryScanService
{
    Task<ScanReport> ScanAsync(CancellationToken ct = default);
    Task<ScanReport> ScanAsync(Func<ScanProgress, Task>? onProgress, CancellationToken ct = default);
}
```

- [ ] **Step 3: 重構 `LibraryScanService.ScanAsync`**

把 `LibraryScanService.cs` 的 `ScanAsync` 換成下列兩個方法（其餘私有方法 `EnumerateBookFiles`/`ComputeHashWithRetryAsync`/`CreateBookFromFileAsync` 不變）：
```csharp
    public Task<ScanReport> ScanAsync(CancellationToken ct = default) => ScanAsync(null, ct);

    public async Task<ScanReport> ScanAsync(Func<ScanProgress, Task>? onProgress, CancellationToken ct = default)
    {
        var report = new ScanReport();
        var folders = await _db.LibraryFolders.Where(f => f.Enabled).ToListAsync(ct);
        var seenCopyIds = new HashSet<Guid>();

        // 先收集所有檔案以計算總數（供進度回報）
        var work = new List<(Domain.Entities.LibraryFolder Folder, string File)>();
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
                var existing = await _db.Set<Domain.Entities.DigitalCopy>().FirstOrDefaultAsync(c => c.FileHash == hash, ct);
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

        var folderIds = folders.Select(f => f.Id).ToList();
        var tracked = await _db.Set<Domain.Entities.DigitalCopy>()
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
```
> 既有 `using Knovault.Domain.Entities;` 已在檔案頂端，故上面可改用簡名 `LibraryFolder`/`DigitalCopy`；此處用完整名稱以避免歧義，兩者皆可。

- [ ] **Step 4: 加進度回報測試到 `LibraryScanServiceTests.cs`**

在 `LibraryScanServiceTests` 類別內新增（沿用既有 `_db`/`_libraryDir`/`_coversDir`/`NewService`/`AddFolderAsync`/`PlaceEpub`）：
```csharp
    [Fact]
    public async Task Scan_reports_progress_per_file()
    {
        await AddFolderAsync();
        PlaceEpub(_libraryDir, "p1.epub");
        PlaceEpub(_libraryDir, "p2.epub");

        var progresses = new List<Knovault.Application.Library.ScanProgress>();
        await using var ctx = _db.NewContext();
        await NewService(ctx).ScanAsync(p => { progresses.Add(p); return Task.CompletedTask; });

        progresses.Should().HaveCount(2);
        progresses.Last().Processed.Should().Be(2);
        progresses.Last().Total.Should().Be(2);
    }
```

- [ ] **Step 5: 跑掃描測試（含既有 4 個確認無回歸）**

Run: `dotnet test tests/Knovault.Infrastructure.Tests --filter LibraryScanServiceTests`
Expected: PASS（5 tests）。

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "掃描服務加入進度回報" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: SSE 掃描串流端點

**Files:** modify `Endpoints/LibraryEndpoints.cs`; modify `Program.cs`（已有 MapLibraryEndpoints，無需改）；Test `ScanStreamTests.cs`

- [ ] **Step 1: 寫整合測試 `ScanStreamTests.cs`**

Create `tests/Knovault.Api.Tests/ScanStreamTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class ScanStreamTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ScanStreamTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Scan_stream_emits_done_event()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/library/scan/stream");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("event: done");
    }
}
```

- [ ] **Step 2: 跑測試確認失敗** — `dotnet test tests/Knovault.Api.Tests --filter ScanStreamTests`，預期失敗（404）。

- [ ] **Step 3: 修改 `LibraryEndpoints.cs`：加 SSE 端點 + 併發 gate + 共用 ToDto**

在 `LibraryEndpoints` 類別頂端加：
```csharp
    private static readonly SemaphoreSlim ScanGate = new(1, 1);
```
在 `MapLibraryEndpoints` 內加：
```csharp
        app.MapGet("/api/library/scan/stream", ScanStream);
```
把 `Scan`（POST）改為用 gate，並新增 `ScanStream`/`WriteSse`/`ToDto`：
```csharp
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
```
> 確保檔案頂端已有 `using Microsoft.AspNetCore.Http;`（Minimal API 通常隱含）；若編譯抱怨 `HttpContext`/`StatusCodes`/`WriteAsync`，加 `using Microsoft.AspNetCore.Http;`。

- [ ] **Step 4: 跑測試確認通過** — 預期 PASS（1 test）。

- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "加入 SSE 掃描進度串流端點" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: 找空閒埠

**Files:** Create `Hosting/NetworkPorts.cs`; Test `NetworkPortsTests.cs`

- [ ] **Step 1: 寫失敗測試 `NetworkPortsTests.cs`**

Create `tests/Knovault.Api.Tests/NetworkPortsTests.cs`:
```csharp
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Knovault.Api.Hosting;
using Xunit;

namespace Knovault.Api.Tests;

public class NetworkPortsTests
{
    [Fact]
    public void FindFreePort_returns_preferred_when_free()
    {
        // 取一個目前可用的埠作為偏好
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var preferred = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        NetworkPorts.FindFreePort(preferred).Should().Be(preferred);
    }

    [Fact]
    public void FindFreePort_returns_alternative_when_preferred_taken()
    {
        var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var taken = ((IPEndPoint)occupied.LocalEndpoint).Port;
        try
        {
            var result = NetworkPorts.FindFreePort(taken);
            result.Should().NotBe(taken);
            result.Should().BeGreaterThan(0);
        }
        finally { occupied.Stop(); }
    }
}
```

- [ ] **Step 2: 跑測試確認失敗** — `dotnet test tests/Knovault.Api.Tests --filter NetworkPortsTests`，預期失敗。

- [ ] **Step 3: 實作 `Hosting/NetworkPorts.cs`**
```csharp
using System.Net;
using System.Net.Sockets;

namespace Knovault.Api.Hosting;

public static class NetworkPorts
{
    /// <summary>偏好埠可用就用它，否則回傳一個系統指派的空閒埠。</summary>
    public static int FindFreePort(int preferred)
    {
        if (IsFree(preferred)) return preferred;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: 跑測試確認通過** — 預期 PASS（2 tests）。

- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "加入找空閒埠工具" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: 託管 Vue 靜態檔 + SPA fallback + 開瀏覽器 + 套用找埠

**Files:** Create `wwwroot/index.html`, `Hosting/BrowserLauncher.cs`; modify `Program.cs`; Test `StaticFilesTests.cs`

- [ ] **Step 1: 建立佔位 `wwwroot/index.html`**

Create `src/Knovault.Api/wwwroot/index.html`:
```html
<!DOCTYPE html>
<html lang="zh-Hant">
<head><meta charset="utf-8"><title>Knovault</title></head>
<body>
  <h1>Knovault</h1>
  <p>API 已啟動。前端（Vue）將於 P5 取代此頁。</p>
</body>
</html>
```

- [ ] **Step 2: 建立 `Hosting/BrowserLauncher.cs`**
```csharp
using System.Diagnostics;

namespace Knovault.Api.Hosting;

public static class BrowserLauncher
{
    /// <summary>盡力而為地開啟預設瀏覽器；失敗不拋例外。</summary>
    public static void TryOpen(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // 無瀏覽器/無桌面環境時忽略
        }
    }
}
```

- [ ] **Step 3: 寫靜態檔整合測試 `StaticFilesTests.cs`**

Create `tests/Knovault.Api.Tests/StaticFilesTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class StaticFilesTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public StaticFilesTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_serves_spa_index()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Knovault");
    }

    [Fact]
    public async Task Unknown_spa_route_falls_back_to_index()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/books/123");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Knovault");
    }
}
```

- [ ] **Step 4: 修改 `Program.cs`：找埠 + UseUrls（守衛 Testing）、靜態檔、SPA fallback、開瀏覽器**

在 `var paths = new AppPaths();` 之後、`AddDbContext` 之前（或任意 builder 設定處）加入找埠與 URL（守衛 Testing）：
```csharp
var isTesting = builder.Environment.IsEnvironment("Testing");
var serverUrl = "";
if (!isTesting)
{
    var port = Knovault.Api.Hosting.NetworkPorts.FindFreePort(5279);
    serverUrl = $"http://localhost:{port}";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
```
在 `var app = builder.Build();` 之後、`app.UseExceptionHandler();` 附近，加入靜態檔中介軟體（在端點對應之前）：
```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
```
在所有 `app.Map*Endpoints();` 之後、`app.Run();` 之前，加入 SPA fallback 與開瀏覽器：
```csharp
app.MapFallbackToFile("index.html");

if (!isTesting)
{
    app.Lifetime.ApplicationStarted.Register(() => Knovault.Api.Hosting.BrowserLauncher.TryOpen(serverUrl));
}
```

- [ ] **Step 5: 跑測試確認通過** — `dotnet test tests/Knovault.Api.Tests --filter StaticFilesTests`，預期 PASS（2 tests）。
> 註：若 `MapFallbackToFile` 對 `/` 未命中，補 `app.MapGet("/", () => Results.Redirect("/index.html"));` 或確認 `UseDefaultFiles` 在 `UseStaticFiles` 之前。

- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "託管 Vue 靜態檔 + SPA fallback + 找埠/開瀏覽器" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: 全量驗證 + 整個 P4 squash + 合併 dev + 推

- [ ] **Step 1: 全量測試**
Run: `dotnet test`
Expected: 全綠。Domain 25 + Infrastructure (26 + 1 = 27) + Api (14 + 1 + 2 + 2 = 19) = 71 passed。

- [ ] **Step 2: 全量建置**
Run: `dotnet build`
Expected: `Build succeeded`，0 警告 0 錯誤。

- [ ] **Step 3: 把整個 P4（a+b+c）squash 成 2 個 commit（計畫 + 實作）**

於 `feat/library-core-p4`：
```bash
git reset --soft dev
git restore --staged .
git add docs/superpowers/plans/2026-05-23-knovault-library-core-04a-api-foundation.md docs/superpowers/plans/2026-05-23-knovault-library-core-04b-api-endpoints.md docs/superpowers/plans/2026-05-23-knovault-library-core-04c-host-experience.md
git commit -m "加入 P4 API 計畫" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
git add -A
git commit -m "實作 P4：完整 REST API、ISBN 查詢、SSE 進度、找埠/開瀏覽器、靜態檔託管" -m "Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

- [ ] **Step 4: 合併回 dev、驗證、刪本地分支、推 dev + 更新遠端 feat 備份**
```bash
git checkout dev
git merge feat/library-core-p4
dotnet test
git branch -d feat/library-core-p4
git push origin dev
git push origin --delete feat/library-core-p4
```
> 最後一行刪除遠端的 P4 備份分支（已合併進 dev，不再需要）。若偏好保留，可改用 `git push --force-with-lease origin feat/library-core-p4` 更新它。

---

## 完成定義 (Definition of Done)

- SSE `GET /api/library/scan/stream`：就地執行掃描、串流 `progress`/`done` 事件、static gate 防併發。
- 掃描服務支援進度回報多載（既有同步 API 不變、無回歸）。
- 找空閒埠（偏好 5279，被占則改派）；啟動時 `UseUrls(0.0.0.0:port)`、開瀏覽器（守衛 Testing）。
- 靜態檔託管 + SPA fallback（佔位 index.html，P5 換 Vue build）。
- `dotnet test` 全綠（約 71）、`dotnet build` 0 警告 0 錯誤。
- 整個 P4 squash 後合併 `dev` 並推；清理遠端 feat 備份分支。

## 範圍外（P5）

- Vue3 + Naive UI 前端四頁面 + PWA（取代佔位 index.html）。
