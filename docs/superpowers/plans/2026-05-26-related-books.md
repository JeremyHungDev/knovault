# Related Books 相關書籍 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在書籍詳情頁加入「相關書籍」Tab，以 Tags／Authors／Publisher 屬性計分自動找出相關書籍並以封面卡片列呈現。

**Architecture:** Application 層定義 `IRelatedBooksStrategy` 介面；Infrastructure 層實作 `AttributeRelatedBooksStrategy`（Tags ×2、Authors ×3、Publisher ×1 記憶體計分）；Api 層新增 `GET /api/books/{id}/related`；前端 BookDetailView 重構為 NTabs，新增 `RelatedBooksSection.vue` 元件。

**Tech Stack:** C# 12 / ASP.NET Core Minimal API / EF Core / SQLite、TypeScript / Vue 3 / Naive UI / Vitest

---

## 異動檔案總覽

| 層 | 檔案 | 動作 |
|---|---|---|
| Application | `src/Knovault.Application/Related/IRelatedBooksStrategy.cs` | 新增 |
| Infrastructure | `src/Knovault.Infrastructure/Related/AttributeRelatedBooksStrategy.cs` | 新增 |
| Infrastructure Test | `tests/Knovault.Infrastructure.Tests/AttributeRelatedBooksStrategyTests.cs` | 新增 |
| Api Endpoint | `src/Knovault.Api/Endpoints/BookEndpoints.cs` | 修改 |
| Api DI | `src/Knovault.Api/Program.cs` | 修改 |
| Api Test | `tests/Knovault.Api.Tests/BookEndpointsTests.cs` | 修改 |
| 前端 API | `web/src/api/books.ts` | 修改 |
| 前端元件 | `web/src/components/RelatedBooksSection.vue` | 新增 |
| 前端元件 Test | `web/src/components/RelatedBooksSection.test.ts` | 新增 |
| 前端頁面 | `web/src/views/BookDetailView.vue` | 修改 |

---

## Task 1：Application 層 — IRelatedBooksStrategy 介面

**Files:**
- Create: `src/Knovault.Application/Related/IRelatedBooksStrategy.cs`

- [ ] **Step 1：建立介面檔案**

建立目錄並建立檔案 `src/Knovault.Application/Related/IRelatedBooksStrategy.cs`，內容如下：

```csharp
using Knovault.Domain.Entities;

namespace Knovault.Application.Related;

public interface IRelatedBooksStrategy
{
    Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source,
        int limit,
        CancellationToken ct = default);
}
```

- [ ] **Step 2：確認編譯通過**

```powershell
dotnet build src\Knovault.Application\Knovault.Application.csproj
```

預期：`Build succeeded`

- [ ] **Step 3：Commit**

```powershell
git add src\Knovault.Application\Related\IRelatedBooksStrategy.cs
git commit -m "feat: 新增 IRelatedBooksStrategy 介面（Application 層）"
```

---

## Task 2：Infrastructure 層 — AttributeRelatedBooksStrategy + 單元測試

**Files:**
- Create: `tests/Knovault.Infrastructure.Tests/AttributeRelatedBooksStrategyTests.cs`
- Create: `src/Knovault.Infrastructure/Related/AttributeRelatedBooksStrategy.cs`

- [ ] **Step 1：寫入失敗測試**

建立 `tests/Knovault.Infrastructure.Tests/AttributeRelatedBooksStrategyTests.cs`：

