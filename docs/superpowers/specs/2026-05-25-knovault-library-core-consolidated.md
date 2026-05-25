# Knovault — 書庫核心 (Library Core) 統整設計文件

- **整合日期**：2026-05-25
- **狀態**：定案（整合自 05-23 核心設計 + 05-24 BookCard 最終版 + 05-24 Media Panel）
- **範圍**：Knovault 子專案 1 完整設計，以最終版 UI 為準

---

## 1. 背景與整體願景

Knovault（芝士庫）是一個自託管的個人知識管理（PKM）系統，把「書」當作知識的源頭節點，整合三個原本分散的孤島：數位書庫、實體書櫃、筆記軟體。完整願景包含雙軌書庫、卡片盒雙鏈筆記、多模態圖片資產、向量搜尋等。

由於整體願景橫跨多個獨立子系統，**無法用單一份 spec 涵蓋**，因此拆分為循序漸進的子專案：

| 子專案 | 內容 | 依賴 |
|--------|------|------|
| **1. 書庫核心**（本文件） | 領域模型、持久層、EPUB/PDF 解析、實體書記錄、REST API、書架/詳情 UI | 無（地基） |
| 2. 卡片盒筆記 | Markdown 卡片、`[[雙向鏈結]]`、反向連結、知識圖譜 | 依賴 1 |
| 3. 多模態資產 | 圖片上傳、標籤、`![[Asset_ID]]` 內嵌 | 依賴 1、2 |
| 4. 搜尋與進階 | 全文 + 向量搜尋、動態標籤過濾、（自動）閱讀進度 | 依賴前面全部 |

> 閱讀器（瀏覽器內 EPUB/PDF 閱讀）**刻意不放在子專案 1**，未來另立獨立子專案。

---

## 2. 子專案 1 範圍

### 範圍內 (In Scope)
- 雙擊執行的 Windows 程式形態（本機伺服器 + 瀏覽器/PWA 介面）。
- 資料夾掃描匯入數位書（EPUB/PDF），解析元數據、封面、TOC。
- 手動新增實體書 + ISBN 線上查詢自動帶入。
- 一本書可同時擁有多個版本（電子 + 實體 + 多格式）。
- 標籤、作者瀏覽、閱讀狀態與手動進度。
- 書架網格、書籍詳情、新增/編輯實體書、設定四個頁面。
- 下載/外部 App 開啟數位檔。

### 範圍外 (Out of Scope)
- 瀏覽器內閱讀器（epub.js/pdf.js）。
- 全文/向量搜尋（子專案 4）。
- 筆記、圖片資產（子專案 2、3）。
- 多使用者、登入/權限。
- 自動閱讀進度（需閱讀器）。

---

## 3. 架構總覽

```
┌─────────────────────────────────────────────┐
│  瀏覽器 / 已安裝的 PWA（獨立視窗）              │
│  Vue 3 SPA（書架、詳情、實體書表單、設定）       │
└───────────────┬─────────────────────────────┘
                │  HTTP / REST (+ SSE 掃描進度)
                ▼
┌─────────────────────────────────────────────┐
│  Knovault 單一 Kestrel 執行檔 (.NET 8/9)       │
│  ┌─ Web 層：REST API + 託管 Vue 靜態檔 ─────┐ │
│  ├─ 應用層：用例（掃描、查書、建實體書…）──┤ │
│  ├─ 領域層：Book / BookCopy / Tag / Progress┤ │
│  └─ 基礎設施：EF Core + SQLite、解析器、       │
│     ISBN 查詢、檔案系統、封面儲存 ───────────┘ │
└───────────────┬─────────────────────────────┘
        ┌───────┴────────┬──────────────┐
        ▼                ▼              ▼
   knovault.db       書庫資料夾        covers/
   (SQLite 單檔)     (EPUB/PDF原地)    (擷取的封面/縮圖)
```

