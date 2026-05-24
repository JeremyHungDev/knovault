<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NButton,
  NSpace,
  NTag,
  NSpin,
  NAlert,
  NSelect,
  NSlider,
  NInputNumber,
  NDivider,
  NList,
  NListItem,
  NThing,
  NPopconfirm,
  NInput,
  NModal,
  NForm,
  NFormItem,
  NEmpty,
  useMessage,
  useDialog,
} from 'naive-ui'
import { booksApi } from '@/api/books'
import { copiesApi } from '@/api/copies'
import { tagsApi } from '@/api/tags'
import { copyFileUrl, coverUrl } from '@/api/http'
import type { BookDetail, Copy, ReadingStatus, UpdatePhysicalRequest } from '@/api/types'
import { useTagsStore } from '@/stores/tags'
import { storeToRefs } from 'pinia'
import {
  READING_STATUS_OPTIONS,
  authorsLine,
  formatFileSize,
} from '@/utils/format'

const route = useRoute()
const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const tagsStore = useTagsStore()
const { tags: allTags } = storeToRefs(tagsStore)

const id = computed(() => route.params.id as string)
const book = ref<BookDetail | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const coverFailed = ref(false)

// 閱讀狀態 / 進度本地編輯狀態
const status = ref<ReadingStatus>('None')
const percent = ref<number | null>(null)
const currentPage = ref<number | null>(null)
const totalPages = ref<number | null>(null)
const savingReading = ref(false)

// NSlider 不接受 null；用 0 代表未設定，NInputNumber 仍保留 nullable
const sliderPercent = computed<number>({
  get: () => percent.value ?? 0,
  set: (v) => { percent.value = v },
})

// 填了目前頁數 + 總頁數 → 自動算 %（使用者仍可手動覆蓋）
watch([currentPage, totalPages], ([cur, tot]) => {
  if (cur != null && tot != null && tot > 0) {
    percent.value = Math.min(100, Math.max(0, Math.round((cur / tot) * 100)))
  }
})

// 有閱讀進度時，自動把狀態切到「閱讀中」（除非已是想讀以外的狀態）
watch([percent, currentPage], ([p, cur]) => {
  const hasProgress = (p != null && p > 0) || (cur != null && cur > 0)
  if (hasProgress && (status.value === 'None' || status.value === 'WantToRead')) {
    status.value = 'Reading'
  }
})

// 封面上傳（含快取破壞，上傳後強制重載 <img>）
const coverInput = ref<HTMLInputElement | null>(null)
const uploadingCover = ref(false)
const coverVersion = ref(0)
const coverSrc = computed(() =>
  book.value ? `${coverUrl(book.value.id)}?v=${coverVersion.value}` : '',
)
function pickCover() {
  coverInput.value?.click()
}
async function onCoverPicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file && book.value) {
    uploadingCover.value = true
    try {
      book.value = await booksApi.uploadCover(book.value.id, file)
      coverFailed.value = false
      coverVersion.value++
      message.success('封面已更新')
    } catch (err) {
      message.error(err instanceof Error ? err.message : '上傳失敗')
    } finally {
      uploadingCover.value = false
    }
  }
  input.value = ''
}

async function load() {
  loading.value = true
  error.value = null
  coverFailed.value = false
  try {
    const b = await booksApi.get(id.value)
    book.value = b
    status.value = b.readingStatus
    percent.value = b.progressPercent
    currentPage.value = b.currentPage
    totalPages.value = b.totalPages
  } catch (e) {
    error.value = e instanceof Error ? e.message : '載入失敗'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  load()
  tagsStore.fetch()
})

const assignedTagSet = computed(() => new Set(book.value?.tags ?? []))
const availableTags = computed(() =>
  allTags.value.filter((t) => !assignedTagSet.value.has(t.name)),
)

function copyFormatLabel(c: Copy): string {
  return c.format.toUpperCase() === 'PDF' ? '📄 PDF' : '📱 EPUB'
}

async function saveReading() {
  if (!book.value) return
  savingReading.value = true
  try {
    const updated = await booksApi.updateReading(book.value.id, {
      readingStatus: status.value,
      percent: percent.value,
      currentPage: currentPage.value,
      totalPages: totalPages.value,
    })
    book.value = updated
    message.success('已更新閱讀狀態')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '更新失敗')
  } finally {
    savingReading.value = false
  }
}