```csharp
using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Related;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Tests;

public class AttributeRelatedBooksStrategyTests
{
    [Fact]
    public async Task Returns_books_ordered_by_score_shared_tags()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tagDesign = new Tag("設計");
            var tagDev = new Tag("開發");
            ctx.Tags.AddRange(tagDesign, tagDev);

            var source = new Book("Clean Code");
            source.AddTag(tagDesign);
            source.AddTag(tagDev);

            var twoTags = new Book("Clean Architecture"); // 2 tags → score 4
            twoTags.AddTag(tagDesign);
            twoTags.AddTag(tagDev);

            var oneTag = new Book("Design Patterns"); // 1 tag → score 2
            oneTag.AddTag(tagDesign);

            var noMatch = new Book("Cooking Book"); // score 0 → excluded

            ctx.Books.AddRange(source, twoTags, oneTag, noMatch);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().HaveCount(2);
            result[0].Title.Should().Be("Clean Architecture"); // score 4
            result[1].Title.Should().Be("Design Patterns");    // score 2
        }
    }

    [Fact]
    public async Task Author_weight_3_beats_single_tag_weight_2()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Clean Code");
            source.SetAuthors(new[] { "Robert Martin" });
            source.AddTag(tag);

            var sameAuthor = new Book("Clean Architecture"); // author → score 3
            sameAuthor.SetAuthors(new[] { "Robert Martin" });

            var sameTag = new Book("Refactoring"); // 1 tag → score 2
            sameTag.AddTag(tag);

            ctx.Books.AddRange(source, sameAuthor, sameTag);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().HaveCount(2);
            result[0].Title.Should().Be("Clean Architecture"); // score 3
            result[1].Title.Should().Be("Refactoring");        // score 2
        }
    }

    [Fact]
    public async Task Publisher_match_scores_1_point()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var source = new Book("Book A");
            source.UpdateMetadata("Book A", null, null, "Pearson", null, null, null);

            var samePublisher = new Book("Book B");
            samePublisher.UpdateMetadata("Book B", null, null, "Pearson", null, null, null);

            var diffPublisher = new Book("Book C");
            diffPublisher.UpdateMetadata("Book C", null, null, "O'Reilly", null, null, null);

            ctx.Books.AddRange(source, samePublisher, diffPublisher);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().ContainSingle(b => b.Title == "Book B");
            result.Should().NotContain(b => b.Title == "Book C");
        }
    }

    [Fact]
    public async Task Does_not_return_source_book_itself()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Source Book");
            source.AddTag(tag);

            var other = new Book("Other Book");
            other.AddTag(tag);

            ctx.Books.AddRange(source, other);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().NotContain(b => b.Id == sourceId);
            result.Should().ContainSingle(b => b.Title == "Other Book");
        }
    }

    [Fact]
    public async Task Respects_limit_parameter()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var tag = new Tag("科技");
            ctx.Tags.Add(tag);

            var source = new Book("Source");
            source.AddTag(tag);

            for (var i = 0; i < 5; i++)
            {
                var b = new Book($"Book {i}");
                b.AddTag(tag);
                ctx.Books.Add(b);
            }
            ctx.Books.Add(source);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 3);

            result.Should().HaveCount(3);
        }
    }

    [Fact]
    public async Task Returns_empty_when_no_match()
    {
        using var testDb = new SqliteTestDb();
        Guid sourceId;

        await using (var ctx = testDb.NewContext())
        {
            var source = new Book("Lone Book");
            source.SetAuthors(new[] { "Author A" });

            var other = new Book("Other Lone");
            other.SetAuthors(new[] { "Author B" });

            ctx.Books.AddRange(source, other);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using (var ctx = testDb.NewContext())
        {
            var source = await ctx.Books
                .Include(b => b.Tags)
                .SingleAsync(b => b.Id == sourceId);

            var strategy = new AttributeRelatedBooksStrategy(ctx);
            var result = await strategy.GetRelatedAsync(source, limit: 10);

            result.Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 2：執行測試 — 預期編譯失敗（找不到 AttributeRelatedBooksStrategy）**

```powershell
dotnet test tests\Knovault.Infrastructure.Tests\Knovault.Infrastructure.Tests.csproj -v minimal
```

預期：`error CS0246: The type or namespace name 'AttributeRelatedBooksStrategy' could not be found`

- [ ] **Step 3：建立 AttributeRelatedBooksStrategy**

建立 `src/Knovault.Infrastructure/Related/AttributeRelatedBooksStrategy.cs`：

```csharp
using Knovault.Application.Related;
using Knovault.Domain.Entities;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Related;

