import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const STORAGE_KEY = 'knovault.theme.dark'

export const useThemeStore = defineStore('theme', () => {
  const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
  const prefersDark =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  const dark = ref(stored != null ? stored === '1' : prefersDark)

  function syncClass(v: boolean) {
    if (typeof document !== 'undefined') {
      document.documentElement.classList.toggle('dark', v)
    }
  }

  syncClass(dark.value)

  function toggle() {
    dark.value = !dark.value
  }

  watch(dark, (v) => {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, v ? '1' : '0')
    syncClass(v)
  }, { flush: 'sync' })

  return { dark, toggle }
})
