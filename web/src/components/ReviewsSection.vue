<!-- web/src/components/ReviewsSection.vue -->
<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import {
  NRadioGroup,
  NRadioButton,
  NSelect,
  NSpin,
  NAlert,
  NEmpty,
  NButton,
  NSpace,
} from "naive-ui";
import { reviewsApi } from "@/api/reviews";
import type { ReviewsResult, ReviewSourceResult } from "@/api/types";

const props = defineProps<{ bookId: string; isbn: string | null }>();

const loading = ref(false);
const error = ref<string | null>(null);
const result = ref<ReviewsResult | null>(null);
const refreshing = ref(false);

const platformOptions = [
  { label: "Goodreads", value: "Goodreads" },
  { label: "博客來", value: "BooksComTw" },
];
const selectedPlatform = ref<string>("Goodreads");

const currentSource = computed<ReviewSourceResult | null>(() => {
  if (!result.value) return null;
  return (
    result.value.sources.find((s) => s.source === selectedPlatform.value) ??
    null
  );
});

const isBooksComTw = computed(() => selectedPlatform.value === "BooksComTw");

async function load() {
  if (!props.isbn) return;
  loading.value = true;
  error.value = null;
  try {
    result.value = await reviewsApi.get(props.bookId);
  } catch (e) {
    error.value = e instanceof Error ? e.message : "載入失敗";
  } finally {
    loading.value = false;
  }
}

async function refresh() {
  if (!props.isbn) return;
  refreshing.value = true;
  error.value = null;
  try {
    result.value = await reviewsApi.refresh(props.bookId);
  } catch (e) {
    error.value = e instanceof Error ? e.message : "重新整理失敗";
  } finally {
    refreshing.value = false;
  }
}

