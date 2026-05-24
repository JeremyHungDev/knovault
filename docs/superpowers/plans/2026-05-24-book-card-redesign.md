# BookCard Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將 BookCard 改為封面滿版卡片，底部漸層 overlay 顯示書名 / 作者，右下角三點選單提供快速操作（編輯 / 狀態 / 實體版本 / 刪除）。

**Architecture:** 直接改寫 `BookCard.vue`（移除 NCard，改用自訂 div），加入 `NDropdown` 三點選單，操作完成後 emit `'refresh'` 讓 `LibraryView` 重拉書單。進度條為 `cover-wrap` 下方的 2px div（`v-if`）。

**Tech Stack:** Vue 3 `<script setup>` + Naive UI（NDropdown、useDialog、useMessage）+ Vitest + @vue/test-utils。

---

## 檔案異動總覽

| 動作 | 路徑 |
|------|------|
| 修改 | `web/src/components/BookCard.vue` |
| 修改 | `web/src/components/BookCard.test.ts` |
| 修改 | `web/src/views/LibraryView.vue` |

---

## Task 1：更新測試 + 重寫 BookCard.vue

**Files:**
- Modify: `web/src/components/BookCard.test.ts`
- Modify: `web/src/components/BookCard.vue`

### 步驟

- [ ] **Step 1：更新測試檔**

完整替換 `web/src/components/BookCard.test.ts` 內容：

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import BookCard from './BookCard.vue'
import type { BookSummary } from '@/api/types'

const pushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: pushMock }),
}))

// 部分 mock naive-ui：保留其餘 export，只覆蓋 composables 與常用元件
vi.mock('naive-ui', async () => {
  const mod = await vi.importActual<Record<string, unknown>>('naive-ui')
  return {
    ...mod,
    useDialog: vi.fn(() => ({ warning: vi.fn() })),
    useMessage: vi.fn(() => ({ success: vi.fn(), error: vi.fn() })),
    NDropdown: { name: 'NDropdown', template: '<div class="dropdown"><slot /></div>' },
    NEllipsis: { name: 'NEllipsis', template: '<span><slot /></span>' },
  }
})

function makeBook(p: Partial<BookSummary> = {}): BookSummary {
  return {
    id: 'abc',
    title: '深度工作',
    authors: ['Cal Newport'],
    coverPath: null,
    readingStatus: 'Reading',
    progressPercent: 45,
    hasDigital: true,
    hasPhysical: true,
    ...p,
  }
}

