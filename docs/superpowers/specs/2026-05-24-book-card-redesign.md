# Knovault — BookCard 重設計（封面滿版 + 三點選單）

- **日期**：2026-05-24
- **分支**：`feat/library-core-p5`
- **範圍**：`BookCard.vue` 視覺改版 + 三點選單操作；`LibraryView.vue` 加 refresh 監聽。不動後端、不改 LibraryView 版面結構。

---

## 目標

- 封面圖滿版佔整張卡片，書名 / 作者以底部漸層 overlay 顯示。
- 卡片整體點擊 → 進詳情頁（行為不變）。
- 右下角 ⋮ 按鈕 → dropdown 選單，提供快速操作（編輯 / 狀態 / 新增實體版本 / 刪除）。
- 底部 2px 進度條取代閱讀狀態 tag。

---

## 視覺設計

### 卡片結構

```
┌────────────────────┐
│                    │
│   （封面圖）        │  ← 3:4 比例，object-fit: cover，滿版
│                    │
│  📱 📚             │  ← 左上角 .badges（保留現有）
│                    │
│▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│  ← 漸層遮罩：linear-gradient(transparent → rgba(0,0,0,0.75))
│ Clean Architecture │  ← 書名 n-ellipsis :line-clamp="2"，白色
│ Robert C. Martin ⋮ │  ← 作者（左，ellipsis）+ ⋮ button（右）
└────────────────────┘
│██████░░░░░░░░░░░░░│  ← 進度條 2px，progressPercent 填色
```

### 進度條規則

| 狀態 | 顏色 |
|------|------|
| Reading（有 progressPercent） | `#18a058`（綠，progressPercent% 寬）|
| Finished | `#18a058`（綠，100% 寬） |
| None / WantToRead / 無進度 | 無（不顯示進度條） |

### 閱讀狀態 tag

移除（由進度條取代）。

---

## 三點選單

使用 `NDropdown` (`trigger="click"`)，選項結構：

```
├─ 編輯書目
├─ 標記閱讀狀態 ▶
│   ├─ ✓ 未分類   (None)
│   ├─ 想讀       (WantToRead)
│   ├─ 閱讀中     (Reading)
│   └─ 已讀       (Finished)
├─ 新增實體版本
└─ ─────────────
   刪除（紅色）
```

### 選單行為

| 選項 | 行為 |
|------|------|
| 編輯書目 | `router.push(`/books/${id}/edit`)` |
| 標記閱讀狀態（子選項） | `PATCH /api/books/{id}/reading`（`readingStatus` 欄位，其餘不動）→ emit `'refresh'` |
| 新增實體版本 | `router.push(`/books/${id}`)` |
| 刪除 | `dialog.warning` 確認框 → `DELETE /api/books/{id}` → emit `'refresh'` |

- 目前閱讀狀態的子選項 label 加 `✓` 前綴。
- 刪除選項用 `render-label` 渲染紅色文字。
- ⋮ 按鈕用 `@click.stop` 阻止事件冒泡（避免同時觸發卡片整體點擊）。

---

## 元件介面

### BookCard.vue

```typescript
// Props（不變）
defineProps<{ book: BookSummary }>()

// Emits（新增）
defineEmits<{ refresh: [] }>()
```

新增 Naive UI imports：`NDropdown`、`useDialog`（刪除確認）、`useMessage`（操作回饋）。

### LibraryView.vue

```html
<!-- 僅加 @refresh -->
<book-card :book="b" @refresh="books.fetch()" />
```

---

## 範圍外

- 「Newly Added Series」/ 「On Deck」分區版面（未來另立計畫）
- 閱讀器功能（未來另立計畫）
- 卡片尺寸 / 格線欄數調整
