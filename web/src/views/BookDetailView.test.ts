import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { reactive, nextTick } from 'vue'
import { setActivePinia, createPinia } from 'pinia'
import BookDetailView from './BookDetailView.vue'
import type { BookDetail } from '@/api/types'

// ── Hoisted mocks ─────────────────────────────────────────────────────────────
const { getMock, pushMock } = vi.hoisted(() => ({
  getMock: vi.fn(),
  pushMock: vi.fn(),
}))

// reactive route — 測試中直接改 .params.id 即可觸發 computed 重算
const mockRoute = reactive({ params: { id: 'book-a' } })

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute,
  useRouter: () => ({ push: pushMock }),
}))

vi.mock('@/api/books', () => ({
  booksApi: {
    get: getMock,
    uploadCover: vi.fn(),
    updateReading: vi.fn(),
    updatePhysical: vi.fn(),
    remove: vi.fn(),
  },
}))

vi.mock('@/api/copies', () => ({ copiesApi: { remove: vi.fn() } }))

vi.mock('@/api/tags', () => ({
  tagsApi: { list: vi.fn().mockResolvedValue([]), assign: vi.fn(), unassign: vi.fn() },
}))

vi.mock('@/api/http', () => ({
  coverUrl: () => 'http://cover',
  copyFileUrl: () => 'http://file',
  coverThumbUrl: () => 'http://thumb',
}))

vi.mock('@/components/RelatedBooksSection.vue', () => ({
  default: { name: 'RelatedBooksSection', template: '<div />', props: ['bookId'] },
}))

vi.mock('@/utils/format', () => ({
  READING_STATUS_OPTIONS: [],
  authorsLine: (a: string[]) => a.join(', '),
  formatFileSize: () => '1 MB',
}))

vi.mock('naive-ui', async () => {
  const mod = await vi.importActual<Record<string, unknown>>('naive-ui')
  return {
    ...mod,
    useMessage: vi.fn(() => ({ error: vi.fn(), success: vi.fn() })),
    useDialog: vi.fn(() => ({ warning: vi.fn() })),
    NSpin:       { name: 'NSpin',       template: '<div><slot /></div>', inheritAttrs: false },
    NAlert:      { name: 'NAlert',      template: '<div />', props: ['type', 'title'] },
    NButton:     { name: 'NButton',     template: '<button><slot /></button>', props: ['quaternary', 'size'] },
    NTabs:       { name: 'NTabs',       template: '<div><slot /></div>', props: ['type', 'animated'] },
    NTabPane:    { name: 'NTabPane',    template: '<div><slot /></div>', props: ['name', 'tab'] },
    NTag:        { name: 'NTag',        template: '<span><slot /></span>', props: ['closable', 'size', 'type', 'bordered'] },
    NSelect:     { name: 'NSelect',     template: '<div />', props: ['modelValue', 'options', 'size', 'placeholder', 'loading', 'disabled'] },
    NInput:      { name: 'NInput',      template: '<input />', props: ['modelValue', 'placeholder', 'clearable', 'size'] },
    NList:       { name: 'NList',       template: '<div><slot /></div>', props: ['bordered'] },
    NListItem:   { name: 'NListItem',   template: '<div><slot /></div>' },
    NThing:      { name: 'NThing',      template: '<div />' },
    NPopconfirm: { name: 'NPopconfirm', template: '<div><slot name="trigger" /></div>' },
    NSpace:      { name: 'NSpace',      template: '<div><slot /></div>' },
    NEmpty:      { name: 'NEmpty',      template: '<div />', props: ['description', 'size'] },
    NModal:      { name: 'NModal',      template: '<div />', props: ['show', 'preset', 'title', 'positiveText', 'negativeText', 'loading'] },
    NForm:       { name: 'NForm',       template: '<form><slot /></form>', props: ['labelPlacement', 'labelWidth'] },
    NFormItem:   { name: 'NFormItem',   template: '<div><slot /></div>', props: ['label'] },
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────
function makeBookDetail(id = 'book-a'): BookDetail {
  return {
    id,
    title: 'Test Book',
    subtitle: null,
    authors: ['Author A'],
    publisher: null,
    publishedDate: null,
    language: null,
    isbn: null,
    description: null,
    coverPath: null,
    readingStatus: 'None',
    tags: [],
    copies: [],
    isPhysical: false,
    physicalLocation: null,
    physicalNotes: null,
    hasDigital: false,
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────
describe('BookDetailView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockRoute.params.id = 'book-a'
  })

  afterEach(() => {
    getMock.mockReset()
    pushMock.mockClear()
  })

  it('loads book on mount', async () => {
    getMock.mockResolvedValue(makeBookDetail('book-a'))

    mount(BookDetailView)
    await flushPromises()

    expect(getMock).toHaveBeenCalledWith('book-a')
  })

  it('re-fetches book when route id changes', async () => {
    getMock.mockResolvedValue(makeBookDetail('book-a'))

    mount(BookDetailView)
    await flushPromises()

    expect(getMock).toHaveBeenCalledWith('book-a')
    getMock.mockClear()
    getMock.mockResolvedValue(makeBookDetail('book-b'))

    // 模擬 Vue Router 切換到另一本書（同元件複用）
    mockRoute.params.id = 'book-b'
    await nextTick()
    await flushPromises()

    // 這個測試目前會失敗，因為 BookDetailView 沒有 watch(id, load)
    expect(getMock).toHaveBeenCalledWith('book-b')
  })
})
