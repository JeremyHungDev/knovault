# Book Media Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 後端補實體書位置欄位與新端點；前端書籍詳情頁將「形式 Switch」與「數位檔」兩區塊合併為統一「版本」面板。

**Architecture:** 在 `Book` entity 加 `PhysicalLocation`/`PhysicalNotes` 欄位，新增 `PATCH /api/books/{id}/physical` 端點，前端詳情頁換掉舊 Switch 並以清單渲染所有媒體版本（實體 + 數位）。不動 `DigitalCopy` 結構、不動書目編輯路徑。

**Tech Stack:** .NET 8 / EF Core / SQLite（後端）；Vue 3 `<script setup>` + Naive UI + Pinia（前端）；xUnit + FluentAssertions（後端測試）；Vitest（前端測試）。

---

## 檔案異動總覽

| 動作 | 路徑 |
|------|------|
| 修改 | `src/Knovault.Domain/Entities/Book.cs` |
| 修改 | `src/Knovault.Api/Contracts/BookDetailDto.cs` |
| 新增 | `src/Knovault.Api/Contracts/UpdatePhysicalRequest.cs` |
| 修改 | `src/Knovault.Api/Mapping/BookMappings.cs` |
| 修改 | `src/Knovault.Api/Endpoints/BookEndpoints.cs` |
| Migration | `dotnet ef migrations add AddBookPhysicalLocation` |
| 修改 | `tests/Knovault.Domain.Tests/BookTests.cs` |
| 修改 | `tests/Knovault.Api.Tests/BookEndpointsTests.cs` |
| 修改 | `web/src/api/types.ts` |
| 修改 | `web/src/api/books.ts` |
| 修改 | `web/src/views/BookDetailView.vue` |

---

## Task 1：Domain — Book entity 補實體書位置

**Files:**
- Modify: `src/Knovault.Domain/Entities/Book.cs`
- Test: `tests/Knovault.Domain.Tests/BookTests.cs`

- [ ] **Step 1：寫失敗測試**

在 `tests/Knovault.Domain.Tests/BookTests.cs` 的 `BookTests` 類別底部加入：

```csharp
[Fact]
public void SetPhysicalInfo_with_true_sets_location_and_notes()
{
    var book = NewBook();
    book.SetPhysicalInfo(true, "書房 B 櫃-第3層", "借給小明");

    book.IsPhysical.Should().BeTrue();
    book.PhysicalLocation.Should().Be("書房 B 櫃-第3層");
    book.PhysicalNotes.Should().Be("借給小明");
    book.HasPhysical.Should().BeTrue();
}

[Fact]
public void SetPhysicalInfo_with_false_clears_location_and_notes()
{
    var book = NewBook();
    book.SetPhysicalInfo(true, "書房 A 櫃", "備註");
    book.SetPhysicalInfo(false, null, null);

    book.IsPhysical.Should().BeFalse();
    book.PhysicalLocation.Should().BeNull();
    book.PhysicalNotes.Should().BeNull();
}
```

- [ ] **Step 2：跑測試確認失敗**

```
dotnet test tests/Knovault.Domain.Tests --filter "SetPhysicalInfo" -v minimal
```

預期：`FAILED` — `Book does not contain a definition for 'SetPhysicalInfo'`

- [ ] **Step 3：實作 Book entity 異動**

在 `src/Knovault.Domain/Entities/Book.cs` 做以下修改：

在 `IsPhysical` 行下面加兩個欄位：

```csharp
public bool IsPhysical { get; private set; }
public string? PhysicalLocation { get; private set; }   // 新增
public string? PhysicalNotes { get; private set; }      // 新增
```

保留現有 `SetPhysical(bool isPhysical)` 方法不動（`CreatePhysicalBook` 端點仍使用它）。

在 `SetPhysical` 方法下面新增：

```csharp
public void SetPhysicalInfo(bool isPhysical, string? location, string? notes)
{
    IsPhysical = isPhysical;
    PhysicalLocation = isPhysical ? location?.Trim() : null;
    PhysicalNotes = isPhysical ? notes?.Trim() : null;
    Touch();
}
```

