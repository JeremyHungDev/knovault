<!-- web/src/components/ReviewsSection.vue -->
<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  NSegmented,
  NSpin,
  NAlert,
  NEmpty,
  NButton,
  NSpace,
} from 'naive-ui'
import { reviewsApi } from '@/api/reviews'
import type { ReviewsResult, ReviewSourceResult } from '@/api/types'

const props = defineProps<{ bookId: string; isbn: string | null }>()

const loading = ref(false)
const error = ref<string | null>(null)
const result = ref<ReviewsResult | null>(null)
const refreshing = ref(false)

const platformOptions = [
  { label: 'Goodreads', value: 'Goodreads' },
  { label: '博客來', value: 'BooksComTw' },
]
const selectedPlatform = ref<string>('Goodreads')

const currentSource = computed<ReviewSourceResult | null>(() => {
  if (!result.value) return null
  return result.value.sources.find(s => s.source === selectedPlatform.value) ?? null
})

const isBooksComTw = computed(() => selectedPlatform.value === 'BooksComTw')

async function load() {
  if (!props.isbn) return
  loading.value = true
  error.value = null
  try {
    result.value = await reviewsApi.get(props.bookId)
  } catch (e) {
    error.value = e instanceof Error ? e.message : '載入失敗'
  } finally {
    loading.value = false
  }
}

async function refresh() {
  if (!props.isbn) return
  refreshing.value = true
  error.value = null
  try {
    result.value = await reviewsApi.refresh(props.bookId)
  } catch (e) {
    error.value = e instanceof Error ? e.message : '重新整理失敗'
  } finally {
    refreshing.value = false
  }
}

function formatFetchedAt(iso: string | null): string {
  if (!iso) return '尚未抓取'
  return new Date(iso).toLocaleString('zh-TW', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

function stars(rating: number | null): string {
  if (rating == null) return ''
  const full = Math.round(rating)
  return '★'.repeat(full) + '☆'.repeat(Math.max(0, 5 - full))
}

onMounted(load)
watch(() => props.bookId, load)
</script>

<template>
  <div class="reviews-section">
    <!-- 無 ISBN -->
    <n-empty v-if="!isbn" description="此書無 ISBN，無法查詢外部評論" />

    <template v-else>
      <!-- 平台切換 -->
      <div class="reviews-toolbar">
        <n-segmented
          v-model:value="selectedPlatform"
          :options="platformOptions"
          size="small"
        />
        <n-space v-if="currentSource?.fetchedAt" align="center" class="fetch-meta">
          <span class="fetch-time">資料更新：{{ formatFetchedAt(currentSource.fetchedAt) }}</span>
          <n-button size="tiny" :loading="refreshing" @click="refresh">重新整理</n-button>
        </n-space>
      </div>

      <!-- 載入中 -->
      <n-spin v-if="loading" style="margin-top: 24px" />

      <!-- 錯誤 -->
      <n-alert v-else-if="error" type="error" :title="error" style="margin-top: 12px">
        <n-button size="small" @click="load">重試</n-button>
      </n-alert>

      <!-- 博客來佔位 -->
      <div v-else-if="isBooksComTw" class="placeholder-box">
        <p>博客來評論功能開發中</p>
        <a
          :href="`https://search.books.com.tw/search/query/key/${isbn}/cat/BKall`"
          target="_blank"
          rel="noopener"
        >
          前往博客來查詢 ↗
        </a>
      </div>

      <!-- 無評論 -->
      <n-empty
        v-else-if="!currentSource || currentSource.reviews.length === 0"
        description="尚無評論"
        style="margin-top: 24px"
      >
        <template #extra>
          <n-button size="small" :loading="refreshing" @click="refresh">從網路抓取</n-button>
        </template>
      </n-empty>

      <!-- 評論列表 -->
      <div v-else class="reviews-list">
        <div v-for="(review, i) in currentSource.reviews" :key="i" class="review-card">
          <div class="review-header">
            <span class="reviewer">{{ review.reviewerName ?? '匿名' }}</span>
            <span v-if="review.rating" class="stars">{{ stars(review.rating) }}</span>
            <span class="review-date">{{ review.reviewDate?.slice(0, 10) ?? '' }}</span>
            <span v-if="review.helpfulCount" class="helpful">👍 {{ review.helpfulCount }}</span>
          </div>
          <p v-if="review.reviewText" class="review-text">{{ review.reviewText }}</p>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reviews-section {
  padding-top: 8px;
}
.reviews-toolbar {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}
.fetch-meta {
  font-size: 12px;
  opacity: 0.65;
}
.fetch-time {
  font-size: 12px;
}
.placeholder-box {
  margin-top: 16px;
  padding: 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
  text-align: center;
}
.placeholder-box a {
  color: var(--n-color);
  text-decoration: none;
  opacity: 0.8;
}
.reviews-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.review-card {
  padding: 12px 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
}
.review-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 6px;
  font-size: 13px;
}
.reviewer {
  font-weight: 600;
}
.stars {
  color: #f0a500;
  letter-spacing: 1px;
}
.review-date {
  opacity: 0.55;
}
.helpful {
  opacity: 0.6;
}
.review-text {
  margin: 0;
  font-size: 14px;
  line-height: 1.65;
  white-space: pre-wrap;
  opacity: 0.88;
}
</style>
