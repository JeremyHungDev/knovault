# BookFormView 頁面標題區重構 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將 `BookFormView` 頁面標題區從橫向 flex row 改為上下堆疊，讓「← 返回」連結視覺層級低於頁面標題，提升可讀性。

**Architecture:** 僅修改 `BookFormView.vue` 的 template 與 scoped CSS。將 `.topbar`（flex row）改為 `.page-header`（flex column），並為返回按鈕加上 `.back-btn` 樣式。新增對應的元件測試。

**Tech Stack:** Vue 3 (Composition API)、Naive UI (`NButton text`)、Vitest + Vue Test Utils

---

## 受影響檔案

| 動作 | 路徑 |
|------|------|
| 修改 | `web/src/views/BookFormView.vue` |
| 新增 | `web/src/views/BookFormView.test.ts` |

---

### Task 1：新增元件測試（先寫失敗測試）

**Files:**
- Create: `web/src/views/BookFormView.test.ts`

- [ ] **Step 1：建立測試檔，mock 必要依賴**

建立 `web/src/views/BookFormView.test.ts`，內容如下：

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { reactive } from 'vue'
import { setActivePinia, createPinia } from 'pinia'
import BookFormView from './BookFormView.vue'

// ── Hoisted mocks ──────────────────────────────────────────────────────────
const { pushMock, backMock } = vi.hoisted(() => ({
  pushMock: vi.fn(),
  backMock: vi.fn(),
}))

const mockRoute = reactive({ name: 'book-add', params: {} })

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute,
  useRouter: () => ({ push: pushMock, back: backMock }),
}))

vi.mock('@/api/books', () => ({
  booksApi: {
    get: vi.fn(),
    createPhysical: vi.fn(),
    updatePhysical: vi.fn(),
  },
}))

vi.mock('@/api/library', () => ({
  libraryApi: { isbnLookup: vi.fn() },
}))

vi.mock('naive-ui', async () => {
  const mod = await vi.importActual<Record<string, unknown>>('naive-ui')
  return {
    ...mod,
    useMessage: vi.fn(() => ({ error: vi.fn(), success: vi.fn(), warning: vi.fn() })),
    NSpin:         { name: 'NSpin',         template: '<div><slot /></div>', props: ['show'] },
    NForm:         { name: 'NForm',         template: '<form><slot /></form>', props: ['labelPlacement'] },
    NFormItem:     { name: 'NFormItem',     template: '<div><slot /></div>', props: ['label', 'required'] },
    NInput:        { name: 'NInput',        template: '<input />', props: ['modelValue', 'placeholder', 'type', 'autosize'] },
    NInputGroup:   { name: 'NInputGroup',   template: '<div><slot /></div>' },
    NButton:       { name: 'NButton',       template: '<button><slot /></button>', props: ['text', 'quaternary', 'type', 'ghost', 'loading'] },
    NSpace:        { name: 'NSpace',        template: '<div><slot /></div>', props: ['justify'] },
    NDynamicInput: { name: 'NDynamicInput', template: '<div />', props: ['modelValue', 'min', 'placeholder', 'preset'] },
    NAlert:        { name: 'NAlert',        template: '<div />', props: ['type', 'showIcon'] },
  }
})

// ── Helper ──────────────────────────────────────────────────────────────────
function mountView() {
  setActivePinia(createPinia())
  return mount(BookFormView, { global: { stubs: { teleport: true } } })
}

// ── Tests ───────────────────────────────────────────────────────────────────
describe('BookFormView — 頁面標題區', () => {
  beforeEach(() => {
    mockRoute.name = 'book-add'
    mockRoute.params = {}
  })

  it('應渲染 .page-header 而非 .topbar', () => {
    const wrapper = mountView()
    expect(wrapper.find('.page-header').exists()).toBe(true)
    expect(wrapper.find('.topbar').exists()).toBe(false)
  })

  it('返回按鈕應有 .back-btn class 且文字為「← 返回」', () => {
    const wrapper = mountView()
    const btn = wrapper.find('.back-btn')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toBe('← 返回')
  })

  it('新增模式標題應顯示「新增實體書」', () => {
    const wrapper = mountView()
    expect(wrapper.find('.page-header h2').text()).toBe('新增實體書')
  })

  it('編輯模式標題應顯示「編輯書籍」', async () => {
    mockRoute.name = 'book-edit'
    mockRoute.params = { id: 'abc' }
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.find('.page-header h2').text()).toBe('編輯書籍')
  })
})
```

- [ ] **Step 2：執行測試，確認全部失敗（預期）**

```bash
cd web && npx vitest run src/views/BookFormView.test.ts
```

預期：4 個測試全部 FAIL（`.page-header` 尚不存在）

---

### Task 2：修改 Template 與 CSS

**Files:**
- Modify: `web/src/views/BookFormView.vue:146-149`（template topbar 區塊）
- Modify: `web/src/views/BookFormView.vue:235-243`（CSS .topbar 區塊）

- [ ] **Step 3：修改 template — 將 `.topbar` 改為 `.page-header`**

找到 [BookFormView.vue:146-149](../../../web/src/views/BookFormView.vue#L146-L149)，將：

```html
<div class="topbar">
  <n-button quaternary @click="router.back()">◀ 返回</n-button>
  <h2>{{ isEdit ? "編輯書籍" : "新增實體書" }}</h2>
</div>
```

改為：

```html
<div class="page-header">
  <n-button text class="back-btn" @click="router.back()">← 返回</n-button>
  <h2>{{ isEdit ? "編輯書籍" : "新增實體書" }}</h2>
</div>
```

- [ ] **Step 4：修改 CSS — 移除 `.topbar`，新增 `.page-header` 與 `.back-btn`**

找到 [BookFormView.vue:235-243](../../../web/src/views/BookFormView.vue#L235-L243)，將：

```css
.topbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.topbar h2 {
  margin: 0;
}
```

改為：

```css
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

- [ ] **Step 5：執行測試，確認全部通過**

```bash
cd web && npx vitest run src/views/BookFormView.test.ts
```

預期輸出：

```
 ✓ src/views/BookFormView.test.ts (4)
   ✓ BookFormView — 頁面標題區 > 應渲染 .page-header 而非 .topbar
   ✓ BookFormView — 頁面標題區 > 返回按鈕應有 .back-btn class 且文字為「← 返回」
   ✓ BookFormView — 頁面標題區 > 新增模式標題應顯示「新增實體書」
   ✓ BookFormView — 頁面標題區 > 編輯模式標題應顯示「編輯書籍」
```

- [ ] **Step 6：執行全套測試，確認無迴歸**

```bash
cd web && npm test
```

預期：所有既有測試繼續通過

- [ ] **Step 7：Commit**

```bash
git add web/src/views/BookFormView.vue web/src/views/BookFormView.test.ts
git commit -m "style(web): BookFormView 頁首改為上下堆疊，返回連結縮小置頂"
```
