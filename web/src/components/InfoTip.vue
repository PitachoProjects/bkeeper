<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <span class="info-tip" :title="text" tabindex="0">i</span>
</template>

<style scoped>
.info-tip {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  border: 1px solid var(--color-border-strong);
  color: var(--color-text-muted);
  font-size: 0.65rem;
  font-style: italic;
  cursor: help;
  margin-left: 0.3rem;
  line-height: 1;
  flex-shrink: 0;
}
</style>
