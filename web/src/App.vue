<script setup lang="ts">
import { computed } from 'vue'
import {
  NConfigProvider,
  NDialogProvider,
  NLoadingBarProvider,
  NMessageProvider,
  NNotificationProvider,
  darkTheme,
  zhTW,
  dateZhTW,
} from 'naive-ui'
import { useThemeStore } from '@/stores/theme'
import AppShell from '@/components/AppShell.vue'

const themeStore = useThemeStore()
const theme = computed(() => (themeStore.dark ? darkTheme : null))
</script>

<template>
  <n-config-provider :theme="theme" :locale="zhTW" :date-locale="dateZhTW">
    <n-loading-bar-provider>
      <n-message-provider>
        <n-dialog-provider>
          <n-notification-provider>
            <app-shell />
          </n-notification-provider>
        </n-dialog-provider>
      </n-message-provider>
    </n-loading-bar-provider>
  </n-config-provider>
</template>
