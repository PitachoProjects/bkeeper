<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import { useAuthStore } from '@/stores/auth'
import RadarChart from '@/components/RadarChart.vue'
import ScoreRing from '@/components/ScoreRing.vue'

interface OpenAlertItem {
  id: string
  family: string
  severity: string
  status: string
  dueAt: string
  ruleCodes: string[]
}

interface RuleCatalogItem {
  code: string
  description: string
}

interface MemberDetail {
  id: string
  name: string
  email: string | null
  phone: string | null
  status: string
  joinDate: string
  awayUntil: string | null
  injuryFlagUntil: string | null
}

interface MemberNote {
  id: string
  text: string
  source: string
  isActive: boolean
  createdAt: string
}

interface TimelineItem {
  type: string
  at: string
  summary: string
}

interface Consent {
  channel: string
  granted: boolean
}

interface OutreachItem {
  id: string
  channel: string
  templateKey: string | null
  body: string
  sentBy: string
  status: string
  isHoldout: boolean
  createdAt: string
}

interface GoalProgressItem {
  id: string
  date: string
  value: number
  source: string
}

interface GoalItem {
  id: string
  category: string
  description: string
  metric: string
  baselineValue: number | null
  targetValue: number | null
  unit: string | null
  targetDate: string | null
  status: string
  progress: GoalProgressItem[]
}

interface FormSummary {
  key: string
  cadence: string
  questionCount: number
}

interface RiskScore {
  snapshotWeek: string
  pChurn28d: number
  band: string
  topReasons: string[]
  modelVersion: string
}

interface HealthScoreFactor {
  factor: string
  score: number | null
  weight: number
  contribution: number
  included: boolean
  reason: string | null
}

interface HealthScoreItem {
  memberId: string
  calculationDate: string
  configVersion: number
  overallScore: number | null
  insufficientData: boolean
  tenureDays: number
  sessionCount: number
  factors: HealthScoreFactor[]
  calculatedAt: string
}

const FACTOR_ORDER = ['Attendance', 'Consistency', 'BookingBehaviour', 'Progress', 'Engagement']

interface WorkoutMixItem {
  tag: string
  count: number
}

interface PaymentItem {
  id: string
  memberId: string
  membershipId: string | null
  amount: number
  currency: string
  paymentDate: string
  status: string
  method: string
  notes: string | null
}

const { t } = useI18n()
const GOAL_CATEGORIES = ['Strength', 'Skill', 'BodyComposition', 'Endurance', 'CompetitionEvent', 'HealthRehab', 'Consistency', 'Social']
const TIMELINE_FILTERS = ['note', 'Attended', 'NoShow', 'LateCancel']
const PAYMENT_STATUSES = ['Completed', 'Refunded', 'Failed']
const PAYMENT_METHODS = ['Card', 'Cash', 'Transfer', 'Other']

const route = useRoute()
const memberId = route.params.id as string
const auth = useAuthStore()
const canSeeRisk = computed(() => auth.role === 'Manager' || auth.role === 'Owner')
const canSeePayments = computed(() => auth.role === 'Owner' || auth.role === 'Manager' || auth.role === 'Reception')
const canManageTriggers = computed(() => auth.role === 'Manager' || auth.role === 'Owner')

const openAlerts = ref<OpenAlertItem[]>([])
const worstOpenSeverity = computed(() => {
  if (openAlerts.value.some((a) => a.severity === 'Red')) return 'red'
  if (openAlerts.value.some((a) => a.severity === 'Amber')) return 'amber'
  return openAlerts.value.length > 0 ? 'info' : ''
})
const recomputingHealthScore = ref(false)
const rerunningRules = ref(false)
const triggerMessage = ref('')
const ruleDescriptions = ref<Record<string, string>>({})

