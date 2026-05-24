# BookCard 重新設計：封面置頂 + 底部資訊列

日期：2026-05-24

## 目標
把 BookCard 從「封面滿版 + 底部 overlay」改成「上半部封面 + 下半部實心資訊列」。
資訊列 = 檔案格式 icon + 書名 + 三點選單，中間夾綠色進度條。

## 版面
```
┌─────────────┐
│  封面 contain │  上半部：完整顯示封面（不裁切、留邊置中）
├─────────────┤
│███░░░░░░░░░░│  綠色進度條 3px（夾在中間）
├─────────────┤
│ [icon] 書名 ⋮│  下半部：實心資訊列
└─────────────┘
```

## 後端
- `BookSummaryDto` 新增 `Formats : IReadOnlyList<string>`（數位副本去重格式，如 `["Pdf"]`、`["Epub","Pdf"]`）。
- `BookMappings.ToSummaryDto`：`Formats = b.Copies.OfType<DigitalCopy>().Select(c => c.Format.ToString()).Distinct().ToList()`。endpoint 已 `.Include(b => b.Copies)`，不需額外查詢。
- 前端 `BookSummary` type 新增 `formats: string[]`。

## 前端 BookCard.vue
- **封面區**：`aspect-ratio: 3/4` + `object-fit: contain` + 中性 letterbox 背景；無封面維持首字 placeholder。
- **資訊列**：實心背景，用 `useThemeVars()`（`cardColor` / `textColorBase`）支援深淺色。
  - 左：格式 icon — inline SVG 文件圖示，PDF=紅、EPUB=藍綠，圖示內含縮寫；多格式並排（最多 2）。**純實體書（無數位檔）顯示「實體書」icon。**
  - 中：書名 `n-ellipsis` 單行省略（**不顯示作者**）。
  - 右：⋮ 三點選單 — 選項與所有處理邏輯（編輯/狀態/新增實體/刪除/導頁/refresh）完全不變。
- **進度條**：維持現有邏輯（Reading 顯示 %、Finished 100%、其餘不顯示），位置移到封面與資訊列之間。
- **移除**左上角 📱/📚 角標（資訊列 icon 已表達數位/實體）。

## 測試
- `BookCard.test.ts`：移除「作者顯示」「📱📚 角標」斷言；新增「書名顯示」「依 formats 渲染格式 icon」「純實體顯示實體 icon」「無封面 placeholder」維持。
- 後端 mapping 測試：含 Epub+Pdf 副本 → `Formats` 去重正確。

## 不做（YAGNI）
- 格式 icon 不可點擊（卡片點擊→詳情、選單→操作）。
- 不引入 icon 套件，全用 inline SVG。
