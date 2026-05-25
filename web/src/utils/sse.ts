import type {
  ScanProgress,
  ScanProgressRaw,
  ScanReport,
  ScanReportRaw,
} from '@/api/types'

// SSE 串流的 progress / done 事件用後端原生序列化（PascalCase），
// 在此正規化成前端慣用的 camelCase。

export function normalizeProgress(raw: ScanProgressRaw): ScanProgress {
  return {
    processed: raw.Processed ?? 0,
    total: raw.Total ?? 0,
    currentFile: raw.CurrentFile ?? null,
  }
}

export function normalizeReport(raw: ScanReportRaw): ScanReport {
  return {
    added: raw.Added ?? 0,
    updated: raw.Updated ?? 0,
    skipped: raw.Skipped ?? 0,
    markedMissing: raw.MarkedMissing ?? 0,
    failures: raw.Failures ?? [],
  }
}

export function parseProgressData(data: string): ScanProgress {
  return normalizeProgress(JSON.parse(data) as ScanProgressRaw)
}

export function parseReportData(data: string): ScanReport {
  return normalizeReport(JSON.parse(data) as ScanReportRaw)
}