async function assignTag(tagId: string) {
  if (!book.value) return
  try {
    await tagsApi.assign(book.value.id, tagId)
    await load()
    await tagsStore.fetch()
  } catch (e) {
    message.error(e instanceof Error ? e.message : '加標籤失敗')
  }
}

async function unassignTag(tagName: string) {
  if (!book.value) return
  const tag = allTags.value.find((t) => t.name === tagName)
  if (!tag) return
  try {
    await tagsApi.unassign(book.value.id, tag.id)
    await load()
    await tagsStore.fetch()
  } catch (e) {
    message.error(e instanceof Error ? e.message : '移除標籤失敗')
  }
}

const newTagName = ref('')
async function createAndAssign() {
  if (!book.value || !newTagName.value.trim()) return
  try {
    const tag = await tagsStore.create(newTagName.value.trim())
    await tagsApi.assign(book.value.id, tag.id)
    newTagName.value = ''
    await load()
  } catch (e) {
    message.error(e instanceof Error ? e.message : '建立標籤失敗')
  }
}

function download(copy: Copy) {
  window.open(copyFileUrl(copy.id), '_blank')
}

async function removeCopy(copy: Copy) {
  try {
    await copiesApi.remove(copy.id)
    await load()
    message.success('已移除版本')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '移除失敗')
  }
}

// 版本面板 — 實體版本 modal
const showPhysicalModal = ref(false)
const physicalForm = ref({ location: '', notes: '' })
const savingPhysical = ref(false)

function openAddPhysical() {
  physicalForm.value = { location: '', notes: '' }
  showPhysicalModal.value = true
}

function openEditPhysical() {
  if (!book.value) return
  physicalForm.value = {
    location: book.value?.physicalLocation ?? '',
    notes: book.value?.physicalNotes ?? '',
  }
  showPhysicalModal.value = true
}

async function savePhysical() {
  if (!book.value || savingPhysical.value) return
  savingPhysical.value = true
  try {
    const req: UpdatePhysicalRequest = {
      isPhysical: true,
      location: physicalForm.value.location || null,
      notes: physicalForm.value.notes || null,
    }
    book.value = await booksApi.updatePhysical(book.value.id, req)
    showPhysicalModal.value = false
    message.success('已更新實體版本')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '更新失敗')
  } finally {
    savingPhysical.value = false
  }
}

function confirmDelete() {
  if (!book.value) return
  dialog.warning({
    title: '刪除書籍',
    content: '將移除此目錄項與其版本紀錄，但永不刪除硬碟上的書檔。確定刪除？',
    positiveText: '刪除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await booksApi.remove(book.value!.id)
        message.success('已刪除')
        router.push('/')
      } catch (e) {
        message.error(e instanceof Error ? e.message : '刪除失敗')
      }
    },
  })
}
</script>

