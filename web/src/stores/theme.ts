import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const STORAGE_KEY = 'knovault.theme.dark'

export const useThemeStore = defineStore('theme', () => {
  const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
  // 預設跟隨系統，但 spec 偏好暗色：無紀錄時若系統為暗色則暗，否則亮。
  const prefersDark =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  const dark = ref(stored != null ? stored === '1' : prefersDark)

  function toggle() {
    dark.value = !dark.value
  }

  watch(dark, (v) => {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, v ? '1' : '0')
  })

  return { dark, toggle }
})