### 形態與部署
- **使用形態**：本機伺服器 + 視窗。打包成可雙擊執行的 Windows 程式，啟動本機 Kestrel 伺服器並自動開瀏覽器；可在 Edge/Chrome「安裝」成獨立視窗 PWA。同網段手機/平板可用瀏覽器連 `http://<電腦IP>:<port>`。
- **單一執行檔**：`dotnet publish -r win-x64 --self-contained`；Vue 打包後靜態檔由 Kestrel 託管。未來同一套程式碼可 target linux 上 Docker 到 NAS。
- **桌面外殼**：以瀏覽器 / PWA 為外殼，**不使用** WebView2/Electron/Photino 原生外殼。
- **資料落地**：`%AppData%\Knovault\` —— `knovault.db`（SQLite）、`covers/`、`config.json`（埠號等啟動設定）、`logs/`。**書檔本身留在原地不搬動**，書庫資料夾以唯讀方式對待。備份只需複製此資料夾。

### 技術選型
- 後端：.NET 8/9 (C#)、ASP.NET Core Minimal API、EF Core、**SQLite**（領域層不依賴具體 DB，日後可換 PostgreSQL）。
- 前端：Vue 3（`<script setup>`）、Vite、Vue Router、Pinia、**Naive UI**（含暗色主題）、`vite-plugin-pwa`。
- 登入：**單人、不需登入**（信任內網）。

### SQLite 並發設定
WAL 下 SQLite 仍是**單一寫入者**，對策：
- 連線開 **WAL + `busy_timeout` 30 秒**。
- **寫入集中在掃描服務**，並**批次** `SaveChanges`（每 N 本一次），縮短鎖定窗口。
- **不使用 `Cache=Shared`**：反可能引發更難纏的 `SQLITE_LOCKED`。

---

## 4. 領域模型

核心：**Book（邏輯作品 = 知識源節點）↔ BookCopy（你持有的版本，多型）**。一本書可同時有 EPUB + PDF + 實體。

### `Book`（聚合根）
| 欄位 | 說明 |
|------|------|
| `Id` | Guid |
| `Title` / `Subtitle?` | 書名 |
| `Authors` | 有序作者清單（`BookAuthor` 連結表：BookId, Order, Name） |
| `Language?` `Publisher?` `PublishedDate?` | `PublishedDate` 存字串以容忍不完整日期（如「2019」） |
| `Description?` | 簡介 |
| `Isbn?` | 數位/實體皆可有 |
| `CoverPath?` | 指向 `covers/` 的封面檔 |
| `Tags` | 多對多 → `Tag` |
| `ReadingStatus` | enum `None`/`WantToRead`/`Reading`/`Finished` |
| `Progress`（owned 值物件） | `Percent?`(0–100) + `CurrentPage?` + `TotalPages?` |
| `CreatedAt` / `UpdatedAt` | |
| `Copies` | 一對多 → `BookCopy` |
| `HasDigital` / `HasPhysical` | 衍生旗標（書架徽章與篩選用） |
| `PhysicalLocation?` | 館藏位置，例如「書房 B 櫃-第3層」 |
| `PhysicalNotes?` | 備註，例如「借給小明」 |

> 設定 `PhysicalLocation` / `PhysicalNotes` 時一併設 `HasPhysical = true`；刪除實體版本時設 `HasPhysical = false` 並清空兩欄。

### `BookCopy`（TPH 多型）
- 共同：`Id`、`BookId`、`AddedAt`、`Notes?`
- **`DigitalCopy`**：`FilePath`、`Format`(Epub/Pdf)、`FileSizeBytes`、`FileHash`、`FileLastModified`、`TocJson`、`LibraryFolderId`、`LastScannedAt`、`IsMissing`（檔案遺失旗標）、`ParseFailed`（解析失敗旗標）
- **`PhysicalCopy`**：（此型別已由 `Book` 上的 `PhysicalLocation` / `PhysicalNotes` 直接處理，目前為單一實體副本模型）

### `Tag`（一級實體，未來給筆記/資產共用）
`Id`、`Name`(唯一)、`Color?`、`CreatedAt`。與 Book 多對多。

### `LibraryFolder`（掃描資料夾）
`Id`、`Path`(唯一)、`DisplayName?`、`Enabled`、`AddedAt`、`LastScannedAt?`。一對多 → DigitalCopy。

### 關係圖
```
LibraryFolder 1───* DigitalCopy
Book 1───* BookCopy ◄── DigitalCopy
Book 1───1 (owned) Progress
Book *───* Tag
```

---

## 5. 匯入與解析

### 5.1 掃描流程
1. 設定頁加入書庫資料夾 → 按「掃描」（亦可開機自動掃）。
2. 遞迴走資料夾，找 `*.epub`、`*.pdf`。
3. 每個檔算身分 = `大小 + 最後修改時間 + 內容快速雜湊`，比對既有 `DigitalCopy`：
   - **新檔** → 解析元數據 → 建 `Book` + `DigitalCopy`。
   - **移動過**（雜湊相同、路徑不同）→ 只更新路徑。
   - **不見了**（原路徑消失）→ 標記 `IsMissing`，**不自動刪目錄項**（保住標籤/筆記/狀態）。
   - **重複檔**（同雜湊已匯入）→ 跳過、僅更新路徑。
4. 掃描在背景非同步跑（一次一個），進度用 **SSE** 即時回報。
5. **重掃不覆蓋使用者編輯**：元數據只在首次匯入時解析；之後重掃只更新檔案身分欄位。另提供手動「從檔案重讀元數據」按鈕。
6. **掃描預設每個新檔開一本新 Book**，不自動依 ISBN/書名合併。
7. **手動觸發／開機掃描**，不用 FileSystemWatcher 即時監看。讀檔以 `FileShare.Read`；鎖住 → 等 500ms 重試×3，仍鎖則跳過並列入掃描報告。

### 5.2 EPUB 解析
`System.IO.Compression.ZipArchive` + `System.Xml` 讀 `.opf`，取 Dublin Core 元數據。封面與 TOC 分別依 EPUB2/EPUB3 格式解析。

### 5.3 PDF 解析
元數據 + 頁數：**PdfPig**（純 C#）。封面：**PDFium 系算繪第 1 頁成圖**；失敗 fallback「書名佔位圖」。

### 5.4 元數據 fallback
書名缺 → 清理過的檔名；作者缺 → 「未知作者」；一切解析結果事後可手動編輯。

### 5.5 ISBN 查詢（實體書手動新增時）
`IBookMetadataProvider` 抽象。**OpenLibrary 為主（免 key）**，可選 Google Books 作 fallback。10s 逾時 → 提示 → 轉手動填。

### 5.6 封面與縮圖
存 `covers/{bookId}.{ext}`，用 **ImageSharp** 產縮圖供書架網格用。

### 5.7 效能
快速雜湊 = `大小 + 前 1MB SHA-256`，用 `ArrayPool`/`Span` 串流計算。下載書檔用 ASP.NET 串流回應。

---

## 6. API 設計

REST / JSON，前綴 `/api`。Entity 與回傳 DTO 分離；分頁回 `{ items, total, page, pageSize }`；錯誤用 ProblemDetails (RFC 7807)。`BookCopy` 以帶 `type`(`digital`/`physical`) 的 discriminated DTO 回傳。

### 書籍
| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/api/books` | 列表。參數：`search`、`tag`、`status`、`kind`、`sort`、`page/pageSize`。回摘要（封面縮圖 URL、**`formats`**、狀態/進度） |
| GET | `/api/books/{id}` | 詳情（含 copies、TOC、標籤、進度、**`physicalLocation`**、**`physicalNotes`**） |
| POST | `/api/books` | 手動新增實體書 |
| PUT | `/api/books/{id}` | 編輯書目欄位 |
| PATCH | `/api/books/{id}/reading` | 快速更新閱讀狀態/進度 |
| PATCH | `/api/books/{id}/physical` | **更新實體版本資訊**（`isPhysical`, `location`, `notes`） |
| DELETE | `/api/books/{id}` | 刪除目錄項（**永不刪硬碟書檔**） |
| POST | `/api/books/{id}/reread-metadata` | 從檔案重讀元數據 |
| GET | `/api/books/{id}/cover`・`/cover/thumb` | 封面原圖／縮圖 |

