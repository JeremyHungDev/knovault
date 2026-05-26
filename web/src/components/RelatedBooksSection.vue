<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { NEllipsis, NEmpty, NSpin, useMessage } from 'naive-ui'
import { booksApi } from '@/api/books'
import { coverThumbUrl } from '@/api/http'
import type { BookSummary } from '@/api/types'

const props = defineProps<{ bookId: string }>()

const router = useRouter()
const message = useMessage()
const loading = ref(true)
const books = ref<BookSummary[]>([])
const coverFailed = ref<Record<string, boolean>>({})

async function fetchRelated(bookId: string) {
  loading.value = true
  books.value = []
  try {
    books.value = await booksApi.related(bookId)
  } catch (e) {
    message.error(e instanceof Error ? e.message : '載入相關書籍失敗')
  } finally {
    loading.value = false
  }
}

watch(() => props.bookId, fetchRelated, { immediate: true })
</script>

<template>
  <n-spin :show="loading">
    <n-empty v-if="!loading && books.length === 0" description="暫無相關書籍" />
    <div v-else-if="!loading" class="related-row">
      <div
        v-for="book in books"
        :key="book.id"
        class="related-card"
        role="button"
        tabindex="0"
        @click="router.push(`/books/${book.id}`)"
        @keydown.enter="router.push(`/books/${book.id}`)"
      >
        <img
          v-if="book.coverPath && !coverFailed[book.id]"
          :src="coverThumbUrl(book.id)"
          :alt="book.title"
          class="related-cover"
          loading="lazy"
          @error="coverFailed[book.id] = true"
        />
        <div v-else class="related-cover related-placeholder">
          {{ book.title.slice(0, 1) || '書' }}
        </div>
        <n-ellipsis class="related-title">{{ book.title }}</n-ellipsis>
      </div>
    </div>
  </n-spin>
</template>

<style scoped>
.related-row {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
}
.related-card {
  flex: 0 0 120px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 6px;
  outline: none;
}
.related-card:focus-visible {
  outline: 2px solid var(--n-color-target, #18a058);
  border-radius: 8px;
}
.related-cover {
  width: 120px;
  aspect-ratio: 3/4;
  object-fit: cover;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.12);
}
.related-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 36px;
  color: rgba(128, 128, 128, 0.5);
}
.related-title {
  font-size: 12px;
  width: 120px;
}
</style>
