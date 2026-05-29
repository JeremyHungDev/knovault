import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useThemeStore } from './theme'

beforeEach(() => {
  localStorage.clear()
  document.documentElement.classList.remove('dark')
  setActivePinia(createPinia())
})

describe('useThemeStore html class sync', () => {
  it('html.dark class is set correctly after toggle', () => {
    const store = useThemeStore()
    store.toggle()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })

  it('html.dark class is removed after toggling back', () => {
    const store = useThemeStore()
    store.toggle()
    store.toggle()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })

  it('html.dark class matches initial store.dark on creation', () => {
    const store = useThemeStore()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })
})
