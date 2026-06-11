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

// Solarized Light
const lightOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor:        '#268bd2',
    primaryColorHover:   '#3a9fdc',
    primaryColorPressed: '#1e6fa8',
    successColor:        '#859900',
    warningColor:        '#b58900',
    errorColor:          '#dc322f',
    infoColor:           '#2aa198',
    bodyColor:           '#fdf6e3',
  },
}

// Kavita-style dark
const darkOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor:        '#4ac694',
    primaryColorHover:   '#66d4a8',
    primaryColorPressed: '#3aa97c',
    successColor:        '#4ac694',
    warningColor:        '#d0a52b',
    errorColor:          '#e25d5a',
    infoColor:           '#58a6da',
    bodyColor:           '#1f2020',
    cardColor:           '#202122',
    popoverColor:        '#2a2b2c',
    modalColor:          '#2a2b2c',
  },
  Layout: {
    headerColor: '#2a2b2c',
  },
}

const themeOverrides = computed(() =>
  themeStore.dark ? darkOverrides : lightOverrides,
)
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
