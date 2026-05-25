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
    label: () => h('span', { style: { color: '#e88080' } }, '刪除'),
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
  <div class="book-card" @click="open">
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

      <div class="overlay">
        <n-ellipsis class="title" :line-clamp="2">{{ book.title }}</n-ellipsis>
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

  </div>
</template>

<style scoped>
.book-card {
  cursor: pointer;
  border-radius: 8px;
  overflow: hidden;
  background: rgba(128, 128, 128, 0.12);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}
.book-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.28);
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
.overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 28px 8px 8px;
  background: linear-gradient(transparent, rgba(0, 0, 0, 0.78));
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.title {
  font-weight: 600;
  font-size: 13px;
  color: #fff;
  line-height: 1.3;
}
.author-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.author {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.75);
}
.menu-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  color: rgba(255, 255, 255, 0.8);
  cursor: pointer;
  font-size: 18px;
  padding: 0 2px;
  line-height: 1;
  border-radius: 4px;
}
.menu-btn:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.15);
}
.menu-btn:focus-visible {
  outline: 2px solid #18a058;
  outline-offset: 2px;
  color: #fff;
}
</style>
