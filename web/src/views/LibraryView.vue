<script setup lang="ts">
import { computed, onMounted } from 'vue'
import {
  NInput,
  NSelect,
  NButton,
  NSpace,
  NPagination,
  NEmpty,
  NSpin,
  NGrid,
  NGridItem,
  NAlert,
  useMessage,
} from 'naive-ui'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'
import { useBooksStore } from '@/stores/books'
import { useTagsStore } from '@/stores/tags'
import { useLibraryStore } from '@/stores/library'
import { READING_STATUS_OPTIONS } from '@/utils/format'
import BookCard from '@/components/BookCard.vue'
import ScanProgressBar from '@/components/ScanProgressBar.vue'

const books = useBooksStore()
const tagsStore = useTagsStore()
const lib = useLibraryStore()
const message = useMessage()
const router = useRouter()

const { pageItems, total, loading, error, filters, page, pageCount } = storeToRefs(books)
const { tags } = storeToRefs(tagsStore)
const { scanning } = storeToRefs(lib)

const kindOptions = [
  { label: '全部類型', value: 'all' },
  { label: '數位', value: 'digital' },
  { label: '實體', value: 'physical' },
]
const statusOptions = [{ label: '全部狀態', value: 'all' }, ...READING_STATUS_OPTIONS]
const sortOptions = [
  { label: '書名 A→Z', value: 'title-asc' },
  { label: '書名 Z→A', value: 'title-desc' },
  { label: '依狀態', value: 'status' },
]
const tagOptions = computed(() => [
  { label: '全部標籤', value: '' },
  ...tags.value.map((t) => ({ label: `${t.name} (${t.bookCount})`, value: t.name })),
])

onMounted(() => {
  books.fetch()
  tagsStore.fetch()
})

let searchTimer: ReturnType<typeof setTimeout> | undefined
function onSearch(v: string) {
  filters.value.search = v
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => books.setSearch(v), 300)
}

async function runScan() {
  try {
    const report = await lib.startScan()
    message.success(
      `掃描完成：新增 ${report.added}、更新 ${report.updated}、跳過 ${report.skipped}` +
        (report.markedMissing ? `、標記遺失 ${report.markedMissing}` : '') +
        (report.failures.length ? `、失敗 ${report.failures.length}` : ''),
    )
    await books.fetch()
    await tagsStore.fetch()
  } catch (e) {
    message.error(e instanceof Error ? e.message : '掃描失敗')
  }
}
</script>

<template>
  <div>
    <div class="toolbar">
      <n-input
        :value="filters.search"
        placeholder="🔍 搜尋書名"
        clearable
        class="search"
        @update:value="onSearch"
      />
      <n-select
        :value="filters.kind"
        :options="kindOptions"
        class="filter"
        @update:value="(v) => books.setFilter('kind', v)"
      />
      <n-select
        :value="filters.status"
        :options="statusOptions"
        class="filter"
        @update:value="(v) => books.setFilter('status', v)"
      />
      <n-select
        :value="filters.tag ?? ''"
        :options="tagOptions"
        class="filter"
        @update:value="(v) => books.setFilter('tag', v || null)"
      />
      <n-select
        :value="filters.sort"
        :options="sortOptions"
        class="filter"
        @update:value="(v) => books.setFilter('sort', v)"
      />
      <n-button type="primary" :loading="scanning" :disabled="scanning" @click="runScan">
        掃描
      </n-button>
    </div>

    <scan-progress-bar />

    <n-alert v-if="error" type="error" :title="error" class="mb" />

    <n-spin :show="loading">
      <template v-if="pageItems.length">
        <n-grid cols="2 s:3 m:4 l:5 xl:6" :x-gap="14" :y-gap="14" responsive="screen">
          <n-grid-item v-for="b in pageItems" :key="b.id">
            <book-card :book="b" />
          </n-grid-item>
        </n-grid>

        <div class="pager">
          <n-pagination
            :page="page"
            :page-count="pageCount"
            @update:page="(p) => books.setPage(p)"
          />
          <span class="count">共 {{ total }} 本</span>
        </div>
      </template>

      <n-empty v-else-if="!loading" description="書架空空如也" class="empty">
        <template #extra>
          <n-space>
            <n-button @click="router.push('/settings')">設定書庫資料夾並掃描</n-button>
            <n-button type="primary" @click="router.push('/books/new')">新增實體書</n-button>
          </n-space>
        </template>
      </n-empty>
    </n-spin>
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  margin-bottom: 6px;
}
.search {
  flex: 1 1 220px;
  min-width: 180px;
}
.filter {
  width: 130px;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  margin-top: 24px;
}
.count {
  font-size: 13px;
  opacity: 0.65;
}
.empty {
  margin-top: 64px;
}
.mb {
  margin-bottom: 16px;
}
</style>
