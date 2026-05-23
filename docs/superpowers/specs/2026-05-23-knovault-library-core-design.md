# Knovault — 子專案 1：書庫核心 (Library Core) 設計文件

- **日期**：2026-05-23
- **狀態**：設計定案，待寫實作計畫
- **範圍**：Knovault 整體願景的「第一個子專案」。本文件只涵蓋書庫核心。

---

## 1. 背景與整體願景

Knovault（芝士庫）是一個自託管的個人知識管理（PKM）系統，把「書」當作知識的源頭節點，整合三個原本分散的孤島：數位書庫、實體書櫃、筆記軟體。完整願景包含雙軌書庫、卡片盒雙鏈筆記、多模態圖片資產、向量搜尋等。

由於整體願景橫跨多個獨立子系統，**無法用單一份 spec 涵蓋**，因此拆分為循序漸進的子專案，每個各自走 spec → plan → 實作：

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
- **使用形態**：A. 本機伺服器 + 視窗。打包成可雙擊執行的 Windows 程式，啟動本機 Kestrel 伺服器並自動開瀏覽器；可在 Edge/Chrome「安裝」成獨立視窗 PWA。同網段手機/平板可用瀏覽器連 `http://<電腦IP>:<port>`。
- **單一執行檔**：`dotnet publish -r win-x64 --self-contained`；Vue 打包後靜態檔由 Kestrel 託管。未來同一套程式碼可 target linux 上 Docker 到 NAS。
- **桌面外殼**：以瀏覽器 / PWA 為外殼，**不使用** WebView2/Electron/Photino 原生外殼。
- **資料落地**：`%AppData%\Knovault\` —— `knovault.db`（SQLite）、`covers/`、`config.json`（埠號等啟動設定）、`logs/`。**書檔本身留在原地不搬動**，書庫資料夾以唯讀方式對待。備份只需複製此資料夾。

### 技術選型
- 後端：.NET 8/9 (C#)、ASP.NET Core Minimal API、EF Core、**SQLite**（領域層不依賴具體 DB，日後可換 PostgreSQL）。
- 前端：Vue 3（`<script setup>`）、Vite、Vue Router、Pinia、**Naive UI**（含暗色主題）、`vite-plugin-pwa`。
- 登入：**單人、不需登入**（信任內網）。

### SQLite 並發設定（避免 `database is locked`）
WAL 下 SQLite 仍是**單一寫入者**：背景掃描在寫、使用者同時 PATCH 會撞 `SQLITE_BUSY`。對策：
- 連線開 **WAL + `busy_timeout` 30 秒**（鎖定時於驅動層等待重試，而非拋錯崩潰）。
- **寫入集中在掃描服務**，並**批次** `SaveChanges`（每 N 本一次），縮短鎖定窗口；使用者 PATCH 為偶發，靠 busy_timeout 等待即可。
- **不使用 `Cache=Shared`**：shared-cache 主要用於共享 in-memory DB，對檔案型並發無益、反可能引發更難纏的 `SQLITE_LOCKED`。
- 註：EF Core 的 SQLite provider 無內建重試執行策略（`EnableRetryOnFailure` 屬 SQL Server），實際靠 busy_timeout。

---

## 4. 領域模型

核心：**Book（邏輯作品 = 知識源節點）↔ BookCopy（你持有的版本，多型）**。一本書可同時有 EPUB + PDF + 實體。

### `Book`（聚合根，單一實體）
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

> 閱讀狀態/進度、封面屬於「作品」放在 Book；TOC 屬於各檔放在 DigitalCopy。

### `BookCopy`（TPH 多型，一本書可有多個）
- 共同：`Id`、`BookId`、`AddedAt`、`Notes?`
- **`DigitalCopy`**：`FilePath`、`Format`(Epub/Pdf)、`FileSizeBytes`、`FileHash`、`FileLastModified`、`TocJson`、`LibraryFolderId`、`LastScannedAt`、`IsMissing`（檔案遺失旗標）、`ParseFailed`（解析失敗旗標）
- **`PhysicalCopy`**：`Location?`（可空）、`AcquiredDate?`

### `Tag`（一級實體，未來給筆記/資產共用）
`Id`、`Name`(唯一)、`Color?`、`CreatedAt`。與 Book 多對多。

### `LibraryFolder`（掃描資料夾）
`Id`、`Path`(唯一)、`DisplayName?`、`Enabled`、`AddedAt`、`LastScannedAt?`。一對多 → DigitalCopy。

### 關係圖
```
LibraryFolder 1───* DigitalCopy
Book 1───* BookCopy ◄┬─ DigitalCopy
                     └─ PhysicalCopy
