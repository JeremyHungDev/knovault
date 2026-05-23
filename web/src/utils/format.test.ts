import { describe, it, expect } from 'vitest'
import {
  authorsLine,
  formatFileSize,
  readingStatusLabel,
  readingStatusTagType,
} from './format'

describe('formatFileSize', () => {
  it('handles null / negative', () => {
    expect(formatFileSize(null)).toBe('—')
    expect(formatFileSize(undefined)).toBe('—')
    expect(formatFileSize(-5)).toBe('—')
  })

  it('formats bytes', () => {
    expect(formatFileSize(0)).toBe('0 B')
    expect(formatFileSize(512)).toBe('512 B')
  })

  it('formats KB / MB / GB', () => {
    expect(formatFileSize(1024)).toBe('1.0 KB')
    expect(formatFileSize(3_355_443)).toBe('3.2 MB')
    expect(formatFileSize(12 * 1024 * 1024)).toBe('12.0 MB')
    expect(formatFileSize(1024 * 1024 * 1024)).toBe('1.0 GB')
  })
})

describe('authorsLine', () => {
  it('falls back to 未知作者', () => {
    expect(authorsLine(null)).toBe('未知作者')
    expect(authorsLine([])).toBe('未知作者')
  })
  it('joins with separator', () => {
    expect(authorsLine(['甲', '乙'])).toBe('甲、乙')
  })
})

describe('reading status helpers', () => {
  it('labels statuses', () => {
    expect(readingStatusLabel('Reading')).toBe('在讀')
    expect(readingStatusLabel('Finished')).toBe('讀完')
    expect(readingStatusLabel('None')).toBe('未標記')
    expect(readingStatusLabel(null)).toBe('未標記')
  })
  it('maps tag types', () => {
    expect(readingStatusTagType('Reading')).toBe('info')
    expect(readingStatusTagType('Finished')).toBe('success')
    expect(readingStatusTagType('WantToRead')).toBe('warning')
    expect(readingStatusTagType('None')).toBe('default')
  })
})
