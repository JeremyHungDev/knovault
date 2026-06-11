# Kavita 風深色模式 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 深色模式從 Solarized Dark 換成 Kavita 風（近黑灰 + 亮綠 `#4ac694`），淺色維持 Solarized Light 並定案卡片暖灰 `#dcd5c6`，同時修正既有小型配色問題。

**Architecture:** 純 token 值替換——`theme.css` 的 `:root.dark` 整組換成 Kavita 色系並新增 2 個 token；`App.vue` 的 Naive UI `themeOverrides` 改為隨 `themeStore.dark` 切換的 `computed`；`BookCard.vue` 三處小修。元件層其餘零修改。

**Tech Stack:** Vue 3, Pinia, Naive UI 2.x, Vitest

**Spec:** `docs/superpowers/specs/2026-06-12-kavita-dark-theme-design.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `web/src/styles/theme.css` | 全部 CSS token（淺色微調 + 深色全換 + 2 個新 token） |
| Modify | `web/src/App.vue` | Naive UI 雙套 themeOverrides（computed） |
| Modify | `web/src/components/BookCard.vue` | 刪除紅 token 化、hover 底色、上緣分隔線 |
| Delete | `web/verify-solarized.mjs` | 過時的臨時驗證腳本 |

注意：開始前工作區有 `theme.css` 未 commit 的試驗值（`--bg-card: #c8c8c8`），Task 1 直接以新內容覆蓋即可，不需先 revert。`web/tsconfig.app.tsbuildinfo` 是 build 產物，**不要** commit 它。

---

## Task 1: 重寫 `theme.css` token

**Files:**
- Modify: `web/src/styles/theme.css`

- [ ] **Step 1: 以下列完整內容取代整個檔案**

```css
/* Solarized Light (default) */
:root {
  color-scheme: light;

  --bg-base:      #fdf6e3;
  --bg-elevated:  #eee8d5;
  --bg-surface:   rgba(88, 110, 117, 0.06);

  --text-primary:   #586e75;
  --text-secondary: #657b83;
  --text-muted:     #839496;

  --hover-bg: rgba(88, 110, 117, 0.10);

  /* Card info-bar: warm grays matched to the cream base */
  --bg-card:         #dcd5c6;
  --bg-card-hover:   #d2cab9;
  --border-card:     rgba(0, 0, 0, 0.12);
  --text-card:       #1a1a1a;
  --text-card-sub:   rgba(0, 0, 0, 0.50);
  --text-card-muted: rgba(0, 0, 0, 0.55);
  --hover-card:      rgba(0, 0, 0, 0.08);

  --accent-brand:  #859900;
  --accent-blue:   #268bd2;
  --accent-yellow: #b58900;
  --accent-red:    #dc322f;
}

/* Kavita-style dark */
:root.dark {
  color-scheme: dark;

  --bg-base:      #1f2020;
  --bg-elevated:  #2a2b2c;
  --bg-surface:   rgba(255, 255, 255, 0.06);

  --text-primary:   #efefef;
  --text-secondary: rgba(255, 255, 255, 0.60);
  --text-muted:     rgba(255, 255, 255, 0.45);

  --hover-bg: rgba(255, 255, 255, 0.08);

  --bg-card:         #202122;
  --bg-card-hover:   #2c2d2e;
  --border-card:     rgba(255, 255, 255, 0.08);
  --text-card:       #efefef;
  --text-card-sub:   rgba(255, 255, 255, 0.55);
  --text-card-muted: rgba(255, 255, 255, 0.70);
  --hover-card:      rgba(255, 255, 255, 0.10);

  --accent-brand:  #4ac694;
  --accent-blue:   #58a6da;
  --accent-yellow: #d0a52b;
  --accent-red:    #e25d5a;
}
```

- [ ] **Step 2: 跑既有測試確認沒壞**

Run: `cd web && npm run test`
Expected: 全數 PASS（測試不驗 CSS 值）

- [ ] **Step 3: Commit**

```bash
git add web/src/styles/theme.css
git commit -m "feat(web): Kavita-style dark tokens + warm light card gray"
```

