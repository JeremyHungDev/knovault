# Solarized Theme System Design

**Date:** 2026-05-29  
**Status:** Approved

## Goal

Centralize all dark/light mode colors into a single CSS custom properties file, replacing scattered hardcoded color values in Vue SFC `<style scoped>` blocks. Reference palette: VSCode Solarized Light / Solarized Dark.

## Current State

- Colors hardcoded in each Vue component's `<style scoped>`
- Dark/light toggled via `:class="themeStore.dark ? 'info-dark' : 'info-light'"` in templates
- Each component imports `useThemeStore` just for color switching
- No centralized source of truth for colors

## Target State

- One `web/src/styles/theme.css` defines all CSS custom properties
- `:root` = light values, `:root.dark` = dark values
- `theme.ts` store toggles `document.documentElement.classList` on change
- Components use `var(--token)` only — no JS theme checks in CSS logic
- Naive UI gets `themeOverrides` to align accent colors

---

## Solarized Palette Reference

| Name   | Hex       | Role                            |
|--------|-----------|---------------------------------|
| base03 | `#002b36` | Dark: darkest background        |
| base02 | `#073642` | Dark: background highlights     |
| base01 | `#586e75` | Dark: comments; Light: emphasis |
| base00 | `#657b83` | Body text (dark mode)           |
| base0  | `#839496` | Body text (dark mode, primary)  |
| base1  | `#93a1a1` | Dark: emphasis; Light: comments |
| base2  | `#eee8d5` | Light: background highlights    |
| base3  | `#fdf6e3` | Light: lightest background      |
| green  | `#859900` | Accent                          |
| blue   | `#268bd2` | Accent (primary action)         |
| cyan   | `#2aa198` | Accent (secondary)              |
| yellow | `#b58900` | Accent (warning / stars)        |
| red    | `#dc322f` | Accent (error / danger)         |

---

## CSS Token Design

### Backgrounds

| Token          | Light     | Dark      | Usage                          |
|----------------|-----------|-----------|--------------------------------|
| `--bg-base`    | `#fdf6e3` | `#002b36` | Page main background           |
| `--bg-elevated`| `#eee8d5` | `#073642` | Card info-bar, header, panels  |
| `--bg-surface` | `rgba(88,110,117,.06)` | `rgba(131,148,150,.06)` | Review cards, content boxes |

### Text

| Token              | Light     | Dark      | Usage                           |
|--------------------|-----------|-----------|---------------------------------|
| `--text-primary`   | `#586e75` | `#93a1a1` | Titles, main content            |
| `--text-secondary` | `#93a1a1` | `#586e75` | Author names, subtitles         |
| `--text-muted`     | `#839496` | `#657b83` | Placeholder, menu buttons       |

### Interaction

| Token        | Light                   | Dark                    | Usage          |
|--------------|-------------------------|-------------------------|----------------|
| `--hover-bg` | `rgba(88,110,117,.10)`  | `rgba(131,148,150,.10)` | Hover backgrounds |

### Accent Colors (same in both modes)

| Token            | Value     | Usage                       | Replaces   |
|------------------|-----------|-----------------------------|------------|
| `--accent-brand` | `#859900` | Brand color, logo, focus    | `#18a058`  |
| `--accent-blue`  | `#268bd2` | Links, primary actions      | (new)      |
| `--accent-yellow`| `#b58900` | Star ratings                | `#f0a500`  |
| `--accent-red`   | `#dc322f` | Delete, error               | `#e88080`  |

---

## File Changes

### 1. New file: `web/src/styles/theme.css`

```css
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

Accent tokens are not repeated in `:root.dark` — they are identical in both modes.

### 2. `web/src/main.ts`

Add: `import './styles/theme.css'`

### 3. `web/src/stores/theme.ts`

On store init and on `watch(dark, ...)`: sync `document.documentElement.classList.toggle('dark', v)`.

### 4. `web/src/App.vue`

Add `themeOverrides` computed prop to `NConfigProvider`:

```ts
const themeOverrides = {
  common: {
    primaryColor:  '#268bd2',
    successColor:  '#859900',
    warningColor:  '#b58900',
    errorColor:    '#dc322f',
    infoColor:     '#2aa198',
  }
}
```

Pass as `:theme-overrides="themeOverrides"` to `NConfigProvider`.

### 5. Components to update

Replace all hardcoded colors with CSS variables and remove `themeStore` dark/light class logic:

| File | Specific changes |
|------|-----------------|
| `BookCard.vue` | Remove `.info-dark`/`.info-light` classes + `themeStore` import; `.info-bar` bg → `var(--bg-elevated)`; title → `var(--text-primary)`; author → `var(--text-secondary)`; menu-btn → `var(--text-muted)`; hover-bg → `var(--hover-bg)`; focus outline → `var(--accent-brand)` |
| `AppShell.vue` | Logo SVG `rect` fill: add CSS rule `.brand svg rect:first-child { fill: var(--accent-brand); }` (removes hardcoded `fill="#18a058"` attribute override via CSS) |
| `ReviewsSection.vue` | `rgba(128,128,128,.06)` × 2 → `var(--bg-surface)`; `#f0a500` → `var(--accent-yellow)` |
| `BookDetailView.vue` | `rgba(128,128,128,.12)` / `rgba(128,128,128,.08)` → `var(--bg-surface)`; `rgba(128,128,128,.5)` → `var(--text-muted)` |
| `BookFormView.vue` | `#aaa` → `var(--text-muted)` |

`RelatedBooksSection.vue` has no hardcoded themed colors — no changes needed.

**Do not replace** (intentionally opaque, overlaid on images or content):
- `rgba(0,0,0,.45)` — badge background on book cover
- `rgba(0,0,0,.25)` / `rgba(0,0,0,.4)` — modal/overlay backdrops
- `#fff` — white text on dark badge background
- Box shadows (`rgba(0,0,0,...)`) — keep as-is

---

## Out of Scope

- Naive UI component internals (managed by `darkTheme` + `themeOverrides`)
- Badge background `rgba(0,0,0,.45)` — overlaid on images, intentionally opaque black
- Modal overlay `rgba(0,0,0,.25)` — standard overlay, not themed
- Box shadow values — keep as-is
