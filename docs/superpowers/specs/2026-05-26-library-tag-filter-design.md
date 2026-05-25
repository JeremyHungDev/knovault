# LibraryView 標籤篩選修復設計

**日期：** 2026-05-26  
**狀態：** 已核准  
**範疇：** 修復 LibraryView 標籤篩選下拉無效的問題

---

## 背景

LibraryView 的「標籤」篩選下拉已有 UI 及 store 綁定，但完全無作用。原因有三：

1. `GET /api/books` 的 EF Core query 沒有 `Include(b => b.Tags)`
2. `BookSummaryDto`（後端）及 `BookSummary`（前端型別）均缺少 `tags` 欄位
3. `applyFilters()` 中 `tag` 欄位存在於 `BookFilters` 介面，但篩選邏輯從未實作

---

## 設計目標

- 選擇標籤後，書單只顯示「包含該標籤」的書
- 選「全部標籤」時顯示所有書（現有行為）
- 與現有 kind / status / sort 客戶端篩選模式保持一致
- 不改變 API 介面（無新 query param）

---

## 不在範疇內

- BookCard 顯示標籤
- 標籤顏色設定 UI
- 多標籤同時篩選（單選已足夠）
- 伺服器端標籤篩選

---

## 架構決策

採用**客戶端篩選**延伸方案：後端一次將標籤名稱隨書單回傳，前端在已有的 `applyFilters()` 中補上 tag 邏輯。

理由：
- 個人規模資料量，一次帶回標籤無效能疑慮
- 與現有 kind / status 篩選一致，不引入混合模式
- 修改最小，不動 API 簽章

---

## 變更清單

### 後端（C#）

**1. `src/Knovault.Api/Contracts/BookSummaryDto.cs`**

新增欄位：
```csharp
public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
```

**2. `src/Knovault.Api/Mapping/BookMappings.cs`**

`ToSummaryDto()` 加入 Tags 映射：
```csharp
Tags = b.Tags.Select(t => t.Name).ToList(),
```

**3. `src/Knovault.Api/Endpoints/BookEndpoints.cs`**

`ListBooks` query 加入 Tags eager loading：
```csharp
var query = db.Books.Include(b => b.Copies).Include(b => b.Tags).AsQueryable();
```

### 前端（TypeScript / Vue）

**4. `web/src/api/types.ts`**

`BookSummary` 介面加入欄位：
```typescript
tags: string[]
```

**5. `web/src/stores/books.ts`**

`applyFilters()` 補上 tag 篩選（加在 status 篩選之後）：
```typescript
if (f.tag) result = result.filter((b) => b.tags.includes(f.tag!))
```

---

## 資料流

```
GET /api/books
  → ListBooks: Include(Copies) + Include(Tags)
  → ToSummaryDto(): Tags = tag names[]
  → BookSummaryDto.Tags

前端 books store fetch()
  → BookSummary.tags: string[]
  → applyFilters(): f.tag → b.tags.includes(f.tag)
  → LibraryView 標籤下拉有效
```

---

## 測試考量

- 既有 `TagEndpointsTests.cs` 覆蓋標籤 CRUD，無需新增
- `applyFilters()` 的 tag 邏輯可在 `books.ts` 對應的 unit test 中補充（若有）
- 手動驗證：在 Settings 建立標籤 → 貼到書上 → LibraryView 選標籤 → 只顯示該書

---

## 影響評估

| 面向 | 影響 |
|------|------|
| API 相容性 | 無破壞（新增欄位，非移除） |
| 效能 | 輕微增加（Tags JOIN），個人規模可接受 |
| 測試 | 現有測試不受影響 |
| 前端型別 | `BookSummary.tags` 為必填，需確認無其他地方建構此型別 |
