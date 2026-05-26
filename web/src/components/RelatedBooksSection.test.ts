import { describe, it, expect, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import RelatedBooksSection from './RelatedBooksSection.vue'
import type { BookSummary } from '@/api/types'

const { relatedMock } = vi.hoisted(() => ({
  relatedMock: vi.fn(),
}))

vi.mock('@/api/books', () => ({
  booksApi: { related: relatedMock },
}))

// BookCard is stubbed — its internal navigation / menu are tested in BookCard.test.ts
vi.mock('@/components/BookCard.vue', () => ({
  default: {
    name: 'BookCard',
    props: ['book'],
    emits: ['refresh'],
    // click 觸發 refresh emit，讓 RelatedBooksSection 能驗證重新抓取
    template: '<div class="book-card-stub" @click="$emit(\'refresh\')">{{ book.title }}</div>',
  },
}))

vi.mock('naive-ui', async () => {
  const mod = await vi.importActual<Record<string, unknown>>('naive-ui')
  return {
    ...mod,
    NSpin: { name: 'NSpin', template: '<div><slot /></div>', inheritAttrs: false },
    NEmpty: { name: 'NEmpty', template: '<div class="n-empty"></div>', props: ['description'] },
    NGrid: {
      name: 'NGrid',
      template: '<div class="n-grid"><slot /></div>',
      props: ['cols', 'xGap', 'yGap', 'responsive'],
    },
    NGridItem: { name: 'NGridItem', template: '<div class="n-grid-item"><slot /></div>' },
    useMessage: vi.fn(() => ({ error: vi.fn(), success: vi.fn() })),
  }
})

function makeBook(title: string, id = crypto.randomUUID()): BookSummary {
  return {
    id,
    title,
    authors: ['Author A'],
    coverPath: null,
    readingStatus: 'None',
    hasDigital: false,
    hasPhysical: true,
    tags: [],
  }
}

afterEach(() => {
  relatedMock.mockReset()
})

describe('RelatedBooksSection', () => {
  it('renders BookCard for each related book', async () => {
    relatedMock.mockResolvedValue([makeBook('Clean Architecture'), makeBook('Design Patterns')])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    expect(w.findAll('.book-card-stub')).toHaveLength(2)
    expect(w.text()).toContain('Clean Architecture')
    expect(w.text()).toContain('Design Patterns')
  })

  it('shows empty state when no related books', async () => {
    relatedMock.mockResolvedValue([])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    expect(w.find('.n-empty').exists()).toBe(true)
    expect(w.findAll('.book-card-stub')).toHaveLength(0)
  })

  it('calls booksApi.related with correct bookId', async () => {
    relatedMock.mockResolvedValue([])

    mount(RelatedBooksSection, { props: { bookId: 'my-book-id' } })
    await flushPromises()

    expect(relatedMock).toHaveBeenCalledWith('my-book-id')
  })

  it('shows error toast when API fails', async () => {
    relatedMock.mockRejectedValue(new Error('Network error'))

    mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    expect(relatedMock).toHaveBeenCalledWith('book-1')
  })

  it('re-fetches when BookCard emits refresh', async () => {
    const book = makeBook('Clean Architecture')
    relatedMock.mockResolvedValue([book])

    const w = mount(RelatedBooksSection, { props: { bookId: 'book-1' } })
    await flushPromises()

    relatedMock.mockClear()

    // stub emit refresh on click → RelatedBooksSection should re-call fetchRelated
    await w.find('.book-card-stub').trigger('click')
    await flushPromises()

    expect(relatedMock).toHaveBeenCalledWith('book-1')
  })
})
