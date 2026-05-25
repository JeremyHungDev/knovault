// 與後端 DTO 對齊（一般 JSON 回應為 camelCase）。

export type ReadingStatus = 'None' | 'WantToRead'

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface BookSummary {
  id: string
  title: string
  authors: string[]
  coverPath: string | null
  readingStatus: ReadingStatus
  hasDigital: boolean
  hasPhysical: boolean
  tags: string[]
}

// 形式重構後 copy 僅代表數位檔（實體已改為 Book.isPhysical 旗標）。
export interface Copy {
  id: string
  format: string // "Epub" | "Pdf"
  fileSizeBytes: number
  isMissing: boolean
  parseFailed: boolean
}

export interface BookDetail {
  id: string
  title: string
  subtitle: string | null
  authors: string[]
  language: string | null
  publisher: string | null
  publishedDate: string | null
  description: string | null
  isbn: string | null
  coverPath: string | null
  readingStatus: ReadingStatus
  hasDigital: boolean
  isPhysical: boolean
  physicalLocation: string | null
  physicalNotes: string | null
  tags: string[]
  copies: Copy[]
}

export interface Tag {
  id: string
  name: string
  color: string | null
  bookCount: number
}

export interface AuthorFacet {
  name: string
  bookCount: number
}

export interface Folder {
  id: string
  path: string
  displayName: string | null
  enabled: boolean
  lastScannedAt: string | null
}

export interface IsbnMetadata {
  title: string | null
  authors: string[]
  publisher: string | null
  publishedDate: string | null
  isbn: string | null
  pageCount: number | null
  coverUrl: string | null
}

export interface ScanReport {
  added: number
  updated: number
  skipped: number
  markedMissing: number
  failures: string[]
}

// SSE 串流用後端原生序列化（PascalCase）。
export interface ScanProgressRaw {
  Processed: number
  Total: number
  CurrentFile: string | null
}

export interface ScanReportRaw {
  Added: number
  Updated: number
  Skipped: number
  MarkedMissing: number
  Failures: string[]
}

export interface ScanProgress {
  processed: number
  total: number
  currentFile: string | null
}

// 請求體型別
export interface CreatePhysicalBookRequest {
  title: string
  authors: string[]
  isbn?: string | null
  publisher?: string | null
  publishedDate?: string | null
  language?: string | null
  description?: string | null
  coverUrl?: string | null
}

export interface UpdateBookRequest {
  title: string
  subtitle?: string | null
  authors: string[]
  language?: string | null
  publisher?: string | null
  publishedDate?: string | null
  description?: string | null
  isbn?: string | null
  isPhysical: boolean
}

export interface UpdateReadingRequest {
  readingStatus: ReadingStatus
}

export interface UpdatePhysicalRequest {
  isPhysical: boolean
  location?: string | null
  notes?: string | null
}

export interface CreateFolderRequest {
  path: string
  displayName?: string | null
}

export interface CreateTagRequest {
  name: string
  color?: string | null
}