public sealed class AttributeRelatedBooksStrategy(KnovaultDbContext db) : IRelatedBooksStrategy
{
    public async Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source,
        int limit,
        CancellationToken ct = default)
    {
        var sourceTags = source.Tags.Select(t => t.Id).ToHashSet();
        var sourceAuthors = source.Authors
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = await db.Books
            .Include(b => b.Tags)
            .Where(b => b.Id != source.Id)
            .ToListAsync(ct);

        return candidates
            .Select(b => new
            {
                Book  = b,
                Score = b.Tags.Count(t => sourceTags.Contains(t.Id)) * 2
                      + b.Authors.Count(a => sourceAuthors.Contains(a.Name)) * 3
                      + (b.Publisher != null && b.Publisher == source.Publisher ? 1 : 0)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.Book)
            .ToList();
    }
}
```

> 注意：`Authors` 是 EF Core OwnsMany 設定，查詢 Books 時會自動載入，**不需要** `Include(b => b.Authors)`。

- [ ] **Step 4：執行測試 — 預期全部通過**

```powershell
dotnet test tests\Knovault.Infrastructure.Tests\Knovault.Infrastructure.Tests.csproj -v minimal
```

預期：6 個新測試全部 PASS，既有測試不受影響。

- [ ] **Step 5：Commit**

```powershell
git add src\Knovault.Infrastructure\Related\AttributeRelatedBooksStrategy.cs `
        tests\Knovault.Infrastructure.Tests\AttributeRelatedBooksStrategyTests.cs
git commit -m "feat: AttributeRelatedBooksStrategy 屬性計分策略 + 單元測試"
```

---

## Task 3：Api 層 — endpoint + DI 註冊 + 整合測試

**Files:**
- Modify: `tests/Knovault.Api.Tests/BookEndpointsTests.cs`
- Modify: `src/Knovault.Api/Endpoints/BookEndpoints.cs`
- Modify: `src/Knovault.Api/Program.cs`

- [ ] **Step 1：在 BookEndpointsTests.cs 加入失敗測試**

開啟 `tests/Knovault.Api.Tests/BookEndpointsTests.cs`，在最後一個 `}` 之前加入：

```csharp
[Fact]
public async Task Related_returns_books_sharing_same_author()
{
    var client = _factory.CreateClient();

    var sourceResp = await client.PostAsJsonAsync("/api/books",
        new CreatePhysicalBookRequest { Title = "Clean Code", Authors = new() { "Robert Martin" } });
    var source = (await sourceResp.Content.ReadFromJsonAsync<BookDetailDto>())!;

    await client.PostAsJsonAsync("/api/books",
        new CreatePhysicalBookRequest { Title = "Clean Architecture", Authors = new() { "Robert Martin" } });

    await client.PostAsJsonAsync("/api/books",
        new CreatePhysicalBookRequest { Title = "Cooking Book", Authors = new() { "Chef A" } });

    var result = await client.GetFromJsonAsync<BookSummaryDto[]>(
        $"/api/books/{source.Id}/related");

    result.Should().ContainSingle(b => b.Title == "Clean Architecture");
    result.Should().NotContain(b => b.Title == "Cooking Book");
    result.Should().NotContain(b => b.Id == source.Id);
}

[Fact]
public async Task Related_returns_404_for_missing_book()
{
    var client = _factory.CreateClient();
    var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}/related");
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Related_returns_empty_array_when_no_matching_books()
{
    var client = _factory.CreateClient();

    var createResp = await client.PostAsJsonAsync("/api/books",
        new CreatePhysicalBookRequest { Title = "Lone Book", Authors = new() { "Solo Author" } });
    var book = (await createResp.Content.ReadFromJsonAsync<BookDetailDto>())!;

    var result = await client.GetFromJsonAsync<BookSummaryDto[]>(
        $"/api/books/{book.Id}/related");

    result.Should().BeEmpty();
}
```

- [ ] **Step 2：執行測試 — 預期失敗（路由不存在 → 404）**

```powershell
dotnet test tests\Knovault.Api.Tests\Knovault.Api.Tests.csproj --filter "Related" -v minimal
```

預期：3 個測試均 FAIL（`Related_returns_404_for_missing_book` 會 PASS 因為路由不存在本來就回 404，另外兩個 FAIL）。