- [ ] **Step 4：跑測試確認通過**

```
dotnet test tests/Knovault.Domain.Tests --filter "SetPhysicalInfo" -v minimal
```

預期：`PASSED`（2 筆）

- [ ] **Step 5：Commit**

```
git add src/Knovault.Domain/Entities/Book.cs tests/Knovault.Domain.Tests/BookTests.cs
git commit -m "domain: Book 補 PhysicalLocation/PhysicalNotes + SetPhysicalInfo 方法"
```

---

## Task 2：Infrastructure — EF Migration

**Files:**
- 自動產生：`src/Knovault.Infrastructure/Persistence/Migrations/...`

- [ ] **Step 1：產生 Migration**

```
dotnet ef migrations add AddBookPhysicalLocation --project src/Knovault.Infrastructure --startup-project src/Knovault.Api
```

預期：產生新的 Migration 檔，包含 `AddColumn` 兩行。

- [ ] **Step 2：確認 Migration 內容正確**

開啟剛產生的 Migration `.cs`，確認含有類似：

```csharp
migrationBuilder.AddColumn<string>(
    name: "PhysicalLocation",
    table: "Books",
    type: "TEXT",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "PhysicalNotes",
    table: "Books",
    type: "TEXT",
    nullable: true);
```

若有其他非預期異動，檢查原因後修正。

- [ ] **Step 3：Commit**

```
git add src/Knovault.Infrastructure/Persistence/Migrations/
git commit -m "migration: AddBookPhysicalLocation"
```

---

## Task 3：API — 合約、Mapping、新端點

**Files:**
- Create: `src/Knovault.Api/Contracts/UpdatePhysicalRequest.cs`
- Modify: `src/Knovault.Api/Contracts/BookDetailDto.cs`
- Modify: `src/Knovault.Api/Mapping/BookMappings.cs`
- Modify: `src/Knovault.Api/Endpoints/BookEndpoints.cs`
- Test: `tests/Knovault.Api.Tests/BookEndpointsTests.cs`

- [ ] **Step 1：寫失敗整合測試**

在 `tests/Knovault.Api.Tests/BookEndpointsTests.cs` 底部加入：

```csharp
[Fact]
public async Task Patch_physical_sets_location_and_returns_detail()
{
    var client = _factory.CreateClient();

    // 先建一本實體書
    var createResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest
    {
        Title = "DDD 紅書",
        Authors = new() { "Eric Evans" }
    });
    var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

    // PATCH physical
    var patchResp = await client.PatchAsJsonAsync(
        $"/api/books/{book!.Id}/physical",
        new { isPhysical = true, location = "書房 B 櫃", notes = "精裝本" });
    patchResp.StatusCode.Should().Be(HttpStatusCode.OK);

    var updated = await patchResp.Content.ReadFromJsonAsync<BookDetailDto>();
    updated!.IsPhysical.Should().BeTrue();
    updated.PhysicalLocation.Should().Be("書房 B 櫃");
    updated.PhysicalNotes.Should().Be("精裝本");
}

[Fact]
public async Task Patch_physical_false_clears_fields()
{
    var client = _factory.CreateClient();

    var createResp = await client.PostAsJsonAsync("/api/books", new CreatePhysicalBookRequest
    {
        Title = "測試書",
        Authors = new() { "作者" }
    });
    var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

    // 先設位置
    await client.PatchAsJsonAsync(
        $"/api/books/{book!.Id}/physical",
        new { isPhysical = true, location = "書房", notes = "備註" });

    // 再取消
    var clearResp = await client.PatchAsJsonAsync(
        $"/api/books/{book.Id}/physical",
        new { isPhysical = false });
    clearResp.StatusCode.Should().Be(HttpStatusCode.OK);

    var cleared = await clearResp.Content.ReadFromJsonAsync<BookDetailDto>();
    cleared!.IsPhysical.Should().BeFalse();
    cleared.PhysicalLocation.Should().BeNull();
    cleared.PhysicalNotes.Should().BeNull();
}

[Fact]
public async Task Patch_physical_missing_book_returns_404()
{
    var client = _factory.CreateClient();
    var resp = await client.PatchAsJsonAsync(
        $"/api/books/{Guid.NewGuid()}/physical",
        new { isPhysical = true });
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 2：跑測試確認失敗**

```
dotnet test tests/Knovault.Api.Tests --filter "Patch_physical" -v minimal
```

預期：`FAILED` — 404 或連線錯誤（端點不存在）

- [ ] **Step 3：建立 UpdatePhysicalRequest**

新增 `src/Knovault.Api/Contracts/UpdatePhysicalRequest.cs`：

```csharp
namespace Knovault.Api.Contracts;

