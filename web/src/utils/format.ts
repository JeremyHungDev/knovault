import type { ReadingStatus } from '@/api/types'

export function formatFileSize(bytes: number | null | undefined): string {
  if (bytes == null || bytes < 0) return '—'
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)))
  const value = bytes / Math.pow(1024, i)
  const fixed = i === 0 ? value.toFixed(0) : value.toFixed(value >= 100 ? 0 : 1)
  return `${fixed} ${units[i]}`
}

export const READING_STATUS_LABELS: Record<ReadingStatus, string> = {
  None: '未標記',
  WantToRead: '想讀',
}

export const READING_STATUS_OPTIONS: { label: string; value: ReadingStatus }[] = [
  { label: '未標記', value: 'None' },
  { label: '想讀', value: 'WantToRead' },
]

export function readingStatusLabel(status: ReadingStatus | string | null | undefined): string {
  if (!status) return READING_STATUS_LABELS.None
  return READING_STATUS_LABELS[status as ReadingStatus] ?? String(status)
}

export type StatusTagType = 'default' | 'warning'

export function readingStatusTagType(status: ReadingStatus | string | null | undefined): StatusTagType {
  return status === 'WantToRead' ? 'warning' : 'default'
}

export function authorsLine(authors: string[] | null | undefined): string {
  if (!authors || authors.length === 0) return '未知作者'
  return authors.join('、')
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '從未'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('zh-Hant', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}
