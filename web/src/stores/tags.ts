import { defineStore } from 'pinia'
import { ref } from 'vue'
import { tagsApi } from '@/api/tags'
import type { Tag } from '@/api/types'

export const useTagsStore = defineStore('tags', () => {
  const tags = ref<Tag[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetch() {
    loading.value = true
    error.value = null
    try {
      tags.value = await tagsApi.list()
    } catch (e) {
      error.value = e instanceof Error ? e.message : '載入標籤失敗'
    } finally {
      loading.value = false
    }
  }

  async function create(name: string, color?: string | null) {
    const tag = await tagsApi.create({ name, color: color ?? null })
    await fetch()
    return tag
  }

  async function remove(id: string) {
    await tagsApi.remove(id)
    await fetch()
  }

  return { tags, loading, error, fetch, create, remove }
})
