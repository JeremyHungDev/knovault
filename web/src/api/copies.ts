import { http } from './http'

// 形式重構後 copy 僅代表數位檔：只保留刪除（下載走 copyFileUrl 直連）。
export const copiesApi = {
  remove: (copyId: string) => http.del<void>(`/copies/${copyId}`),
}
