import { http } from './http'
import type { ReviewsResult } from './types'

export const reviewsApi = {
  get: (bookId: string): Promise<ReviewsResult> =>
    http.get<ReviewsResult>(`/books/${bookId}/reviews`),

  refresh: (bookId: string): Promise<ReviewsResult> =>
    http.post<ReviewsResult>(`/books/${bookId}/reviews/refresh`),
}
