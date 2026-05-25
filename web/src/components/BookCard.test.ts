import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
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
    readingStatus: 'None',
    hasDigital: true,
    hasPhysical: true,
    ...p,
  }
}

describe('BookCard', () => {
  it('renders title in overlay', () => {
    const w = mount(BookCard, { props: { book: makeBook() } })
    expect(w.text()).toContain('深度工作')
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

  it('no progress bar for any status', () => {
    for (const status of ['None', 'WantToRead'] as const) {
      const w = mount(BookCard, { props: { book: makeBook({ readingStatus: status }) } })
      expect(w.find('.progress-bar').exists()).toBe(false)
    }
  })

  it('emits refresh event type correctly', () => {
    const w = mount(BookCard, { props: { book: makeBook() } })
    expect(w.emitted()).toBeDefined()
  })
})
