# LibraryView 標籤篩選修復 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修復 LibraryView 標籤篩選下拉無效的問題，讓使用者可以按標籤篩選書單。

**Architecture:** 後端 `GET /api/books` 加入 Tags eager loading 並在 `BookSummaryDto` 中回傳標籤名稱清單；前端 `BookSummary` 型別新增 `tags` 欄位，並在現有的 `applyFilters()` 客戶端篩選函式中補上 tag 過濾邏輯，與 kind/status 篩選模式一致。

**Tech Stack:** C# 12 / ASP.NET Core Minimal API / EF Core、TypeScript / Vue 3 / Pinia / Vitest

---

## 異動檔案總覽

| 層 | 檔案 | 動作 |
|---|---|---|
| 後端 Contract | `src/Knovault.Api/Contracts/BookSummaryDto.cs` | 修改：加 `Tags` 欄位 |
| 後端 Mapping | `src/Knovault.Api/Mapping/BookMappings.cs` | 修改：`ToSummaryDto()` 補 Tags 映射 |
| 後端 Endpoint | `src/Knovault.Api/Endpoints/BookEndpoints.cs` | 修改：`ListBooks` 加 `Include(b => b.Tags)` |
| 後端 Test | `tests/Knovault.Api.Tests/BookEndpointsTests.cs` | 修改：新增 Tags 回傳測試 |
| 前端 Types | `web/src/api/types.ts` | 修改：`BookSummary` 加 `tags: string[]` |
| 前端 Store | `web/src/stores/books.ts` | 修改：`applyFilters()` 補 tag 邏輯 |
| 前端 Test | `web/src/stores/books.test.ts` | 修改：helper 補 tags、新增 tag 篩選測試 |

---

## Task 1：後端 — Tags 進入書單回應

**Files:**
- Modify: `tests/Knovault.Api.Tests/BookEndpointsTests.cs`
- Modify: `src/Knovault.Api/Contracts/BookSummaryDto.cs`
- Modify: `src/Knovault.Api/Mapping/BookMappings.cs`
- Modify: `src/Knovault.Api/Endpoints/BookEndpoints.cs`

- [ ] **Step 1：在 `BookEndpointsTests.cs` 尾端加入失敗測試**

開啟 `tests/Knovault.Api.Tests/BookEndpointsTests.cs`，在最後一個 `}` 之前加入：

```csharp
[Fact]
public async Task List_books_summary_includes_assigned_tag_names()
{
    var client = _factory.CreateClient();

    var bookResp = await client.PostAsJsonAsync("/api/books",
        new CreatePhysicalBookRequest { Title = "標籤篩選測試書", Authors = new() { "作者A" } });
    var book = (await bookResp.Content.ReadFromJsonAsync<BookDetailDto>())!;

    var tagResp = await client.PostAsJsonAsync("/api/tags",
        new CreateTagRequest { Name = "心理學" });
    var tag = (await tagResp.Content.ReadFromJsonAsync<TagDto>())!;

    await client.PostAsync($"/api/books/{book.Id}/tags/{tag.Id}", null);

    var list = await client.GetFromJsonAsync<PagedResult<BookSummaryDto>>("/api/books");
    var summary = list!.Items.Single(b => b.Id == book.Id);
    summary.Tags.Should().ContainSingle().Which.Should().Be("心理學");
}
```

- [ ] **Step 2：執行測試 — 預期編譯失敗（`BookSummaryDto` 沒有 `Tags`）**

```powershell
dotnet test tests\Knovault.Api.Tests\Knovault.Api.Tests.csproj -v minimal
```

預期輸出包含：`error CS1061: 'BookSummaryDto' does not contain a definition for 'Tags'`

- [ ] **Step 3：`BookSummaryDto.cs` 加入 Tags 欄位**

開啟 `src/Knovault.Api/Contracts/BookSummaryDto.cs`，在 `HasPhysical` 之後加入一行：

```csharp
namespace Knovault.Api.Contracts;

public sealed record BookSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? CoverPath { get; init; }
    public string ReadingStatus { get; init; } = "";
    public bool HasDigital { get; init; }
    public bool HasPhysical { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 4：`BookMappings.cs` 的 `ToSummaryDto()` 補上 Tags 映射**

開啟 `src/Knovault.Api/Mapping/BookMappings.cs`，將 `ToSummaryDto()` 改為：

```csharp
public static BookSummaryDto ToSummaryDto(this Book b) => new()
{
    Id = b.Id,
    Title = b.Title,
    Authors = b.Authors.Select(a => a.Name).ToList(),
    CoverPath = b.CoverPath,
    ReadingStatus = b.ReadingStatus.ToString(),
    HasDigital = b.HasDigital,
    HasPhysical = b.HasPhysical,
    Tags = b.Tags.Select(t => t.Name).ToList()
};
```

- [ ] **Step 5：`BookEndpoints.cs` 的 `ListBooks` 加入 Tags eager loading**

開啟 `src/Knovault.Api/Endpoints/BookEndpoints.cs`，將 `ListBooks` 裡的 query 這一行：

```csharp
var query = db.Books.Include(b => b.Copies).AsQueryable();
```

改為：

```csharp
var query = db.Books.Include(b => b.Copies).Include(b => b.Tags).AsQueryable();
```

- [ ] **Step 6：執行測試 — 預期全部通過**

```powershell
dotnet test tests\Knovault.Api.Tests\Knovault.Api.Tests.csproj -v minimal
```

預期輸出：所有測試 PASSED，包含 `List_books_summary_includes_assigned_tag_names`。

- [ ] **Step 7：Commit**

```powershell
git add src\Knovault.Api\Contracts\BookSummaryDto.cs `
       src\Knovault.Api\Mapping\BookMappings.cs `
       src\Knovault.Api\Endpoints\BookEndpoints.cs `
       tests\Knovault.Api.Tests\BookEndpointsTests.cs
git commit -m "feat: 書單摘要加入標籤欄位，ListBooks 載入 Tags"
```

