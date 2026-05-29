# Solarized Theme System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Centralize all dark/light colors into a single CSS custom-properties file using VSCode Solarized Light/Dark palette, replacing scattered hardcoded colors across Vue SFC components.

**Architecture:** A new `theme.css` file defines `:root` (light) and `:root.dark` (dark) CSS variables. The `theme.ts` Pinia store syncs `document.documentElement.classList` on init and on every toggle. Components drop all JS-based dark/light class logic and use `var(--token)` only. Naive UI gets `themeOverrides` for Solarized accent colors.

**Tech Stack:** Vue 3, Pinia, Naive UI 2.x, Vitest + @vue/test-utils + happy-dom

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `web/src/styles/theme.css` | All CSS custom properties (light + dark) |
| Modify | `web/src/main.ts` | Import theme.css |
| Modify | `web/src/stores/theme.ts` | Sync `html.dark` class on init + watch |
| Create | `web/src/stores/theme.test.ts` | Verify html class sync behavior |
| Modify | `web/src/App.vue` | Add Naive UI `themeOverrides` |
| Modify | `web/src/components/BookCard.vue` | Remove dark/light class logic; use var() |
| Modify | `web/src/components/AppShell.vue` | Logo SVG fill via CSS var |
| Modify | `web/src/components/ReviewsSection.vue` | Replace 2 hardcoded colors |
| Modify | `web/src/views/BookDetailView.vue` | Replace 3 hardcoded colors |
| Modify | `web/src/views/BookFormView.vue` | Replace 1 hardcoded color |

`RelatedBooksSection.vue` — no themed colors, no change needed.

---

## Task 1: Create `theme.css` and import it

**Files:**
- Create: `web/src/styles/theme.css`
- Modify: `web/src/main.ts`

- [ ] **Step 1: Create the CSS file**

Create `web/src/styles/theme.css` with this exact content:

```css
/* Solarized Light (default) */
:root {
  --bg-base:      #fdf6e3;
  --bg-elevated:  #eee8d5;
  --bg-surface:   rgba(88, 110, 117, 0.06);

  --text-primary:   #586e75;
  --text-secondary: #93a1a1;
  --text-muted:     #839496;

  --hover-bg: rgba(88, 110, 117, 0.10);

  --accent-brand:  #859900;
  --accent-blue:   #268bd2;
  --accent-yellow: #b58900;
  --accent-red:    #dc322f;
}

/* Solarized Dark */
:root.dark {
  --bg-base:      #002b36;
  --bg-elevated:  #073642;
  --bg-surface:   rgba(131, 148, 150, 0.06);

  --text-primary:   #93a1a1;
  --text-secondary: #586e75;
  --text-muted:     #657b83;

  --hover-bg: rgba(131, 148, 150, 0.10);
}
```

Note: accent tokens (`--accent-*`) are not redefined in `:root.dark` — they are identical in both modes.

- [ ] **Step 2: Import in `web/src/main.ts`**

Replace the file with:

```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './styles/theme.css'
import App from './App.vue'
import router from './router'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
```

- [ ] **Step 3: Commit**

```bash
git add web/src/styles/theme.css web/src/main.ts
git commit -m "feat(web): add Solarized CSS custom properties"
```

---

## Task 2: Update `theme.ts` to sync `html.dark` class

**Files:**
- Modify: `web/src/stores/theme.ts`
- Create: `web/src/stores/theme.test.ts`

- [ ] **Step 1: Write the failing tests first**

Create `web/src/stores/theme.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useThemeStore } from './theme'

beforeEach(() => {
  localStorage.clear()
  document.documentElement.classList.remove('dark')
  setActivePinia(createPinia())
})

describe('useThemeStore html class sync', () => {
  it('html.dark class is set correctly after toggle', () => {
    const store = useThemeStore()
    store.toggle()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })

  it('html.dark class is removed after toggling back', () => {
    const store = useThemeStore()
    store.toggle()
    store.toggle()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })

  it('html.dark class matches initial store.dark on creation', () => {
    const store = useThemeStore()
    expect(document.documentElement.classList.contains('dark')).toBe(store.dark)
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd web && npm run test -- theme.test
```

Expected: 3 failures — `html class sync` behavior not implemented yet.

- [ ] **Step 3: Update `web/src/stores/theme.ts`**

Replace the file with:

