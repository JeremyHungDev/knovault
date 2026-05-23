# Knovault 書庫核心 — P5 前端實作計畫 (Vue SPA)

- **日期**：2026-05-23
- **分支**：`feat/library-core-p5`（自 `dev` 切出）
- **範圍**：建置 spec §7 的四個頁面 Vue 3 SPA，消費 P1–P4 的 REST API，打包進 `src/Knovault.Api/wwwroot`。

## 目標與驗收

- `npm install` 能用公開 registry 安裝（若被擋則停下回報）。
- `npm run build` 成功，輸出落在 `../src/Knovault.Api/wwwroot`（`emptyOutDir: true`）。
- 少量 Vitest 單元測試（stores / 元件）通過。
- 後端契約對齊：路由、camelCase 一般回應、SSE `progress`(PascalCase) / `done`(PascalCase)。
- `feat/library-core-p5` 推上 origin，**不**合併進 `dev`（留給人工視覺審查）。

## 技術選型（per spec §7）

- Vue 3 `<script setup>` + Vite + Vue Router + Pinia + Naive UI（明暗主題）+ `vite-plugin-pwa`。
- SSE 用原生 `EventSource`。
- 開發 proxy：`/api` → `http://localhost:5279`。

## 契約注意點（從原始碼確認）

- 一般 JSON 回應為 **camelCase**（ASP.NET Core 預設）。
- SSE 串流用原生 `JsonSerializer.Serialize`，無設定 → **PascalCase**：
  - `event: progress` → `{ Processed, Total, CurrentFile }`
  - `event: done` → `{ Added, Updated, Skipped, MarkedMissing, Failures[] }`
- 清單 `GET /api/books` 後端目前僅支援 `search` / `page` / `pageSize`（`tag`/`status`/`kind`/`sort` 尚未實作）→ 前端送出這些參數無害，但 type/status/tag/sort 額外於前端過濾與排序，並標註此落差。
- `BookDetailDto` 不含 TOC 欄位 → 詳情頁 TOC 區塊以「後端尚未暴露 TOC」佔位處理。
- Copy 為 discriminated DTO：`type: "digital"|"physical"`，digital 才有 `format/fileSizeBytes/isMissing/parseFailed`，physical 才有 `location`。
- `PUT /api/copies/{id}` 與 `POST /api/books/{id}/copies` 僅支援實體版本（location/notes）。
- 封面縮圖：`/api/books/{id}/cover/thumb`；原圖 `/api/books/{id}/cover`。
- 下載數位檔：`/api/copies/{id}/file`。

## 任務拆解（逐項驗證 + 提交）

1. **計畫文件**（本檔）→ 提交。
2. **Scaffold web/**：`package.json`、`vite.config.ts`、`tsconfig`、`index.html`、`main.ts`、`App.vue`、Router、Naive UI provider、主題切換。`build.outDir` 指向 wwwroot。`npm install` + `npm run build` 通過。
3. **API client + stores**：`api/http.ts`（fetch 封裝、ProblemDetails 解析）、`api/types.ts`、Pinia stores：`books`、`tags`、`authors`、`library`(folders+scan SSE)、`theme`。
4. **書架頁**：封面網格、搜尋/篩選/排序列、掃描鈕 + SSE 進度條、分頁、空狀態。
5. **書籍詳情頁**：封面、元數據、版本清單（下載/位置/遺失/解析失敗徽章）、標籤指派/移除、閱讀狀態與進度、編輯/刪除、新增實體版本。
6. **新增/編輯實體書頁**：ISBN 查詢自動帶入 → 表單 → POST/PUT。
7. **設定頁**：書庫資料夾 CRUD、觸發掃描、主題、關於。
8. **Vitest 測試**：stores（SSE 解析、查詢參數）、一兩個純函式/元件。`npx vitest run` 通過。
9. **PWA**：`vite-plugin-pwa` manifest + autoUpdate。
10. **.gitignore + wwwroot**：把 `src/Knovault.Api/wwwroot/` 視為產物加入 `.gitignore`；從版控移除既有 placeholder `index.html`（`git rm --cached`）。
11. **收尾**：build + tests 綠燈 → squash 成 ~2 commits（計畫 + 實作）→ push。**不 merge dev**。

## 風險 / 待人工確認

- 無法看到實際 UI 渲染，僅以 build 成功 + 單元測試把關。
- 後端 `GET /api/books` 篩選/排序未完整實作；前端做 client-side 補強，需人工確認體驗。
- TOC 後端未暴露於 DetailDto，詳情頁 TOC 暫為佔位。
- 數位版本「重新定位/重讀元數據」後端有端點（reread-metadata）但部分互動仍待人工確認。