#### `PATCH /api/books/{id}/physical` 規格
```json
// Request
{ "isPhysical": true, "location": "書房 B 櫃-第3層", "notes": "借給小明" }
// isPhysical: false → 清空 location / notes 並取消實體旗標
// 回傳：更新後的 BookDetailDto；404 若書不存在
```

#### `BookSummaryDto` 新增欄位
```json
{ "formats": ["Epub", "Pdf"] }
// 數位副本去重格式清單；純實體書為空陣列 []
```

#### `BookDetailDto` 新增欄位
```json
{ "physicalLocation": "書房 B 櫃-第3層", "physicalNotes": "借給小明" }
// 兩欄皆可為 null
```

### 版本 Copy
| 方法 | 路徑 | 說明 |
|------|------|------|
| POST | `/api/books/{id}/copies` | 替既有書加數位版本 |
| PUT | `/api/copies/{copyId}` | 更新版本（備註） |
| DELETE | `/api/copies/{copyId}` | 移除數位版本 |
| GET | `/api/copies/{copyId}/file` | 串流下載/開啟數位檔 |

### 標籤 / 作者 / 書庫 / ISBN / 設定
| 方法 | 路徑 | 說明 |
|------|------|------|
| GET/POST/PUT/DELETE | `/api/tags…` | 標籤 CRUD（GET 附各標籤書數） |
| GET | `/api/authors` | 作者清單＋書數 |
| GET/POST/DELETE | `/api/library/folders…` | 書庫資料夾管理 |
| POST | `/api/library/scan` | 觸發掃描（進行中再觸發回 409） |
| GET | `/api/library/scan/stream` | SSE 即時掃描進度 |
| GET | `/api/metadata/isbn/{isbn}` | ISBN 查詢（預填表單用，不存檔） |
| GET/PUT | `/api/settings` | 應用設定 |
| GET | `/api/health` | 健康檢查 |

