# Kavita 風深色模式設計規格

**日期：** 2026-06-12
**狀態：** 已與使用者確認
**參考：** [Kavita demo](https://demo.kavitareader.com/home) 的深色視覺風格

## 目標

深色模式從 Solarized Dark 換成 Kavita 風格（近黑灰背景 + 亮綠強調色）；淺色模式維持 Solarized Light，僅定案卡片資訊列底色為暖灰，並一併修正幾個既有的小型配色問題。

## 決策紀錄（與使用者逐項確認）

1. **範圍：** 只換深色模式配色，淺色維持 Solarized Light。不引入 Kavita 的版面結構、字體（Poppins）或卡片進度條等功能性元素。
2. **深色強調色：** 採「深淺各自最佳」策略——深色用 Kavita 綠 `#4ac694`，淺色維持 Solarized（品牌橄欖綠 `#859900`、主互動藍 `#268bd2`）。「綠色 = 品牌」概念跨模式一致，亮度配合各自背景。
3. **淺色卡片資訊列：** `#dcd5c6` 暖灰（與奶油底 `#fdf6e3` 同色溫），取代已 commit 的 `#e0e0e0` 與工作區未 commit 的 `#c8c8c8` 試驗值。
4. **深色導覽列：** `#2a2b2c`（比內容區稍亮的傳統 elevation 層次），不採 Kavita 的純黑導覽列。

## 實作方式（方案一：純 token 值替換）

現有 CSS 變數架構不動。元件已全部走 `var(--token)`，因此改動集中在三個檔案：`web/src/styles/theme.css`、`web/src/App.vue`、`web/src/components/BookCard.vue`。

### 1. Token 層 — `theme.css`

#### `:root.dark` 全部換成 Kavita 色系

| Token | 新值 | 備註 |
|-------|------|------|
| `--bg-base` | `#1f2020` | 頁面背景 |
| `--bg-elevated` | `#2a2b2c` | 導覽列等上層面板 |
| `--bg-surface` | `rgba(255,255,255,.06)` | 細微面板（評論卡等） |
| `--text-primary` | `#efefef` | |
| `--text-secondary` | `rgba(255,255,255,.60)` | |
| `--text-muted` | `rgba(255,255,255,.45)` | |
| `--hover-bg` | `rgba(255,255,255,.08)` | |
| `--accent-brand` | `#4ac694` | Kavita 綠（深色模式覆寫） |
| `--accent-blue` | `#58a6da` | Solarized 藍提亮版 |
| `--accent-yellow` | `#d0a52b` | 星等用，提亮 |
| `--accent-red` | `#e25d5a` | 提亮 |
| `--bg-card` | `#202122` | 卡片資訊列 |
| `--text-card` | `#efefef` | |
| `--text-card-sub` | `rgba(255,255,255,.55)` | |
| `--text-card-muted` | `rgba(255,255,255,.70)` | |
| `--hover-card` | `rgba(255,255,255,.10)` | |

注意：`--accent-*` 原本只定義在 `:root`（兩模式共用），本次改為深色模式覆寫整組。

另外，`--bg-base`、`--bg-elevated`、`--text-primary`、`--text-secondary`、`--hover-bg`、`--accent-blue` 目前沒有元件直接消費（頁面底色與文字由 Naive UI 主題控制）——仍照表更新值，作為調色盤的單一事實來源；`--accent-red` 會因 BookCard 修正（見第 3 節）開始被使用。

#### `:root`（淺色）只動兩個既有值

| Token | 舊值 | 新值 | 理由 |
|-------|------|------|------|
| `--bg-card` | `#c8c8c8`（工作區）/ `#e0e0e0`（已 commit） | `#dcd5c6` | 與奶油底同色溫的暖灰，解決中性灰偏冷顯髒的問題 |
| `--text-secondary` | `#93a1a1` | `#657b83` | 原值在 `#fdf6e3` 上對比僅約 2.2:1，小字不可讀 |

#### 新增兩個 token（兩模式都定義）

| Token | Light | Dark | 用途 |
|-------|-------|------|------|
| `--border-card` | `rgba(0,0,0,.12)` | `rgba(255,255,255,.08)` | 卡片資訊列上緣分隔線 |
| `--bg-card-hover` | `#d2cab9` | `#2c2d2e` | 資訊列 hover 底色，取代 `brightness` filter |

### 2. Naive UI 主題 — `App.vue`

`themeOverrides` 從固定常數改成 `computed(() => themeStore.dark ? darkOverrides : lightOverrides)`：

**Light（Solarized，修正 hover 跨色相）：**

```
primaryColor: #268bd2, primaryColorHover: #3a9fdc, primaryColorPressed: #1e6fa8
successColor: #859900, warningColor: #b58900, errorColor: #dc322f, infoColor: #2aa198
bodyColor: #fdf6e3
```

注意：頁面背景實際由 Naive UI 的 `bodyColor` 控制（`n-layout` 取此值），`--bg-base` token 目前無元件消費。淺色補上 `bodyColor: #fdf6e3` 確保 Solarized 奶油底真正生效。

**Dark（Kavita）：**

```
primaryColor: #4ac694, primaryColorHover: #66d4a8, primaryColorPressed: #3aa97c
successColor: #4ac694, warningColor: #d0a52b, errorColor: #e25d5a, infoColor: #58a6da
bodyColor: #1f2020, cardColor: #202122, popoverColor: #2a2b2c, modalColor: #2a2b2c
Layout: { headerColor: '#2a2b2c' }
```

`n-layout-header` 取 `Layout.headerColor`，即為使用者選定的稍亮導覽列。

### 3. 元件層 — `BookCard.vue`

1. dropdown 刪除項的硬編碼 `#e88080` → `var(--accent-red)`（inline style 支援 CSS 變數）。
2. `.info-bar:hover` 的 `filter: brightness(1.08)` → `background: var(--bg-card-hover)`（brightness 在淺色卡片上幾乎無效，兩模式行為才一致；保留原本的 `transform: translateY(-1px)`）。
3. `.info-bar` 加 `border-top: 1px solid var(--border-card)`。

其他元件（`BookDetailView`、`ReviewsSection`、`BookFormView`、`AppShell`）全走 token，零修改。`AppShell` 的 logo 已走 `var(--accent-brand)`，深色模式自動變 Kavita 綠。

## 不做的事（YAGNI）

- 不引入 Kavita 的 elevation overlay 系統（半透明白疊層做層次）
- 不換字體（Poppins / Spartan / EBGaramond）
- 不加卡片閱讀進度條、下載狀態 badge 等 Kavita 功能元素
- 不做第三套主題或主題選擇器

## 測試與驗證

- **單元測試：** 現有 Vitest 全數應通過（測試不驗 CSS 值；BookCard 結構不變）。
- **視覺驗證清單**（兩種模式各跑一次）：
  - 書庫卡片：底色、文字對比、hover 效果、分隔線
  - 詳情頁：封面 placeholder、閱讀狀態框、tab
  - 評論區：星等顏色、評論卡底色
  - 新增/編輯表單：返回鈕、按鈕主色
  - 導覽列：底色、logo 綠、主題切換按鈕
  - dropdown / dialog / message：popover 底色、刪除紅
- **既有工作區異動處理：** `theme.css` 未 commit 的 `#c8c8c8` 試驗值由本設計的 `#dcd5c6` 取代；`web/verify-solarized.mjs` 臨時驗證腳本於實作時刪除。