describe('BookCard', () => {
  it('renders title and author in overlay', () => {
    const w = mount(BookCard, { props: { book: makeBook() } })
    expect(w.text()).toContain('深度工作')
    expect(w.text()).toContain('Cal Newport')
  })

  it('shows placeholder when no cover', () => {
    const w = mount(BookCard, { props: { book: makeBook({ coverPath: null }) } })
    expect(w.find('img').exists()).toBe(false)
    expect(w.find('.cover-placeholder').exists()).toBe(true)
  })

  it('renders cover img when coverPath present', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ coverPath: 'abc.jpg' }) },
    })
    const img = w.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toContain('/api/books/abc/cover/thumb')
  })

  it('navigates to detail on card click', async () => {
    pushMock.mockClear()
    const w = mount(BookCard, { props: { book: makeBook() } })
    await w.find('.book-card').trigger('click')
    expect(pushMock).toHaveBeenCalledWith('/books/abc')
  })

  it('menu button click does not navigate', async () => {
    pushMock.mockClear()
    const w = mount(BookCard, { props: { book: makeBook() } })
    await w.find('.menu-btn').trigger('click')
    expect(pushMock).not.toHaveBeenCalled()
  })

  it('shows both digital and physical badges', () => {
    const w = mount(BookCard, { props: { book: makeBook() } })
    const badges = w.find('.badges').text()
    expect(badges).toContain('📱')
    expect(badges).toContain('📚')
  })

  it('shows progress bar when Reading with progressPercent', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ readingStatus: 'Reading', progressPercent: 45 }) },
    })
    const bar = w.find('.progress-bar')
    expect(bar.exists()).toBe(true)
    expect(bar.attributes('style')).toContain('width: 45%')
  })

  it('shows full green progress bar when Finished', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ readingStatus: 'Finished', progressPercent: 100 }) },
    })
    const bar = w.find('.progress-bar')
    expect(bar.exists()).toBe(true)
    expect(bar.attributes('style')).toContain('width: 100%')
  })

  it('no progress bar when None', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ readingStatus: 'None', progressPercent: null }) },
    })
    expect(w.find('.progress-bar').exists()).toBe(false)
  })

  it('no progress bar when WantToRead', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ readingStatus: 'WantToRead', progressPercent: null }) },
    })
    expect(w.find('.progress-bar').exists()).toBe(false)
  })

  it('emits refresh event type correctly', () => {
    const w = mount(BookCard, { props: { book: makeBook() } })
    // emit 型別測試：確認元件有 refresh emit 定義
    expect(w.emitted()).toBeDefined()
  })
})
```

- [ ] **Step 2：跑測試確認失敗**

```
cd web && npx vitest run src/components/BookCard.test.ts
```

預期：多數 FAIL（`.book-card`、`.menu-btn`、`.progress-bar` 等 selector 不存在）

- [ ] **Step 3：重寫 BookCard.vue**

完整替換 `web/src/components/BookCard.vue`：

```vue
<script setup lang="ts">
import { computed, h, ref } from 'vue'
import { NDropdown, NEllipsis, useDialog, useMessage } from 'naive-ui'
import { useRouter } from 'vue-router'
import type { BookSummary, ReadingStatus } from '@/api/types'
import { booksApi } from '@/api/books'
import { coverThumbUrl } from '@/api/http'
import { authorsLine } from '@/utils/format'

const props = defineProps<{ book: BookSummary }>()
const emit = defineEmits<{ refresh: [] }>()

const router = useRouter()
const dialog = useDialog()
const message = useMessage()
const coverFailed = ref(false)

function open() {
  router.push(`/books/${props.book.id}`)
}

// 進度條 style（null = 不顯示）
const progressBarStyle = computed<Record<string, string> | null>(() => {
  const { readingStatus, progressPercent } = props.book
  if (readingStatus === 'Finished')
    return { width: '100%', background: '#18a058' }
  if (readingStatus === 'Reading' && progressPercent != null)
    return { width: `${progressPercent}%`, background: '#18a058' }
  return null
})

// 三點選單 options
const STATUS_LABELS: Record<ReadingStatus, string> = {
  None: '未分類',
  WantToRead: '想讀',
  Reading: '閱讀中',
  Finished: '已讀',
}

const dropdownOptions = computed(() => [
  { label: '編輯書目', key: 'edit' },
  {
    label: '標記閱讀狀態',
    key: 'status',
    children: (['None', 'WantToRead', 'Reading', 'Finished'] as ReadingStatus[]).map((s) => ({
      label: props.book.readingStatus === s
        ? `✓ ${STATUS_LABELS[s]}`
        : STATUS_LABELS[s],
      key: `status:${s}`,
    })),
  },
  { label: '新增實體版本', key: 'add-physical' },
  { type: 'divider', key: 'div-1' },
  {
    label: () => h('span', { style: { color: '#e88080' } }, '刪除'),
    key: 'delete',
  },
])

async function handleSelect(key: string) {
  if (key === 'edit') {
    router.push(`/books/${props.book.id}/edit`)
  } else if (key.startsWith('status:')) {
    const status = key.split(':')[1] as ReadingStatus
    try {
      await booksApi.updateReading(props.book.id, { readingStatus: status })
      emit('refresh')
    } catch (e) {
      message.error(e instanceof Error ? e.message : '更新失敗')
    }
  } else if (key === 'add-physical') {
    router.push(`/books/${props.book.id}`)
  } else if (key === 'delete') {
    dialog.warning({
      title: '刪除書籍',
      content: '將移除此目錄項與其版本紀錄，但永不刪除硬碟上的書檔。確定刪除？',
      positiveText: '刪除',
      negativeText: '取消',
      onPositiveClick: async () => {
        try {
          await booksApi.remove(props.book.id)
          emit('refresh')
          message.success('已刪除')
        } catch (e) {
          message.error(e instanceof Error ? e.message : '刪除失敗')
        }
      },
    })
  }
}
</script>