<template>
  <n-spin :show="loading">
    <n-alert v-if="error" type="error" :title="error" />
    <template v-if="book">
      <div class="topbar">
        <n-button quaternary @click="router.push('/')">◀ 返回</n-button>
        <n-space>
          <n-button @click="router.push(`/books/${book.id}/edit`)">編輯</n-button>
          <n-button type="error" ghost @click="confirmDelete">刪除</n-button>
        </n-space>
      </div>

      <div class="header">
        <div class="cover-col">
          <div
            class="cover-clickable"
            :class="{ 'is-empty': !book.coverPath || coverFailed }"
            role="button"
            tabindex="0"
            title="點擊上傳 / 更換封面"
            @click="pickCover"
            @keydown.enter="pickCover"
          >
            <img
              v-if="book.coverPath && !coverFailed"
              :src="coverSrc"
              :alt="book.title"
              class="cover"
              @error="coverFailed = true"
            />
            <div v-else class="cover cover-placeholder">{{ book.title.slice(0, 1) }}</div>
            <div class="cover-overlay">
              <span v-if="!book.coverPath || coverFailed" class="cover-plus">＋</span>
              <span class="cover-overlay-text">
                {{ book.coverPath && !coverFailed ? '更換封面' : '上傳封面' }}
              </span>
            </div>
            <div v-if="uploadingCover" class="cover-loading"><n-spin size="small" /></div>
          </div>
          <input
            ref="coverInput"
            type="file"
            accept="image/*"
            style="display: none"
            @change="onCoverPicked"
          />
        </div>
        <div class="info-col">
          <h1 class="title">
            {{ book.title }}
            <small v-if="book.subtitle">{{ book.subtitle }}</small>
          </h1>
          <p class="authors">{{ authorsLine(book.authors) }}</p>
          <p class="meta-line">
            <span v-if="book.publisher">{{ book.publisher }}</span>
            <span v-if="book.publishedDate">· {{ book.publishedDate }}</span>
            <span v-if="book.language">· {{ book.language }}</span>
            <span v-if="book.isbn">· ISBN {{ book.isbn }}</span>
          </p>

          <div class="tags-row">
            <span class="label">標籤：</span>
            <n-tag
              v-for="t in book.tags"
              :key="t"
              closable
              size="small"
              @close="unassignTag(t)"
            >
              {{ t }}
            </n-tag>
            <n-select
              v-if="availableTags.length"
              size="small"
              class="tag-add"
              placeholder="+ 加標籤"
              :options="availableTags.map((t) => ({ label: t.name, value: t.id }))"
              @update:value="assignTag"
            />
            <n-input
              v-model:value="newTagName"
              size="small"
              class="tag-new"
              placeholder="新標籤"
              @keyup.enter="createAndAssign"
            />
          </div>

          <div class="reading-box">
            <div class="reading-row">
              <span class="label">狀態：</span>
              <n-select
                v-model:value="status"
                size="small"
                class="status-select"
                :options="READING_STATUS_OPTIONS"
              />
            </div>
            <div class="reading-row">
              <span class="label">進度：</span>
              <n-slider
                v-model:value="sliderPercent"
                :min="0"
                :max="100"
                class="slider"
                :tooltip="true"
              />
              <n-input-number
                v-model:value="percent"
                size="small"
                :min="0"
                :max="100"
                class="pct-input"
              >
                <template #suffix>%</template>
              </n-input-number>
            </div>
            <div class="reading-row pages">
              <span class="label">頁數：</span>
              <n-input-number
                v-model:value="currentPage"
                size="small"
                :min="0"
                placeholder="現在頁"
                class="page-input"
              />
              <span>/</span>
              <n-input-number
                v-model:value="totalPages"
                size="small"
                :min="0"
                placeholder="總頁"
                class="page-input"
              />
              <n-button
                size="small"
                type="primary"
                :loading="savingReading"
                @click="saveReading"
              >
                儲存
              </n-button>
            </div>
          </div>
        </div>
      </div>

      <template v-if="book.description">
        <n-divider>簡介</n-divider>
        <p class="description">{{ book.description }}</p>
      </template>

      <!-- 版本面板 -->
      <n-divider>版本</n-divider>
      <div class="versions-toolbar">
        <n-button
          v-if="!book.isPhysical"
          size="small"
          @click="openAddPhysical"
        >
          ＋ 新增實體版本
        </n-button>
      </div>

      <n-empty
        v-if="!book.isPhysical && book.copies.length === 0"
        size="small"
        description="尚無版本，可新增實體版本或掃描書庫資料夾"
      />

      <n-list v-else bordered>
        <!-- 實體列 -->
        <n-list-item v-if="book.isPhysical">
          <n-thing>
            <template #header>🏠 實體書</template>
            <template #description>
              <div v-if="book.physicalLocation" class="copy-meta">
                📍 {{ book.physicalLocation }}
              </div>
              <div v-if="book.physicalNotes" class="copy-meta">
                📝 {{ book.physicalNotes }}
              </div>
            </template>
          </n-thing>
          <template #suffix>
            <n-button size="small" @click="openEditPhysical">📝 編輯</n-button>
          </template>
        </n-list-item>

        <!-- 數位檔列 -->
        <n-list-item v-for="c in book.copies" :key="c.id">
          <n-thing>
            <template #header>
              {{ copyFormatLabel(c) }}
              <span class="dim">{{ formatFileSize(c.fileSizeBytes) }}</span>
              <n-tag v-if="c.isMissing" type="error" size="small" :bordered="false">
                ⚠ 檔案遺失
              </n-tag>
              <n-tag v-if="c.parseFailed" type="warning" size="small" :bordered="false">
                ⚠ 解析失敗
              </n-tag>
            </template>
          </n-thing>
          <template #suffix>
            <n-space>
              <n-button v-if="!c.isMissing" size="small" @click="download(c)">
                📥 下載
              </n-button>
              <n-popconfirm @positive-click="removeCopy(c)">
                <template #trigger>
                  <n-button size="small" quaternary type="error">移除</n-button>
                </template>
                確定移除此數位檔紀錄？（不刪硬碟檔）
              </n-popconfirm>
            </n-space>
          </template>
        </n-list-item>
      </n-list>

      <!-- 新增 / 編輯實體版本 modal -->
      <n-modal
        v-model:show="showPhysicalModal"
        preset="dialog"
        title="實體版本資訊"
        positive-text="確認"
        negative-text="取消"
        :loading="savingPhysical"
        @positive-click="savePhysical"
      >
        <n-form label-placement="left" label-width="80">
          <n-form-item label="📍 館藏位置">
            <n-input
              v-model:value="physicalForm.location"
              placeholder="例：書房 B 櫃-第3層（選填）"
              clearable
            />
          </n-form-item>
          <n-form-item label="📝 備註">
            <n-input
              v-model:value="physicalForm.notes"
              placeholder="例：借給小明（選填）"
              clearable
            />
          </n-form-item>
        </n-form>
      </n-modal>
    </template>
  </n-spin>