Book *───* Tag
Book 1───1 (owned) Progress
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
5. **重掃不覆蓋使用者編輯**：元數據只在首次匯入時解析；之後重掃只更新檔案身分欄位（路徑/雜湊/大小/掃描時間）。另提供手動「從檔案重讀元數據」按鈕。
6. **掃描預設每個新檔開一本新 Book**，不自動依 ISBN/書名合併（避免誤併）。要把版本歸到既有書（如數位書補登實體版、EPUB+PDF 合為一書）是手動動作，走詳情頁「新增版本」或新增實體書時選「歸到既有書」。靠 ISBN 自動合併留待後續子專案。
7. **觸發方式為手動／開機掃描，不是 FileSystemWatcher 即時監看**（故「複製到一半就被搶讀」機率低）。讀檔算雜湊/解析時以 `FileShare.Read` 開啟；若檔案被占用（複製未完成）→ 等 500ms 重試最多 3 次，仍鎖住則**跳過並列入掃描報告**，下次重掃再處理。資料庫寫入**每 N 本批次** `SaveChanges`（見 §3 SQLite 並發設定）。

### 5.2 EPUB 解析（內建，無重依賴）
- `System.IO.Compression.ZipArchive` + `System.Xml` 讀 `META-INF/container.xml` → `.opf`。
- 取 Dublin Core：書名、作者（含 `file-as`）、語言、出版社、日期、ISBN（`dc:identifier`）、簡介。
- 封面：EPUB3 看 manifest `properties="cover-image"`；EPUB2 看 `<meta name="cover">`。
- TOC：EPUB3 讀 `nav.xhtml`，EPUB2 讀 `toc.ncx` → 序列化成 `TocJson`。

### 5.3 PDF 解析
- 元數據 + 頁數：用 **PdfPig**（純 C#）取 Info 字典 Title/Author + 總頁數（自動帶入 `Progress.TotalPages`）。
- 封面：**用 PDFium 系算繪庫算繪第 1 頁成圖**；失敗 fallback「書名佔位圖」。

### 5.4 元數據 fallback
- 書名缺 → 清理過的檔名；作者缺 → 「未知作者」；一切解析結果事後可手動編輯。

### 5.5 ISBN 查詢（實體書手動新增時）
- 抽象 `IBookMetadataProvider`。**OpenLibrary 為主（免 key）**，介面上可選 Google Books 作為 fallback。
- 輸入 ISBN → 帶入書名/作者/出版社/日期/簡介/封面/頁數 → 可手改 → 存。
- 10s 逾時 → 提示 → 轉手動填；查無/網路失敗同樣轉手動。

### 5.6 封面與縮圖
- 擷取/下載的封面存 `covers/{bookId}.{ext}`，並用 **ImageSharp** 產縮圖供書架網格用。

### 5.7 效能落點
- **檔案雜湊**：快速雜湊 = `大小 + 前 1MB SHA-256`，用 `ArrayPool`/`Span` 串流計算（足夠去重/偵測移動，極低碰撞）。
- **下載書檔**：用 ASP.NET 串流檔案結果（底層 pipelines），記憶體平穩。
- 不過度雕琢：小 XML 直接讀，不硬套 Span。

---

## 6. API 設計

REST / JSON，前綴 `/api`。Entity 與回傳 DTO 分離；分頁回 `{ items, total, page, pageSize }`；錯誤用 ProblemDetails (RFC 7807)；不做版本號。實作用 Minimal API，依功能分組。

> **Copy 序列化（避免 TPH 型別誤用）**：`BookCopy` 不直接吐給前端；以**帶 `type`(`digital`/`physical`) 的 discriminated DTO** 回傳，mapper 端用 C# pattern matching 分流，前端拿到乾淨的強型別物件。（TPH 為單表查詢、無 join，不會過度查閱；要防的是 mapping 時誤把實體當數位讀 `FilePath`。）

