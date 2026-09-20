<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { api } from '@/lib/api'

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

const TABS = ['Retention', 'Alert ops', 'Workouts', 'My week'] as const
const tab = ref<(typeof TABS)[number]>('Retention')
const loading = ref(true)

const retention = ref<RetentionOverview | null>(null)
const alertOps = ref<AlertOperations | null>(null)
const workouts = ref<WorkoutMix | null>(null)
const myWeek = ref<MyWeek | null>(null)

async function loadTab() {
  loading.value = true
  try {
    if (tab.value === 'Retention' && !retention.value) retention.value = await api.get('/dashboards/retention')
    if (tab.value === 'Alert ops' && !alertOps.value) alertOps.value = await api.get('/dashboards/alerts')
    if (tab.value === 'Workouts' && !workouts.value) workouts.value = await api.get('/dashboards/workouts')
    if (tab.value === 'My week' && !myWeek.value) myWeek.value = await api.get('/dashboards/my-week')
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
    <h1>Dashboards</h1>
    <div class="tabs">
      <button v-for="t in TABS" :key="t" :class="{ ghost: tab !== t }" @click="tab = t">{{ t }}</button>
    </div>

    <p v-if="loading">Loading…</p>

    <template v-else-if="tab === 'Retention' && retention">
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ retention.activeCount }}</span><span class="stat-label">Active</span></div>
        <div class="stat"><span class="stat-value">{{ retention.newThisMonth }}</span><span class="stat-label">New this month</span></div>
        <div class="stat"><span class="stat-value">{{ retention.churnedThisMonth }}</span><span class="stat-label">Churned this month</span></div>
        <div class="stat"><span class="stat-value" :class="retention.netChange >= 0 ? 'good-text' : 'bad-text'">{{ retention.netChange >= 0 ? '+' : '' }}{{ retention.netChange }}</span><span class="stat-label">Net change</span></div>
        <div class="stat"><span class="stat-value">{{ retention.monthlyChurnRatePct }}%</span><span class="stat-label">Monthly churn</span></div>
        <div class="stat"><span class="stat-value">{{ retention.lapsedCount }}</span><span class="stat-label">Lapsed (≥45d, still active)</span></div>
      </div>

      <section class="card">
        <div class="section-header">
          <h2>Cohort retention</h2>
          <button class="ghost" @click="exportRetentionCsv">Export CSV</button>
        </div>
        <p class="hint">% of each join-month cohort still active, by months since joining.</p>
        <table class="cohort-table">
          <thead>
            <tr>
              <th>Cohort</th>
              <th>Size</th>
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
        <h2>Tenure at churn</h2>
        <p class="hint">How long members stuck around before cancelling.</p>
        <div class="histogram">
          <div v-for="b in retention.tenureAtChurnHistogram" :key="b.label" class="bar-row">
            <span class="bar-label">{{ b.label }}</span>
            <div class="bar-track"><div class="bar-fill" :style="{ width: (b.count / Math.max(1, ...retention.tenureAtChurnHistogram.map((x) => x.count))) * 100 + '%' }"></div></div>
            <span class="bar-count">{{ b.count }}</span>
          </div>
        </div>
      </section>
    </template>

    <template v-else-if="tab === 'Alert ops' && alertOps">
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ alertOps.slaComplianceRatePct }}%</span><span class="stat-label">SLA compliance</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.avgTimeToClaimHours ?? '—' }}</span><span class="stat-label">Avg hours to claim</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.saveRatePct }}%</span><span class="stat-label">Save rate</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.treatedReturnRatePct ?? '—' }}%</span><span class="stat-label">Treated return rate</span></div>
        <div class="stat"><span class="stat-value">{{ alertOps.holdoutReturnRatePct ?? '—' }}%</span><span class="stat-label">Holdout return rate</span></div>
      </div>

      <section class="card">
        <h2>Volume</h2>
        <div class="two-col">
          <div>
            <h3>By severity</h3>
            <ul class="kv"><li v-for="(v, k) in alertOps.volumeBySeverity" :key="k"><span>{{ k }}</span><span>{{ v }}</span></li></ul>
          </div>
          <div>
            <h3>By family</h3>
            <ul class="kv"><li v-for="(v, k) in alertOps.volumeByFamily" :key="k"><span>{{ k }}</span><span>{{ v }}</span></li></ul>
          </div>
        </div>
      </section>

      <section class="card">
        <h2>Outcomes mix</h2>
        <ul class="kv"><li v-for="(v, k) in alertOps.outcomesMix" :key="k"><span>{{ k.replaceAll('_', ' ') }}</span><span>{{ v }}</span></li></ul>
        <p v-if="Object.keys(alertOps.outcomesMix).length === 0" class="empty">No resolved alerts yet.</p>
      </section>
    </template>

    <template v-else-if="tab === 'Workouts' && workouts">
      <section class="card">
        <h2>Window × type (last 12 weeks)</h2>
        <table>
          <thead><tr><th>Window</th><th>Type</th><th>Visits</th></tr></thead>
          <tbody>
            <tr v-for="(c, i) in workouts.windowTypeHeatmap" :key="i"><td>{{ c.window }}</td><td>{{ c.type }}</td><td>{{ c.count }}</td></tr>
            <tr v-if="workouts.windowTypeHeatmap.length === 0"><td colspan="3" class="empty">No attended visits in the last 12 weeks.</td></tr>
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>Class fill by slot</h2>
        <table>
          <thead><tr><th>Class type</th><th>Window</th><th>Avg fill</th></tr></thead>
          <tbody>
            <tr v-for="(c, i) in workouts.classFillBySlot" :key="i"><td>{{ c.classType }}</td><td>{{ c.window }}</td><td>{{ c.avgFillPct }}%</td></tr>
            <tr v-if="workouts.classFillBySlot.length === 0"><td colspan="3" class="empty">No capacity data in the last 12 weeks.</td></tr>
          </tbody>
        </table>
      </section>
    </template>

    <template v-else-if="tab === 'My week' && myWeek">
      <div class="stat-row">
        <div class="stat"><span class="stat-value">{{ myWeek.openAlerts.length }}</span><span class="stat-label">Open (Coach)</span></div>
        <div class="stat"><span class="stat-value">{{ myWeek.dueThisWeekCount }}</span><span class="stat-label">Due this week</span></div>
        <div class="stat"><span class="stat-value">{{ myWeek.resolvedThisWeekCount }}</span><span class="stat-label">Resolved this week</span></div>
      </div>
      <section class="card">
        <h2>Open alerts</h2>
        <table>
          <thead><tr><th>Severity</th><th>Member</th><th>Status</th><th>Due</th></tr></thead>
          <tbody>
            <tr v-for="a in myWeek.openAlerts" :key="a.id">
              <td><span class="badge" :class="a.severity.toLowerCase()">{{ a.severity }}</span></td>
              <td><RouterLink :to="`/members/${a.memberId}`">{{ a.memberName }}</RouterLink></td>
              <td>{{ a.status }}</td>
              <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
            </tr>
            <tr v-if="myWeek.openAlerts.length === 0"><td colspan="4" class="empty">Nothing open — nice week.</td></tr>
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
