<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import BarChart from '@/components/BarChart.vue'
import HeatmapGrid from '@/components/HeatmapGrid.vue'

const { t } = useI18n()

interface HeatmapCell {
  day: string
  window: string
  type: string
  count: number
}
interface ClassFill {
  classType: string
  window: string
  avgFillPct: number
}
interface RecentSession {
  date: string
  classType: string
  workoutTitle: string | null
  workoutDescription: string | null
  tag: string
  attendedCount: number
  coachName: string | null
}
interface WorkoutMix {
  windowTypeHeatmap: HeatmapCell[]
  classFillBySlot: ClassFill[]
  recentSessions: RecentSession[]
}

const WINDOW_ORDER = ['Early', 'Morning', 'Lunch', 'Afternoon', 'Evening']

const loading = ref(true)
const data = ref<WorkoutMix | null>(null)
const typeFilter = ref('')
const recentClassTypeFilter = ref('')
const recentTypeFilter = ref('')

const workoutTypes = computed(() => [...new Set((data.value?.windowTypeHeatmap ?? []).map((c) => c.type))].sort())
const heatmapCells = computed(() =>
  (data.value?.windowTypeHeatmap ?? [])
    .filter((c) => !typeFilter.value || c.type === typeFilter.value)
    .reduce<{ day: string; window: string; count: number }[]>((acc, c) => {
      const existing = acc.find((x) => x.day === c.day && x.window === c.window)
      if (existing) existing.count += c.count
      else acc.push({ day: c.day, window: c.window, count: c.count })
      return acc
    }, []),
)

const recentClassTypes = computed(() => [...new Set((data.value?.recentSessions ?? []).map((s) => s.classType))].sort())
const recentTypes = computed(() => [...new Set((data.value?.recentSessions ?? []).map((s) => s.tag))].sort())
const filteredRecentSessions = computed(() =>
  (data.value?.recentSessions ?? [])
    .filter((s) => !recentClassTypeFilter.value || s.classType === recentClassTypeFilter.value)
    .filter((s) => !recentTypeFilter.value || s.tag === recentTypeFilter.value),
)

function formatSessionDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

onMounted(async () => {
  loading.value = true
  try {
    data.value = await api.get('/dashboards/workouts')
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div>
    <h1>{{ t('dashboards.pages.workouts') }}</h1>
    <p class="page-subtitle">{{ t('dashboards.workouts.pageHint') }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else-if="data">
      <section class="card">
        <div class="section-header">
          <h2>{{ t('dashboards.workouts.heatmapTitle') }}</h2>
          <select v-model="typeFilter">
            <option value="">{{ t('dashboards.workouts.allTypes') }}</option>
            <option v-for="ty in workoutTypes" :key="ty" :value="ty">{{ ty }}</option>
          </select>
        </div>
        <p class="hint">{{ t('dashboards.workouts.windowsHint') }}</p>
        <HeatmapGrid :windows="WINDOW_ORDER" :cells="heatmapCells" />
        <p v-if="data.windowTypeHeatmap.length === 0" class="empty">{{ t('dashboards.workouts.empty') }}</p>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.workouts.fillTitle') }}</h2>
        <p class="hint">{{ t('dashboards.workouts.fillHint') }}</p>
        <BarChart
          :bars="data.classFillBySlot.map((c) => ({ label: `${c.classType} · ${c.window}`, value: c.avgFillPct }))"
          value-suffix="%"
        />
        <p v-if="data.classFillBySlot.length === 0" class="empty">{{ t('dashboards.workouts.fillEmpty') }}</p>
      </section>

      <section class="card">
        <div class="section-header">
          <h2>{{ t('dashboards.workouts.recentTitle') }}</h2>
          <div class="recent-filters">
            <select v-model="recentClassTypeFilter">
              <option value="">{{ t('dashboards.workouts.allClassTypes') }}</option>
              <option v-for="ct in recentClassTypes" :key="ct" :value="ct">{{ ct }}</option>
            </select>
            <select v-model="recentTypeFilter">
              <option value="">{{ t('dashboards.workouts.allTypes') }}</option>
              <option v-for="ty in recentTypes" :key="ty" :value="ty">{{ ty }}</option>
            </select>
          </div>
        </div>
        <p class="hint">{{ t('dashboards.workouts.recentHint') }}</p>
        <table>
          <thead>
            <tr>
              <th>{{ t('dashboards.workouts.date') }}</th>
              <th>{{ t('dashboards.workouts.classType') }}</th>
              <th>{{ t('dashboards.workouts.type') }}</th>
              <th>{{ t('dashboards.workouts.workout') }}</th>
              <th>{{ t('dashboards.workouts.coach') }}</th>
              <th>{{ t('dashboards.workouts.attended') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(s, i) in filteredRecentSessions" :key="i">
              <td>{{ formatSessionDate(s.date) }}</td>
              <td>{{ s.classType }}</td>
              <td><span class="badge">{{ s.tag }}</span></td>
              <td :title="s.workoutDescription ?? undefined">{{ s.workoutTitle ?? '—' }}</td>
              <td>{{ s.coachName ?? '—' }}</td>
              <td>{{ s.attendedCount }}</td>
            </tr>
            <tr v-if="filteredRecentSessions.length === 0"><td colspan="6" class="empty">{{ t('dashboards.workouts.recentEmpty') }}</td></tr>
          </tbody>
        </table>
      </section>
    </template>
  </div>
</template>

<style scoped>
.recent-filters {
  display: flex;
  gap: 0.5rem;
}
</style>