---

## 7. 前端

**技術**：Vue 3 + Vite + Vue Router + Pinia + Naive UI（暗色主題）；SSE 用 `EventSource`；PWA 用 `vite-plugin-pwa`。

**頁面（路由）**：
1. **書架**（首頁）：封面網格、搜尋/篩選/排序列、掃描鈕＋進度、分頁。
2. **書籍詳情**：封面、元數據、版本面板、標籤、狀態/進度、編輯/刪除。
3. **新增/編輯實體書**：ISBN 查詢自動帶入 → 表單。
4. **設定**：書庫資料夾管理、開機自動掃、預設排序、ISBN 來源、關於。

---

### 7.1 書架 BookCard（最終版：封面置頂 + 資訊列）

```
┌─────────────┐
│             │  上半部：封面 object-fit: contain，3:4 比例
│  封面 contain│  無封面 → 書名首字 placeholder
│             │
├─────────────┤
│███░░░░░░░░░░│  ← 綠色進度條 3px（封面與資訊列之間）
├─────────────┤
│ [icon] 書名⋮│  ← 實心資訊列（下半部）
└─────────────┘
```

#### 資訊列規格
| 位置 | 內容 |
|------|------|
| 左 | 格式 icon（inline SVG，PDF=紅、EPUB=藍綠，圖示內含縮寫；多格式並排最多 2 個；純實體書顯示「實體書」icon） |
| 中 | 書名，`n-ellipsis` 單行省略（**不顯示作者**） |
| 右 | ⋮ 三點選單（`NDropdown`） |

資訊列顏色用 `useThemeVars()`（`cardColor` / `textColorBase`）自動適配深淺色主題。

**移除**：左上角 📱/📚 角標（格式 icon 已取代）、閱讀狀態 tag。

