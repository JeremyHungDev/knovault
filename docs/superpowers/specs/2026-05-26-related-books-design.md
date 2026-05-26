# Related Books — 相關書籍設計文件

- **日期**：2026-05-26
- **狀態**：已核准
- **範疇**：子專案 1.5 — 書籍詳情頁加入「相關書籍」分頁，以屬性計分找出相關書籍

---

## 1. 背景與定位

### 在 Roadmap 裡的位置

| 子專案 | 內容 | Related 功能 |
|--------|------|-------------|
| 1. 書庫核心（已完成） | 領域模型、API、UI 地基 | — |
| **1.5（本文件）** | **Related Books 屬性計分** | ✅ 現在實作 |
| 4. 搜尋與進階（選做） | 全文 + 向量搜尋 | 若實作，換掉計分策略即可 |

Related Books 是子專案 1 的自然延伸。`IRelatedBooksStrategy` 介面**預留**向量 embedding 的擴充點——若未來決定實作語意搜尋，只需在 Infrastructure 新增 `EmbeddingRelatedBooksStrategy` 並透過 DI 替換，**前端與 API 介面完全不動**。向量搜尋本身不在本文件範疇，也不承諾一定實作。

---

## 2. 範圍

### 範圍內
- `GET /api/books/{id}/related?limit=10` 新 endpoint
- 屬性計分策略（Tags、Authors、Publisher）
- BookDetailView 重構為 Tabs（簡介 / 版本 / 相關書籍）
- 新元件 `RelatedBooksSection.vue`（水平封面捲動列）
- 空狀態處理

### 範圍外
- AI / 向量 embedding（子專案 4）
- 手動指定關聯（YAGNI，屬性計分已足夠）
- 相關書籍的新增/移除管理 UI
- BookCard 顯示相關數量

---

## 3. 架構

### 四層配合

```
Domain          ← 不動（Book 已有 Tags / Authors / Publisher）
Application     ← 新增 IRelatedBooksStrategy 介面
Infrastructure  ← 新增 AttributeRelatedBooksStrategy 實作
Api             ← 新增 GET /api/books/{id}/related endpoint
```

### 擴充點（選做，不承諾）

若未來決定實作向量 embedding，擴充路徑如下：

```
現在（屬性計分）                    若未來實作語意搜尋
──────────────────────────────      ────────────────────────────────────
GET /api/books/{id}/related         GET /api/books/{id}/related   ← 介面不變
      ↓                                     ↓
IRelatedBooksStrategy               IRelatedBooksStrategy         ← 介面不變
      ↓                                     ↓
AttributeRelatedBooksStrategy       EmbeddingRelatedBooksStrategy ← 只換實作
      ↓                                     ↓
Tags/Authors/Publisher 記憶體計分    pgvector / sqlite-vec 查詢
```

屬性計分本身完整可用，embedding 只是可選升級，不做也沒有缺口。

---

## 4. 後端設計

### 4.1 計分演算法

```
score(candidateBook) =
  (共同 Tags 數 × 2) +
  (共同 Authors 數 × 3) +
  (Publisher 相同 ? 1 : 0)
```

| 屬性 | 權重 | 理由 |
|------|------|------|
| 共同 Authors | ×3 | 同作者相關性最強 |
| 共同 Tags | ×2 | 主題關聯 |
| 同 Publisher | ×1 | 補充信號 |

規則：
- 排除來源書本身（`Id != source.Id`）
- 僅回傳 `score > 0` 的書
- 依 score DESC 排序
- 預設 `limit = 10`，最大 50

### 4.2 Application 層 — 介面

```csharp
// src/Knovault.Application/Related/IRelatedBooksStrategy.cs
namespace Knovault.Application.Related;

public interface IRelatedBooksStrategy
{
    Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source,
        int limit,
        CancellationToken ct = default);
}
```

### 4.3 Infrastructure 層 — 屬性計分實作

```csharp
// src/Knovault.Infrastructure/Related/AttributeRelatedBooksStrategy.cs
namespace Knovault.Infrastructure.Related;

public sealed class AttributeRelatedBooksStrategy(KnovaultDbContext db)
    : IRelatedBooksStrategy
{
    public async Task<IReadOnlyList<Book>> GetRelatedAsync(
        Book source, int limit, CancellationToken ct = default)
    {
        var all = await db.Books
            .Include(b => b.Tags)
            .Include(b => b.Authors)
            .Where(b => b.Id != source.Id)
            .ToListAsync(ct);

        var sourceTags    = source.Tags.Select(t => t.Id).ToHashSet();
        var sourceAuthors = source.Authors.Select(a => a.Name).ToHashSet(
                                StringComparer.OrdinalIgnoreCase);

        return all
            .Select(b => new
            {
                Book  = b,
                Score = b.Tags.Count(t => sourceTags.Contains(t.Id)) * 2
                      + b.Authors.Count(a => sourceAuthors.Contains(a.Name)) * 3
                      + (b.Publisher != null
                         && b.Publisher == source.Publisher ? 1 : 0)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.Book)
            .ToList();
    }
}
```

> 個人書庫規模（< 10,000 本）記憶體計分完全可接受，不需複雜 SQL。

### 4.4 Api 層 — Endpoint

```
GET /api/books/{id}/related?limit=10
```