public sealed record UpdatePhysicalRequest
{
    public bool IsPhysical { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
```

- [ ] **Step 4：BookDetailDto 補欄位**

修改 `src/Knovault.Api/Contracts/BookDetailDto.cs`，在 `IsPhysical` 後面加：

```csharp
public bool IsPhysical { get; init; }
public string? PhysicalLocation { get; init; }   // 新增
public string? PhysicalNotes { get; init; }      // 新增
```

- [ ] **Step 5：BookMappings 補 mapping**

修改 `src/Knovault.Api/Mapping/BookMappings.cs` 的 `ToDetailDto`，在 `IsPhysical = b.IsPhysical,` 後加：

```csharp
IsPhysical = b.IsPhysical,
PhysicalLocation = b.PhysicalLocation,   // 新增
PhysicalNotes = b.PhysicalNotes,         // 新增
```

- [ ] **Step 6：新增 PATCH 端點**

在 `src/Knovault.Api/Endpoints/BookEndpoints.cs` 的 `MapBookEndpoints` 方法裡，在 `group.MapDelete` 那行之前加一行：

```csharp
group.MapPatch("/{id:guid}/physical", UpdatePhysical);
```

在同一個 class 底部加 handler：

```csharp
private static async Task<IResult> UpdatePhysical(KnovaultDbContext db, Guid id, UpdatePhysicalRequest req)
{
    var book = await db.Books
        .Include(b => b.Copies)
        .Include(b => b.Tags)
        .FirstOrDefaultAsync(b => b.Id == id);
    if (book is null) return Results.NotFound();
    book.SetPhysicalInfo(req.IsPhysical, req.Location, req.Notes);
    await db.SaveChangesAsync();
    return Results.Ok(book.ToDetailDto());
}
```

- [ ] **Step 7：跑測試確認通過**

```
dotnet test tests/Knovault.Api.Tests --filter "Patch_physical" -v minimal
```

預期：`PASSED`（3 筆）

- [ ] **Step 8：跑全部後端測試確認無迴歸**

```
dotnet test -v minimal
```

預期：所有測試 `PASSED`，0 失敗。

- [ ] **Step 9：Commit**

```
git add src/Knovault.Api/Contracts/ src/Knovault.Api/Mapping/ src/Knovault.Api/Endpoints/ tests/Knovault.Api.Tests/
git commit -m "api: PATCH /books/{id}/physical 端點 + BookDetailDto 補位置欄位"
```

---

## Task 4：前端型別與 API 層

**Files:**
- Modify: `web/src/api/types.ts`
- Modify: `web/src/api/books.ts`

- [ ] **Step 1：更新 types.ts**

在 `web/src/api/types.ts` 的 `BookDetail` interface，在 `isPhysical: boolean` 後加：

```typescript
export interface BookDetail {
  id: string
  title: string
  subtitle: string | null
  authors: string[]
  language: string | null
  publisher: string | null
  publishedDate: string | null
  description: string | null
  isbn: string | null
  coverPath: string | null
  readingStatus: ReadingStatus
  progressPercent: number | null
  currentPage: number | null
  totalPages: number | null
  hasDigital: boolean
  isPhysical: boolean
  physicalLocation: string | null   // 新增
  physicalNotes: string | null      // 新增
  tags: string[]
  copies: Copy[]
}
```

在檔案底部（`CreateTagRequest` 後）新增：

```typescript
export interface UpdatePhysicalRequest {
  isPhysical: boolean
  location?: string | null
  notes?: string | null
}
```

- [ ] **Step 2：更新 books.ts**

在 `web/src/api/books.ts`，import 加入 `UpdatePhysicalRequest`：

```typescript
import type {
  BookDetail,
  BookSummary,
  CreatePhysicalBookRequest,
  PagedResult,
  UpdateBookRequest,
  UpdatePhysicalRequest,   // 新增
  UpdateReadingRequest,
} from './types'
```

在 `booksApi` 物件裡，`updateReading` 後加：

```typescript
updatePhysical: (id: string, req: UpdatePhysicalRequest) =>
  http.patch<BookDetail>(`/books/${id}/physical`, req),
```

- [ ] **Step 3：Commit**

```
git add web/src/api/types.ts web/src/api/books.ts
git commit -m "前端 api: 補 UpdatePhysicalRequest 型別與 updatePhysical 方法"
```

---

## Task 5：前端 UI — 版本面板重設計

**Files:**
- Modify: `web/src/views/BookDetailView.vue`

### Script 異動

- [ ] **Step 1：移除 NSwitch import**

在 `<script setup>` 的 Naive UI imports 中移除 `NSwitch`。

將：
```typescript
import {
  NButton, NSpace, NTag, NSpin, NAlert, NSelect, NSlider,
  NInputNumber, NDivider, NList, NListItem, NThing, NPopconfirm,
  NInput, NSwitch, NEmpty, useMessage, useDialog,
} from 'naive-ui'
```
改為：
```typescript
import {
  NButton, NSpace, NTag, NSpin, NAlert, NSelect, NSlider,
  NInputNumber, NDivider, NList, NListItem, NThing, NPopconfirm,
  NInput, NModal, NForm, NFormItem, NEmpty, useMessage, useDialog,
} from 'naive-ui'
```

- [ ] **Step 2：補 import**

在 `import { booksApi } from '@/api/books'` 的那一行確認（無需修改，已有）。

在 `import type { BookDetail, Copy, ReadingStatus } from '@/api/types'` 改為：

```typescript
import type { BookDetail, Copy, ReadingStatus, UpdatePhysicalRequest } from '@/api/types'
```

- [ ] **Step 3：加 modal 狀態與 handler**

移除現有的 `togglePhysical` function（整個 `async function togglePhysical` 區塊）。

在其位置加入以下程式碼：

```typescript
// 版本面板 — 實體版本 modal
const showPhysicalModal = ref(false)
const physicalForm = ref({ location: '', notes: '' })
const savingPhysical = ref(false)

function openAddPhysical() {
  physicalForm.value = { location: '', notes: '' }
  showPhysicalModal.value = true
}

function openEditPhysical() {
  physicalForm.value = {
    location: book.value?.physicalLocation ?? '',
    notes: book.value?.physicalNotes ?? '',
  }
  showPhysicalModal.value = true
}

async function savePhysical() {
  if (!book.value) return
  savingPhysical.value = true
  try {
    const req: UpdatePhysicalRequest = {
      isPhysical: true,
      location: physicalForm.value.location || null,
      notes: physicalForm.value.notes || null,
    }
    book.value = await booksApi.updatePhysical(book.value.id, req)
    showPhysicalModal.value = false
    message.success('已更新實體版本')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '更新失敗')
  } finally {
    savingPhysical.value = false
  }
}
```

### Template 異動

- [ ] **Step 4：移除舊區塊**

在 `<template>` 中，移除以下三個區塊（含 `<n-divider>` 標題）：

1. 整個「形式（只是紀錄）」divider + div.form-row：
```html
<n-divider>形式（只是紀錄）</n-divider>
<div class="form-row">
  ...
</div>
```

2. 整個「數位檔」section：
```html
<template v-if="book.hasDigital">
  <n-divider>數位檔</n-divider>
  ...
</template>
```

3. 整個「目錄 TOC」section：
```html
<n-divider>目錄 TOC</n-divider>
<n-empty ... />
```

- [ ] **Step 5：加入版本面板**

在剛才移除的位置（`</template>` 結束前，`</n-spin>` 之前）插入：

```html
<!-- 版本面板 -->
<n-divider>版本</n-divider>
<div class="versions-toolbar">
  <span />
  <n-button
    v-if="!book.isPhysical"
    size="small"
    @click="openAddPhysical"
  >
    ＋ 新增實體版本
  </n-button>
</div>

<n-empty
  v-if="!book.isPhysical && book.copies.length === 0"
  size="small"
  description="尚無版本，可新增實體版本或掃描書庫資料夾"
/>

<n-list v-else bordered>
  <!-- 實體列 -->
  <n-list-item v-if="book.isPhysical">
    <n-thing>
      <template #header>🏠 實體書</template>
      <template #description>
        <div v-if="book.physicalLocation" class="copy-meta">
          📍 {{ book.physicalLocation }}
        </div>
        <div v-if="book.physicalNotes" class="copy-meta">
          📝 {{ book.physicalNotes }}
        </div>
      </template>
    </n-thing>
    <template #suffix>
      <n-button size="small" @click="openEditPhysical">📝 編輯</n-button>
    </template>
  </n-list-item>