### 書籍
| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/api/books` | 列表。參數：`search`、`tag`、`status`、`kind`、`sort`、`page/pageSize`。回摘要（封面縮圖 URL、版本徽章、狀態/進度） |
| GET | `/api/books/{id}` | 詳情（含 copies、TOC、標籤、進度） |
| POST | `/api/books` | 手動新增實體書 |
| PUT | `/api/books/{id}` | 編輯書目欄位 |
| PATCH | `/api/books/{id}/reading` | 快速更新閱讀狀態/進度 |
| DELETE | `/api/books/{id}` | 刪除目錄項（**永不刪硬碟書檔**） |
| POST | `/api/books/{id}/reread-metadata` | 從檔案重讀元數據 |
| GET | `/api/books/{id}/cover`・`/cover/thumb` | 封面原圖／縮圖 |

### 版本 Copy
| 方法 | 路徑 | 說明 |
|------|------|------|
| POST | `/api/books/{id}/copies` | 替既有書加版本 |
| PUT | `/api/copies/{copyId}` | 更新版本（實體位置、備註） |
| DELETE | `/api/copies/{copyId}` | 移除版本 |
| GET | `/api/copies/{copyId}/file` | 串流下載/開啟數位檔 |

### 標籤 / 作者 / 書庫 / ISBN / 設定
| 方法 | 路徑 | 說明 |
|------|------|------|
| GET/POST/PUT/DELETE | `/api/tags…` | 標籤 CRUD（GET 附各標籤書數） |
| GET | `/api/authors` | 作者清單＋書數 |
| GET/POST/DELETE | `/api/library/folders…` | 書庫資料夾管理 |
| POST | `/api/library/scan` | 觸發掃描，回 scan job id（進行中再觸發回 409） |
| GET | `/api/library/scan/stream` | SSE 即時掃描進度 |
| GET | `/api/metadata/isbn/{isbn}` | ISBN 查詢（回候選元數據預填表單，不存檔） |
| GET/PUT | `/api/settings` | 應用設定（開機自動掃、預設排序、ISBN 來源…） |
| GET | `/api/health` | 健康檢查 |

---

## 7. 前端

**技術**：Vue 3 + Vite + Vue Router + Pinia + Naive UI（暗色主題）；SSE 用 `EventSource`；PWA 用 `vite-plugin-pwa`。打包後靜態檔由 Kestrel 託管。**不含 epub.js/pdf.js**。

**頁面（路由）**：
1. **書架**（首頁）：封面網格、搜尋/篩選/排序列、掃描鈕＋進度、分頁。作者/標籤瀏覽做成篩選。
2. **書籍詳情**：封面、元數據、版本清單、TOC、標籤、狀態/進度、編輯/刪除。
3. **新增/編輯實體書**：ISBN 查詢自動帶入 → 表單。
4. **設定**：書庫資料夾管理、開機自動掃、預設排序、ISBN 來源、關於。

### 書架版型
```
┌─ Knovault ─────────────────────────────── [⚙ 設定] ─┐
│ [🔍 搜尋書名/作者]  類型▾ 狀態▾ 標籤▾  排序▾  [掃描] │
│ 掃描中… ████████░░░░ 42/120                            │
│ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐             │
│ │封面 │ │封面 │ │封面 │ │封面 │ │封面 │             │
│ │📱📚 │ │📄   │ │📚⚠ │ │📱   │ │📄📚 │             │
│ │書名 │ │書名 │ │書名 │ │書名 │ │書名 │             │
│ │作者 │ │作者 │ │作者 │ │作者 │ │作者 │             │
│ │●在讀│ │○想讀│ │✓讀完│ │●在讀│ │○想讀│             │
│ └─────┘ └─────┘ └─────┘ └─────┘ └─────┘             │
│                              ◀ 1 2 3 … ▶              │
└───────────────────────────────────────────────────────┘
```

### 書籍詳情版型
```
┌─ ◀ 返回 ───────────────────────────── [編輯] [刪除] ─┐
│ ┌─────┐ 書名（副標）                                  │
│ │封面 │ 作者 · 出版社 · 2019 · 語言   ISBN 978…       │
│ │     │ 標籤：[哲學][筆記][+]                          │
│ └─────┘ 狀態：(想讀/在讀/讀完)  進度：[ 45 ]% ▕▆▆▆▁▁│
│ 簡介：………………………………………………               │
│ ─ 我擁有的版本 ─────────────────────────────────────  │
│  📱 EPUB 3.2MB              [下載/開啟] [移除]         │
│  📄 PDF  12MB ⚠檔案遺失     [重新定位] [移除]          │
│  📚 實體 位置：書房 B 櫃-第3層  [編輯] [移除]          │
│  [＋ 新增版本]                                          │
│ ─ 目錄 TOC ─────────────────────────────────────────  │
│  ▸ 第一章 …   ▸ 第二章 …                                │
└────────────────────────────────────────────────────────┘
```

---

## 8. 錯誤處理與邊界情況

原則：**一個壞檔不能拖垮整個掃描，也不能弄丟使用者資料。**

| 情況 | 處理 |
|------|------|
| 解析失敗（EPUB 損壞、PDF 加密、XML 壞） | 仍以檔名為書名匯入，標 `ParseFailed`「⚠ 解析失敗」；記錄原因；繼續掃下一本 |
| 元數據缺/亂 | fallback 檔名＋「未知作者」，可手改 |
| 檔案移動/刪除 | 移動→更新路徑；刪除→標 `IsMissing`，不自動刪目錄項 |
| 檔案使用中（複製未完成/被占用） | 以 `FileShare.Read` 開啟；鎖住→等 500ms 重試×3→仍鎖則跳過並列入掃描報告 |
| 資料庫鎖定（掃描+使用者同時寫） | WAL + `busy_timeout` 30s 等待重試；寫入集中掃描服務並批次（見 §3） |
| 重複檔（同雜湊） | 跳過、只更新路徑 |
| ISBN 查詢失敗（網路/逾時/查無） | 10s 逾時 → 提示 → 轉手動填 |
| PDF 第一頁算繪失敗 | fallback「書名佔位圖」 |
| 資料夾無法存取（路徑沒了/權限/NAS 離線） | 回報該資料夾錯誤、跳過、不崩潰；設定頁顯示狀態 |
| 重複觸發掃描 | 一次只跑一個；進行中再觸發回 409／當前 job |
| 埠號被占用 | 啟動時自動找下一個可用埠、更新 config、開瀏覽器到正確網址 |
| 刪除書 | 確認對話框；永不刪硬碟檔；移除目錄項＋copies＋縮圖；標籤保留 |
| 移除書庫資料夾 | 預設保留書、把該資料夾數位版本標遺失；另提供「連同其書一起移除」 |
| 刪除使用中的標籤 | 確認後從所有書移除 |
| 驗證 | 實體書至少要有書名；ISBN 格式檢查；進度 0–100%、現頁≤總頁 |
| 空狀態 | 未設資料夾 → 引導；無書 → 「掃描或新增實體書」提示 |

**日誌與掃描報告**：Serilog 寫到 `%AppData%\Knovault\logs`；每次掃描結束給報告（成功 X 本、失敗 Y 本及原因）顯示在 UI。
**資料安全**：SQLite 開 **WAL 模式**；封面/縮圖可重生；備份只需複製 `%AppData%\Knovault\`。

---

## 9. 測試策略（TDD）

測試先行，重點壓在邏輯藏 bug 處，CRUD 接線從簡。

| 層級 | 測什麼 | 工具 |
|------|--------|------|
| 單元 | 解析器（EPUB/PDF 元數據、TOC）、檔名清理、ISBN 驗證、快速雜湊、fallback | xUnit + FluentAssertions |
| 整合 | EF Core 倉儲（臨時 SQLite 檔）、掃描流程（臨時資料夾放樣本書）、API 端點 | `WebApplicationFactory` |
| 外部來源 | ISBN 查詢（mock HTTP，不打真網路；錄製回應合約測試） | WireMock.Net / stub handler |
| 前端 | store／元件單元測試；E2E 冒煙先少量或延後 | Vitest |

**測試資料**：repo 內附小樣本 fixtures——合法 EPUB、PDF、損壞檔、缺元數據檔各一。

---

## 10. 專案結構與打包

```
Knovault.sln
src/
  Knovault.Domain/          ← 實體/列舉/值物件（Book, BookCopy, Tag…），零相依
  Knovault.Application/     ← 用例/介面（IBookMetadataProvider、掃描、查詢）、DTO
  Knovault.Infrastructure/  ← EF Core(SQLite)、DbContext、Migrations、EPUB/PDF 解析、
                              ISBN(OpenLibrary/GoogleBooks)、檔案系統、封面/縮圖(ImageSharp)、雜湊
  Knovault.Api/             ← 單一執行檔：Minimal API、SSE、託管 Vue 靜態檔、
                              啟動邏輯（找空閒埠、開瀏覽器）、DI 接線
