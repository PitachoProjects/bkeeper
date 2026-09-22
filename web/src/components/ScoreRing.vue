<script setup lang="ts">
import { computed } from 'vue'

// Circular progress ring for a single prominent score — hand-rolled SVG like RadarChart.vue,
// no charting dependency needed for one arc.
const props = withDefaults(defineProps<{ value: number; max?: number; colorClass?: string; size?: number }>(), {
  max: 100,
  size: 108,
})

const RADIUS = 46
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

const fraction = computed(() => Math.max(0, Math.min(1, props.value / props.max)))
const dashOffset = computed(() => CIRCUMFERENCE * (1 - fraction.value))
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 120 120" class="score-ring" :class="colorClass">
    <circle cx="60" cy="60" :r="RADIUS" class="track" />
    <circle
      cx="60"
      cy="60"
      :r="RADIUS"
      class="progress"
      :stroke-dasharray="CIRCUMFERENCE"
      :stroke-dashoffset="dashOffset"
      transform="rotate(-90 60 60)"
    />
    <text x="60" y="56" text-anchor="middle" class="value">{{ Math.round(value) }}</text>
    <text x="60" y="76" text-anchor="middle" class="max">/ {{ max }}</text>
  </svg>
</template>

<style scoped>
.score-ring {
  flex-shrink: 0;
}
.track {
  fill: none;
  stroke: var(--color-bg-soft);
  stroke-width: 10;
}
.progress {
  fill: none;
  stroke: var(--color-accent);
  stroke-width: 10;
  stroke-linecap: round;
  transition: stroke-dashoffset 0.3s ease;
}
.score-ring.green .progress {
  stroke: var(--color-success);
}
.score-ring.amber .progress {
  stroke: var(--color-warning);
}
.score-ring.red .progress {
  stroke: var(--color-danger);
}
.value {
  fill: var(--color-text);
  font-size: 30px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.max {
  fill: var(--color-text-faint);
  font-size: 13px;
}
</style>