- [ ] **Step 3：在 Program.cs 註冊 DI**

開啟 `src/Knovault.Api/Program.cs`，在 `builder.Services.AddProblemDetails();` 之前加入：

```csharp
using Knovault.Application.Related;
using Knovault.Infrastructure.Related;
```

（加在檔案最上方 using 區塊）

然後在 `builder.Services.AddProblemDetails();` 這一行之後加入：

```csharp
builder.Services.AddScoped<IRelatedBooksStrategy, AttributeRelatedBooksStrategy>();
```

- [ ] **Step 4：在 BookEndpoints.cs 加入 related endpoint**

開啟 `src/Knovault.Api/Endpoints/BookEndpoints.cs`，在 `MapBookEndpoints` 方法的 `group.MapPost("/{id:guid}/cover", UploadCover)...` 那一行之後加入：

```csharp
group.MapGet("/{id:guid}/related", GetRelated);
```

然後在檔案最後一個 `}` 之前加入靜態方法：

```csharp
private static async Task<IResult> GetRelated(
    KnovaultDbContext db,
    IRelatedBooksStrategy strategy,
    Guid id,
    int limit = 10,
    CancellationToken ct = default)
{
    limit = Math.Clamp(limit, 1, 50);

    var book = await db.Books
        .Include(b => b.Tags)
        .FirstOrDefaultAsync(b => b.Id == id, ct);

    if (book is null) return Results.NotFound();

    var related = await strategy.GetRelatedAsync(book, limit, ct);
    return Results.Ok(related.Select(b => b.ToSummaryDto()).ToList());
}
```

在 `BookEndpoints.cs` 的 using 區塊頂部確認有以下 using（若無則加入）：

```csharp
using Knovault.Application.Related;
```

- [ ] **Step 5：執行測試 — 預期全部通過**

```powershell
dotnet test tests\Knovault.Api.Tests\Knovault.Api.Tests.csproj -v minimal
```

預期：所有測試 PASS，包含 3 個新的 `Related_*` 測試。

- [ ] **Step 6：Commit**

```powershell
git add src\Knovault.Api\Endpoints\BookEndpoints.cs `
        src\Knovault.Api\Program.cs `
        tests\Knovault.Api.Tests\BookEndpointsTests.cs
git commit -m "feat: GET /api/books/{id}/related endpoint + DI 註冊 + 整合測試"
```

---

## Task 4：前端 — booksApi.related() + RelatedBooksSection.vue + 元件測試

**Files:**
- Modify: `web/src/api/books.ts`
- Create: `web/src/components/RelatedBooksSection.test.ts`
- Create: `web/src/components/RelatedBooksSection.vue`

- [ ] **Step 1：booksApi 加入 related()**

開啟 `web/src/api/books.ts`，在 `uploadCover` 方法之後加入：

```typescript
related: (id: string, limit = 10): Promise<BookSummary[]> =>
  http.get<BookSummary[]>(`/books/${id}/related?limit=${limit}`),
```

- [ ] **Step 2：建立失敗元件測試**

建立 `web/src/components/RelatedBooksSection.test.ts`：