const member = ref<MemberDetail | null>(null)
const riskScore = ref<RiskScore | null>(null)
const healthScore = ref<HealthScoreItem | null>(null)
const healthScoreHistory = ref<HealthScoreItem[]>([])
const orderedFactors = computed(() =>
  healthScore.value ? [...healthScore.value.factors].sort((a, b) => FACTOR_ORDER.indexOf(a.factor) - FACTOR_ORDER.indexOf(b.factor)) : [],
)
const healthScoreBand = computed(() => {
  const score = healthScore.value?.overallScore
  if (score === null || score === undefined) return ''
  if (score >= 75) return 'green'
  if (score >= 50) return 'amber'
  return 'red'
})
const notes = ref<MemberNote[]>([])
const timeline = ref<TimelineItem[]>([])
const consent = ref<Consent[]>([])
const outreach = ref<OutreachItem[]>([])
const goals = ref<GoalItem[]>([])
const forms = ref<FormSummary[]>([])
const workoutMix = ref<WorkoutMixItem[]>([])
const workoutMixHasData = computed(() => workoutMix.value.some((w) => w.count > 0))
const newNote = ref('')
const savingNote = ref(false)
const showAddGoal = ref(false)
const newGoal = ref({ category: 'Consistency', description: '', metric: '', baselineValue: '', targetValue: '', unit: '', targetDate: '' })
const progressGoalId = ref<string | null>(null)
const progressValue = ref('')
const progressDate = ref(new Date().toISOString().slice(0, 10))
const sentFormLink = ref('')
const payments = ref<PaymentItem[]>([])
const showAddPayment = ref(false)
const newPayment = ref({ amount: '', currency: 'EUR', paymentDate: new Date().toISOString().slice(0, 10), status: 'Completed', method: 'Card', notes: '' })
const timelineFilter = ref('')
const timelineFiltered = computed(() =>
  timelineFilter.value ? timeline.value.filter((t) => t.type === timelineFilter.value) : timeline.value,
)
const TIMELINE_PAGE_SIZE = 10
const timelinePage = ref(1)
const timelineTotalPages = computed(() => Math.max(1, Math.ceil(timelineFiltered.value.length / TIMELINE_PAGE_SIZE)))
const timelinePageItems = computed(() =>
  timelineFiltered.value.slice((timelinePage.value - 1) * TIMELINE_PAGE_SIZE, timelinePage.value * TIMELINE_PAGE_SIZE),
)
function setTimelineFilter(f: string) {
  timelineFilter.value = f
  timelinePage.value = 1
}

async function load() {
  ;[member.value, notes.value, timeline.value, consent.value, outreach.value, goals.value, forms.value, workoutMix.value, openAlerts.value] = await Promise.all([
    api.get<MemberDetail>(`/members/${memberId}`),
    api.get<MemberNote[]>(`/members/${memberId}/notes`),
    api.get<TimelineItem[]>(`/members/${memberId}/timeline`),
    api.get<Consent[]>(`/members/${memberId}/consent`),
    api.get<OutreachItem[]>(`/members/${memberId}/outreach`),
    api.get<GoalItem[]>(`/members/${memberId}/goals`),
    api.get<FormSummary[]>('/forms'),
    api.get<WorkoutMixItem[]>(`/members/${memberId}/workout-mix`),
    api.get<OpenAlertItem[]>(`/alerts?memberId=${memberId}`),
  ])

  if (canSeeRisk.value) {
    riskScore.value = await api.get<RiskScore | null>(`/risk-scores/members/${memberId}`)
  }

  ;[healthScore.value, healthScoreHistory.value] = await Promise.all([
    api.get<HealthScoreItem | null>(`/members/${memberId}/health-score`),
    api.get<HealthScoreItem[]>(`/members/${memberId}/health-score/history`),
  ])
  if (canSeePayments.value) {
    payments.value = await api.get<PaymentItem[]>(`/members/${memberId}/payments`)
  }
}

async function addPayment() {
  if (!newPayment.value.amount || Number(newPayment.value.amount) <= 0) return
  const payment = await api.post<PaymentItem>(`/members/${memberId}/payments`, {
    membershipId: null,
    amount: Number(newPayment.value.amount),
    currency: newPayment.value.currency || 'EUR',
    paymentDate: newPayment.value.paymentDate,
    status: newPayment.value.status,
    method: newPayment.value.method,
    notes: newPayment.value.notes || null,
  })
  payments.value = [payment, ...payments.value]
  newPayment.value = { amount: '', currency: 'EUR', paymentDate: new Date().toISOString().slice(0, 10), status: 'Completed', method: 'Card', notes: '' }
  showAddPayment.value = false
}