web/                        ← Vue3 + Vite + Naive UI（publish 時建置進 Api/wwwroot）
tests/
  Knovault.Domain.Tests / Knovault.Infrastructure.Tests / Knovault.Api.Tests
  fixtures/                 ← 樣本書檔
```

**分層關鍵**：`Domain`／`Application` 不依賴 SQLite —— 日後換 PostgreSQL、上 NAS Docker 只動 `Infrastructure`。
**打包**：`dotnet publish -c Release -r win-x64 --self-contained` → 單一自包含執行檔；同碼可 target linux 上 Docker。

---

## 11. 已鎖定的關鍵決定（Decision Log）

| # | 決定 | 理由 |
|---|------|------|
| D1 | 整體拆 4 個子專案，先做書庫核心 | 願景過大，逐塊各自 spec→plan→實作 |
| D2 | 形態 A：本機伺服器 + 瀏覽器/PWA 外殼 | 滿足「雙擊執行」又不犧牲多裝置/NAS |
| D3 | 子專案 1 不含閱讀器 | 聚焦地基與差異化（知識整合）；閱讀器另立子專案 |
| D4 | 資料庫 SQLite（領域層抽象） | 貼合桌面雙擊形態；日後可換 Postgres |
| D5 | 資料夾掃描匯入數位書 | 貼合「自動掃描」與本機檔案 |
| D6 | 單人、不需登入 | 個人本機使用，YAGNI |
| D7 | 實體書手動 + ISBN 查詢（OpenLibrary 主） | 省去手打、免 API key |
| D8 | 閱讀狀態 + 手動進度 | 回應「進度最難追蹤」痛點 |
| D9 | Book ↔ 多個 BookCopy（TPH） | 一本書可同時電子+實體+多格式 |
| D10 | PDF 封面算繪第一頁 + fallback | 書架封面體驗 |
| D11 | 快速雜湊（大小+前 1MB） | 去重/偵測移動足夠且快 |
| D12 | 前端 Naive UI | Vue3 原生、輕、元件齊、暗色 |
| D13 | 後端四層（Domain/Application/Infrastructure/Api） | 邊界清楚、強制 DB 可換 |
| D14 | SQLite 並發：WAL + busy_timeout + 集中/批次寫入；**不用 Cache=Shared** | 避免 `database is locked`；Cache=Shared 反增 `SQLITE_LOCKED` 風險 |
| D15 | 掃描為手動/開機觸發（非即時 watcher）；讀檔 `FileShare.Read` + 鎖定重試/跳過 | 避開複製未完成的搶讀；一個壞檔不拖垮整批 |
| D16 | Copy 以帶 `type` 的 discriminated DTO 暴露 + pattern matching 分流 | 防 TPH 型別誤用、不把實體吐前端 |

---

## 12. 後續子專案（Roadmap）

- **子專案 2 — 卡片盒筆記**：Markdown 卡片、`[[雙向鏈結]]`、反向連結、筆記連到書、知識圖譜。Tag 一級實體已就緒可共用。
- **子專案 3 — 多模態資產**：圖片上傳（Pipelines）、標籤、`![[Asset_ID]]` 內嵌進筆記。
- **子專案 4 — 搜尋與進階**：全文 + 向量搜尋（換/加 PostgreSQL + pgvector 或 sqlite-vec）、動態標籤過濾。
- **獨立子專案 — 閱讀器**：瀏覽器內 EPUB/PDF 閱讀（epub.js/pdf.js）+ 自動閱讀進度。
