<script setup lang="ts">
import { computed } from 'vue'

// Day x window heatmap — a real grid in a fixed day/window order, instead of a table sorted by
// count (which made days look "jumpy" since row order followed volume, not the calendar).
const DAY_ORDER = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

const props = defineProps<{ windows: string[]; cells: { day: string; window: string; count: number }[] }>()

const maxCount = computed(() => Math.max(1, ...props.cells.map((c) => c.count)))
const days = computed(() => DAY_ORDER.filter((d) => props.cells.some((c) => c.day === d)))

function countFor(day: string, window: string) {
  return props.cells.find((c) => c.day === day && c.window === window)?.count ?? 0
}
function intensity(count: number) {
  return count === 0 ? 0 : 0.15 + 0.85 * (count / maxCount.value)
}
</script>

<template>
  <div class="heatmap" v-if="days.length > 0">
    <div class="heatmap-grid" :style="{ gridTemplateColumns: `70px repeat(${windows.length}, 1fr)` }">
      <div class="corner"></div>
      <div v-for="w in windows" :key="w" class="col-label">{{ w }}</div>
      <template v-for="d in days" :key="d">
        <div class="row-label">{{ d }}</div>
        <div
          v-for="w in windows"
          :key="w"
          class="cell"
          :class="{ strong: intensity(countFor(d, w)) > 0.5 }"
          :style="{ '--intensity': intensity(countFor(d, w)) }"
          :title="`${d} · ${w}: ${countFor(d, w)} visits`"
        >
          {{ countFor(d, w) || '' }}
        </div>
      </template>
    </div>
  </div>
  <p v-else class="empty">—</p>
</template>

<style scoped>
.heatmap-grid {
  display: grid;
  gap: 3px;
  font-size: 0.78rem;
}
.corner {
  background: transparent;
}
.col-label,
.row-label {
  font-size: 0.68rem;
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0.2rem;
}
.row-label {
  justify-content: flex-start;
  font-weight: 600;
}
.cell {
  background: color-mix(in srgb, var(--color-accent) calc(var(--intensity) * 100%), var(--color-bg-soft));
  color: var(--color-text);
  border-radius: 4px;
  min-height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-variant-numeric: tabular-nums;
}
.cell.strong {
  color: var(--color-accent-contrast);
}
</style>