</template>

<style scoped>
.topbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16px;
}
.versions-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}
.copy-meta {
  font-size: 13px;
  opacity: 0.8;
  margin-top: 2px;
}
.header {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
}
.cover-col {
  flex: 0 0 200px;
}
.cover-clickable {
  position: relative;
  width: 200px;
  border-radius: 8px;
  cursor: pointer;
  outline: none;
}
.cover {
  display: block;
  width: 200px;
  aspect-ratio: 3 / 4;
  object-fit: cover;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.12);
}
.cover-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 60px;
  color: rgba(128, 128, 128, 0.5);
}
.cover-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.45);
  color: #fff;
  opacity: 0;
  transition: opacity 0.15s ease;
}
.cover-clickable:hover .cover-overlay,
.cover-clickable:focus-visible .cover-overlay {
  opacity: 1;
}
/* 無封面時讓 ＋ 一直顯示，提示可點擊上傳 */
.cover-clickable.is-empty .cover-overlay {
  opacity: 1;
  background: rgba(0, 0, 0, 0.25);
}
.cover-plus {
  font-size: 40px;
  line-height: 1;
  font-weight: 300;
}
.cover-overlay-text {
  font-size: 13px;
}
.cover-loading {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.4);
  border-radius: 8px;
}
.info-col {
  flex: 1 1 360px;
  min-width: 280px;
}
.title {
  margin: 0 0 6px;
  font-size: 24px;
}
.title small {
  font-size: 15px;
  opacity: 0.6;
  font-weight: 400;
  margin-left: 6px;
}
.authors {
  margin: 0 0 4px;
  font-size: 15px;
}
.meta-line {
  margin: 0 0 12px;
  font-size: 13px;
  opacity: 0.7;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.tags-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 14px;
}
.label {
  font-size: 13px;
  opacity: 0.7;
}
.tag-add {
  width: 120px;
}
.tag-new {
  width: 110px;
}
.reading-box {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.08);
}
.reading-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.status-select {
  width: 140px;
}
.slider {
  flex: 1;
  max-width: 260px;
}
.pct-input {
  width: 110px;
}
.page-input {
  width: 120px;
}
.description {
  white-space: pre-wrap;
  line-height: 1.7;
  opacity: 0.9;
}
.dim {
  font-size: 12px;
  opacity: 0.6;
  margin-left: 6px;
}
.add-copy {
  margin-top: 12px;
}
</style>
