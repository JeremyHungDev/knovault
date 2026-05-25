import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { libraryApi } from '@/api/library'
import type { Folder, ScanProgress, ScanReport } from '@/api/types'
import { parseProgressData, parseReportData } from '@/utils/sse'

const SCAN_STREAM_URL = '/api/library/scan/stream'

export const useLibraryStore = defineStore('library', () => {
  const folders = ref<Folder[]>([])
  const loadingFolders = ref(false)

  const scanning = ref(false)
  const progress = ref<ScanProgress | null>(null)
  const lastReport = ref<ScanReport | null>(null)
  const scanError = ref<string | null>(null)

  let source: EventSource | null = null

  const progressPercent = computed(() => {
    const p = progress.value
    if (!p || p.total <= 0) return 0
    return Math.min(100, Math.round((p.processed / p.total) * 100))
  })

  async function fetchFolders() {
    loadingFolders.value = true
    try {
      folders.value = await libraryApi.folders()
    } finally {
      loadingFolders.value = false
    }
  }

  async function addFolder(path: string, displayName?: string | null) {
    const folder = await libraryApi.addFolder({ path, displayName: displayName ?? null })
    await fetchFolders()
    return folder
  }

  async function removeFolder(id: string) {
    await libraryApi.removeFolder(id)
    await fetchFolders()
  }

  // 用 SSE 跑掃描以取得即時進度。回傳 Promise 在掃描結束（done）或出錯時 resolve/reject。
  function startScan(): Promise<ScanReport> {
    if (scanning.value) return Promise.reject(new Error('掃描進行中'))
    scanning.value = true
    progress.value = { processed: 0, total: 0, currentFile: null }
    lastReport.value = null
    scanError.value = null

    return new Promise<ScanReport>((resolve, reject) => {
      source = new EventSource(SCAN_STREAM_URL)

      source.addEventListener('progress', (ev) => {
        try {
          progress.value = parseProgressData((ev as MessageEvent).data)
        } catch {
          // 單筆解析失敗不中斷整體掃描
        }
      })

      source.addEventListener('done', (ev) => {
        try {
          lastReport.value = parseReportData((ev as MessageEvent).data)
        } catch {
          lastReport.value = null
        }
        finishScan()
        resolve(lastReport.value ?? { added: 0, updated: 0, skipped: 0, markedMissing: 0, failures: [] })
      })

      source.onerror = () => {
        // SSE 在 done 後 server 會關連線，瀏覽器觸發 error；若已有報告視為正常結束。
        if (lastReport.value) return
        scanError.value = '掃描連線中斷'
        finishScan()
        reject(new Error(scanError.value))
      }
    })
  }

  function finishScan() {
    scanning.value = false
    if (source) {
      source.close()
      source = null
    }
  }

  return {
    folders,
    loadingFolders,
    scanning,
    progress,
    progressPercent,
    lastReport,
    scanError,
    fetchFolders,
    addFolder,
    removeFolder,
    startScan,
  }
})
