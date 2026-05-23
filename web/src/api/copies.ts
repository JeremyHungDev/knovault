import { http } from './http'
import type { AddPhysicalCopyRequest, BookDetail, UpdateCopyRequest } from './types'

export const copiesApi = {
  addPhysical: (bookId: string, req: AddPhysicalCopyRequest) =>
    http.post<BookDetail>(`/books/${bookId}/copies`, req),
  update: (copyId: string, req: UpdateCopyRequest) => http.put<void>(`/copies/${copyId}`, req),
  remove: (copyId: string) => http.del<void>(`/copies/${copyId}`),
}
