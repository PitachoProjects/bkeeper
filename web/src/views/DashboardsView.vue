<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, ApiError } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
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

interface AlertOperations {
  volumeBySeverity: Record<string, number>
  volumeByFamily: Record<string, number>
  slaComplianceRatePct: number
  avgTimeToClaimHours: number | null
  outcomesMix: Record<string, number>
  saveRatePct: number
  holdoutReturnRatePct: number | null
  treatedReturnRatePct: number | null
}

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

interface CoachItem {
  id: string
  name: string
  status: string
}

interface MyWeekAlert {
  id: string
  memberId: string
  memberName: string
  severity: string
  status: string
  dueAt: string
}
interface MyWeek {
  openAlerts: MyWeekAlert[]
  dueThisWeekCount: number
  resolvedThisWeekCount: number
}

interface NarrativeResponse {
  status: 'Generated' | 'NotConfigured' | 'Error'
  narrative: string | null
  message: string | null
  evidence: unknown
}

const TABS = ['retention', 'alertOps', 'workouts', 'myWeek'] as const
type TabId = (typeof TABS)[number]

// URL segments (readable, stable) mapped to the internal tab ids above (kept as-is to
// avoid touching every `tab === '...'` check below).
const ROUTE_TO_TAB: Record<string, TabId> = { retention: 'retention', attendance: 'workouts', interventions: 'alertOps', 'my-week': 'myWeek' }
const TAB_TO_ROUTE: Record<TabId, string> = { retention: 'retention', workouts: 'attendance', alertOps: 'interventions', myWeek: 'my-week' }

const tab = computed<TabId>(() => ROUTE_TO_TAB[route.params.tab as string] ?? 'retention')
const loading = ref(true)

function selectTab(tabId: TabId) {
  router.push(`/dashboard/${TAB_TO_ROUTE[tabId]}`)
}

const retention = ref<RetentionOverview | null>(null)
const alertOps = ref<AlertOperations | null>(null)
const workouts = ref<WorkoutMix | null>(null)
const myWeek = ref<MyWeek | null>(null)
const coaches = ref<CoachItem[]>([])
const coachFilter = ref('')

async function loadRetention() {
  const query = coachFilter.value ? `?coachId=${coachFilter.value}` : ''
  retention.value = await api.get(`/dashboards/retention${query}`)
}

const aiConfigured = ref<boolean | null>(null)
const narrative = ref<NarrativeResponse | null>(null)
const narrativeLoading = ref(false)
const narrativeError = ref<string | null>(null)

async function loadTab() {
  loading.value = true
  try {
    if (tab.value === 'retention') {
      if (coaches.value.length === 0) coaches.value = await api.get<CoachItem[]>('/coaches?status=Active')
      await loadRetention()
    }
    if (tab.value === 'alertOps' && !alertOps.value) alertOps.value = await api.get('/dashboards/alerts')
    if (tab.value === 'workouts' && !workouts.value) workouts.value = await api.get('/dashboards/workouts')
    if (tab.value === 'myWeek' && !myWeek.value) myWeek.value = await api.get('/dashboards/my-week')
  } finally {
    loading.value = false
  }
}

