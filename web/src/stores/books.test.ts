import { describe, it, expect } from 'vitest'
import { applyFilters, paginate, type BookFilters } from './books'
import type { BookSummary } from '@/api/types'

function book(p: Partial<BookSummary>): BookSummary {
  return {
    id: p.id ?? crypto.randomUUID(),
    title: p.title ?? '書',
    authors: p.authors ?? [],
    coverPath: p.coverPath ?? null,
    readingStatus: p.readingStatus ?? 'None',
    hasDigital: p.hasDigital ?? false,
    hasPhysical: p.hasPhysical ?? false,
    tags: p.tags ?? [],
  }
}

const base: BookFilters = {
  search: '',
  kind: 'all',
  status: 'all',
  tag: null,
  sort: 'title-asc',
}

describe('applyFilters', () => {
  const books = [
    book({ title: 'B', hasDigital: true, readingStatus: 'Reading' }),
    book({ title: 'A', hasPhysical: true, readingStatus: 'Finished' }),
    book({ title: 'C', hasDigital: true, hasPhysical: true, readingStatus: 'None' }),
  ]

  it('sorts by title ascending by default', () => {
    expect(applyFilters(books, base).map((b) => b.title)).toEqual(['A', 'B', 'C'])
  })

  it('sorts by title descending', () => {
    expect(applyFilters(books, { ...base, sort: 'title-desc' }).map((b) => b.title)).toEqual([
      'C',
      'B',
      'A',
    ])
  })

  it('filters by kind digital', () => {
    const r = applyFilters(books, { ...base, kind: 'digital' })
    expect(r.every((b) => b.hasDigital)).toBe(true)
    expect(r).toHaveLength(2)
  })

  it('filters by kind physical', () => {
    const r = applyFilters(books, { ...base, kind: 'physical' })
    expect(r.every((b) => b.hasPhysical)).toBe(true)
    expect(r).toHaveLength(2)
  })

  it('filters by reading status', () => {
    const r = applyFilters(books, { ...base, status: 'Reading' })
    expect(r).toHaveLength(1)
    expect(r[0].title).toBe('B')
  })

  it('does not mutate input array', () => {
    const copy = [...books]
    applyFilters(books, { ...base, sort: 'title-desc' })
    expect(books).toEqual(copy)
  })

  it('filters by tag', () => {
    const tagged = [
      book({ title: 'A', tags: ['科技'] }),
      book({ title: 'B', tags: ['設計'] }),
      book({ title: 'C', tags: ['科技', '設計'] }),
    ]
    const r = applyFilters(tagged, { ...base, tag: '科技' })
    expect(r).toHaveLength(2)
    expect(r.map((b) => b.title).sort()).toEqual(['A', 'C'])
  })

  it('shows all books when tag filter is null', () => {
    const booksWithTags = [
      book({ title: 'A', tags: ['科技'] }),
      book({ title: 'B', tags: [] }),
    ]
    expect(applyFilters(booksWithTags, { ...base, tag: null })).toHaveLength(2)
  })
})

describe('paginate', () => {
  const items = Array.from({ length: 50 }, (_, i) => i)
  it('returns the requested page slice', () => {
    expect(paginate(items, 1, 24)).toHaveLength(24)
    expect(paginate(items, 1, 24)[0]).toBe(0)
    expect(paginate(items, 2, 24)[0]).toBe(24)
  })
  it('handles last partial page', () => {
    expect(paginate(items, 3, 24)).toEqual([48, 49])
  })
})