#### 進度條規則
| 狀態 | 顏色 / 寬度 |
|------|------------|
| Reading（有 progressPercent） | `#18a058`（綠），progressPercent% 寬 |
| Finished | `#18a058`（綠），100% 寬 |
| None / WantToRead / 無進度 | 不顯示 |

#### 三點選單（⋮）
```
├─ 編輯書目
├─ 標記閱讀狀態 ▶
│   ├─ ✓ 未分類 (None)
│   ├─ 想讀 (WantToRead)
│   ├─ 閱讀中 (Reading)
│   └─ 已讀 (Finished)
├─ 新增實體版本
└─ ─────────────
   刪除（紅色）
```

| 選項 | 行為 |
|------|------|
| 編輯書目 | `router.push(`/books/${id}/edit`)` |
| 標記閱讀狀態 | `PATCH /api/books/{id}/reading` → emit `'refresh'` |
| 新增實體版本 | `router.push(`/books/${id}`)` |
| 刪除 | `dialog.warning` 確認框 → `DELETE /api/books/{id}` → emit `'refresh'` |

- 目前狀態的子選項 label 加 `✓` 前綴。
- ⋮ 按鈕用 `@click.stop` 阻止冒泡。

#### 元件介面
```typescript
defineProps<{ book: BookSummary }>()
defineEmits<{ refresh: [] }>()
// BookSummary 新增欄位
interface BookSummary { formats: string[] }
```

```html
<!-- LibraryView.vue -->
<book-card :book="b" @refresh="books.fetch()" />
```

---

### 7.2 書架版型
```
┌─ Knovault ─────────────────────────── [⚙ 設定] ─┐
│ [🔍 搜尋書名/作者]  類型▾ 狀態▾ 標籤▾  排序▾  [掃描]│
│ 掃描中… ████████░░░░ 42/120                         │
│ ┌───────┐ ┌───────┐ ┌───────┐ ┌───────┐            │
│ │ 封面  │ │ 封面  │ │ 封面  │ │ 封面  │            │
│ │contain│ │contain│ │contain│ │contain│            │
│ ├───────┤ ├───────┤ ├───────┤ ├───────┤            │
│ │▓▓░░░░│ │       │ │▓▓▓▓▓▓│ │       │            │
│ ├───────┤ ├───────┤ ├───────┤ ├───────┤            │
│ │📄書名⋮│ │📱書名⋮│ │📄書名⋮│ │📚書名⋮│            │
│ └───────┘ └───────┘ └───────┘ └───────┘            │
│                          ◀ 1 2 3 … ▶               │
└────────────────────────────────────────────────────┘
```

---

### 7.3 書籍詳情頁 — 版本面板（Media Panel）

#### 版面結構
```
── 版本 ──────────────────────────────── [＋ 新增實體版本]
                                         （僅 !isPhysical 時顯示）

  🏠 實體書
     📍 書房 B 櫃-第3層     （有值才顯示）
     📝 借給小明             （有值才顯示）  [📝 編輯]

  📱 EPUB  5.2 MB                        [📥 下載] [移除]

  📄 PDF   12.0 MB  ⚠ 檔案遺失                     [移除]

（空狀態：!isPhysical && copies.length === 0）
  <n-empty> 尚無版本，可新增實體版本或掃描書庫資料夾
```

#### 互動：新增 / 編輯實體版本
1. 點「＋ 新增實體版本」或「📝 編輯」→ 彈 `<n-modal>`。
2. 欄位：📍 館藏位置（選填）、📝 備註（選填）；編輯時預填現有值。
3. 送出 → `PATCH /api/books/{id}/physical`。
4. 成功 → reload book、modal 關閉。

#### 移除（已移除的舊 UI）
- 「形式（只是紀錄）」Switch。
- 「數位檔」獨立區塊。
- 「目錄 TOC」佔位區塊（後端尚未暴露）。

