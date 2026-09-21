<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ points: { label: string; value: number }[] }>()

const SIZE = 260
const CENTER = SIZE / 2
const RADIUS = 95
const RINGS = [0.25, 0.5, 0.75, 1]

const maxValue = computed(() => Math.max(1, ...props.points.map((p) => p.value)))

function vertex(i: number, fraction: number) {
  const angle = (2 * Math.PI * i) / props.points.length - Math.PI / 2
  return [CENTER + Math.cos(angle) * RADIUS * fraction, CENTER + Math.sin(angle) * RADIUS * fraction]
}

const axisLines = computed(() => props.points.map((_, i) => vertex(i, 1)))
const ringPolygons = computed(() => RINGS.map((r) => props.points.map((_, i) => vertex(i, r).join(',')).join(' ')))
const dataPolygon = computed(() =>
  props.points.map((p, i) => vertex(i, p.value / maxValue.value).join(',')).join(' '),
)
const labels = computed(() => props.points.map((p, i) => ({ ...p, pos: vertex(i, 1.18) })))
</script>

<template>
  <svg :viewBox="`0 0 ${SIZE} ${SIZE}`" class="radar">
    <polygon v-for="(ring, i) in ringPolygons" :key="i" :points="ring" class="ring" />
    <line v-for="(end, i) in axisLines" :key="i" :x1="CENTER" :y1="CENTER" :x2="end[0]" :y2="end[1]" class="axis" />
    <polygon :points="dataPolygon" class="data" />
    <text v-for="l in labels" :key="l.label" :x="l.pos[0]" :y="l.pos[1]" class="label" text-anchor="middle" dominant-baseline="middle">
      {{ l.label }}
    </text>
  </svg>
</template>

<style scoped>
.radar {
  width: 100%;
  max-width: 320px;
  height: auto;
}
.ring {
  fill: none;
  stroke: var(--color-border);
  stroke-width: 1;
}
.axis {
  stroke: var(--color-border);
  stroke-width: 1;
}
.data {
  fill: var(--color-accent-soft);
  stroke: var(--color-accent);
  stroke-width: 1.5;
}
.label {
  fill: var(--color-text-muted);
  font-size: 9px;
}
</style>