```typescript
import { describe, it, expect, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import RelatedBooksSection from './RelatedBooksSection.vue'
import type { BookSummary } from '@/api/types'

const pushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: pushMock }),
}))

const relatedMock = vi.fn()
vi.mock('@/api/books', () => ({
  booksApi: { related: relatedMock },
}))

vi.mock('naive-ui', async () => {
  const mod = await vi.importActual<Record<string, unknown>>('naive-ui')
  return {
    ...mod,
    NSpin: { name: 'NSpin', template: '<div class="n-spin" :show="$attrs.show"><slot /></div>', inheritAttrs: false },
    NEmpty: { name: 'NEmpty', template: '<div class="n-empty"></div>', props: ['description'] },
    NEllipsis: { name: 'NEllipsis', template: '<span><slot /></span>' },
  }
})

function makeBook(title: string, id = crypto.randomUUID()): BookSummary {
  return {
    id,
    title,
    authors: ['Author A'],
    coverPath: null,
    readingStatus: 'None',
    hasDigital: false,
    hasPhysical: true,
    tags: [],
  }
}

afterEach(() => {
  relatedMock.mockReset()
  pushMock.mockClear()
})

describe('RelatedBooksSection', () => {
  it('renders cover cards after data loads', async () => {
    relatedMock.mockResolvedValue([makeBook('Clean Architecture'), makeBook('Design Patterns')])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    expect(w.findAll('.related-card')).toHaveLength(2)
    expect(w.text()).toContain('Clean Architecture')
    expect(w.text()).toContain('Design Patterns')
  })

  it('shows empty state when no related books', async () => {
    relatedMock.mockResolvedValue([])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    expect(w.find('.n-empty').exists()).toBe(true)
    expect(w.findAll('.related-card')).toHaveLength(0)
  })

  it('navigates to book detail on card click', async () => {
    const book = makeBook('Clean Architecture', 'arch-id')
    relatedMock.mockResolvedValue([book])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    await w.find('.related-card').trigger('click')
    expect(pushMock).toHaveBeenCalledWith('/books/arch-id')
  })

  it('calls booksApi.related with correct bookId', async () => {
    relatedMock.mockResolvedValue([])

    mount(RelatedBooksSection, { props: { bookId: 'my-book-id' } })
    await flushPromises()

    expect(relatedMock).toHaveBeenCalledWith('my-book-id')
  })
})
```

- [ ] **Step 3：執行測試 — 預期失敗（元件不存在）**

```powershell
npm --prefix web run test -- RelatedBooksSection
```

預期：`Cannot find module './RelatedBooksSection.vue'`

- [ ] **Step 4：實作 RelatedBooksSection.vue**

建立 `web/src/components/RelatedBooksSection.vue`：

```vue
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { NEllipsis, NEmpty, NSpin } from 'naive-ui'
import { booksApi } from '@/api/books'
import { coverThumbUrl } from '@/api/http'
import type { BookSummary } from '@/api/types'

const props = defineProps<{ bookId: string }>()

const router = useRouter()
const loading = ref(true)
const books = ref<BookSummary[]>([])

onMounted(async () => {
  try {
    books.value = await booksApi.related(props.bookId)
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <n-spin :show="loading">
    <n-empty v-if="!loading && books.length === 0" description="暫無相關書籍" />
    <div v-else-if="!loading" class="related-row">
      <div
        v-for="book in books"
        :key="book.id"
        class="related-card"
        role="button"
        tabindex="0"
        @click="router.push(`/books/${book.id}`)"
        @keydown.enter="router.push(`/books/${book.id}`)"
      >
        <img
          v-if="book.coverPath"
          :src="coverThumbUrl(book.id)"
          :alt="book.title"
          class="related-cover"
        />
        <div v-else class="related-cover related-placeholder">
          {{ book.title.slice(0, 1) }}
        </div>
        <n-ellipsis class="related-title">{{ book.title }}</n-ellipsis>
      </div>
    </div>
  </n-spin>
</template>

<style scoped>
.related-row {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
}
.related-card {
  flex: 0 0 120px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 6px;
  outline: none;
}
.related-card:focus-visible {
  outline: 2px solid var(--n-color-target, #18a058);
  border-radius: 8px;
}
.related-cover {
  width: 120px;
  aspect-ratio: 3/4;
  object-fit: cover;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.12);
}
.related-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 36px;
  color: rgba(128, 128, 128, 0.5);
}
.related-title {
  font-size: 12px;
  width: 120px;
}
</style>
```

- [ ] **Step 5：執行測試 — 預期全部通過**

```powershell
npm --prefix web run test -- RelatedBooksSection
```

預期：4 個測試全部 PASS。

- [ ] **Step 6：執行全部前端測試確認無迴歸**

```powershell
npm --prefix web run test
```

預期：所有測試 PASS。

- [ ] **Step 7：Commit**

```powershell
git add web\src\api\books.ts `
        web\src\components\RelatedBooksSection.vue `
        web\src\components\RelatedBooksSection.test.ts
git commit -m "feat(web): RelatedBooksSection 元件 + booksApi.related() + 測試"
```

