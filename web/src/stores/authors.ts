import { defineStore } from 'pinia'
import { ref } from 'vue'
import { libraryApi } from '@/api/library'
import type { AuthorFacet } from '@/api/types'

export const useAuthorsStore = defineStore('authors', () => {
  const authors = ref<AuthorFacet[]>([])
  const loading = ref(false)

  async function fetch() {
    loading.value = true
    try {
      authors.value = await libraryApi.authors()
    } catch {
      authors.value = []
    } finally {
      loading.value = false
    }
  }

  return { authors, loading, fetch }
})
