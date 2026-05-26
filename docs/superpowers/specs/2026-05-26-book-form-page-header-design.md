# BookFormView 頁面標題區設計

**日期：** 2026-05-26  
**狀態：** 已核准  
**影響檔案：** `web/src/views/BookFormView.vue`

---

## 問題描述

現有 `.topbar` 的佈局是 flex row，將「◀ 返回」按鈕與頁面標題並排於同一行。兩個元素視覺層級相近，導致標題不夠突出，整體頭部區域顯得奇怪。

## 設計決策

**佈局**：改為上下堆疊（flex column）  
**返回連結**：簡潔的「← 返回」，小字、淡灰色，視覺層級低於標題  
**標題**：獨佔一行，字體放大加粗，成為視覺焦點  

## 變更規格

### Template

```html
<!-- 將 .topbar 重命名為 .page-header -->
<div class="page-header">
  <n-button text @click="router.back()" class="back-btn">← 返回</n-button>
  <h2>{{ isEdit ? "編輯書籍" : "新增實體書" }}</h2>
</div>
```

- `n-button` 從 `quaternary` 改為 `text`，去除 hover 背景方塊感
- 箭頭由 Unicode `◀`（實心三角）改為 `←`（箭頭線條），視覺較輕盈

### CSS

```css
/* 移除舊的 .topbar，改用以下 */
.page-header {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  margin-bottom: 20px;
}
.back-btn {
  font-size: 13px;
  color: #aaa;
}
.page-header h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
}
```

### 視覺結果

```
← 返回                ← 13px 淡灰，輔助導覽

編輯書籍              ← 22px 粗體，主要標題
─────────────────────
ISBN...
書名 *
...
```

## 範圍限制

- 僅修改 `BookFormView.vue` 的標題區 HTML + CSS
- 不影響表單邏輯、路由、API 呼叫
- 新增書籍（`新增實體書`）與編輯書籍（`編輯書籍`）套用相同樣式