#### 前端型別
```typescript
interface BookDetail {
  physicalLocation: string | null  // 新增
  physicalNotes: string | null     // 新增
}

interface UpdatePhysicalRequest {
  isPhysical: boolean
  location?: string | null
  notes?: string | null
}
```

#### 書籍詳情整體版型
```
┌─ ◀ 返回 ──────────────────────── [編輯] [刪除] ─┐
│ ┌─────┐ 書名（副標）                              │
│ │封面 │ 作者 · 出版社 · 2019 · 語言  ISBN 978…    │
│ │     │ 標籤：[哲學][筆記][+]                      │
│ └─────┘ 狀態：(想讀/在讀/讀完)  進度：[ 45 ]%      │
│ 簡介：……                                          │
│ ── 版本 ───────────────────── [＋ 新增實體版本]   │
│  🏠 實體  📍書房B櫃                  [📝 編輯]    │
│  📱 EPUB  3.2MB              [📥 下載] [移除]      │
│  📄 PDF   12MB  ⚠檔案遺失            [移除]       │
└───────────────────────────────────────────────────┘
```

---

## 8. 錯誤處理與邊界情況

| 情況 | 處理 |
|------|------|
| 解析失敗（EPUB 損壞、PDF 加密） | 仍以檔名為書名匯入，標 `ParseFailed`；繼續掃下一本 |
| 元數據缺/亂 | fallback 檔名＋「未知作者」，可手改 |
| 檔案移動/刪除 | 移動→更新路徑；刪除→標 `IsMissing`，不自動刪目錄項 |
| 檔案使用中 | 等 500ms 重試×3，仍鎖則跳過並列入掃描報告 |
| 資料庫鎖定 | WAL + `busy_timeout` 30s；批次寫入 |
| 重複檔 | 跳過、只更新路徑 |
| ISBN 查詢失敗 | 10s 逾時 → 提示 → 轉手動填 |
| PDF 封面算繪失敗 | fallback「書名佔位圖」 |
| 資料夾無法存取 | 回報錯誤、跳過、不崩潰 |
| 重複觸發掃描 | 一次只跑一個；進行中再觸發回 409 |
| 埠號被占用 | 啟動時自動找下一個可用埠 |
| 刪除書 | 確認對話框；永不刪硬碟檔；移除目錄項+縮圖；標籤保留 |
| 驗證 | 書名必填；ISBN 格式檢查；進度 0–100%、現頁≤總頁 |
| 空狀態 | 未設資料夾→引導；無書→提示「掃描或新增實體書」 |

**日誌**：Serilog 寫到 `%AppData%\Knovault\logs`；每次掃描結束給報告顯示於 UI。

---

## 9. 測試策略（TDD）

| 層級 | 測什麼 | 工具 |
|------|--------|------|
| 單元 | 解析器（EPUB/PDF 元數據、TOC）、檔名清理、ISBN 驗證、快速雜湊、fallback | xUnit + FluentAssertions |
| 整合 | EF Core 倉儲、掃描流程、API 端點 | `WebApplicationFactory` |
| 外部來源 | ISBN 查詢（mock HTTP） | WireMock.Net |
| 前端 | BookCard 渲染（格式 icon、進度條、無封面）、版本面板空/有狀態、`BookSummary.formats` mapping | Vitest |

**測試重點（前端）**：
- BookCard：`formats` 驅動格式 icon（PDF紅/EPUB藍綠/純實體）；進度條 Reading/Finished/其餘；書名顯示（移除作者斷言）；無封面 placeholder。
- 版本面板：空狀態渲染；實體列有/無 location 差異；`PATCH /physical` 呼叫正確。
- 後端 mapping：含 Epub+Pdf 副本 → `Formats` 去重正確；`physicalLocation`/`physicalNotes` 回傳正確。

---

## 10. 專案結構與打包