  <!-- 數位檔列 -->
  <n-list-item v-for="c in book.copies" :key="c.id">
    <n-thing>
      <template #header>
        {{ copyFormatLabel(c) }}
        <span class="dim">{{ formatFileSize(c.fileSizeBytes) }}</span>
        <n-tag v-if="c.isMissing" type="error" size="small" :bordered="false">
          ⚠ 檔案遺失
        </n-tag>
        <n-tag v-if="c.parseFailed" type="warning" size="small" :bordered="false">
          ⚠ 解析失敗
        </n-tag>
      </template>
    </n-thing>
    <template #suffix>
      <n-space>
        <n-button v-if="!c.isMissing" size="small" @click="download(c)">
          📥 下載
        </n-button>
        <n-popconfirm @positive-click="removeCopy(c)">
          <template #trigger>
            <n-button size="small" quaternary type="error">移除</n-button>
          </template>
          確定移除此數位檔紀錄？（不刪硬碟檔）
        </n-popconfirm>
      </n-space>
    </template>
  </n-list-item>
</n-list>

<!-- 新增 / 編輯實體版本 modal -->
<n-modal
  v-model:show="showPhysicalModal"
  preset="dialog"
  title="實體版本資訊"
  positive-text="確認"
  negative-text="取消"
  :loading="savingPhysical"
  @positive-click="savePhysical"
>
  <n-form label-placement="left" label-width="80">
    <n-form-item label="📍 館藏位置">
      <n-input
        v-model:value="physicalForm.location"
        placeholder="例：書房 B 櫃-第3層（選填）"
        clearable
      />
    </n-form-item>
    <n-form-item label="📝 備註">
      <n-input
        v-model:value="physicalForm.notes"
        placeholder="例：借給小明（選填）"
        clearable
      />
    </n-form-item>
  </n-form>
</n-modal>
```

### Style 異動

- [ ] **Step 6：移除舊 style，加入新 style**

在 `<style scoped>` 中，移除 `.form-row` 與 `.form-spacer` 兩個規則（已不再使用）。

加入：

```css
.versions-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}
.copy-meta {
  font-size: 13px;
  opacity: 0.8;
  margin-top: 2px;
}
```

- [ ] **Step 7：build 確認無 TypeScript 錯誤**

```
cd web && npm run build
```

預期：`built in X.Xs`，0 errors。

- [ ] **Step 8：Commit**

```
git add web/src/views/BookDetailView.vue web/src/api/types.ts web/src/api/books.ts
git commit -m "前端：版本面板重設計 — 移除 Switch、統一實體+數位版本清單"
```

---

## Task 6：收尾驗證

- [ ] **Step 1：跑全部後端測試**

```
dotnet test -v minimal
```

預期：所有 `PASSED`，0 失敗。

- [ ] **Step 2：跑前端測試**

```
cd web && npx vitest run
```

預期：所有 `PASSED`，0 失敗。（現有測試皆不依賴 Switch，無需新增測試）

- [ ] **Step 3：push**

```
git push
```
