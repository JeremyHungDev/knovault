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
    progressPercent: p.progressPercent ?? null,
    hasDigital: p.hasDigital ?? false,
    hasPhysical: p.hasPhysical ?? false,
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
