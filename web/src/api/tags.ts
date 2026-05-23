import { http } from './http'
import type { CreateTagRequest, Tag } from './types'

export const tagsApi = {
  list: () => http.get<Tag[]>('/tags'),
  create: (req: CreateTagRequest) => http.post<Tag>('/tags', req),
  remove: (id: string) => http.del<void>(`/tags/${id}`),
  assign: (bookId: string, tagId: string) =>
    http.post<void>(`/books/${bookId}/tags/${tagId}`),
  unassign: (bookId: string, tagId: string) =>
    http.del<void>(`/books/${bookId}/tags/${tagId}`),
}