function formatFetchedAt(iso: string | null): string {
  if (!iso) return "尚未抓取";
  return new Date(iso).toLocaleString("zh-TW", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function stars(rating: number | null): string {
  if (rating == null) return "";
  const full = Math.round(rating);
  return "★".repeat(full) + "☆".repeat(Math.max(0, 5 - full));
}

// 語言篩選
const langFilter = ref<"all" | "en" | "zh">("all");

function detectLang(text: string | null): "zh" | "en" | "other" {
  if (!text) return "other";
  const chinese = (text.match(/[一-鿿㐀-䶿]/g) ?? []).length;
  if (chinese > 3) return "zh";
  const latin = (text.match(/[a-zA-Z]/g) ?? []).length;
  const nonSpace = text.replace(/\s/g, "").length;
  return nonSpace > 0 && latin / nonSpace > 0.4 ? "en" : "other";
}

const filteredReviews = computed(() => {
  const reviews = currentSource.value?.reviews ?? [];
  const filtered =
    langFilter.value === "all"
      ? reviews
      : reviews.filter((r) => detectLang(r.reviewText) === langFilter.value);
  return [...filtered].sort((a, b) => (b.helpfulCount ?? 0) - (a.helpfulCount ?? 0));
});

const expanded = ref<Set<number>>(new Set());
function toggleExpand(i: number) {
  const s = new Set(expanded.value);
  s.has(i) ? s.delete(i) : s.add(i);
  expanded.value = s;
}

const textEls = ref<Map<number, HTMLElement>>(new Map());
const overflowSet = ref<Set<number>>(new Set());

function setTextEl(el: HTMLElement | null, i: number) {
  if (el) textEls.value.set(i, el);
  else textEls.value.delete(i);
}

function recomputeOverflow() {
  const next = new Set<number>();
  textEls.value.forEach((el, i) => {
    if (expanded.value.has(i)) {
      if (overflowSet.value.has(i)) next.add(i);
    } else {
      if (el.scrollHeight > el.clientHeight) next.add(i);
    }
  });
  overflowSet.value = next;
}

const PAGE_SIZE = 5;
const showAll = ref(false);
const visibleReviews = computed(() =>
  showAll.value ? filteredReviews.value : filteredReviews.value.slice(0, PAGE_SIZE)
);

watch(visibleReviews, () => nextTick(recomputeOverflow));

watch([langFilter, selectedPlatform], () => {
  expanded.value = new Set();
  showAll.value = false;
});

onMounted(load);
watch(() => props.bookId, load);
</script>

<template>
  <div class="reviews-section">
    <!-- 無 ISBN -->
    <n-empty v-if="!isbn" description="此書無 ISBN，無法查詢外部評論" />

    <template v-else>
      <!-- 平台切換 -->
      <div class="reviews-toolbar">
        <n-radio-group
          v-model:value="selectedPlatform"
          size="small"
          name="platform"
        >
          <n-radio-button
            v-for="opt in platformOptions"
            :key="opt.value"
            :value="opt.value"
          >
            {{ opt.label }}
          </n-radio-button>
        </n-radio-group>
        <n-select
          v-if="!isBooksComTw"
          v-model:value="langFilter"
          size="small"
          style="width: 90px"
          :options="[
            { label: '全部', value: 'all' },
            { label: '英文', value: 'en' },
            { label: '中文', value: 'zh' },
          ]"
        />
        <n-space
          v-if="currentSource?.fetchedAt"
          align="center"
          class="fetch-meta"
        >
          <span class="fetch-time"
            >資料更新：{{ formatFetchedAt(currentSource.fetchedAt) }}</span
          >
          <n-button size="tiny" :loading="refreshing" @click="refresh"
            >重新整理</n-button
          >
        </n-space>
      </div>

      <!-- 載入中 -->
      <n-spin v-if="loading" style="margin-top: 24px" />

      <!-- 錯誤 -->
      <n-alert
        v-else-if="error"
        type="error"
        :title="error"
        style="margin-top: 12px"
      >
        <n-button size="small" @click="load">重試</n-button>
      </n-alert>

      <!-- 博客來佔位 -->
      <div v-else-if="isBooksComTw" class="placeholder-box">
        <p>博客來評論功能開發中</p>
        <a
          :href="`https://search.books.com.tw/search/query/key/${isbn}/cat/BKall`"
          target="_blank"
          rel="noopener"
        >
          前往博客來查詢 ↗
        </a>
      </div>

      <!-- 無評論 -->
      <n-empty
        v-else-if="!currentSource || currentSource.reviews.length === 0"
        description="尚無評論"
        style="margin-top: 24px"
      >
        <template #extra>
          <n-button size="small" :loading="refreshing" @click="refresh"
            >從網路抓取</n-button
          >
        </template>
      </n-empty>

      <!-- 篩選後無結果 -->
      <n-empty
        v-else-if="filteredReviews.length === 0"
        description="此語言無評論"
        style="margin-top: 24px"
      />

      <!-- 評論列表 -->
      <div v-else class="reviews-list">
        <div
          v-for="(review, i) in visibleReviews"
          :key="i"
          class="review-card"
        >
          <div class="review-header">
            <span class="reviewer">{{ review.reviewerName ?? "匿名" }}</span>
            <span v-if="review.rating" class="stars">{{
              stars(review.rating)
            }}</span>
            <span class="review-date">{{
              review.reviewDate?.slice(0, 10) ?? ""
            }}</span>
            <span v-if="review.helpfulCount" class="helpful"
              >👍 {{ review.helpfulCount }}</span
            >
          </div>
          <div v-if="review.reviewText" class="review-text-wrap">
            <p
              class="review-text"
              :class="{ collapsed: !expanded.has(i) }"
              :ref="(el) => setTextEl(el as HTMLElement | null, i)"
            >{{ review.reviewText }}</p>
            <button
              v-if="overflowSet.has(i)"
              class="expand-btn"
              @click="toggleExpand(i)"
            >
              {{ expanded.has(i) ? "收合 ▲" : "展開 ▼" }}
            </button>
          </div>
        </div>
        <button
          v-if="filteredReviews.length > PAGE_SIZE"
          class="show-more-btn"
          @click="showAll = !showAll"
        >
          {{ showAll ? '收合 ▲' : `顯示更多（共 ${filteredReviews.length} 筆）▼` }}
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reviews-section {
  padding-top: 8px;
}
.reviews-toolbar {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}
.fetch-meta {
  font-size: 12px;
  opacity: 0.65;
}
.fetch-time {
  font-size: 12px;
}
.placeholder-box {
  margin-top: 16px;
  padding: 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
  text-align: center;
}
.placeholder-box a {
  color: var(--n-color);
  text-decoration: none;
  opacity: 0.8;
}
.reviews-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.review-card {
  padding: 12px 16px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.06);
}
.review-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 6px;
  font-size: 13px;
}
.reviewer {
  font-weight: 600;
}
.stars {
  color: #f0a500;
  letter-spacing: 1px;
}
.review-date {
  opacity: 0.55;
}
.helpful {
  opacity: 0.6;
}
.review-text-wrap {
  position: relative;
}
.review-text {
  margin: 0;
  font-size: 14px;
  line-height: 1.65;
  white-space: pre-wrap;
  opacity: 0.88;
}
.review-text.collapsed {
  display: -webkit-box;
  -webkit-line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.expand-btn {
  margin-top: 4px;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 12px;
  opacity: 0.5;
  padding: 0;
}
.expand-btn:hover {
  opacity: 0.9;
}
.show-more-btn {
  align-self: center;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 13px;
  opacity: 0.5;
  padding: 4px 0;
}
.show-more-btn:hover {
  opacity: 0.9;
}
</style>