async function addGoal() {
  if (!newGoal.value.description.trim() || !newGoal.value.metric.trim()) return
  const goal = await api.post<GoalItem>(`/members/${memberId}/goals`, {
    category: newGoal.value.category,
    description: newGoal.value.description.trim(),
    metric: newGoal.value.metric.trim(),
    baselineValue: newGoal.value.baselineValue ? Number(newGoal.value.baselineValue) : null,
    targetValue: newGoal.value.targetValue ? Number(newGoal.value.targetValue) : null,
    unit: newGoal.value.unit || null,
    targetDate: newGoal.value.targetDate || null,
  })
  goals.value = [goal, ...goals.value]
  newGoal.value = { category: 'Consistency', description: '', metric: '', baselineValue: '', targetValue: '', unit: '', targetDate: '' }
  showAddGoal.value = false
}

function startProgress(goalId: string) {
  progressGoalId.value = goalId
  progressValue.value = ''
  progressDate.value = new Date().toISOString().slice(0, 10)
}

async function confirmProgress() {
  if (!progressGoalId.value || progressValue.value === '') return
  await api.post(`/goals/${progressGoalId.value}/progress`, { date: progressDate.value, value: Number(progressValue.value) })
  progressGoalId.value = null
  await load()
}

async function sendForm(key: string) {
  sentFormLink.value = ''
  const result = await api.post<{ token: string; relativeLink: string; expiresAt: string }>(`/forms/${key}/send?memberId=${memberId}`)
  sentFormLink.value = result.relativeLink
  await load()
}

async function toggleConsent(c: Consent) {
  const next = !c.granted
  await api.put(`/members/${memberId}/consent`, { channel: c.channel, granted: next })
  c.granted = next
}

async function addNote() {
  if (!newNote.value.trim()) return
  savingNote.value = true
  try {
    const note = await api.post<MemberNote>(`/members/${memberId}/notes`, { text: newNote.value.trim() })
    notes.value = [note, ...notes.value]
    newNote.value = ''
  } finally {
    savingNote.value = false
  }
}

async function removeNote(noteId: string) {
  await api.delete(`/members/${memberId}/notes/${noteId}`)
  notes.value = notes.value.filter((n) => n.id !== noteId)
}

async function recomputeHealthScore() {
  recomputingHealthScore.value = true
  triggerMessage.value = ''
  try {
    await api.post(`/health-score/run?memberId=${memberId}`)
    await load()
    triggerMessage.value = t('memberDetail.manualTriggers.done')
  } finally {
    recomputingHealthScore.value = false
  }
}

async function rerunRules() {
  rerunningRules.value = true
  triggerMessage.value = ''
  try {
    await api.post(`/rules/run?memberId=${memberId}`)
    await load()
    triggerMessage.value = t('memberDetail.manualTriggers.done')
  } finally {
    rerunningRules.value = false
  }
}

onMounted(async () => {
  const rules = await api.get<RuleCatalogItem[]>('/rules')
  ruleDescriptions.value = Object.fromEntries(rules.map((r) => [r.code, r.description]))
  await load()
})
</script>