---

## Task 2：前端型別 — `BookSummary` 加 `tags`

**Files:**
- Modify: `web/src/api/types.ts`
- Modify: `web/src/stores/books.test.ts`

- [ ] **Step 1：`types.ts` 的 `BookSummary` 加入 `tags` 欄位**

開啟 `web/src/api/types.ts`，將 `BookSummary` 介面改為（在 `hasPhysical` 之後加一行）：

```typescript
export interface BookSummary {
  id: string
  title: string
  authors: string[]
  coverPath: string | null
  readingStatus: ReadingStatus
  hasDigital: boolean
  hasPhysical: boolean
  tags: string[]
}
```

- [ ] **Step 2：`books.test.ts` 的 `book()` helper 補上 `tags`**

開啟 `web/src/stores/books.test.ts`，將 `book()` 函式的回傳值加入 `tags`（加在 `hasPhysical` 之後）：

```typescript
function book(p: Partial<BookSummary>): BookSummary {
  return {
    id: p.id ?? crypto.randomUUID(),
    title: p.title ?? '書',
    authors: p.authors ?? [],
    coverPath: p.coverPath ?? null,
    readingStatus: p.readingStatus ?? 'None',
    progressPercent: p.progressPercent ?? null,
    hasDigital: p.hasDigital ?? false,
    hasPhysical: p.hasPhysical ?? false,
    tags: p.tags ?? [],
  }
}
```

> 注意：`progressPercent` 是既有欄位，保持不動。

- [ ] **Step 3：執行前端測試 — 預期現有測試全部通過**

```powershell
npm --prefix web run test
```

預期輸出：所有現有測試（sort、filter kind/status、paginate）全數 PASS，無新失敗。

- [ ] **Step 4：Commit**

```powershell
git add web\src\api\types.ts web\src\stores\books.test.ts
git commit -m "feat(web): BookSummary 加入 tags 欄位"
```

---

## Task 3：前端篩選 — `applyFilters()` 補 tag 邏輯

**Files:**
- Modify: `web/src/stores/books.test.ts`
- Modify: `web/src/stores/books.ts`

- [ ] **Step 1：`books.test.ts` 加入 tag 篩選的失敗測試**

開啟 `web/src/stores/books.test.ts`，在 `describe('applyFilters', ...)` 區塊的最後一個 `it(...)` 之後加入兩個測試：

```typescript
it('filters by tag', () => {
  const tagged = [
    book({ title: 'A', tags: ['科技'] }),
    book({ title: 'B', tags: ['設計'] }),
    book({ title: 'C', tags: ['科技', '設計'] }),
  ]
  const r = applyFilters(tagged, { ...base, tag: '科技' })
  expect(r).toHaveLength(2)
  expect(r.map((b) => b.title).sort()).toEqual(['A', 'C'])
})

it('shows all books when tag filter is null', () => {
  const booksWithTags = [
    book({ title: 'A', tags: ['科技'] }),
    book({ title: 'B', tags: [] }),
  ]
  expect(applyFilters(booksWithTags, { ...base, tag: null })).toHaveLength(2)
})
```

- [ ] **Step 2：執行測試 — 預期新增的兩個 tag 測試失敗**

```powershell
npm --prefix web run test
```

預期輸出：`filters by tag` FAIL（applyFilters 不過濾，回傳 3 筆而非 2 筆）；`shows all books when tag filter is null` PASS（null 的路徑本來就不做事）。

- [ ] **Step 3：`books.ts` 的 `applyFilters()` 補上 tag 過濾**

開啟 `web/src/stores/books.ts`，找到 `applyFilters` 函式中 `if (f.status !== 'all')` 那一行的下方，加入 tag 過濾：

```typescript
export function applyFilters(books: BookSummary[], f: BookFilters): BookSummary[] {
  let result = books.slice()

  if (f.kind === 'digital') result = result.filter((b) => b.hasDigital)
  else if (f.kind === 'physical') result = result.filter((b) => b.hasPhysical)

  if (f.status !== 'all') result = result.filter((b) => b.readingStatus === f.status)

  if (f.tag) result = result.filter((b) => b.tags.includes(f.tag!))

  switch (f.sort) {
    case 'title-desc':
      result.sort((a, b) => b.title.localeCompare(a.title, 'zh-Hant'))
      break
    case 'status':
      result.sort((a, b) => a.readingStatus.localeCompare(b.readingStatus))
      break
    case 'title-asc':
    default:
      result.sort((a, b) => a.title.localeCompare(b.title, 'zh-Hant'))
      break
  }
  return result
}
```

- [ ] **Step 4：執行測試 — 預期全部通過**

```powershell
npm --prefix web run test
```

預期輸出：所有測試 PASS，包含 `filters by tag` 和 `shows all books when tag filter is null`。

- [ ] **Step 5：Commit**

```powershell
git add web\src\stores\books.ts web\src\stores\books.test.ts
git commit -m "feat(web): applyFilters 補上標籤篩選邏輯"
```

---

## 手動驗證（所有 Task 完成後）

1. 啟動後端：`dotnet run --project src/Knovault.Api`
2. 啟動前端：`npm --prefix web run dev`
3. 到「設定」→「標籤管理」新增一個標籤（例如「科幻」）
4. 點進任一本書的詳情頁，貼上「科幻」標籤
5. 回到書單頁面，在標籤下拉選「科幻」
6. 確認書單只顯示有「科幻」標籤的書
7. 改選「全部標籤」，確認所有書都回來
