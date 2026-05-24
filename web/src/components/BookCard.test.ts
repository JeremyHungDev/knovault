import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import BookCard from './BookCard.vue'
import type { BookSummary } from '@/api/types'

const pushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: pushMock }),
}))

// 用淺層 stub 取代 Naive UI 元件，專注測自家邏輯。
const stubs = {
  NCard: { template: '<div @click="$emit(\'click\')"><slot /></div>' },
  NTag: { template: '<span class="tag"><slot /></span>' },
  NEllipsis: { template: '<span><slot /></span>' },
}

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
  it('renders title, author and progress', () => {
    const w = mount(BookCard, { props: { book: makeBook() }, global: { stubs } })
    expect(w.text()).toContain('深度工作')
    expect(w.text()).toContain('Cal Newport')
    expect(w.text()).toContain('45%')
    expect(w.text()).toContain('閱讀中')
  })

  it('shows placeholder when no cover', () => {
    const w = mount(BookCard, { props: { book: makeBook({ coverPath: null }) }, global: { stubs } })
    expect(w.find('img').exists()).toBe(false)
    expect(w.find('.cover-placeholder').exists()).toBe(true)
  })

  it('renders cover img when coverPath present', () => {
    const w = mount(BookCard, {
      props: { book: makeBook({ coverPath: 'abc.jpg' }) },
      global: { stubs },
    })
    const img = w.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toContain('/api/books/abc/cover/thumb')
  })

  it('navigates to detail on click', async () => {
    pushMock.mockClear()
    const w = mount(BookCard, { props: { book: makeBook() }, global: { stubs } })
    await w.find('div').trigger('click')
    expect(pushMock).toHaveBeenCalledWith('/books/abc')
  })

  it('shows both digital and physical badges', () => {
    const w = mount(BookCard, { props: { book: makeBook() }, global: { stubs } })
    const badges = w.find('.badges').text()
    expect(badges).toContain('📱')
    expect(badges).toContain('📚')
  })
})
