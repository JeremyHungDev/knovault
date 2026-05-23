<script setup lang="ts">
import { ref } from 'vue'
import { NCard, NTag, NEllipsis } from 'naive-ui'
import { useRouter } from 'vue-router'
import type { BookSummary } from '@/api/types'
import { coverThumbUrl } from '@/api/http'
import { authorsLine, readingStatusLabel, readingStatusTagType } from '@/utils/format'

const props = defineProps<{ book: BookSummary }>()
const router = useRouter()
const coverFailed = ref(false)

function open() {
  router.push(`/books/${props.book.id}`)
}
</script>

<template>
  <n-card class="book-card" hoverable :bordered="true" @click="open">
    <div class="cover-wrap">
      <img
        v-if="book.coverPath && !coverFailed"
        :src="coverThumbUrl(book.id)"
        :alt="book.title"
        class="cover"
        loading="lazy"
        @error="coverFailed = true"
      />
      <div v-else class="cover cover-placeholder">
        <span>{{ book.title.slice(0, 1) || '書' }}</span>
      </div>
      <div class="badges">
        <span v-if="book.hasDigital" title="數位版本">📱</span>
        <span v-if="book.hasPhysical" title="實體版本">📚</span>
      </div>
    </div>
    <div class="meta">
      <n-ellipsis class="title" :line-clamp="2">{{ book.title }}</n-ellipsis>
      <n-ellipsis class="author" :line-clamp="1">{{ authorsLine(book.authors) }}</n-ellipsis>
      <div class="status-row">
        <n-tag size="small" :type="readingStatusTagType(book.readingStatus)" :bordered="false">
          {{ readingStatusLabel(book.readingStatus) }}
        </n-tag>
        <span v-if="book.progressPercent != null" class="pct">{{ book.progressPercent }}%</span>
      </div>
    </div>
  </n-card>
</template>

<style scoped>
.book-card {
  cursor: pointer;
}
.book-card :deep(.n-card__content) {
  padding: 10px;
}
.cover-wrap {
  position: relative;
  aspect-ratio: 3 / 4;
  border-radius: 6px;
  overflow: hidden;
  background: var(--card-bg, rgba(128, 128, 128, 0.12));
}
.cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.cover-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 40px;
  color: rgba(128, 128, 128, 0.6);
}
.badges {
  position: absolute;
  top: 6px;
  left: 6px;
  display: flex;
  gap: 4px;
  font-size: 14px;
  background: rgba(0, 0, 0, 0.45);
  padding: 1px 5px;
  border-radius: 6px;
}
.meta {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.title {
  font-weight: 600;
  font-size: 14px;
}
.author {
  font-size: 12px;
  opacity: 0.7;
}
.status-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 2px;
}
.pct {
  font-size: 12px;
  opacity: 0.8;
}
</style>
