// 輕量 fetch 封裝：統一前綴、JSON、ProblemDetails 錯誤解析。

const BASE = '/api'

export class ApiError extends Error {
  status: number
  detail?: string
  constructor(message: string, status: number, detail?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  message?: string
}

async function parseError(res: Response): Promise<ApiError> {
  let title = `${res.status} ${res.statusText}`
  let detail: string | undefined
  try {
    const body = (await res.json()) as ProblemDetails
    title = body.title || body.message || title
    detail = body.detail
  } catch {
    // 非 JSON 錯誤，沿用狀態文字
  }
  return new ApiError(title, res.status, detail)
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const init: RequestInit = { method, signal }
  if (body !== undefined) {
    init.headers = { 'Content-Type': 'application/json' }
    init.body = JSON.stringify(body)
  }
  const res = await fetch(`${BASE}${path}`, init)
  if (!res.ok) throw await parseError(res)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function requestForm<T>(method: string, path: string, form: FormData): Promise<T> {
  // 不設 Content-Type，讓瀏覽器自動帶 multipart boundary
  const res = await fetch(`${BASE}${path}`, { method, body: form })
  if (!res.ok) throw await parseError(res)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const http = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>('GET', path, undefined, signal),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  del: <T>(path: string) => request<T>('DELETE', path),
  postForm: <T>(path: string, form: FormData) => requestForm<T>('POST', path, form),
}

// 封面 / 檔案的直連 URL（瀏覽器直接走 <img>/下載）。
export const apiUrl = (path: string) => `${BASE}${path}`

export function coverThumbUrl(bookId: string): string {
  return apiUrl(`/books/${bookId}/cover/thumb`)
}

export function coverUrl(bookId: string): string {
  return apiUrl(`/books/${bookId}/cover`)
}

export function copyFileUrl(copyId: string): string {
  return apiUrl(`/copies/${copyId}/file`)
}
