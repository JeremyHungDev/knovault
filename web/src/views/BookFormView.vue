<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NForm,
  NFormItem,
  NInput,
  NInputNumber,
  NButton,
  NSpace,
  NInputGroup,
  NDynamicInput,
  NAlert,
  NSpin,
  useMessage,
} from 'naive-ui'
import { booksApi } from '@/api/books'
import { libraryApi } from '@/api/library'

const route = useRoute()
const router = useRouter()
const message = useMessage()

const editId = computed(() => (route.name === 'book-edit' ? (route.params.id as string) : null))
const isEdit = computed(() => editId.value != null)

const form = reactive({
  title: '',
  subtitle: '' as string,
  authors: [] as string[],
  isbn: '',
  publisher: '',
  publishedDate: '',
  language: '',
  description: '',
  totalPages: null as number | null,
  coverUrl: null as string | null,
  isPhysical: false,
})

const loading = ref(false)
const saving = ref(false)
const isbnLooking = ref(false)
const lookupNote = ref<string | null>(null)

onMounted(async () => {
  if (isEdit.value && editId.value) {
    loading.value = true
    try {
      const b = await booksApi.get(editId.value)
      form.title = b.title
      form.subtitle = b.subtitle ?? ''
      form.authors = [...b.authors]
      form.isbn = b.isbn ?? ''
      form.publisher = b.publisher ?? ''
      form.publishedDate = b.publishedDate ?? ''
      form.language = b.language ?? ''
      form.description = b.description ?? ''
      form.isPhysical = b.isPhysical
    } catch (e) {
      message.error(e instanceof Error ? e.message : '載入失敗')
    } finally {
      loading.value = false
    }
  }
})

async function lookupIsbn() {
  const isbn = form.isbn.replace(/[-\s]/g, '').trim()
  if (!isbn) {
    message.warning('請先輸入 ISBN')
    return
  }
  form.isbn = isbn // 正規化欄位（去掉連字號/空白）
  isbnLooking.value = true
  lookupNote.value = null
  try {
    const meta = await libraryApi.isbnLookup(isbn)
    if (meta.title) form.title = meta.title
    if (meta.authors?.length) form.authors = [...meta.authors]
    if (meta.publisher) form.publisher = meta.publisher
    if (meta.publishedDate) form.publishedDate = meta.publishedDate
    if (meta.isbn) form.isbn = meta.isbn
    if (meta.pageCount) form.totalPages = meta.pageCount
    if (meta.coverUrl) form.coverUrl = meta.coverUrl
    lookupNote.value = '已帶入查詢結果（含封面/總頁數），可手動修改後儲存。'
  } catch (e) {
    lookupNote.value = null
    message.error(
      e instanceof Error
        ? `ISBN 查詢失敗：${e.message}，請改為手動填寫`
        : 'ISBN 查詢失敗，請改為手動填寫',
    )
  } finally {
    isbnLooking.value = false
  }
}

async function submit() {
  if (!form.title.trim()) {
    message.warning('書名為必填')
    return
  }
  saving.value = true
  try {
    const authors = form.authors.map((a) => a.trim()).filter(Boolean)
    const isbn = form.isbn.replace(/[-\s]/g, '').trim() || null
    if (isEdit.value && editId.value) {
      const updated = await booksApi.update(editId.value, {
        title: form.title.trim(),
        subtitle: form.subtitle || null,
        authors,
        language: form.language || null,
        publisher: form.publisher || null,
        publishedDate: form.publishedDate || null,
        description: form.description || null,
        isbn,
        isPhysical: form.isPhysical,
      })
      message.success('已儲存')
      router.push(`/books/${updated.id}`)
    } else {
      const created = await booksApi.createPhysical({
        title: form.title.trim(),
        authors,
        isbn,
        publisher: form.publisher || null,
        publishedDate: form.publishedDate || null,
        language: form.language || null,
        description: form.description || null,
        totalPages: form.totalPages,
        coverUrl: form.coverUrl,
      })
      message.success('已新增實體書')
      router.push(`/books/${created.id}`)
    }
  } catch (e) {
    message.error(e instanceof Error ? e.message : '儲存失敗')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <n-spin :show="loading">
    <div class="page">
      <div class="topbar">
        <n-button quaternary @click="router.back()">◀ 返回</n-button>
        <h2>{{ isEdit ? '編輯書籍' : '新增實體書' }}</h2>
      </div>

      <n-form label-placement="top" class="form">
        <n-form-item label="ISBN（可線上查詢自動帶入；可不加連字號 -）">
          <n-input-group>
            <n-input v-model:value="form.isbn" placeholder="例如 9780321125217（連字號 - 可省略）" />
            <n-button
              v-if="!isEdit"
              :loading="isbnLooking"
              type="primary"
              ghost
              @click="lookupIsbn"
            >
              查詢
            </n-button>
          </n-input-group>
        </n-form-item>
        <n-alert v-if="lookupNote" type="success" :show-icon="false" class="note">
          {{ lookupNote }}
        </n-alert>

        <div v-if="form.coverUrl" class="cover-preview">
          <img :src="form.coverUrl" alt="封面預覽" />
          <n-button text type="error" @click="form.coverUrl = null">移除封面</n-button>
        </div>

        <n-form-item label="書名 *" required>
          <n-input v-model:value="form.title" placeholder="書名" />
        </n-form-item>
        <n-form-item label="副標題">
          <n-input v-model:value="form.subtitle" placeholder="副標題（選填）" />
        </n-form-item>
        <n-form-item label="作者">
          <n-dynamic-input
            v-model:value="form.authors"
            :min="0"
            placeholder="作者姓名"
            preset="input"
          />
        </n-form-item>
        <n-form-item label="出版社">
          <n-input v-model:value="form.publisher" />
        </n-form-item>
        <n-form-item label="出版日期">
          <n-input v-model:value="form.publishedDate" placeholder="如 2019 或 2019-03" />
        </n-form-item>
        <n-form-item label="語言">
          <n-input v-model:value="form.language" placeholder="如 zh-Hant" />
        </n-form-item>
        <n-form-item label="總頁數">
          <n-input-number
            v-model:value="form.totalPages"
            :min="1"
            placeholder="ISBN 查詢可自動帶入"
            style="width: 100%"
          />
        </n-form-item>
        <n-form-item label="簡介">
          <n-input
            v-model:value="form.description"
            type="textarea"
            :autosize="{ minRows: 3, maxRows: 8 }"
          />
        </n-form-item>

        <n-space justify="end">
          <n-button @click="router.back()">取消</n-button>
          <n-button type="primary" :loading="saving" @click="submit">
            {{ isEdit ? '儲存' : '新增' }}
          </n-button>
        </n-space>
      </n-form>
    </div>
  </n-spin>
</template>

<style scoped>
.page {
  max-width: 640px;
  margin: 0 auto;
}
.topbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.topbar h2 {
  margin: 0;
}
.note {
  margin-bottom: 12px;
}
.cover-preview {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  margin-bottom: 12px;
}
.cover-preview img {
  width: 120px;
  height: auto;
  border-radius: 6px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}
</style>