<template>
  <div v-if="member">
    <h1>{{ member.name }}</h1>
    <p class="meta">{{ member.email }} · {{ member.phone }} · {{ t('memberDetail.joined') }} {{ member.joinDate }}</p>

    <div v-if="openAlerts.length > 0" class="alert-banner" :class="worstOpenSeverity">
      <div class="alert-banner-header">
        <span class="badge" :class="worstOpenSeverity">{{ openAlerts.length }}</span>
        <span>{{ t('memberDetail.openAlerts.title') }}</span>
        <RouterLink to="/alerts" class="view-all">{{ t('memberDetail.openAlerts.viewAll') }}</RouterLink>
      </div>
      <ul class="alert-banner-list">
        <li v-for="a in openAlerts" :key="a.id">
          <span class="badge" :class="a.severity.toLowerCase()">{{ severityLabel(a.severity) }}</span>
          <span class="family">{{ a.family }}</span>
          <span v-for="code in a.ruleCodes" :key="code" class="badge rule-chip" :title="ruleDescriptions[code] ?? code">{{ code }}</span>
          <span class="date">{{ t('alerts.due') }}: {{ new Date(a.dueAt).toLocaleString() }}</span>
        </li>
      </ul>
    </div>

    <div v-if="canManageTriggers" class="manual-triggers">
      <button class="ghost" :disabled="recomputingHealthScore" :title="t('memberDetail.manualTriggers.recomputeHealthScoreHint')" @click="recomputeHealthScore">
        {{ recomputingHealthScore ? t('memberDetail.manualTriggers.running') : t('memberDetail.manualTriggers.recomputeHealthScore') }}
      </button>
      <button class="ghost" :disabled="rerunningRules" :title="t('memberDetail.manualTriggers.rerunRulesHint')" @click="rerunRules">
        {{ rerunningRules ? t('memberDetail.manualTriggers.running') : t('memberDetail.manualTriggers.rerunRules') }}
      </button>
      <span v-if="triggerMessage" class="hint">{{ triggerMessage }}</span>
    </div>

    <h2 class="group-title">{{ t('memberDetail.groups.healthAndRisk') }}</h2>

    <section v-if="canSeeRisk" class="card">
      <h2>{{ t('memberDetail.risk.title') }} <span class="shadow-tag">{{ t('memberDetail.risk.shadowMode') }}</span></h2>
      <p class="hint">{{ t('memberDetail.risk.hint') }}</p>
      <div v-if="riskScore" class="risk-row">
        <span class="badge" :class="riskScore.band">{{ t(`memberDetail.risk.bands.${riskScore.band}`) }}</span>
        <span class="text">{{ (riskScore.pChurn28d * 100).toFixed(1) }}% {{ t('memberDetail.risk.chance') }}</span>
        <span class="date">{{ t('memberDetail.risk.weekOf') }} {{ riskScore.snapshotWeek }} · {{ t('memberDetail.risk.model') }} {{ riskScore.modelVersion }}</span>
      </div>
      <ul v-if="riskScore && riskScore.topReasons.length" class="reasons">
        <li v-for="(r, i) in riskScore.topReasons" :key="i">{{ r }}</li>
      </ul>
      <p v-if="riskScore && riskScore.topReasons.length" class="hint reasons-hint">{{ t('memberDetail.risk.reasonsHint') }}</p>
      <p v-if="!riskScore" class="empty">{{ t('memberDetail.risk.empty') }}</p>
    </section>

    <section class="card">
      <h2>{{ t('memberDetail.healthScore.title') }}</h2>
      <p class="hint">{{ t('memberDetail.healthScore.hint') }}</p>

      <template v-if="healthScore">
        <template v-if="healthScore.insufficientData">
          <p class="insufficient">
            <strong>{{ t('memberDetail.healthScore.insufficientTitle') }}</strong><br />
            {{ t('memberDetail.healthScore.insufficientHint') }}
          </p>
          <p class="hint">
            {{ t('memberDetail.healthScore.tenureDays') }}: {{ healthScore.tenureDays }} {{ t('memberDetail.healthScore.tenureDaysUnit') }}
            · {{ t('memberDetail.healthScore.sessionCount') }}: {{ healthScore.sessionCount }}
          </p>
        </template>
        <template v-else>
          <div class="score-header">
            <ScoreRing :value="healthScore.overallScore ?? 0" :color-class="healthScoreBand" />
            <div class="score-meta">
              <span class="badge" :class="healthScoreBand">{{ t(`memberDetail.healthScore.bands.${healthScoreBand}`) }}</span>
              <span class="date">{{ t('memberDetail.healthScore.asOf') }} {{ healthScore.calculationDate }}</span>

              <div v-if="healthScoreHistory.length > 1" class="trend">
                <span class="trend-label">{{ t('memberDetail.healthScore.trend') }}</span>
                <div class="sparkline">
                  <div
                    v-for="h in healthScoreHistory"
                    :key="h.calculationDate"
                    class="bar"
                    :class="{ empty: h.overallScore === null }"
                    :style="{ height: `${Math.max(4, h.overallScore ?? 4)}%` }"
                    :title="`${h.calculationDate}: ${h.overallScore ?? '—'}`"
                  />
                </div>
              </div>
            </div>
          </div>

          <h3 class="breakdown-title">{{ t('memberDetail.healthScore.howCalculated') }}</h3>
          <table class="factor-table">
            <thead>
              <tr>
                <th>{{ t('memberDetail.healthScore.factor') }}</th>
                <th>{{ t('memberDetail.healthScore.weight') }}</th>
                <th>{{ t('memberDetail.healthScore.score') }}</th>
                <th>{{ t('memberDetail.healthScore.contribution') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="f in orderedFactors" :key="f.factor">
                <td>{{ t(`memberDetail.healthScore.factors.${f.factor}`) }}</td>
                <td>{{ f.included ? `${f.weight.toFixed(0)}%` : '—' }}</td>
                <td>{{ f.score !== null ? f.score.toFixed(0) : '—' }}</td>
                <td v-if="f.included">+{{ f.contribution.toFixed(1) }}</td>
                <td v-else class="empty">
                  {{ t('memberDetail.healthScore.notIncluded') }}
                  <template v-if="f.reason">({{ t(`memberDetail.healthScore.reasons.${f.reason}`) }})</template>
                </td>
              </tr>
            </tbody>
          </table>
        </template>
      </template>
      <p v-else class="empty">{{ t('memberDetail.healthScore.empty') }}</p>
    </section>

    <section class="card">
      <h2>{{ t('memberDetail.workoutMix.title') }}</h2>
      <p class="hint">{{ t('memberDetail.workoutMix.hint') }}</p>
      <RadarChart v-if="workoutMixHasData" :points="workoutMix.map((w) => ({ label: w.tag, value: w.count }))" />
      <p v-else class="empty">{{ t('memberDetail.workoutMix.empty') }}</p>
    </section>

    <h2 class="group-title">{{ t('memberDetail.groups.goals') }}</h2>

    <section class="card">
      <div class="section-header">
        <h2>{{ t('memberDetail.goals.title') }}</h2>
        <button class="ghost" @click="showAddGoal = !showAddGoal">{{ showAddGoal ? t('memberDetail.goals.cancel') : t('memberDetail.goals.addGoal') }}</button>
      </div>

      <form v-if="showAddGoal" class="add-goal" @submit.prevent="addGoal">
        <select v-model="newGoal.category">
          <option v-for="c in GOAL_CATEGORIES" :key="c" :value="c">{{ t(`memberDetail.goals.categories.${c}`) }}</option>
        </select>
        <input v-model="newGoal.description" :placeholder="t('memberDetail.goals.descriptionPlaceholder')" />
        <input v-model="newGoal.metric" :placeholder="t('memberDetail.goals.metricPlaceholder')" />
        <input v-model="newGoal.baselineValue" type="number" :placeholder="t('memberDetail.goals.baselinePlaceholder')" />
        <input v-model="newGoal.targetValue" type="number" :placeholder="t('memberDetail.goals.targetPlaceholder')" />
        <input v-model="newGoal.unit" :placeholder="t('memberDetail.goals.unitPlaceholder')" />
        <input v-model="newGoal.targetDate" type="date" />
        <button type="submit">{{ t('memberDetail.goals.save') }}</button>
      </form>

      <ul class="goals">
        <li v-for="g in goals" :key="g.id">
          <div class="goal-row">
            <span class="source">{{ t(`memberDetail.goals.categories.${g.category}`) }}</span>
            <span class="text">{{ g.description }}</span>
            <span class="status">{{ g.status }}</span>
            <span class="date">
              {{ g.progress.at(-1) ? `${g.progress.at(-1)!.value} ${g.unit ?? ''}` : t('memberDetail.goals.noProgress') }}
              <template v-if="g.targetValue !== null"> / {{ g.targetValue }} {{ g.unit }}</template>
            </span>
            <button class="ghost" @click="startProgress(g.id)">{{ t('memberDetail.goals.addProgress') }}</button>
          </div>
          <div v-if="progressGoalId === g.id" class="add-progress">
            <input v-model="progressDate" type="date" />
            <input v-model="progressValue" type="number" :placeholder="t('memberDetail.goals.valuePlaceholder')" />
            <button @click="confirmProgress">{{ t('memberDetail.goals.save') }}</button>
          </div>
        </li>
        <li v-if="goals.length === 0" class="empty">{{ t('memberDetail.goals.empty') }}</li>
      </ul>
    </section>

    <h2 class="group-title">{{ t('memberDetail.groups.activity') }}</h2>

    <section class="card">
      <h2>{{ t('memberDetail.notes.title') }}</h2>
      <p class="hint">{{ t('memberDetail.notes.hint') }}</p>
      <form class="add-note" @submit.prevent="addNote">
        <input v-model="newNote" :placeholder="t('memberDetail.notes.placeholder')" />
        <button type="submit" :disabled="savingNote">{{ t('memberDetail.notes.add') }}</button>
      </form>
      <ul class="notes">
        <li v-for="n in notes.filter((n) => n.isActive)" :key="n.id">
          <span class="source" :class="n.source.toLowerCase()">{{ n.source }}</span>
          <span class="text">{{ n.text }}</span>
          <span class="date">{{ new Date(n.createdAt).toLocaleDateString() }}</span>
          <button class="remove" @click="removeNote(n.id)">✕</button>
        </li>
        <li v-if="notes.filter((n) => n.isActive).length === 0" class="empty">{{ t('memberDetail.notes.empty') }}</li>
      </ul>
    </section>

    <section class="card">
      <h2>{{ t('memberDetail.outreach.title') }}</h2>
      <ul class="outreach">
        <li v-for="o in outreach" :key="o.id">
          <span class="source" :class="o.sentBy.toLowerCase()">{{ o.sentBy }}</span>
          <span class="channel">{{ o.channel }}</span>
          <span class="text">{{ o.isHoldout ? t('memberDetail.outreach.holdout') : o.body }}</span>
          <span class="status">{{ o.status }}</span>
          <span class="date">{{ new Date(o.createdAt).toLocaleString() }}</span>
        </li>
        <li v-if="outreach.length === 0" class="empty">{{ t('memberDetail.outreach.empty') }}</li>
      </ul>
    </section>

    <section class="card">
      <div class="section-header">
        <h2>{{ t('memberDetail.timeline.title') }}</h2>
        <div class="timeline-filters">
          <button class="ghost" :class="{ active: timelineFilter === '' }" @click="setTimelineFilter('')">{{ t('memberDetail.timeline.all') }}</button>
          <button v-for="opt in TIMELINE_FILTERS" :key="opt" class="ghost" :class="{ active: timelineFilter === opt }" @click="setTimelineFilter(opt)">
            {{ t(`memberDetail.timeline.filters.${opt}`) }}
          </button>
        </div>
      </div>
      <ul class="timeline">
        <li v-for="(item, i) in timelinePageItems" :key="i">
          <span class="type" :class="item.type.toLowerCase()">{{ item.type }}</span>
          <span>{{ item.summary }}</span>
          <span class="date">{{ new Date(item.at).toLocaleString() }}</span>
        </li>
        <li v-if="timelineFiltered.length === 0" class="empty">{{ t('memberDetail.timeline.empty') }}</li>
      </ul>
      <div v-if="timelineTotalPages > 1" class="pagination">
        <button class="ghost" :disabled="timelinePage === 1" @click="timelinePage--">{{ t('memberDetail.timeline.prev') }}</button>
        <span class="page-indicator">{{ t('memberDetail.timeline.page', { page: timelinePage, total: timelineTotalPages }) }}</span>
        <button class="ghost" :disabled="timelinePage === timelineTotalPages" @click="timelinePage++">{{ t('memberDetail.timeline.next') }}</button>
      </div>
    </section>

    <h2 class="group-title">{{ t('memberDetail.groups.membershipAdmin') }}</h2>

    <section class="card">
      <h2>{{ t('memberDetail.forms.title') }}</h2>
      <p class="hint">{{ t('memberDetail.forms.hint') }}</p>
      <ul class="forms">
        <li v-for="f in forms" :key="f.key">
          <span class="text">{{ f.key }} <span class="date">({{ f.cadence }}, {{ f.questionCount }} questions)</span></span>
          <button class="ghost" @click="sendForm(f.key)">{{ t('memberDetail.forms.send') }}</button>
        </li>
      </ul>
      <p v-if="sentFormLink" class="hint">{{ t('memberDetail.forms.sentLink') }} <code>{{ sentFormLink }}</code></p>
    </section>

    <section class="card">
      <h2>{{ t('memberDetail.consent.title') }}</h2>
      <p class="hint">{{ t('memberDetail.consent.hint') }}</p>
      <ul class="consent">
        <li v-for="c in consent" :key="c.channel">
          <label>
            <input type="checkbox" :checked="c.granted" @change="toggleConsent(c)" />
            {{ c.channel }}
          </label>
        </li>
      </ul>
    </section>

    <section v-if="canSeePayments" class="card">
      <div class="section-header">
        <h2>{{ t('memberDetail.payments.title') }}</h2>
        <button class="ghost" @click="showAddPayment = !showAddPayment">
          {{ showAddPayment ? t('memberDetail.payments.cancel') : t('memberDetail.payments.addPayment') }}
        </button>
      </div>
      <p class="hint">{{ t('memberDetail.payments.hint') }}</p>

      <form v-if="showAddPayment" class="add-payment" @submit.prevent="addPayment">
        <input v-model="newPayment.amount" type="number" step="0.01" min="0" :placeholder="t('memberDetail.payments.amountPlaceholder')" />
        <input v-model="newPayment.currency" :placeholder="t('memberDetail.payments.currencyPlaceholder')" />
        <input v-model="newPayment.paymentDate" type="date" />
        <select v-model="newPayment.method">
          <option v-for="m in PAYMENT_METHODS" :key="m" :value="m">{{ t(`memberDetail.payments.methods.${m}`) }}</option>
        </select>
        <select v-model="newPayment.status">
          <option v-for="s in PAYMENT_STATUSES" :key="s" :value="s">{{ t(`memberDetail.payments.statuses.${s}`) }}</option>
        </select>
        <input v-model="newPayment.notes" :placeholder="t('memberDetail.payments.notesPlaceholder')" />
        <button type="submit">{{ t('memberDetail.payments.save') }}</button>
      </form>

      <ul class="payments">
        <li v-for="p in payments" :key="p.id">
          <span class="text">{{ p.amount.toFixed(2) }} {{ p.currency }}</span>
          <span class="status" :class="p.status.toLowerCase()">{{ t(`memberDetail.payments.statuses.${p.status}`) }}</span>
          <span class="source">{{ t(`memberDetail.payments.methods.${p.method}`) }}</span>
          <span v-if="p.notes" class="text">{{ p.notes }}</span>
          <span class="date">{{ p.paymentDate }}</span>
        </li>
        <li v-if="payments.length === 0" class="empty">{{ t('memberDetail.payments.empty') }}</li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.meta {
  color: var(--color-text-muted);
  margin-top: -0.5rem;
  font-variant-numeric: tabular-nums;
}
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin-top: -0.25rem;
}
.alert-banner {
  padding: 0.6rem 0.9rem;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  font-size: 0.85rem;
  margin-top: 0.75rem;
}
.alert-banner.red {
  background: var(--color-danger-soft);
  border-color: var(--color-danger);
}
.alert-banner.amber {
  background: var(--color-warning-soft);
  border-color: var(--color-warning);
}
.alert-banner-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.alert-banner .view-all {
  margin-left: auto;
  font-weight: 600;
}
.alert-banner-list {
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.alert-banner-list li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.alert-banner-list .family {
  font-weight: 600;
}
.alert-banner-list .rule-chip {
  cursor: help;
  font-variant-numeric: tabular-nums;
}
.manual-triggers {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-top: 0.75rem;
}
.manual-triggers button {
  padding: 0.35rem 0.7rem;
  font-size: 0.8rem;
}
.group-title {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-faint);
  margin: 2rem 0 0;
}
.group-title:first-child {
  margin-top: 0;
}
.add-note {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}
.add-note input {
  flex: 1;
}
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}
.section-header h2 {
  margin: 0;
}
.timeline-filters {
  display: flex;
  gap: 0.4rem;
}
.timeline-filters .active {
  background: var(--color-bg-soft);
  color: var(--color-text);
}
.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.9rem;
  margin-top: 0.9rem;
}
.page-indicator {
  font-size: 0.82rem;
  color: var(--color-text-muted);
  font-variant-numeric: tabular-nums;
}
.add-goal {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
  margin-bottom: 1rem;
}
.add-goal button {
  grid-column: 1 / -1;
}
.add-payment {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
  margin-bottom: 1rem;
}
.add-payment button {
  grid-column: 1 / -1;
}
.goal-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.4rem 0;
  width: 100%;
}
.add-progress {
  display: flex;
  gap: 0.5rem;
  padding: 0.4rem 0 0.6rem;
}
.notes,
.timeline,
.outreach,
.consent,
.goals,
.forms,
.payments {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.notes li,
.timeline li,
.outreach li,
.goals li,
.forms li,
.payments li {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.4rem 0;
  border-bottom: 1px solid var(--color-border);
}
.goals li {
  flex-direction: column;
  align-items: stretch;
}
code {
  background: var(--color-bg-soft);
  padding: 0.1rem 0.3rem;
  border-radius: 4px;
}
.consent li label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-transform: capitalize;
}
.source,
.type,
.channel,
.status {
  font-size: 0.7rem;
  text-transform: uppercase;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  background: var(--color-bg-soft);
  color: var(--color-text-muted);
}
.source.import,
.source.system {
  background: var(--color-info-soft);
  color: var(--color-info);
}
.text {
  flex: 1;
}
.date {
  margin-left: auto;
  font-size: 0.75rem;
  color: var(--color-text-faint);
  font-variant-numeric: tabular-nums;
}
.remove {
  background: none;
  border: none;
  color: var(--color-text-faint);
  cursor: pointer;
  padding: 0.2rem 0.4rem;
}
.empty {
  color: var(--color-text-faint);
}
.shadow-tag {
  font-size: 0.65rem;
  text-transform: uppercase;
  font-weight: 600;
  color: var(--color-text-faint);
  border: 1px solid var(--color-border-strong);
  border-radius: 4px;
  padding: 0.1rem 0.35rem;
  margin-left: 0.5rem;
  vertical-align: middle;
}
.risk-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}
.score-header {
  display: flex;
  align-items: center;
  gap: 1.1rem;
}
.score-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.4rem;
}
.score-meta .trend {
  margin-top: 0.3rem;
}
.reasons {
  list-style: disc;
  margin: 0.5rem 0 0 1.25rem;
  padding: 0;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
.badge.green {
  background: var(--color-success-soft);
  color: var(--color-success);
}
.insufficient {
  color: var(--color-text-muted);
  background: var(--color-bg-soft);
  border-radius: var(--radius-sm);
  padding: 0.6rem 0.8rem;
  margin: 0;
}
.trend {
  display: flex;
  align-items: flex-end;
  gap: 0.6rem;
  margin-top: 0.75rem;
}
.trend-label {
  font-size: 0.75rem;
  color: var(--color-text-faint);
}
.breakdown-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-text-muted);
  margin-top: 0.9rem;
}
.sparkline {
  display: flex;
  align-items: flex-end;
  gap: 3px;
  height: 32px;
}
.sparkline .bar {
  width: 6px;
  min-height: 4px;
  background: var(--color-accent);
  border-radius: 2px;
}
.sparkline .bar.empty {
  background: var(--color-border-strong);
}
.factor-table {
  margin-top: 0.75rem;
  width: 100%;
}
.factor-table td.empty {
  color: var(--color-text-faint);
  font-size: 0.8rem;
}
</style>
