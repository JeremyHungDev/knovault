import { http } from './http'
import type {
  BookDetail,
  BookSummary,
  CreatePhysicalBookRequest,
  PagedResult,
  UpdateBookRequest,
  UpdateReadingRequest,
} from './types'

export interface ListBooksParams {
  search?: string
  page?: number
  pageSize?: number
}

// 後端目前僅支援 search/page/pageSize；其餘篩選/排序於前端補強。
export function buildBooksQuery(params: ListBooksParams): string {
  const q = new URLSearchParams()
  if (params.search) q.set('search', params.search)
  if (params.page) q.set('page', String(params.page))
  if (params.pageSize) q.set('pageSize', String(params.pageSize))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export const booksApi = {
  list: (params: ListBooksParams = {}) =>
    http.get<PagedResult<BookSummary>>(`/books${buildBooksQuery(params)}`),
  get: (id: string) => http.get<BookDetail>(`/books/${id}`),
  createPhysical: (req: CreatePhysicalBookRequest) => http.post<BookDetail>('/books', req),
  update: (id: string, req: UpdateBookRequest) => http.put<BookDetail>(`/books/${id}`, req),
  updateReading: (id: string, req: UpdateReadingRequest) =>
    http.patch<BookDetail>(`/books/${id}/reading`, req),
  remove: (id: string) => http.del<void>(`/books/${id}`),
  rereadMetadata: (id: string) => http.post<BookDetail>(`/books/${id}/reread-metadata`),
  uploadCover: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return http.postForm<BookDetail>(`/books/${id}/cover`, form)
  },
}
