# 評論功能設計規格

**功能名稱：** BookDetailView 新增「評論」標籤頁  
**建立日期：** 2026-05-29  
**專案：** Knovault — 個人書庫管理應用程式

---

## 目錄

1. [功能概述](#功能概述)
2. [架構設計：Provider 模式](#架構設計provider-模式)
3. [資料模型](#資料模型)
4. [Phase 1：Goodreads 實作](#phase-1goodreads-實作)
5. [Phase 2：博客來規劃](#phase-2博客來規劃)
6. [API 端點](#api-端點)
7. [前端變更](#前端變更)
8. [新增檔案清單](#新增檔案清單)

---

## 功能概述

在 BookDetailView 新增第四個標籤頁「評論」，整合外部書評資料。  
技術堆疊：Vue 3 + Naive UI（前端）、.NET Clean Architecture（後端）、SQLite via EF Core（資料庫）。

**實作分兩個階段：**

- **Phase 1：** Goodreads（使用 HttpClient，不需要 Playwright）
- **Phase 2：** 博客來（需要 Playwright，留待後續實作）

---

## 架構設計：Provider 模式

遵循專案現有模式（`IRelatedBooksStrategy`、`IIsbnMetadataProvider`），以相同方式設計爬蟲與聚合層。

```
IBookReviewScraper              ← 每個平台一個實作
    └── GoodreadsScraper
    └── BooksComTwScraper       ← Phase 2 stub

BookReviewAggregator            ← 實作 IExternalReviewService，協調各爬蟲 + 快取
```

### 介面職責

| 介面 / 類別 | 職責 |
|---|---|
| `IBookReviewScraper` | 定義單一平台爬蟲的抓取合約 |
| `IExternalReviewService` | 對外暴露的聚合服務介面（供 API 層使用） |
| `BookReviewAggregator` | 協調多個 scraper、處理快取讀寫 |

---

## 資料模型

### 新增資料表：`ExternalReviews`

| 欄位 | 型別 | 說明 |
|---|---|---|
| `Id` | `Guid` | 主鍵 (PK) |
| `BookId` | `Guid` | 外鍵 → Books |
| `Source` | `enum ReviewSource` | 以 string 儲存（`HasConversion<string>()`）；值：`BooksComTw` / `Goodreads` |
| `ReviewerName` | `string?` | 評論者名稱 |
| `Rating` | `float?` | 1–5 分制 |
| `ReviewText` | `string?` | 評論內文 |
| `ReviewDate` | `string?` | 以字串儲存（各平台格式不同） |
| `HelpfulCount` | `int?` | 認為有用的人數 |
| `FetchedAt` | `DateTimeOffset` | 此批次資料的抓取時間 |

### 快取策略

- **批次覆寫：** 每次重新整理時，針對同一 `BookId + Source` 的所有現有記錄先刪除再重寫
- **無自動過期：** 僅手動觸發重新整理（使用者按「重新整理」按鈕）
- **`FetchedAt` 回傳前端：** 讓使用者知道資料新鮮度

---

## Phase 1：Goodreads 實作

### 爬蟲流程

Goodreads 使用 HttpClient，不需要 JavaScript 執行環境。

**步驟 1：取得書籍頁面 URL**

```
GET https://www.goodreads.com/book/isbn/{isbn}
```

跟隨重新導向，最終 URL 含有 legacyId，例如：

```
https://www.goodreads.com/book/show/11468377-some-book-title
                                    ^^^^^^^^
                                    legacyId
```

**步驟 2：從 HTML 中解析 `__NEXT_DATA__`**

擷取頁面中的 `<script id="__NEXT_DATA__">` 區塊，解析 JSON 以取得 `kca://work/...` 格式的資源 ID。

**步驟 3：呼叫 Goodreads GraphQL API**

```
POST https://kxbwmqov6jgg3daaamb744ycu4.appsync-api.us-east-1.amazonaws.com/graphql
```

Request headers：

```
X-Api-Key: da2-xpgsdydkbregjhpr6ejzqdhuwy
User-Agent: Mozilla/5.0 ...
```

Request body：

```json
{
  "operationName": "getReviews",
  "variables": {
    "filters": {
      "resourceType": "WORK",
      "resourceId": "kca://work/..."
    },
    "pagination": {
      "limit": 30
    }
  }
}
```

**步驟 4：解析回應**

從回應的 `edges[].node` 節點對應欄位：

| GraphQL 欄位 | 資料庫欄位 |
|---|---|
| reviewer 名稱 | `ReviewerName` |
| rating | `Rating` |
| `text` | `ReviewText` |
| `createdAt` | `ReviewDate` |
| `likeCount` | `HelpfulCount` |

### 風險說明

> Goodreads 的 `X-Api-Key` 硬寫在前端 JS 中，可能在前端部署時輪換。  
> 個人應用可接受此風險；金鑰失效時手動更新。

---

## Phase 2：博客來規劃

Phase 1 完成後記錄，Phase 2 尚未實作。

### 調查結果

| 端點 | 狀態 | 說明 |
|---|---|---|
| `search.books.com.tw` | 可用 | 加上 User-Agent 標頭即可使用；可從重新導向 URL `/item/{item_id}/` 擷取 item_id |
| `www.books.com.tw`（商品頁） | 被擋 | Cloudflare Managed Challenge，需要 JavaScript 執行 |
| AJAX / API 端點 | 被擋 | 回傳 403 |

### Phase 2 實作需求

- 需安裝 **Microsoft.Playwright** NuGet 套件（headless Chromium，約 100 MB）
- 待後續評估後再決定是否引入此依賴

### Phase 1 的博客來處理方式

Phase 1 中，博客來標籤頁顯示佔位內容：

- 文字：「博客來評論功能開發中」
- 提供直連至博客來書籍頁面的外部連結（若有 ISBN 則導向搜尋頁）

---

## API 端點

### `GET /books/{id}/reviews`

取得快取的評論資料。若快取不存在，則觸發爬蟲並等待完成後回傳。

### `POST /books/{id}/reviews/refresh`

強制重新爬蟲，覆蓋現有快取。

### 回應格式

```json
{
  "sources": [
    {
      "source": "Goodreads",
      "fetchedAt": "2026-05-29T10:00:00Z",
      "reviews": [
        {
          "reviewerName": "John Doe",
          "rating": 4.0,
          "reviewText": "A great read that changed my perspective...",
          "reviewDate": "2024-01-15",
          "helpfulCount": 12
        }
      ]
    }
  ]
}
```

---

## 前端變更

### BookDetailView 變更

在現有三個標籤頁後新增第四個：

```
[簡介]  [版本]  [相關書籍]  [評論]   ← 新增
```

### 平台切換器

使用 `NSegmented` 元件（Naive UI）：

```
[ Goodreads ]  [ 博客來 ]
```

### 各平台顯示內容

**Goodreads（Phase 1 實作）：**

- 顯示 `fetchedAt` 時間戳記（「資料更新時間：YYYY-MM-DD HH:mm」）
- 「重新整理」按鈕（觸發 `POST /refresh`）
- 列出所有評論（每平台上限 30 筆，不分頁）
- 每筆評論顯示：星等評分 + 評論者名稱 + 日期 + 評論內文 + 有用人數

**博客來（Phase 1 佔位）：**

- 顯示「博客來評論功能開發中」
- 若有 ISBN，附上直連博客來的外部連結

### 狀態處理

| 情境 | 顯示內容 |
|---|---|
| 書籍無 ISBN | 「此書無 ISBN，無法查詢外部評論」 |
| 首次載入中 | 載入動畫（spinner） |
| 發生錯誤 | 錯誤訊息 + 重試按鈕 |
| 博客來（Phase 1） | 「博客來評論功能開發中」+ 外部連結 |

---

## 新增檔案清單

### Application 層

```
src/Knovault.Application/Reviews/IBookReviewScraper.cs
src/Knovault.Application/Reviews/IExternalReviewService.cs
src/Knovault.Application/Reviews/ScrapedReview.cs
```

### Infrastructure 層

```
src/Knovault.Infrastructure/Reviews/GoodreadsScraper.cs
src/Knovault.Infrastructure/Reviews/BooksComTwScraper.cs   ← Phase 2 stub
src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs
```

### Domain 層

```
src/Knovault.Domain/Entities/ExternalReview.cs
src/Knovault.Domain/Enums/ReviewSource.cs
```

### API 層

```
src/Knovault.Api/Endpoints/ReviewEndpoints.cs
src/Knovault.Api/Contracts/ReviewsResultDto.cs
```

### 前端

```
web/src/api/reviews.ts                     ← 新 API client
web/src/components/ReviewsSection.vue      ← 新元件
web/src/views/BookDetailView.vue           ← 修改：新增評論標籤頁
```
