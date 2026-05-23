import { http } from './http'
import type {
  AuthorFacet,
  CreateFolderRequest,
  Folder,
  IsbnMetadata,
  ScanReport,
} from './types'

export const libraryApi = {
  authors: () => http.get<AuthorFacet[]>('/authors'),
  folders: () => http.get<Folder[]>('/library/folders'),
  addFolder: (req: CreateFolderRequest) => http.post<Folder>('/library/folders', req),
  removeFolder: (id: string) => http.del<void>(`/library/folders/${id}`),
  // 同步掃描（無進度回報）；要進度用 SSE store。
  scan: () => http.post<ScanReport>('/library/scan'),
  isbnLookup: (isbn: string) => http.get<IsbnMetadata>(`/metadata/isbn/${encodeURIComponent(isbn)}`),
}