---

## Task 5：前端 — BookDetailView 重構為 NTabs

**Files:**
- Modify: `web/src/views/BookDetailView.vue`

- [ ] **Step 1：更新 script 區塊的 naive-ui import**

開啟 `web/src/views/BookDetailView.vue`，在 `<script setup>` 的 naive-ui import 行，加入 `NTabs`、`NTabPane`，並移除不再使用的 `NDivider`：

將：
```typescript
import {
  NButton,
  NSpace,
  NTag,
  NSpin,
  NAlert,
  NSelect,
  NDivider,
  NList,
  NListItem,
  NThing,
  NPopconfirm,
  NInput,
  NModal,
  NForm,
  NFormItem,
  NEmpty,
  useMessage,
} from 'naive-ui'
```

改為：
```typescript
import {
  NButton,
  NSpace,
  NTag,
  NSpin,
  NAlert,
  NSelect,
  NTabs,
  NTabPane,
  NList,
  NListItem,
  NThing,
  NPopconfirm,
  NInput,
  NModal,
  NForm,
  NFormItem,
  NEmpty,
  useMessage,
} from 'naive-ui'
```

- [ ] **Step 2：加入 RelatedBooksSection import**

在 `script setup` 區塊的其他 import 之後，加入：

```typescript
import RelatedBooksSection from '@/components/RelatedBooksSection.vue'
```

- [ ] **Step 3：重構 template — 將簡介與版本改為 NTabs**

在 `<template>` 區塊中，找到以下段落並替換：

**舊的（從 `<template v-if="book.description">` 到 `</n-modal>` 之前的版本 modal 結束）：**

```html
      <template v-if="book.description">
        <n-divider>簡介</n-divider>
        <p class="description">{{ book.description }}</p>
      </template>

      <!-- 版本面板 -->
      <n-divider>版本</n-divider>
      <div class="versions-toolbar">
        <n-button
          v-if="!book.isPhysical"
          size="small"
          @click="openAddPhysical"
        >
          ＋ 新增實體版本
        </n-button>
      </div>

      <n-empty
        v-if="!book.isPhysical && book.copies.length === 0"
        size="small"
        description="尚無版本，可新增實體版本或掃描書庫資料夾"
      />

      <n-list v-else bordered>
        <!-- 實體列 -->
        <n-list-item v-if="book.isPhysical">
          <n-thing>
            <template #header>🏠 實體書</template>
            <template #description>
              <div v-if="book.physicalLocation" class="copy-meta">
                📍 {{ book.physicalLocation }}
              </div>
              <div v-if="book.physicalNotes" class="copy-meta">
                📝 {{ book.physicalNotes }}
              </div>
            </template>
          </n-thing>
          <template #suffix>
            <n-button size="small" @click="openEditPhysical">📝 編輯</n-button>
          </template>
        </n-list-item>

        <!-- 數位檔列 -->
        <n-list-item v-for="c in book.copies" :key="c.id">
          <n-thing>
            <template #header>
              {{ copyFormatLabel(c) }}
              <span class="dim">{{ formatFileSize(c.fileSizeBytes) }}</span>
              <n-tag v-if="c.isMissing" type="error" size="small" :bordered="false">
                ⚠ 檔案遺失
              </n-tag>
              <n-tag v-if="c.parseFailed" type="warning" size="small" :bordered="false">
                ⚠ 解析失敗
              </n-tag>
            </template>
          </n-thing>
          <template #suffix>
            <n-space>
              <n-button v-if="!c.isMissing" size="small" @click="download(c)">
                📥 下載
              </n-button>
              <n-popconfirm @positive-click="removeCopy(c)">
                <template #trigger>
                  <n-button size="small" quaternary type="error">移除</n-button>
                </template>
                確定移除此數位檔紀錄？（不刪硬碟檔）
              </n-popconfirm>
            </n-space>
          </template>
        </n-list-item>
      </n-list>
```

**替換為：**

