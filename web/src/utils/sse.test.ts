import { describe, it, expect } from 'vitest'
import { normalizeProgress, normalizeReport, parseProgressData, parseReportData } from './sse'

// 後端 SSE 用原生 JsonSerializer → PascalCase。確認正規化成 camelCase。
describe('normalizeProgress', () => {
  it('maps PascalCase to camelCase', () => {
    expect(normalizeProgress({ Processed: 42, Total: 120, CurrentFile: 'a.epub' })).toEqual({
      processed: 42,
      total: 120,
      currentFile: 'a.epub',
    })
  })
  it('defaults missing fields', () => {
    expect(normalizeProgress({} as never)).toEqual({
      processed: 0,
      total: 0,
      currentFile: null,
    })
  })
})

describe('normalizeReport', () => {
  it('maps report PascalCase', () => {
    expect(
      normalizeReport({
        Added: 3,
        Updated: 1,
        Skipped: 2,
        MarkedMissing: 0,
        Failures: ['x.pdf: 壞檔'],
      }),
    ).toEqual({
      added: 3,
      updated: 1,
      skipped: 2,
      markedMissing: 0,
      failures: ['x.pdf: 壞檔'],
    })
  })
})

describe('parse from SSE data string', () => {
  it('parses progress JSON', () => {
    const data = JSON.stringify({ Processed: 5, Total: 10, CurrentFile: null })
    expect(parseProgressData(data)).toEqual({ processed: 5, total: 10, currentFile: null })
  })
  it('parses report JSON', () => {
    const data = JSON.stringify({
      Added: 1,
      Updated: 0,
      Skipped: 0,
      MarkedMissing: 0,
      Failures: [],
    })
    expect(parseReportData(data)).toEqual({
      added: 1,
      updated: 0,
      skipped: 0,
      markedMissing: 0,
      failures: [],
    })
  })
})
