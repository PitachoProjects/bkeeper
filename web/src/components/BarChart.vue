<script setup lang="ts">
import { computed } from 'vue'

// Simple horizontal bar chart — hand-rolled like RadarChart.vue, no charting dependency for
// what's a handful of divs. Each bar's `colorClass` maps to the existing badge/severity color
// tokens (red/amber/info) so chart colors stay consistent with the badges elsewhere on the page.
const props = defineProps<{ bars: { label: string; value: number; colorClass?: string }[]; valueSuffix?: string }>()

const maxValue = computed(() => Math.max(1, ...props.bars.map((b) => b.value)))
</script>

<template>
  <div class="bar-chart">
    <div v-for="b in bars" :key="b.label" class="bar-row">
      <span class="bar-label">{{ b.label }}</span>
      <div class="bar-track">
        <div class="bar-fill" :class="b.colorClass" :style="{ width: (b.value / maxValue) * 100 + '%' }"></div>
      </div>
      <span class="bar-value">{{ b.value }}{{ valueSuffix ?? '' }}</span>
    </div>
    <p v-if="bars.length === 0" class="empty">—</p>
  </div>
</template>

<style scoped>
.bar-chart {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.bar-row {
  display: grid;
  grid-template-columns: minmax(90px, 140px) 1fr 44px;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.82rem;
}
.bar-label {
  text-transform: capitalize;
  color: var(--color-text-muted);
}
.bar-track {
  background: var(--color-bg-soft);
  border-radius: 4px;
  height: 14px;
  overflow: hidden;
}
.bar-fill {
  background: var(--color-accent);
  height: 100%;
  transition: width 0.2s ease;
}
.bar-fill.red {
  background: var(--color-danger);
}
.bar-fill.amber {
  background: var(--color-warning);
}
.bar-fill.info {
  background: var(--color-info);
}
.bar-value {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
</style>