```ts
import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const STORAGE_KEY = 'knovault.theme.dark'

export const useThemeStore = defineStore('theme', () => {
  const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
  const prefersDark =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  const dark = ref(stored != null ? stored === '1' : prefersDark)

  function syncClass(v: boolean) {
    if (typeof document !== 'undefined') {
      document.documentElement.classList.toggle('dark', v)
    }
  }

  syncClass(dark.value)

  function toggle() {
    dark.value = !dark.value
  }

  watch(dark, (v) => {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, v ? '1' : '0')
    syncClass(v)
  }, { flush: 'sync' })

  return { dark, toggle }
})
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd web && npm run test -- theme.test
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add web/src/stores/theme.ts web/src/stores/theme.test.ts
git commit -m "feat(web): sync html.dark class from theme store"
```

---

## Task 3: Add Naive UI `themeOverrides` to `App.vue`

**Files:**
- Modify: `web/src/App.vue`

- [ ] **Step 1: Update `web/src/App.vue`**

Replace the file with:

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
```

- [ ] **Step 2: Commit**

```bash
git add web/src/App.vue
git commit -m "feat(web): add Solarized Naive UI themeOverrides"
```

---

## Task 4: Update `BookCard.vue`

This is the biggest change: remove the JS-based dark/light class switching and use CSS variables instead.

**Files:**
- Modify: `web/src/components/BookCard.vue`
- Test: `web/src/components/BookCard.test.ts` (existing, should still pass)

- [ ] **Step 1: Run existing tests first to establish baseline**

```bash
cd web && npm run test -- BookCard.test
```

Expected: all pass.

- [ ] **Step 2: Update `web/src/components/BookCard.vue`**

In the `<script setup>` block, remove the `useThemeStore` import and usage. The block becomes:

```ts
import { computed, h, ref } from 'vue'
import { NDropdown, NEllipsis, useDialog, useMessage } from 'naive-ui'
import { useRouter } from 'vue-router'
import type { BookSummary, ReadingStatus } from '@/api/types'
import { booksApi } from '@/api/books'
import { coverThumbUrl } from '@/api/http'
import { authorsLine, READING_STATUS_LABELS } from '@/utils/format'

const props = defineProps<{ book: BookSummary }>()
const emit = defineEmits<{ refresh: [] }>()

const router = useRouter()
const dialog = useDialog()
const message = useMessage()
const coverFailed = ref(false)
```

In the `<template>`, change the info-bar div from:

```html
<div class="info-bar" :class="themeStore.dark ? 'info-dark' : 'info-light'" @click="open">
```

to:

```html
<div class="info-bar" @click="open">
```

Replace the entire `<style scoped>` section with:

```css
.book-card {
  cursor: default;
  border-radius: 8px;
  overflow: hidden;
  background: rgba(128, 128, 128, 0.12);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
  transition: transform 0.15s ease, box-shadow 0.15s ease;
  display: flex;
  flex-direction: column;
}
.book-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.22);
}
.info-bar:hover {
  transform: translateY(-1px);
  filter: brightness(1.08);
}
.cover-wrap {
  position: relative;
  aspect-ratio: 3 / 4;
  overflow: hidden;
}
.cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.cover-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  font-size: 48px;
  color: rgba(128, 128, 128, 0.5);
}
.badges {
  position: absolute;
  top: 6px;
  left: 6px;
  display: flex;
  gap: 4px;
  font-size: 14px;
  background: rgba(0, 0, 0, 0.45);
  padding: 1px 5px;
  border-radius: 6px;
}
.info-bar {
  cursor: pointer;
  min-height: 55px;
  padding: 8px 6px 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  box-sizing: border-box;
  background: var(--bg-elevated);
}
.info-bar .title {
  color: var(--text-primary);
}
.info-bar .author {
  color: var(--text-secondary);
}
.info-bar .menu-btn {
  color: var(--text-muted);
}
.info-bar .menu-btn:hover {
  color: var(--text-primary);
  background: var(--hover-bg);
}
.title {
  font-weight: 600;
  font-size: 13px;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
}
.author-row {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  min-width: 0;
}
.author {
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.menu-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 18px;
  padding: 0 2px;
  line-height: 1;
  border-radius: 4px;
}
.menu-btn:focus-visible {
  outline: 2px solid var(--accent-brand);
  outline-offset: 2px;
}
```

- [ ] **Step 3: Run tests to verify nothing broke**

```bash
cd web && npm run test -- BookCard.test
```

Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add web/src/components/BookCard.vue
git commit -m "refactor(web): BookCard uses CSS var tokens, drops JS theme logic"
```

---

## Task 5: Update `AppShell.vue` — logo brand color

**Files:**
- Modify: `web/src/components/AppShell.vue`

- [ ] **Step 1: Add CSS rule for logo SVG fill**

