<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NForm,
  NFormItem,
  NInput,
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
  location: '',
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
    } catch (e) {
      message.error(e instanceof Error ? e.message : '載入失敗')
    } finally {
      loading.value = false
    }
  }
})

async function lookupIsbn() {
  const isbn = form.isbn.trim()
  if (!isbn) {
    message.warning('請先輸入 ISBN')
    return
  }
  isbnLooking.value = true
  lookupNote.value = null
  try {
    const meta = await libraryApi.isbnLookup(isbn)
    if (meta.title) form.title = meta.title
    if (meta.authors?.length) form.authors = [...meta.authors]
    if (meta.publisher) form.publisher = meta.publisher
    if (meta.publishedDate) form.publishedDate = meta.publishedDate
    if (meta.isbn) form.isbn = meta.isbn
    lookupNote.value = '已帶入查詢結果，可手動修改後儲存。'
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
    if (isEdit.value && editId.value) {
      const updated = await booksApi.update(editId.value, {
        title: form.title.trim(),
        subtitle: form.subtitle || null,
        authors,
        language: form.language || null,
        publisher: form.publisher || null,
        publishedDate: form.publishedDate || null,
        description: form.description || null,
        isbn: form.isbn || null,
      })
      message.success('已儲存')
      router.push(`/books/${updated.id}`)
    } else {
      const created = await booksApi.createPhysical({
        title: form.title.trim(),
        authors,
        isbn: form.isbn || null,
        publisher: form.publisher || null,
        publishedDate: form.publishedDate || null,
        language: form.language || null,
        description: form.description || null,
        location: form.location || null,
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
        <n-form-item label="ISBN（可線上查詢自動帶入）">
          <n-input-group>
            <n-input v-model:value="form.isbn" placeholder="978…" />
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
        <n-form-item v-if="!isEdit" label="實體位置">
          <n-input v-model:value="form.location" placeholder="如 書房 B 櫃-第3層" />
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
</style>
