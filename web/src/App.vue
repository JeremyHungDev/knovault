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
  type GlobalThemeOverrides,
} from 'naive-ui'
import { useThemeStore } from '@/stores/theme'
import AppShell from '@/components/AppShell.vue'

const themeStore = useThemeStore()
const theme = computed(() => (themeStore.dark ? darkTheme : null))

const themeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor:        '#268bd2',
    primaryColorHover:   '#2aa198',
    primaryColorPressed: '#268bd2',
    successColor:        '#859900',
    warningColor:        '#b58900',
    errorColor:          '#dc322f',
    infoColor:           '#2aa198',
  },
}
</script>

<template>
  <n-config-provider :theme="theme" :theme-overrides="themeOverrides" :locale="zhTW" :date-locale="dateZhTW">
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