In `web/src/components/AppShell.vue`, add this rule to the `<style scoped>` section (after `.content-inner`):

```css
.brand :deep(svg rect:first-child) {
  fill: var(--accent-brand);
}
```

The `:deep()` is required because the SVG is rendered inside `<n-icon>` which is a child component — scoped styles don't penetrate child component roots without it.

- [ ] **Step 2: Commit**

```bash
git add web/src/components/AppShell.vue
git commit -m "feat(web): AppShell logo color via CSS var(--accent-brand)"
```

---

## Task 6: Update `ReviewsSection.vue`

**Files:**
- Modify: `web/src/components/ReviewsSection.vue`

- [ ] **Step 1: Replace hardcoded colors in `<style scoped>`**

In `web/src/components/ReviewsSection.vue`, make these two replacements in the `<style scoped>` block:

1. `.placeholder-box` background:
   - Old: `background: rgba(128, 128, 128, 0.06);`
   - New: `background: var(--bg-surface);`

2. `.review-card` background:
   - Old: `background: rgba(128, 128, 128, 0.06);`
   - New: `background: var(--bg-surface);`

3. `.stars` color:
   - Old: `color: #f0a500;`
   - New: `color: var(--accent-yellow);`

- [ ] **Step 2: Commit**

```bash
git add web/src/components/ReviewsSection.vue
git commit -m "feat(web): ReviewsSection uses CSS var tokens"
```

---

## Task 7: Update `BookDetailView.vue`

**Files:**
- Modify: `web/src/views/BookDetailView.vue`

- [ ] **Step 1: Replace hardcoded colors in `<style scoped>`**

In `web/src/views/BookDetailView.vue`, make these replacements in the `<style scoped>` block:

1. `.cover` background (cover placeholder background):
   - Old: `background: rgba(128, 128, 128, 0.12);`
   - New: `background: var(--bg-surface);`

2. `.cover-placeholder` color:
   - Old: `color: rgba(128, 128, 128, 0.5);`
   - New: `color: var(--text-muted);`

3. `.reading-box` background:
   - Old: `background: rgba(128, 128, 128, 0.08);`
   - New: `background: var(--bg-surface);`

Leave these as-is (intentional opaque overlays on images/content):
- `rgba(0, 0, 0, 0.45)` — `.cover-overlay` background
- `rgba(0, 0, 0, 0.25)` — `.cover-clickable.is-empty .cover-overlay` background
- `rgba(0, 0, 0, 0.4)` — `.cover-loading` background
- `#fff` — `.cover-overlay` text color
- All box-shadow values

- [ ] **Step 2: Run existing tests**

```bash
cd web && npm run test -- BookDetailView.test
```

Expected: all pass (tests don't check CSS values).

- [ ] **Step 3: Commit**

```bash
git add web/src/views/BookDetailView.vue
git commit -m "feat(web): BookDetailView uses CSS var tokens"
```

---

## Task 8: Update `BookFormView.vue`

**Files:**
- Modify: `web/src/views/BookFormView.vue`

- [ ] **Step 1: Replace hardcoded color in `<style scoped>`**

In `web/src/views/BookFormView.vue`, in the `<style scoped>` block:

`.back-btn` color:
- Old: `color: #aaa;`
- New: `color: var(--text-muted);`

Leave `box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2)` on `.cover-preview img` as-is.

- [ ] **Step 2: Run existing tests**

```bash
cd web && npm run test -- BookFormView.test
```

Expected: all pass.

- [ ] **Step 3: Commit**

```bash
git add web/src/views/BookFormView.vue
git commit -m "feat(web): BookFormView uses CSS var tokens"
```

---

## Task 9: Full test suite + visual verification

**Files:** none

- [ ] **Step 1: Run the full test suite**

```bash
cd web && npm run test
```

Expected: all tests pass. Fix any failures before proceeding.

- [ ] **Step 2: Start dev server and verify visually**

```bash
cd web && npm run dev
```

Open `http://localhost:5173` in a browser. Check:
- Default (light) mode: cream/warm background (`#fdf6e3`), muted teal text (`#586e75`)
- Toggle to dark mode: deep teal background (`#002b36`), card panels `#073642`
- Book card info-bar color changes with theme
- Logo SVG color changes with theme (olive green `#859900`)
- Star ratings display `#b58900` gold
- Delete menu item displays `#dc322f` red

- [ ] **Step 3: Final commit if any last-minute tweaks were needed**

```bash
git add -p
git commit -m "fix(web): visual tweaks after Solarized theme integration"
```

Only run this step if you made additional changes during visual verification. Skip if no changes were needed.
