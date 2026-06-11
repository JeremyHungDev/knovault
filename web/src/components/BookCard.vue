<script setup lang="ts">
import { computed, h, ref } from 'vue'
import { NDropdown, NEllipsis, useDialog, useMessage } from 'naive-ui'
import { useRouter } from 'vue-router'
import type { BookSummary, ReadingStatus } from '@/api/types'
import { booksApi } from '@/api/books'
import { coverThumbUrl } from '@/api/http'
import { authorsLine, READING_STATUS_LABELS } from '@/utils/format'

const props = defineProps<{ book: BookSummary }>()
const emit = defineEmits<{ refresh: [] }>()

const router = useRouter()
const dialog = useDialog()
const message = useMessage()
const coverFailed = ref(false)

function open() {
  router.push(`/books/${props.book.id}`)
}

// 三點選單 options
const dropdownOptions = computed(() => [
  { label: '編輯書目', key: 'edit' },
  {
    label: '標記閱讀狀態',
    key: 'status',
    children: (['None', 'WantToRead'] as ReadingStatus[]).map((s) => ({
      label: props.book.readingStatus === s
        ? `✓ ${READING_STATUS_LABELS[s]}`
        : READING_STATUS_LABELS[s],
      key: `status:${s}`,
    })),
  },
  { label: '新增實體版本', key: 'add-physical' },
  { type: 'divider', key: 'div-1' },
  {
    label: () => h('span', { style: { color: 'var(--accent-red)' } }, '刪除'),
    key: 'delete',
  },
])

async function handleSelect(key: string) {
  if (key === 'edit') {
    router.push(`/books/${props.book.id}/edit`)
  } else if (key.startsWith('status:')) {
    const status = key.split(':')[1] as ReadingStatus
    try {
      await booksApi.updateReading(props.book.id, { readingStatus: status })
      emit('refresh')
    } catch (e) {
      message.error(e instanceof Error ? e.message : '更新失敗')
    }
  } else if (key === 'add-physical') {
    router.push(`/books/${props.book.id}`)
  } else if (key === 'delete') {
    dialog.warning({
      title: '刪除書籍',
      content: '將移除此目錄項與其版本紀錄，但永不刪除硬碟上的書檔。確定刪除？',
      positiveText: '刪除',
      negativeText: '取消',
      onPositiveClick: async () => {
        try {
          await booksApi.remove(props.book.id)
          emit('refresh')
          message.success('已刪除')
        } catch (e) {
          message.error(e instanceof Error ? e.message : '刪除失敗')
        }
      },
    })
  }
}
</script>

<template>
  <div class="book-card">
    <div class="cover-wrap" :data-has-digital="book.hasDigital">
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

    <div class="info-bar" @click="open">
      <div class="title">{{ book.title }}</div>
      <div class="author-row">
        <n-ellipsis class="author" style="flex: 1">
          {{ authorsLine(book.authors) }}
        </n-ellipsis>
        <n-dropdown
          :options="dropdownOptions"
          trigger="click"
          placement="bottom-end"
          @select="handleSelect"
        >
          <button class="menu-btn" aria-label="書籍選項" @click.stop>⋮</button>
        </n-dropdown>
      </div>
    </div>
  </div>
</template>

<style scoped>
.book-card {
  cursor: default;
  border-radius: 8px;
  overflow: hidden;
  background: rgba(128, 128, 128, 0.12);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
  transition: transform 0.15s ease, box-shadow 0.15s ease;
  display: flex;
  flex-direction: column;
}
.book-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.22);
}
.info-bar:hover {
  transform: translateY(-1px);
  background: var(--bg-card-hover);
}
.cover-wrap {
  position: relative;
  aspect-ratio: 3 / 4;
  overflow: hidden;
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
  width: 100%;
  height: 100%;
  font-size: 48px;
  color: rgba(128, 128, 128, 0.5);
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
.info-bar {
  cursor: pointer;
  min-height: 55px;
  padding: 8px 6px 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  box-sizing: border-box;
  background: var(--bg-card);
  border-top: 1px solid var(--border-card);
  transition: background-color 0.15s ease, transform 0.15s ease;
}
.info-bar .title {
  color: var(--text-card);
}
.info-bar .author {
  color: var(--text-card-sub);
}
.info-bar .menu-btn {
  color: var(--text-card-muted);
}
.info-bar .menu-btn:hover {
  color: var(--text-card);
  background: var(--hover-card);
}
.title {
  font-weight: 600;
  font-size: 13px;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
}
.author-row {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  min-width: 0;
}
.author {
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.menu-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 18px;
  padding: 0 2px;
  line-height: 1;
  border-radius: 4px;
}
.menu-btn:focus-visible {
  outline: 2px solid var(--accent-brand);
  outline-offset: 2px;
}
</style>
