<script setup lang="ts">
import { ref, watch } from 'vue'
import { NEmpty, NSpin, useMessage } from 'naive-ui'
import { NGrid, NGridItem } from 'naive-ui'
import { booksApi } from '@/api/books'
import type { BookSummary } from '@/api/types'
import BookCard from '@/components/BookCard.vue'

const props = defineProps<{ bookId: string }>()

const message = useMessage()
const loading = ref(true)
const books = ref<BookSummary[]>([])

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
    <n-grid
      v-else-if="!loading"
      cols="2 s:3 m:4 l:5 xl:6"
      :x-gap="14"
      :y-gap="14"
      responsive="screen"
    >
      <n-grid-item v-for="book in books" :key="book.id">
        <book-card :book="book" @refresh="fetchRelated(props.bookId)" />
      </n-grid-item>
    </n-grid>
  </n-spin>
</template>