---

## Task 2: `App.vue` 雙套 Naive UI themeOverrides

**Files:**
- Modify: `web/src/App.vue`

- [ ] **Step 1: 以下列完整內容取代整個檔案**

```vue
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
```

- [ ] **Step 2: 跑既有測試**

Run: `cd web && npm run test`
Expected: 全數 PASS

- [ ] **Step 3: Commit**

```bash
git add web/src/App.vue
git commit -m "feat(web): per-mode Naive UI themeOverrides (Solarized light / Kavita dark)"
```

---

## Task 3: `BookCard.vue` 三處小修

**Files:**
- Modify: `web/src/components/BookCard.vue`
- Test: `web/src/components/BookCard.test.ts`（既有，應持續通過）

- [ ] **Step 1: 先跑既有測試建立基準**

Run: `cd web && npm run test -- BookCard.test`
Expected: 全數 PASS

- [ ] **Step 2: dropdown 刪除項顏色 token 化**

在 `<script setup>` 的 `dropdownOptions` 裡（約第 38 行），把：

```ts
    label: () => h('span', { style: { color: '#e88080' } }, '刪除'),
```

改成：

```ts
    label: () => h('span', { style: { color: 'var(--accent-red)' } }, '刪除'),
```

- [ ] **Step 3: hover 改用底色 token、info-bar 加分隔線**

在 `<style scoped>` 裡，把：

```css
.info-bar:hover {
  transform: translateY(-1px);
  filter: brightness(1.08);
}
```

改成：

```css
.info-bar:hover {
  transform: translateY(-1px);
  background: var(--bg-card-hover);
}
```

並在 `.info-bar` 規則中加一行 `border-top`，改完長這樣：

```css
.info-bar {
  cursor: pointer;
  min-height: 55px;
  padding: 8px 6px 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  box-sizing: border-box;
  background: var(--bg-card);
  border-top: 1px solid var(--border-card);
}
```

- [ ] **Step 4: 跑測試確認沒壞**

Run: `cd web && npm run test -- BookCard.test`
Expected: 全數 PASS

- [ ] **Step 5: Commit**

```bash
git add web/src/components/BookCard.vue
git commit -m "fix(web): BookCard delete uses accent-red token, hover bg + card border"
```

---

## Task 4: 清理 + 全套測試 + 視覺驗證

**Files:**
- Delete: `web/verify-solarized.mjs`

- [ ] **Step 1: 刪除過時驗證腳本**

```bash
rm web/verify-solarized.mjs
```

（此檔未被 git 追蹤，刪掉即可，不用 git rm。）

- [ ] **Step 2: 跑完整測試套件**

Run: `cd web && npm run test`
Expected: 全數 PASS。若有失敗，先修再繼續。

- [ ] **Step 3: 啟動 dev server 視覺驗證**

Run: `cd web && npm run dev`

打開 `http://localhost:5173`，兩種模式各檢查一輪：

**淺色（預設）：**
- 頁面背景是奶油色 `#fdf6e3`（不是白色——這次補了 `bodyColor` 才生效）
- 卡片資訊列是暖灰 `#dcd5c6`，與封面之間有細分隔線
- 卡片 hover 時資訊列變深一階（不再是無感的 brightness）
- logo 橄欖綠 `#859900`

**深色（按 🌙 切換）：**
- 頁面背景近黑灰 `#1f2020`、導覽列稍亮 `#2a2b2c`
- 卡片資訊列 `#202122`、文字近白
- logo 與主按鈕變 Kavita 綠 `#4ac694`
- 星等是提亮的金色 `#d0a52b`
- 卡片選單「刪除」是提亮紅 `#e25d5a`，dialog 確認鈕同色系
- dropdown / dialog 底色 `#2a2b2c`

- [ ] **Step 4: 若視覺驗證需要微調，最後 commit**

```bash
git add -p
git commit -m "fix(web): visual tweaks after Kavita dark theme integration"
```

只在有額外修改時執行，否則跳過。
