# Knovault — 版本面板重設計 (Book Media Panel)

- **日期**：2026-05-24
- **分支**：`feat/library-core-p5`
- **範圍**：後端補實體書位置欄位 + 新端點；前端書籍詳情頁「版本」區塊重設計。

---

## 背景與動機

原本詳情頁的「形式（只是紀錄）」Switch 語意不直觀，且「數位檔」與「形式」分成兩塊、沒有統一的版本視角。

此次改造：
- 後端：`Book` 補 `PhysicalLocation` / `PhysicalNotes` 欄位，新增 `PATCH /api/books/{id}/physical` 端點。
- 前端：移除 Switch、合併兩區塊為單一「版本」面板，統一呈現所有媒體版本（實體 + 數位）。

不動現有 `DigitalCopy` 結構、不動 `PUT /api/books/{id}` 書目編輯路徑。

---

## 領域模型異動

### `Book` entity 新增欄位

| 欄位 | 型別 | 說明 |
|------|------|------|
| `PhysicalLocation` | `string?` | 館藏位置，例如「書房 B 櫃-第3層」|
| `PhysicalNotes` | `string?` | 備註，例如「借給小明」|

`IsPhysical` 旗標保留。設定 Location / Notes 時一併設 `IsPhysical = true`；刪除實體版本時設 `IsPhysical = false` 並清空兩欄。

### Migration

新增兩個可空欄位，現有資料不受影響。

---

## 後端 API

### 新端點

```
PATCH /api/books/{id}/physical
```

**Request body：**
```json
{
  "isPhysical": true,
  "location": "書房 B 櫃-第3層",
  "notes": "借給小明"
}
```
- `isPhysical: false` → 清空 Location / Notes 並取消實體旗標。
- `location` / `notes` 選填，可為 null。

**回傳：** 更新後的 `BookDetailDto`（與 `GET /api/books/{id}` 相同結構）。

**錯誤：** 404 若書不存在。

### `BookDetailDto` 補充欄位

```json
{
  "physicalLocation": "書房 B 櫃-第3層",
  "physicalNotes": "借給小明"
}
```
兩欄皆可為 null。

---

## 前端 UI

### 移除

- 「形式（只是紀錄）」divider + n-tag 組 + Switch 整塊。
- 「數位檔」divider + n-list 整塊。
- 「目錄 TOC」divider + n-empty 佔位（後端尚未暴露，先移除不佔版面）。

### 新增：版本面板

Divider 標題「版本」，右側行內顯示「＋ 新增實體版本」按鈕（僅 `!isPhysical` 時顯示）。

清單內容：

```
── 版本 ─────────────────────────────── [＋ 新增實體版本]

  🏠 實體書
     📍 書房 B 櫃-第3層          （有值才顯示）
     📝 借給小明                 （有值才顯示）  [📝 編輯]

  📱 EPUB  5.2 MB                              [📥 下載] [移除]

  📄 PDF   12.0 MB  ⚠ 檔案遺失                          [移除]
```

**空狀態**（`!isPhysical && copies.length === 0`）：
`<n-empty>` 顯示「尚無版本，可新增實體版本或掃描書庫資料夾」。

### 互動：新增實體版本

1. 點「＋ 新增實體版本」→ 彈 `<n-modal>` 表單。
2. 欄位：📍 館藏位置（選填）、📝 備註（選填）。
3. 送出 → `PATCH /api/books/{id}/physical`，body `{ isPhysical: true, location, notes }`。
4. 成功 → reload book、modal 關閉、實體列出現、按鈕消失。

### 互動：編輯實體版本

1. 點實體列「📝 編輯」→ 同一個 modal，欄位預填現有值。
2. 送出 → 同一支 `PATCH` endpoint。
3. 成功 → reload book、modal 關閉。

### 互動：移除實體版本（未來可選擴充）

若之後需要，可在實體列加「移除」按鈕，呼叫 `PATCH /api/books/{id}/physical` 帶 `{ isPhysical: false }`。本次**不實作**（保持 minimal）。

---

## 前端型別 / API 層

### `types.ts` 異動

```typescript
// BookDetail 補兩欄
interface BookDetail {
  // ... 既有欄位 ...
  physicalLocation: string | null   // 新增
  physicalNotes: string | null      // 新增
}

// 新請求型別
interface UpdatePhysicalRequest {
  isPhysical: boolean
  location?: string | null
  notes?: string | null
}
```

### `booksApi` 新增方法

```typescript
updatePhysical: (id: string, req: UpdatePhysicalRequest) =>
  http.patch<BookDetail>(`/books/${id}/physical`, req)
```

---

## 測試

| 層級 | 測試項目 |
|------|----------|
| 後端整合 | `PATCH /physical` 設定位置 → DTO 回傳正確值 |
| 後端整合 | `PATCH /physical` `isPhysical: false` → 清空欄位 |
| 後端整合 | 404 when book not found |
| 前端單元 | 版本面板空狀態渲染 |
| 前端單元 | 實體列有 / 無 location 的渲染差異 |

---

## 範圍外（不在本次）

- 移除實體版本按鈕（可後續補）
- 數位檔手動上傳
- TOC 顯示
- 多筆實體副本（需模型重構為 BookMedia 表，為未來方案三）