```html
      <n-tabs type="line" animated class="detail-tabs">
        <n-tab-pane name="description" tab="簡介">
          <p v-if="book.description" class="description">{{ book.description }}</p>
          <n-empty v-else size="small" description="本書暫無簡介" />
        </n-tab-pane>

        <n-tab-pane name="copies" tab="版本">
          <div class="versions-toolbar">
            <n-button
              v-if="!book.isPhysical"
              size="small"
              @click="openAddPhysical"
            >
              ＋ 新增實體版本
            </n-button>
          </div>

          <n-empty
            v-if="!book.isPhysical && book.copies.length === 0"
            size="small"
            description="尚無版本，可新增實體版本或掃描書庫資料夾"
          />

          <n-list v-else bordered>
            <!-- 實體列 -->
            <n-list-item v-if="book.isPhysical">
              <n-thing>
                <template #header>🏠 實體書</template>
                <template #description>
                  <div v-if="book.physicalLocation" class="copy-meta">
                    📍 {{ book.physicalLocation }}
                  </div>
                  <div v-if="book.physicalNotes" class="copy-meta">
                    📝 {{ book.physicalNotes }}
                  </div>
                </template>
              </n-thing>
              <template #suffix>
                <n-button size="small" @click="openEditPhysical">📝 編輯</n-button>
              </template>
            </n-list-item>

            <!-- 數位檔列 -->
            <n-list-item v-for="c in book.copies" :key="c.id">
              <n-thing>
                <template #header>
                  {{ copyFormatLabel(c) }}
                  <span class="dim">{{ formatFileSize(c.fileSizeBytes) }}</span>
                  <n-tag v-if="c.isMissing" type="error" size="small" :bordered="false">
                    ⚠ 檔案遺失
                  </n-tag>
                  <n-tag v-if="c.parseFailed" type="warning" size="small" :bordered="false">
                    ⚠ 解析失敗
                  </n-tag>
                </template>
              </n-thing>
              <template #suffix>
                <n-space>
                  <n-button v-if="!c.isMissing" size="small" @click="download(c)">
                    📥 下載
                  </n-button>
                  <n-popconfirm @positive-click="removeCopy(c)">
                    <template #trigger>
                      <n-button size="small" quaternary type="error">移除</n-button>
                    </template>
                    確定移除此數位檔紀錄？（不刪硬碟檔）
                  </n-popconfirm>
                </n-space>
              </template>
            </n-list-item>
          </n-list>
        </n-tab-pane>

        <n-tab-pane name="related" tab="相關書籍">
          <related-books-section :book-id="id" />
        </n-tab-pane>
      </n-tabs>
```

- [ ] **Step 4：在 `<style scoped>` 加入 tabs 外距**

在 `web/src/views/BookDetailView.vue` 的 `<style scoped>` 區塊加入：

```css
.detail-tabs {
  margin-top: 24px;
}
```

同時移除 `.description` 樣式中若有的 `margin-top`（tab pane 已提供間距），`.versions-toolbar` 的 `margin-bottom` 保留。

- [ ] **Step 5：執行前端型別檢查**

```powershell
npm --prefix web run build:nocheck
```

預期：`built in ...ms`，無報錯。

- [ ] **Step 6：執行全部前端測試確認無迴歸**

```powershell
npm --prefix web run test
```

預期：所有測試 PASS。

- [ ] **Step 7：Commit**

```powershell
git add web\src\views\BookDetailView.vue
git commit -m "feat(web): BookDetailView 重構為 NTabs（簡介 / 版本 / 相關書籍）"
```

---

## 手動驗證（所有 Task 完成後）

1. 啟動後端：`dotnet run --project src/Knovault.Api`
2. 啟動前端：`npm --prefix web run dev`
3. 新增兩本同作者的書（例如同設 `Robert Martin`）
4. 點進其中一本書的詳情頁
5. 確認頁面有三個 Tab：**簡介 / 版本 / 相關書籍**
6. 點選「相關書籍」Tab，確認另一本書的封面卡片出現
7. 點擊封面卡片，確認跳轉到對應書籍詳情頁
8. 新增一本無相關屬性的書，查看其「相關書籍」Tab，確認顯示「暫無相關書籍」
