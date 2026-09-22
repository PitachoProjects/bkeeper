<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, ApiError } from '@/lib/api'
import ExplainThis from '@/components/ExplainThis.vue'
import BarChart from '@/components/BarChart.vue'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const authStore = useAuthStore()
const isManagerOrOwner = computed(() => authStore.role === 'Manager' || authStore.role === 'Owner')

interface CohortCurve {
  cohortLabel: string
  cohortSize: number
  retentionByMonth: (number | null)[]
}
interface HistogramBucket {
  label: string
  count: number
}
interface RetentionOverview {
  activeCount: number
  newThisMonth: number
  churnedThisMonth: number
  netChange: number
  monthlyChurnRatePct: number
  lapsedCount: number
  cohorts: CohortCurve[]
  tenureAtChurnHistogram: HistogramBucket[]
}
interface CoachItem {
  id: string
  name: string
  status: string
}
interface NarrativeResponse {
  status: 'Generated' | 'NotConfigured' | 'Error'
  narrative: string | null
  message: string | null
  evidence: unknown
}

const COHORT_RANGES = [
  { key: '6m', months: 6 },
  { key: '12m', months: 12 },
  { key: '2y', months: 24 },
  { key: '5y', months: 60 },
] as const
type CohortRangeKey = (typeof COHORT_RANGES)[number]['key']
const cohortRange = ref<CohortRangeKey>('12m')

const loading = ref(true)
const retention = ref<RetentionOverview | null>(null)
const coaches = ref<CoachItem[]>([])
const coachFilter = ref('')

const visibleCohorts = computed(() => {
  if (!retention.value) return []
  const months = COHORT_RANGES.find((r) => r.key === cohortRange.value)!.months
  const cutoff = new Date()
  cutoff.setMonth(cutoff.getMonth() - months)
  const cutoffLabel = `${cutoff.getFullYear()}-${String(cutoff.getMonth() + 1).padStart(2, '0')}`
  return retention.value.cohorts.filter((c) => c.cohortLabel >= cutoffLabel)
})
const cohortMonthColumns = computed(() => Math.max(0, ...visibleCohorts.value.map((c) => c.retentionByMonth.length - 1)))

async function loadRetention() {
  const query = coachFilter.value ? `?coachId=${coachFilter.value}` : ''
  retention.value = await api.get(`/dashboards/retention${query}`)
}

const aiConfigured = ref<boolean | null>(null)
const narrative = ref<NarrativeResponse | null>(null)
const narrativeLoading = ref(false)
const narrativeError = ref<string | null>(null)

async function checkAiConfigured() {
  if (!isManagerOrOwner.value || aiConfigured.value !== null) return
  try {
    const res = await api.get<{ configured: boolean }>('/insights/status')
    aiConfigured.value = res.configured
  } catch {
    aiConfigured.value = false
  }
}

async function getAiSummary() {
  narrativeLoading.value = true
  narrativeError.value = null
  try {
    narrative.value = await api.post<NarrativeResponse>('/insights/narrative', { scope: 'retention-overview' })
  } catch (e) {
    narrativeError.value = e instanceof ApiError ? e.message : t('dashboards.retention.aiSummary.error')
  } finally {
    narrativeLoading.value = false
  }
}

async function onCoachFilterChange() {
  loading.value = true
  try {
    await loadRetention()
  } finally {
    loading.value = false
  }
}

function retentionCellClass(v: number | null): string {
  if (v === null) return ''
  if (v >= 0.8) return 'good'
  if (v >= 0.5) return 'mid'
  return 'bad'
}

function exportRetentionCsv() {
  window.open(`${import.meta.env.VITE_API_URL ?? 'http://localhost:5080'}/dashboards/retention/export`, '_blank')
}

