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
