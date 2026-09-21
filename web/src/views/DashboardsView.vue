<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import ExplainThis from '@/components/ExplainThis.vue'

const { t } = useI18n()

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
interface WorkoutMix {
  windowTypeHeatmap: HeatmapCell[]
  classFillBySlot: ClassFill[]
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

const TABS = ['retention', 'alertOps', 'workouts', 'myWeek'] as const
const tab = ref<(typeof TABS)[number]>('retention')
const loading = ref(true)

const retention = ref<RetentionOverview | null>(null)
const alertOps = ref<AlertOperations | null>(null)
const workouts = ref<WorkoutMix | null>(null)
const myWeek = ref<MyWeek | null>(null)

async function loadTab() {
  loading.value = true
  try {
    if (tab.value === 'retention' && !retention.value) retention.value = await api.get('/dashboards/retention')
    if (tab.value === 'alertOps' && !alertOps.value) alertOps.value = await api.get('/dashboards/alerts')
    if (tab.value === 'workouts' && !workouts.value) workouts.value = await api.get('/dashboards/workouts')
    if (tab.value === 'myWeek' && !myWeek.value) myWeek.value = await api.get('/dashboards/my-week')
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

watch(tab, loadTab)
onMounted(loadTab)
</script>

<template>
  <div>
    <h1>{{ t('dashboards.title') }}</h1>
    <div class="tabs">
      <button v-for="tabId in TABS" :key="tabId" :class="{ ghost: tab !== tabId }" @click="tab = tabId">{{ t(`dashboards.tabs.${tabId}`) }}</button>
    </div>

    <p v-if="loading">Loading…</p>

    <template v-else-if="tab === 'retention' && retention">
      <div class="stat-row">
        <div class="stat">
          <span class="stat-value">{{ retention.activeCount }}</span>
          <span class="stat-label">{{ t('dashboards.retention.active') }}<ExplainThis metric-key="active_members" /></span>
          <span class="stat-period">{{ t('dashboards.periods.asOfToday') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ retention.newThisMonth }}</span>
          <span class="stat-label">{{ t('dashboards.retention.newThisMonth') }}<ExplainThis metric-key="new_members_this_month" /></span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ retention.churnedThisMonth }}</span>
          <span class="stat-label">{{ t('dashboards.retention.churnedThisMonth') }}<ExplainThis metric-key="churned_members_this_month" /></span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value" :class="retention.netChange >= 0 ? 'good-text' : 'bad-text'">{{ retention.netChange >= 0 ? '+' : '' }}{{ retention.netChange }}</span>
          <span class="stat-label">{{ t('dashboards.retention.netChange') }}<ExplainThis metric-key="net_member_change" /></span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ retention.monthlyChurnRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.retention.monthlyChurn') }}<ExplainThis metric-key="monthly_churn_rate_pct" /></span>
          <span class="stat-period">{{ t('dashboards.periods.thisMonth') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ retention.lapsedCount }}</span>
          <span class="stat-label">{{ t('dashboards.retention.lapsed') }}<ExplainThis metric-key="lapsed_members" /></span>
          <span class="stat-period">{{ t('dashboards.periods.asOfToday') }}</span>
        </div>
      </div>

      <section class="card">
        <div class="section-header">
          <h2>{{ t('dashboards.retention.cohortTitle') }}<ExplainThis metric-key="cohort_retention_curve" /></h2>
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
        <h2>{{ t('dashboards.retention.tenureTitle') }}<ExplainThis metric-key="tenure_at_churn_histogram" /></h2>
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
        <div class="stat">
          <span class="stat-value">{{ alertOps.slaComplianceRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.slaCompliance') }}<ExplainThis metric-key="alert_sla_compliance_rate_pct" /></span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ alertOps.avgTimeToClaimHours ?? '—' }}</span>
          <span class="stat-label">{{ t('dashboards.alertOps.avgHoursToClaim') }}<ExplainThis metric-key="alert_avg_time_to_claim_hours" /></span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ alertOps.saveRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.saveRate') }}<ExplainThis metric-key="alert_save_rate_pct" /></span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ alertOps.treatedReturnRatePct ?? '—' }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.treatedReturnRate') }}<ExplainThis metric-key="alert_return_rate_holdout_vs_treated_pct" /></span>
          <span class="stat-period">{{ t('dashboards.periods.next14Days') }}</span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ alertOps.holdoutReturnRatePct ?? '—' }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.holdoutReturnRate') }}<ExplainThis metric-key="alert_return_rate_holdout_vs_treated_pct" /></span>
          <span class="stat-period">{{ t('dashboards.periods.next14Days') }}</span>
        </div>
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
  display: flex;
  align-items: center;
}
.stat-period {
  font-size: 0.68rem;
  color: var(--color-text-faint);
  margin-top: 0.1rem;
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
</style>
