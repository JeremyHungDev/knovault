<script setup lang="ts">
import { NLayout, NLayoutHeader, NLayoutContent, NButton, NIcon, NSpace } from 'naive-ui'
import { RouterView, useRouter } from 'vue-router'
import { useThemeStore } from '@/stores/theme'

const themeStore = useThemeStore()
const router = useRouter()
</script>

<template>
  <n-layout position="absolute">
    <n-layout-header bordered class="app-header">
      <div class="brand" @click="router.push('/')">
        <n-icon size="22">
          <svg viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg">
            <rect width="64" height="64" rx="12" fill="#18a058" />
            <g fill="#fff">
              <rect x="16" y="14" width="9" height="36" rx="1.5" />
              <rect x="27" y="14" width="9" height="36" rx="1.5" />
              <rect x="38" y="16" width="9" height="34" rx="1.5" transform="rotate(12 42.5 33)" />
            </g>
          </svg>
        </n-icon>
        <span class="brand-text">Knovault 芝士庫</span>
      </div>
      <n-space :size="8" align="center">
        <n-button quaternary @click="router.push('/')">書架</n-button>
        <n-button quaternary @click="router.push('/books/new')">新增實體書</n-button>
        <n-button quaternary @click="themeStore.toggle()">
          {{ themeStore.dark ? '☀ 亮色' : '🌙 暗色' }}
        </n-button>
        <n-button quaternary @click="router.push('/settings')">⚙ 設定</n-button>
      </n-space>
    </n-layout-header>
    <n-layout-content class="app-content" :native-scrollbar="false">
      <div class="content-inner">
        <router-view v-slot="{ Component }">
          <component :is="Component" />
        </router-view>
      </div>
    </n-layout-content>
  </n-layout>
</template>

<style scoped>
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  height: 56px;
}
.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  user-select: none;
}
.brand-text {
  font-weight: 600;
  font-size: 16px;
}
.app-content {
  top: 56px;
  bottom: 0;
}
.content-inner {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}
.brand :deep(svg rect:first-child) {
  fill: var(--accent-brand);
}
</style>
