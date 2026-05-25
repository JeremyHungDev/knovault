import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { booksApi } from '@/api/books'
import type { BookSummary, ReadingStatus } from '@/api/types'

export type KindFilter = 'all' | 'digital' | 'physical'
export type StatusFilter = 'all' | ReadingStatus
export type SortKey = 'title-asc' | 'title-desc' | 'status'

// 後端 GET /api/books 目前只支援 search/page/pageSize。
// 為了讓類型/狀態/標籤/排序立即可用，前端策略：
//   - 用 search 命中後端，抓回較大頁，再於前端做篩選/排序/分頁。
// 個人規模可接受；待後端補齊 query 後可改回伺服器端。
const FETCH_PAGE_SIZE = 200

export interface BookFilters {
  search: string
  kind: KindFilter
  status: StatusFilter
  tag: string | null
  sort: SortKey
}

export function applyFilters(books: BookSummary[], f: BookFilters): BookSummary[] {
  let result = books.slice()

  if (f.kind === 'digital') result = result.filter((b) => b.hasDigital)
  else if (f.kind === 'physical') result = result.filter((b) => b.hasPhysical)

  if (f.status !== 'all') result = result.filter((b) => b.readingStatus === f.status)

  if (f.tag) result = result.filter((b) => b.tags.includes(f.tag!))

  switch (f.sort) {
    case 'title-desc':
      result.sort((a, b) => b.title.localeCompare(a.title, 'zh-Hant'))
      break
    case 'status':
      result.sort((a, b) => a.readingStatus.localeCompare(b.readingStatus))
      break
    case 'title-asc':
    default:
      result.sort((a, b) => a.title.localeCompare(b.title, 'zh-Hant'))
      break
  }
  return result
}

export function paginate<T>(items: T[], page: number, pageSize: number): T[] {
  const start = (page - 1) * pageSize
  return items.slice(start, start + pageSize)
}

export const useBooksStore = defineStore('books', () => {
  const all = ref<BookSummary[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const filters = ref<BookFilters>({
    search: '',
    kind: 'all',
    status: 'all',
    tag: null,
    sort: 'title-asc',
  })

  const page = ref(1)
  const pageSize = ref(24)

  const filtered = computed(() => applyFilters(all.value, filters.value))
  const total = computed(() => filtered.value.length)
  const pageCount = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))
  const pageItems = computed(() => paginate(filtered.value, page.value, pageSize.value))

  async function fetch() {
    loading.value = true
    error.value = null
    try {
      const res = await booksApi.list({
        search: filters.value.search || undefined,
        page: 1,
        pageSize: FETCH_PAGE_SIZE,
      })
      all.value = res.items
      if (page.value > pageCount.value) page.value = 1
    } catch (e) {
      error.value = e instanceof Error ? e.message : '載入失敗'
      all.value = []
    } finally {
      loading.value = false
    }
  }

  function setSearch(v: string) {
    filters.value.search = v
    page.value = 1
    return fetch()
  }

  function setFilter<K extends keyof BookFilters>(key: K, value: BookFilters[K]) {
    filters.value[key] = value
    page.value = 1
  }

  function setPage(p: number) {
    page.value = p
  }

  return {
    all,
    loading,
    error,
    filters,
    page,
    pageSize,
    filtered,
    total,
    pageCount,
    pageItems,
    fetch,
    setSearch,
    setFilter,
    setPage,
  }
})