```
Knovault.sln
src/
  Knovault.Domain/          ← 實體/列舉/值物件，零相依
  Knovault.Application/     ← 用例/介面/DTO
  Knovault.Infrastructure/  ← EF Core、Migrations、解析器、ISBN、ImageSharp、雜湊
  Knovault.Api/             ← Minimal API、SSE、託管 Vue 靜態檔、啟動邏輯、DI 接線
web/                        ← Vue3 + Vite + Naive UI
tests/
  Knovault.Domain.Tests / Knovault.Infrastructure.Tests / Knovault.Api.Tests
  fixtures/                 ← 樣本書檔（合法 EPUB、PDF、損壞檔、缺元數據各一）
```

**打包**：`dotnet publish -c Release -r win-x64 --self-contained`。

---

## 11. 已鎖定的關鍵決定（Decision Log）

| # | 決定 | 理由 |
|---|------|------|
| D1 | 整體拆 4 個子專案，先做書庫核心 | 願景過大，逐塊各自 spec→plan→實作 |
| D2 | 形態：本機伺服器 + 瀏覽器/PWA 外殼 | 滿足「雙擊執行」又不犧牲多裝置/NAS |
| D3 | 子專案 1 不含閱讀器 | 聚焦地基；閱讀器另立子專案 |
| D4 | 資料庫 SQLite（領域層抽象） | 貼合桌面雙擊形態；日後可換 Postgres |
| D5 | 資料夾掃描匯入數位書 | 貼合本機檔案與自動掃描 |
| D6 | 單人、不需登入 | 個人本機使用，YAGNI |
| D7 | 實體書手動 + ISBN 查詢（OpenLibrary 主） | 省去手打、免 API key |
| D8 | 閱讀狀態 + 手動進度 | 回應「進度最難追蹤」痛點 |
| D9 | Book ↔ 多個 DigitalCopy（TPH）+ Book 直接存實體資訊 | 一書多數位格式；單一實體副本模型簡化 |
| D10 | PDF 封面算繪第一頁 + fallback | 書架封面體驗 |
| D11 | 快速雜湊（大小+前 1MB） | 去重/偵測移動足夠且快 |
| D12 | 前端 Naive UI | Vue3 原生、輕、元件齊、暗色 |
| D13 | 後端四層（Domain/Application/Infrastructure/Api） | 邊界清楚、強制 DB 可換 |
| D14 | SQLite：WAL + busy_timeout + 集中/批次寫入；不用 Cache=Shared | 避免 `database is locked` |
| D15 | 掃描為手動觸發；`FileShare.Read` + 鎖定重試/跳過 | 避開複製未完成的搶讀 |
| D16 | Copy 以帶 `type` 的 discriminated DTO + pattern matching 分流 | 防 TPH 型別誤用 |
| D17 | BookCard：封面置頂（contain）+ 實心資訊列 + 格式 icon（取代角標）+ 進度條 | 視覺更清晰；格式 icon 比角標資訊豐富 |
| D18 | BookSummaryDto 新增 `formats` 欄位（去重格式清單） | 前端格式 icon 需要，無需額外查詢（已 Include Copies） |
| D19 | 實體書資訊（Location/Notes）存 Book 而非獨立 PhysicalCopy 表 | 單一實體副本場景夠用；簡化模型與 API |
| D20 | 版本面板統一呈現實體+數位；移除語意不清的 Switch | 單一入口管理所有版本，UX 更直覺 |

---

## 12. 後續子專案（Roadmap）

- **子專案 2 — 卡片盒筆記**：Markdown 卡片、`[[雙向鏈結]]`、反向連結、知識圖譜。
- **子專案 3 — 多模態資產**：圖片上傳、標籤、`![[Asset_ID]]` 內嵌。
- **子專案 4 — 搜尋與進階**：全文 + 向量搜尋、動態標籤過濾。
- **獨立子專案 — 閱讀器**：瀏覽器內 EPUB/PDF 閱讀 + 自動閱讀進度。
- **未來擴充 — 多筆實體副本**：若需要記錄多本實體，需重構為獨立 `PhysicalCopy` 表（現行 Book 直存模型將成瓶頸）。