<template>
  <div class="book-card" @click="open">
    <div class="cover-wrap">
      <img
        v-if="book.coverPath && !coverFailed"
        :src="coverThumbUrl(book.id)"
        :alt="book.title"
        class="cover"
        loading="lazy"
        @error="coverFailed = true"
      />
      <div v-else class="cover cover-placeholder">
        <span>{{ book.title.slice(0, 1) || '書' }}</span>
      </div>

      <div class="badges">
        <span v-if="book.hasDigital" title="數位版本">📱</span>
        <span v-if="book.hasPhysical" title="實體版本">📚</span>
      </div>

      <div class="overlay">
        <n-ellipsis class="title" :line-clamp="2">{{ book.title }}</n-ellipsis>
        <div class="author-row">
          <n-ellipsis class="author" style="flex: 1">
            {{ authorsLine(book.authors) }}
          </n-ellipsis>
          <n-dropdown
            :options="dropdownOptions"
            trigger="click"
            placement="bottom-end"
            @select="handleSelect"
          >
            <button class="menu-btn" @click.stop>⋮</button>
          </n-dropdown>
        </div>
      </div>
    </div>

    <div v-if="progressBarStyle" class="progress-bar" :style="progressBarStyle" />
  </div>
</template>

<style scoped>
.book-card {
  cursor: pointer;
  border-radius: 8px;
  overflow: hidden;
  background: rgba(128, 128, 128, 0.12);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}
.book-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.28);
}
.cover-wrap {
  position: relative;
  aspect-ratio: 3 / 4;
  overflow: hidden;
}
.cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.cover-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  font-size: 48px;
  color: rgba(128, 128, 128, 0.5);
}
.badges {
  position: absolute;
  top: 6px;
  left: 6px;
  display: flex;
  gap: 4px;
  font-size: 14px;
  background: rgba(0, 0, 0, 0.45);
  padding: 1px 5px;
  border-radius: 6px;
}
.overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 28px 8px 8px;
  background: linear-gradient(transparent, rgba(0, 0, 0, 0.78));
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.title {
  font-weight: 600;
  font-size: 13px;
  color: #fff;
  line-height: 1.3;
}
.author-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.author {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.75);
}
.menu-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  color: rgba(255, 255, 255, 0.8);
  cursor: pointer;
  font-size: 18px;
  padding: 0 2px;
  line-height: 1;
  border-radius: 4px;
}
.menu-btn:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.15);
}
.progress-bar {
  height: 3px;
  transition: width 0.3s ease;
}
</style>
```

- [ ] **Step 4：跑測試確認通過**

```
cd web && npx vitest run src/components/BookCard.test.ts
```

預期：所有測試 PASSED（10 筆）

- [ ] **Step 5：Commit**

```
git add web/src/components/BookCard.vue web/src/components/BookCard.test.ts
git commit -m "前端：BookCard 重設計 — 封面滿版 + 底部 overlay + 三點選單"
```

---

## Task 2：LibraryView — 加 refresh 監聽

**Files:**
- Modify: `web/src/views/LibraryView.vue`

- [ ] **Step 1：加 @refresh 監聽**

在 `web/src/views/LibraryView.vue` 中找到：

```html
<book-card :book="b" />
```

改為：

```html
<book-card :book="b" @refresh="books.fetch()" />
```

- [ ] **Step 2：跑全部前端測試確認無迴歸**

```
cd web && npx vitest run
```

預期：所有 PASSED，0 失敗。

- [ ] **Step 3：Build**

```
cd web && npm run build
```

預期：`built in X.Xs`，0 errors。

- [ ] **Step 4：Commit**

```
git add web/src/views/LibraryView.vue
git commit -m "前端：BookCard 操作後 refresh 書單"
```

---

## Task 3：push

- [ ] **Step 1：push**

```
git push
```