| 狀況 | 回應 |
|------|------|
| 書存在 | `200 OK` → `BookSummaryDto[]` |
| 書不存在 | `404 Not Found` |
| `limit` 超出範圍（< 1 或 > 50） | `400 Bad Request` |

回傳 `BookSummaryDto[]`，重用現有 DTO（已含 `id`、`title`、`authors`、`coverPath`、`tags`），無需新型別。

### 4.5 DI 註冊

```csharp
// src/Knovault.Api/Program.cs
builder.Services.AddScoped<IRelatedBooksStrategy, AttributeRelatedBooksStrategy>();
```

---

## 5. 前端設計

### 5.1 BookDetailView 重構：NDivider → NTabs

**現有結構：**
```
頭部（封面 + 書目資訊）
── 簡介 ──   （v-if book.description）
── 版本 ──
```

**重構後：**
```
頭部（封面 + 書目資訊）← 完全不動

[簡介] [版本] [相關書籍]
```

Tab 規格：

```html
<n-tabs type="line" animated>
  <n-tab-pane name="description" tab="簡介">
    <!-- 有描述顯示文字，無描述顯示 <n-empty> -->
  </n-tab-pane>

  <n-tab-pane name="copies" tab="版本">
    <!-- 現有版本面板原封不動搬入 -->
  </n-tab-pane>

  <n-tab-pane name="related" tab="相關書籍">
    <related-books-section :book-id="id" />
  </n-tab-pane>
</n-tabs>
```

未來子專案 2（筆記）只需新增一個 tab：
```html
<n-tab-pane name="notes" tab="筆記">...</n-tab-pane>
```

### 5.2 新元件 RelatedBooksSection.vue

```
web/src/components/RelatedBooksSection.vue
```

Props：`{ bookId: string }`

行為：
- `onMounted` → `GET /api/books/{bookId}/related`
- loading：`<n-spin>`
- 有資料：水平捲動封面列
- 空狀態：`<n-empty description="暫無相關書籍" />`

版型：
```
  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐  → scroll
  │ 封面 │  │ 封面 │  │ 封面 │  │ 封面 │
  │      │  │      │  │      │  │      │
  └──────┘  └──────┘  └──────┘  └──────┘
   書名A     書名B     書名C     書名D
```

封面規格：
- 寬 120px，`aspect-ratio: 3/4`，`object-fit: cover`，`border-radius: 8px`
- 無封面 → 書名首字 placeholder（與現有 BookCard 一致）
- 點擊整張卡片 → `router.push('/books/{id}')`
- 書名：單行省略，`n-ellipsis`，max-width 120px

### 5.3 新增 API 呼叫

```typescript
// web/src/api/books.ts 新增
related: (id: string, limit = 10): Promise<BookSummary[]> =>
  http.get(`/api/books/${id}/related?limit=${limit}`),
```

---

## 6. 異動檔案總覽

| 層 | 檔案 | 動作 |
|---|---|---|
| Application | `src/Knovault.Application/Related/IRelatedBooksStrategy.cs` | 新增 |
| Infrastructure | `src/Knovault.Infrastructure/Related/AttributeRelatedBooksStrategy.cs` | 新增 |
| Api Endpoint | `src/Knovault.Api/Endpoints/BookEndpoints.cs` | 修改：加 `related` endpoint |
| Api DI | `src/Knovault.Api/Program.cs` | 修改：註冊 strategy |
| 前端 API | `web/src/api/books.ts` | 修改：加 `related()` |
| 前端元件 | `web/src/components/RelatedBooksSection.vue` | 新增 |
| 前端頁面 | `web/src/views/BookDetailView.vue` | 修改：重構為 Tabs |
| 後端測試 | `tests/Knovault.Api.Tests/BookEndpointsTests.cs` | 修改：加 related endpoint 測試 |
| 前端測試 | `web/src/components/RelatedBooksSection.test.ts` | 新增 |

---

## 7. 測試策略

| 層級 | 測試內容 | 工具 |
|------|---------|------|
| 單元 | `AttributeRelatedBooksStrategy`：共同 Tags/Authors/Publisher 計分正確；排除自身；空結果 | xUnit |
| 整合 | `GET /api/books/{id}/related`：回傳正確書單、404 書不存在 | `WebApplicationFactory` |
| 前端 | `RelatedBooksSection`：loading → 有資料 → 封面列；空狀態 | Vitest |

---

## 8. 決策紀錄

| # | 決定 | 理由 |
|---|------|------|
| D21 | `IRelatedBooksStrategy` 介面抽象 | 預留向量搜尋擴充點（不承諾實作）；若做，只換實作，API/前端不動 |
| D22 | 計分：Authors ×3、Tags ×2、Publisher ×1 | 作者相關性最強；Publisher 為補充信號 |
| D23 | 記憶體計分（撈全部書後在記憶體算） | 個人書庫規模可接受；避免複雜 SQL |
| D24 | 回傳 `BookSummaryDto[]`，不建新 DTO | 現有 DTO 已含前端所需欄位 |
| D25 | BookDetailView 重構為 NTabs | 筆記（子專案 2）等未來 tab 直接新增，不再重構 |
| D26 | 水平捲動封面列，不分組 | 「就用圖片呈現」，簡單直觀 |