onMounted(async () => {
  loading.value = true
  try {
    coaches.value = await api.get<CoachItem[]>('/coaches?status=Active')
    await loadRetention()
    await checkAiConfigured()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div>
    <h1>{{ t('dashboards.pages.retention') }}</h1>
    <p class="page-subtitle">{{ t('dashboards.retention.pageHint') }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else-if="retention">
      <div class="coach-filter">
        <label>{{ t('dashboards.retention.coachFilter') }}</label>
        <select v-model="coachFilter" @change="onCoachFilterChange">
          <option value="">{{ t('dashboards.retention.allCoaches') }}</option>
          <option v-for="c in coaches" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </div>
      <div class="stat-row">
        <div class="stat">
          <ExplainThis metric-key="active_members" icon-only />
          <span class="stat-value">{{ retention.activeCount }}</span>
          <span class="stat-label">{{ t('dashboards.retention.active') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.asOfToday') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="new_members_this_month" icon-only />
          <span class="stat-value">{{ retention.newThisMonth }}</span>
          <span class="stat-label">{{ t('dashboards.retention.newThisMonth') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="churned_members_this_month" icon-only />
          <span class="stat-value">{{ retention.churnedThisMonth }}</span>
          <span class="stat-label">{{ t('dashboards.retention.churnedThisMonth') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="net_member_change" icon-only />
          <span class="stat-value" :class="retention.netChange >= 0 ? 'good-text' : 'bad-text'">{{ retention.netChange >= 0 ? '+' : '' }}{{ retention.netChange }}</span>
          <span class="stat-label">{{ t('dashboards.retention.netChange') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="monthly_churn_rate_pct" icon-only />
          <span class="stat-value">{{ retention.monthlyChurnRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.retention.monthlyChurn') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="lapsed_members" icon-only />
          <span class="stat-value">{{ retention.lapsedCount }}</span>
          <span class="stat-label">{{ t('dashboards.retention.lapsed') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.asOfToday') }}</span>
        </div>
      </div>

      <section v-if="isManagerOrOwner" class="card ai-summary">
        <h2>{{ t('dashboards.retention.aiSummary.title') }}</h2>
        <p class="hint">{{ t('dashboards.retention.aiSummary.hint') }}</p>

        <button
          class="ghost"
          :disabled="narrativeLoading || aiConfigured === false"
          :title="aiConfigured === false ? t('dashboards.retention.aiSummary.notConfiguredTooltip') : undefined"
          @click="getAiSummary"
        >
          {{ narrativeLoading ? t('dashboards.retention.aiSummary.loading') : t('dashboards.retention.aiSummary.button') }}
        </button>

        <p v-if="narrativeError" class="bad-text ai-message">{{ narrativeError }}</p>

        <template v-if="narrative">
          <p v-if="narrative.status === 'NotConfigured'" class="hint ai-message">{{ narrative.message }}</p>
          <p v-else-if="narrative.status === 'Error'" class="bad-text ai-message">{{ narrative.message }}</p>
          <template v-else>
            <span class="badge ai-generated-badge">{{ t('dashboards.retention.aiSummary.aiGeneratedLabel') }}</span>
            <p class="ai-narrative">{{ narrative.narrative }}</p>
            <details class="ai-evidence">
              <summary>{{ t('dashboards.retention.aiSummary.evidenceToggle') }}</summary>
              <pre>{{ JSON.stringify(narrative.evidence, null, 2) }}</pre>
            </details>
          </template>
        </template>
      </section>

      <section class="card">
        <div class="section-header">
          <h2>{{ t('dashboards.retention.cohortTitle') }}<ExplainThis metric-key="cohort_retention_curve" /></h2>
          <button class="ghost" @click="exportRetentionCsv">{{ t('dashboards.retention.exportCsv') }}</button>
        </div>
        <p class="hint">{{ t('dashboards.retention.cohortHint') }}</p>
        <div class="range-filter" role="group" :aria-label="t('dashboards.retention.rangeLabel')">
          <button
            v-for="r in COHORT_RANGES"
            :key="r.key"
            :class="{ ghost: cohortRange !== r.key }"
            @click="cohortRange = r.key"
          >
            {{ t(`dashboards.retention.ranges.${r.key}`) }}
          </button>
        </div>
        <div class="cohort-table-wrap">
          <table class="cohort-table">
            <thead>
              <tr>
                <th>{{ t('dashboards.retention.cohort') }}</th>
                <th>{{ t('dashboards.retention.size') }}</th>
                <th v-for="i in cohortMonthColumns + 1" :key="i">M{{ i - 1 }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in visibleCohorts" :key="c.cohortLabel">
                <td>{{ c.cohortLabel }}</td>
                <td>{{ c.cohortSize }}</td>
                <td v-for="(v, i) in c.retentionByMonth" :key="i" class="cell" :class="retentionCellClass(v)">
                  {{ v !== null ? Math.round(v * 100) + '%' : '' }}
                </td>
              </tr>
              <tr v-if="visibleCohorts.length === 0"><td :colspan="cohortMonthColumns + 3" class="empty">{{ t('dashboards.retention.rangeEmpty') }}</td></tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.retention.tenureTitle') }}<ExplainThis metric-key="tenure_at_churn_histogram" /></h2>
        <p class="hint">{{ t('dashboards.retention.tenureHint') }}</p>
        <BarChart :bars="retention.tenureAtChurnHistogram.map((b) => ({ label: b.label, value: b.count }))" />
      </section>
    </template>
  </div>
</template>

<style scoped>
.coach-filter {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
.range-filter {
  display: flex;
  gap: 0.4rem;
  margin-bottom: 0.9rem;
}
.range-filter button {
  padding: 0.3rem 0.7rem;
  font-size: 0.78rem;
}
.cohort-table-wrap {
  overflow-x: auto;
}
.cohort-table th,
.cohort-table td {
  text-align: center;
  font-variant-numeric: tabular-nums;
}
.cohort-table td:first-child,
.cohort-table th:first-child {
  text-align: left;
}
.cell.good {
  background: var(--color-success-soft);
  color: var(--color-success);
}
.cell.mid {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}
.cell.bad {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.ai-summary .hint {
  margin-bottom: 0.75rem;
}
.ai-message {
  margin-top: 0.6rem;
}
.ai-generated-badge {
  display: inline-block;
  margin-top: 0.75rem;
  background: var(--color-info-soft);
  color: var(--color-info);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  font-size: 0.65rem;
}
.ai-narrative {
  white-space: pre-wrap;
  margin: 0.5rem 0 0;
  line-height: 1.5;
}
.ai-evidence {
  margin-top: 0.75rem;
}
.ai-evidence summary {
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 0.85rem;
}
.ai-evidence pre {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background: var(--color-bg-soft);
  border-radius: var(--radius-md);
  overflow-x: auto;
  font-size: 0.75rem;
}
</style>
