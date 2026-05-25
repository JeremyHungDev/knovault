<script setup lang="ts">
import { NProgress, NText, NEllipsis } from 'naive-ui'
import { storeToRefs } from 'pinia'
import { useLibraryStore } from '@/stores/library'

const lib = useLibraryStore()
const { scanning, progress, progressPercent } = storeToRefs(lib)
</script>

<template>
  <div v-if="scanning" class="scan-bar">
    <div class="scan-line">
      <n-text depth="2">
        掃描中… {{ progress?.processed ?? 0 }}/{{ progress?.total ?? 0 }}
      </n-text>
      <n-ellipsis v-if="progress?.currentFile" class="current" :line-clamp="1">
        {{ progress?.currentFile }}
      </n-ellipsis>
    </div>
    <n-progress
      type="line"
      :percentage="progressPercent"
      :indicator-placement="'inside'"
      :processing="true"
    />
  </div>
</template>

<style scoped>
.scan-bar {
  margin: 8px 0 16px;
}
.scan-line {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 4px;
  font-size: 13px;
}
.current {
  opacity: 0.6;
  max-width: 60%;
}
</style>
