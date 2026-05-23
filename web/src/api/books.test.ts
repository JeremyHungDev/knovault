import { describe, it, expect } from 'vitest'
import { buildBooksQuery } from './books'

describe('buildBooksQuery', () => {
  it('returns empty string for no params', () => {
    expect(buildBooksQuery({})).toBe('')
  })
  it('encodes search', () => {
    expect(buildBooksQuery({ search: '哲學' })).toBe(`?search=${encodeURIComponent('哲學')}`)
  })
  it('includes page and pageSize', () => {
    expect(buildBooksQuery({ page: 2, pageSize: 24 })).toBe('?page=2&pageSize=24')
  })
  it('omits falsy page/pageSize', () => {
    expect(buildBooksQuery({ page: 0, pageSize: 0 })).toBe('')
  })
})
