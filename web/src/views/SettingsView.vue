<script setup lang="ts">
import { onMounted, ref } from 'vue'
import {
  NCard,
  NList,
  NListItem,
  NThing,
  NButton,
  NSpace,
  NInput,
  NInputGroup,
  NEmpty,
  NPopconfirm,
  NTag,
  NText,
  NDivider,
  useMessage,
} from 'naive-ui'
import { storeToRefs } from 'pinia'
import { useLibraryStore } from '@/stores/library'
import { useTagsStore } from '@/stores/tags'
import { formatDate } from '@/utils/format'
import ScanProgressBar from '@/components/ScanProgressBar.vue'

const lib = useLibraryStore()
const tagsStore = useTagsStore()
const message = useMessage()

const { folders, loadingFolders, scanning, lastReport } = storeToRefs(lib)
const { tags } = storeToRefs(tagsStore)

const newPath = ref('')
const newDisplayName = ref('')
const newTagName = ref('')

onMounted(() => {
  lib.fetchFolders()
  tagsStore.fetch()
})

async function addFolder() {
  if (!newPath.value.trim()) {
    message.warning('請輸入資料夾路徑')
    return
  }
  try {
    await lib.addFolder(newPath.value.trim(), newDisplayName.value.trim() || null)
    newPath.value = ''
    newDisplayName.value = ''
    message.success('已新增書庫資料夾')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '新增失敗')
  }
}

async function removeFolder(id: string) {
  try {
    await lib.removeFolder(id)
    message.success('已移除（該資料夾的數位版本將標記為遺失）')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '移除失敗')
  }
}

async function runScan() {
  try {
    const report = await lib.startScan()
    message.success(
      `掃描完成：新增 ${report.added}、更新 ${report.updated}、跳過 ${report.skipped}`,
    )
    await lib.fetchFolders()
  } catch (e) {
    message.error(e instanceof Error ? e.message : '掃描失敗')
  }
}

async function addTag() {
  if (!newTagName.value.trim()) return
  try {
    await tagsStore.create(newTagName.value.trim())
    newTagName.value = ''
  } catch (e) {
    message.error(e instanceof Error ? e.message : '新增標籤失敗')
  }
}

async function removeTag(id: string) {
  try {
    await tagsStore.remove(id)
    message.success('已刪除標籤')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '刪除失敗')
  }
}
</script>

<template>
  <div class="settings">
    <h2>設定</h2>

    <n-card title="書庫資料夾" class="card">
      <n-input-group class="add-row">
        <n-input v-model:value="newPath" placeholder="資料夾完整路徑（如 D:\\Books）" />
        <n-input v-model:value="newDisplayName" placeholder="顯示名稱（選填）" style="max-width: 180px" />
        <n-button type="primary" @click="addFolder">新增</n-button>
      </n-input-group>

      <n-list v-if="folders.length" bordered>
        <n-list-item v-for="f in folders" :key="f.id">
          <n-thing :title="f.displayName || f.path">
            <template #description>
              <n-space size="small" align="center">
                <n-text depth="3">{{ f.path }}</n-text>
                <n-tag v-if="!f.enabled" size="small" type="warning" :bordered="false">已停用</n-tag>
                <n-text depth="3">上次掃描：{{ formatDate(f.lastScannedAt) }}</n-text>
              </n-space>
            </template>
          </n-thing>
          <template #suffix>
            <n-popconfirm @positive-click="removeFolder(f.id)">
              <template #trigger>
                <n-button size="small" quaternary type="error">移除</n-button>
              </template>
              移除後保留書籍，但該資料夾的數位版本會標記為遺失。確定？
            </n-popconfirm>
          </template>
        </n-list-item>
      </n-list>
      <n-empty v-else-if="!loadingFolders" description="尚未加入任何書庫資料夾" />

      <n-divider />
      <n-space align="center">
        <n-button type="primary" :loading="scanning" :disabled="scanning" @click="runScan">
          立即掃描
        </n-button>
        <n-text v-if="lastReport" depth="3">
          上次：新增 {{ lastReport.added }}、更新 {{ lastReport.updated }}、跳過
          {{ lastReport.skipped }}、遺失 {{ lastReport.markedMissing }}、失敗
          {{ lastReport.failures.length }}
        </n-text>
      </n-space>
      <scan-progress-bar />
      <div v-if="lastReport && lastReport.failures.length" class="failures">
        <n-text depth="2">掃描失敗清單：</n-text>
        <ul>
          <li v-for="(fail, i) in lastReport.failures" :key="i">{{ fail }}</li>
        </ul>
      </div>
    </n-card>

    <n-card title="標籤管理" class="card">
      <n-input-group class="add-row">
        <n-input v-model:value="newTagName" placeholder="新標籤名稱" @keyup.enter="addTag" />
        <n-button type="primary" @click="addTag">新增</n-button>
      </n-input-group>
      <n-space>
        <n-tag
          v-for="t in tags"
          :key="t.id"
          closable
          :type="'default'"
          @close="removeTag(t.id)"
        >
          {{ t.name }} ({{ t.bookCount }})
        </n-tag>
      </n-space>
      <n-empty v-if="!tags.length" size="small" description="尚無標籤" />
    </n-card>

    <n-card title="關於" class="card">
      <n-space vertical size="small">
        <n-text>Knovault 芝士庫 — 自託管個人書庫（書庫核心子專案）。</n-text>
        <n-text depth="3">
          一本「書」可同時擁有數位（EPUB/PDF）與實體版本；資料夾掃描匯入、ISBN 查詢、標籤、閱讀進度。
        </n-text>
        <n-text depth="3">前端：Vue 3 + Vite + Naive UI + Pinia。後端：.NET 8 + SQLite。</n-text>
      </n-space>
    </n-card>
  </div>
</template>

<style scoped>
.settings {
  max-width: 800px;
  margin: 0 auto;
}
.card {
  margin-bottom: 20px;
}
.add-row {
  margin-bottom: 14px;
}
.failures {
  margin-top: 12px;
  font-size: 13px;
}
.failures ul {
  margin: 6px 0 0;
  padding-left: 20px;
  opacity: 0.8;
}
</style>