async function onCoachFilterChange() {
  loading.value = true
  try {
    await loadRetention()
  } finally {
    loading.value = false
// Cheap "is the feature turned on" check — never triggers a paid LLM call, so it's safe to run
// automatically when the retention tab first opens (unlike getAiSummary, which is user-triggered only).
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

function retentionCellClass(v: number | null): string {
  if (v === null) return ''
  if (v >= 0.8) return 'good'
  if (v >= 0.5) return 'mid'
  return 'bad'
}

function exportRetentionCsv() {
  window.open(`${import.meta.env.VITE_API_URL ?? 'http://localhost:5080'}/dashboards/retention/export`, '_blank')
}

watch(tab, loadTab)
onMounted(() => {
  if (!(route.params.tab as string in ROUTE_TO_TAB)) router.replace(`/dashboard/${TAB_TO_ROUTE[tab.value]}`)
  loadTab()
})
</script>

<template>
  <div>
    <h1>{{ t('dashboards.title') }}</h1>
    <div class="tabs">
      <button v-for="tabId in TABS" :key="tabId" :class="{ ghost: tab !== tabId }" @click="selectTab(tabId)">{{ t(`dashboards.tabs.${tabId}`) }}</button>
    </div>

    <p v-if="loading">Loading…</p>

    <template v-else-if="tab === 'retention' && retention">
      <div class="coach-filter">
        <label>{{ t('dashboards.retention.coachFilter') }}</label>
        <select v-model="coachFilter" @change="onCoachFilterChange">
          <option value="">{{ t('dashboards.retention.allCoaches') }}</option>
          <option v-for="c in coaches" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </div>
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ retention.activeCount }}</span><span class="stat-label">{{ t('dashboards.retention.active') }}</span></div>
        <div class="stat"><span class="stat-value">{{ retention.newThisMonth }}</span><span class="stat-label">{{ t('dashboards.retention.newThisMonth') }}</span></div>
        <div class="stat"><span class="stat-value">{{ retention.churnedThisMonth }}</span><span class="stat-label">{{ t('dashboards.retention.churnedThisMonth') }}</span></div>
        <div class="stat"><span class="stat-value" :class="retention.netChange >= 0 ? 'good-text' : 'bad-text'">{{ retention.netChange >= 0 ? '+' : '' }}{{ retention.netChange }}</span><span class="stat-label">{{ t('dashboards.retention.netChange') }}</span></div>
        <div class="stat"><span class="stat-value">{{ retention.monthlyChurnRatePct }}%</span><span class="stat-label">{{ t('dashboards.retention.monthlyChurn') }}</span></div>
        <div class="stat"><span class="stat-value">{{ retention.lapsedCount }}</span><span class="stat-label">{{ t('dashboards.retention.lapsed') }}</span></div>
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
          <h2>{{ t('dashboards.retention.cohortTitle') }}</h2>
          <button class="ghost" @click="exportRetentionCsv">{{ t('dashboards.retention.exportCsv') }}</button>
        </div>
        <p class="hint">{{ t('dashboards.retention.cohortHint') }}</p>
        <table class="cohort-table">
          <thead>
            <tr>
              <th>{{ t('dashboards.retention.cohort') }}</th>
              <th>{{ t('dashboards.retention.size') }}</th>
              <th v-for="(_, i) in retention.cohorts[0]?.retentionByMonth ?? []" :key="i">M{{ i }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in retention.cohorts" :key="c.cohortLabel">
              <td>{{ c.cohortLabel }}</td>
              <td>{{ c.cohortSize }}</td>
              <td v-for="(v, i) in c.retentionByMonth" :key="i" class="cell" :class="retentionCellClass(v)">
                {{ v !== null ? Math.round(v * 100) + '%' : '' }}
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.retention.tenureTitle') }}</h2>
        <p class="hint">{{ t('dashboards.retention.tenureHint') }}</p>
        <div class="histogram">
          <div v-for="b in retention.tenureAtChurnHistogram" :key="b.label" class="bar-row">
            <span class="bar-label">{{ b.label }}</span>
            <div class="bar-track"><div class="bar-fill" :style="{ width: (b.count / Math.max(1, ...retention.tenureAtChurnHistogram.map((x) => x.count))) * 100 + '%' }"></div></div>
            <span class="bar-count">{{ b.count }}</span>
          </div>
        </div>
      </section>
    </template>

    <template v-else-if="tab === 'alertOps' && alertOps">
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ alertOps.slaComplianceRatePct }}%</span><span class="stat-label">{{ t('dashboards.alertOps.slaCompliance') }}</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.avgTimeToClaimHours ?? '—' }}</span><span class="stat-label">{{ t('dashboards.alertOps.avgHoursToClaim') }}</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.saveRatePct }}%</span><span class="stat-label">{{ t('dashboards.alertOps.saveRate') }}</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.treatedReturnRatePct ?? '—' }}%</span><span class="stat-label">{{ t('dashboards.alertOps.treatedReturnRate') }}</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.holdoutReturnRatePct ?? '—' }}%</span><span class="stat-label">{{ t('dashboards.alertOps.holdoutReturnRate') }}</span></div>
      </div>

      <section class="card">
        <h2>{{ t('dashboards.alertOps.volumeTitle') }}</h2>
        <div class="two-col">
          <div>
            <h3>{{ t('dashboards.alertOps.bySeverity') }}</h3>
            <ul class="kv"><li v-for="(v, k) in alertOps.volumeBySeverity" :key="k"><span>{{ severityLabel(k) }}</span><span>{{ v }}</span></li></ul>
          </div>
          <div>
            <h3>{{ t('dashboards.alertOps.byFamily') }}</h3>
            <ul class="kv"><li v-for="(v, k) in alertOps.volumeByFamily" :key="k"><span>{{ k }}</span><span>{{ v }}</span></li></ul>
          </div>
        </div>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.alertOps.outcomesTitle') }}</h2>
        <ul class="kv"><li v-for="(v, k) in alertOps.outcomesMix" :key="k"><span>{{ k.replaceAll('_', ' ') }}</span><span>{{ v }}</span></li></ul>
        <p v-if="Object.keys(alertOps.outcomesMix).length === 0" class="empty">{{ t('dashboards.alertOps.empty') }}</p>
      </section>
    </template>

    <template v-else-if="tab === 'workouts' && workouts">
      <section class="card">
        <h2>{{ t('dashboards.workouts.heatmapTitle') }}</h2>
        <p class="hint">{{ t('dashboards.workouts.windowsHint') }}</p>
        <table>
          <thead><tr><th>{{ t('dashboards.workouts.day') }}</th><th>{{ t('dashboards.workouts.window') }}</th><th>{{ t('dashboards.workouts.type') }}</th><th>{{ t('dashboards.workouts.visits') }}</th></tr></thead>
          <tbody>
            <tr v-for="(c, i) in workouts.windowTypeHeatmap" :key="i"><td>{{ c.day }}</td><td>{{ c.window }}</td><td>{{ c.type }}</td><td>{{ c.count }}</td></tr>
            <tr v-if="workouts.windowTypeHeatmap.length === 0"><td colspan="4" class="empty">{{ t('dashboards.workouts.empty') }}</td></tr>
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.workouts.fillTitle') }}</h2>
        <table>
          <thead><tr><th>{{ t('dashboards.workouts.classType') }}</th><th>{{ t('dashboards.workouts.window') }}</th><th>{{ t('dashboards.workouts.avgFill') }}</th></tr></thead>
          <tbody>
            <tr v-for="(c, i) in workouts.classFillBySlot" :key="i"><td>{{ c.classType }}</td><td>{{ c.window }}</td><td>{{ c.avgFillPct }}%</td></tr>
            <tr v-if="workouts.classFillBySlot.length === 0"><td colspan="3" class="empty">{{ t('dashboards.workouts.fillEmpty') }}</td></tr>
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.workouts.recentTitle') }}</h2>
        <p class="hint">{{ t('dashboards.workouts.recentHint') }}</p>
        <table>
          <thead>
            <tr>
              <th>{{ t('dashboards.workouts.date') }}</th>
              <th>{{ t('dashboards.workouts.classType') }}</th>
              <th>{{ t('dashboards.workouts.workout') }}</th>
              <th>{{ t('dashboards.workouts.coach') }}</th>
              <th>{{ t('dashboards.workouts.attended') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(s, i) in workouts.recentSessions" :key="i">
              <td>{{ new Date(s.date).toLocaleDateString() }}</td>
              <td>{{ s.classType }}</td>
              <td>{{ s.workoutTitle ?? '—' }}</td>
              <td>{{ s.coachName ?? '—' }}</td>
              <td>{{ s.attendedCount }}</td>
            </tr>
            <tr v-if="workouts.recentSessions.length === 0"><td colspan="5" class="empty">{{ t('dashboards.workouts.recentEmpty') }}</td></tr>
          </tbody>
        </table>
      </section>
    </template>

    <template v-else-if="tab === 'myWeek' && myWeek">
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ myWeek.openAlerts.length }}</span><span class="stat-label">{{ t('dashboards.myWeek.openCoach') }}</span></div>
        <div class="stat"><span class="stat-value">{{ myWeek.dueThisWeekCount }}</span><span class="stat-label">{{ t('dashboards.myWeek.dueThisWeek') }}</span></div>
        <div class="stat"><span class="stat-value">{{ myWeek.resolvedThisWeekCount }}</span><span class="stat-label">{{ t('dashboards.myWeek.resolvedThisWeek') }}</span></div>
      </div>
      <section class="card">
        <h2>{{ t('dashboards.myWeek.openAlertsTitle') }}</h2>
        <table>
          <thead><tr><th>{{ t('alerts.severity') }}</th><th>{{ t('alerts.member') }}</th><th>{{ t('alerts.status') }}</th><th>{{ t('dashboards.myWeek.due') }}</th></tr></thead>
          <tbody>
            <tr v-for="a in myWeek.openAlerts" :key="a.id">
              <td><span class="badge" :class="a.severity.toLowerCase()">{{ severityLabel(a.severity) }}</span></td>
              <td><RouterLink :to="`/members/${a.memberId}`">{{ a.memberName }}</RouterLink></td>
              <td>{{ a.status }}</td>
              <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
            </tr>
            <tr v-if="myWeek.openAlerts.length === 0"><td colspan="4" class="empty">{{ t('dashboards.myWeek.empty') }}</td></tr>
          </tbody>
        </table>
      </section>
    </template>
  </div>
</template>

<style scoped>
.tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.25rem;
}
.coach-filter {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
.stat-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1rem;
}
.stat {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.75rem 1.1rem;
  display: flex;
  flex-direction: column;
  min-width: 130px;
}
.stat-value {
  font-size: 1.5rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.stat-label {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}
.good-text {
  color: var(--color-success);
}
.bad-text {
  color: var(--color-danger);
}
.card {
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin: 0.1rem 0 0.75rem;
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
.histogram {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.bar-row {
  display: grid;
  grid-template-columns: 70px 1fr 40px;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.8rem;
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
}
.bar-count {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.two-col {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}
.two-col h3 {
  font-size: 0.85rem;
  margin-bottom: 0.4rem;
  color: var(--color-text-muted);
}
.kv {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.kv li {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
  padding: 0.2rem 0;
  border-bottom: 1px solid var(--color-border);
  text-transform: capitalize;
}
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: var(--color-bg-soft);
  color: var(--color-text-muted);
}
.badge.red {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.badge.amber {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}
.badge.info {
  background: var(--color-info-soft);
  color: var(--color-info);
}
.empty {
  color: var(--color-text-faint);
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
