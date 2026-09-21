<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { useAuthStore } from '@/stores/auth'
import RadarChart from '@/components/RadarChart.vue'

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

interface WorkoutMixItem {
  tag: string
  count: number
}

const { t } = useI18n()
const GOAL_CATEGORIES = ['Strength', 'Skill', 'BodyComposition', 'Endurance', 'CompetitionEvent', 'HealthRehab', 'Consistency', 'Social']
const TIMELINE_FILTERS = ['note', 'Attended', 'NoShow', 'LateCancel']

const route = useRoute()
const memberId = route.params.id as string
const auth = useAuthStore()
const canSeeRisk = computed(() => auth.role === 'Manager' || auth.role === 'Owner')

const member = ref<MemberDetail | null>(null)
const riskScore = ref<RiskScore | null>(null)
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
const timelineFilter = ref('')
const timelineFiltered = computed(() =>
  timelineFilter.value ? timeline.value.filter((t) => t.type === timelineFilter.value) : timeline.value,
)

async function load() {
  ;[member.value, notes.value, timeline.value, consent.value, outreach.value, goals.value, forms.value, workoutMix.value] = await Promise.all([
    api.get<MemberDetail>(`/members/${memberId}`),
    api.get<MemberNote[]>(`/members/${memberId}/notes`),
    api.get<TimelineItem[]>(`/members/${memberId}/timeline`),
    api.get<Consent[]>(`/members/${memberId}/consent`),
    api.get<OutreachItem[]>(`/members/${memberId}/outreach`),
    api.get<GoalItem[]>(`/members/${memberId}/goals`),
    api.get<FormSummary[]>('/forms'),
    api.get<WorkoutMixItem[]>(`/members/${memberId}/workout-mix`),
  ])

  if (canSeeRisk.value) {
    riskScore.value = await api.get<RiskScore | null>(`/risk-scores/members/${memberId}`)
  }
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

onMounted(load)
</script>

<template>
  <div v-if="member">
    <h1>{{ member.name }}</h1>
    <p class="meta">{{ member.email }} · {{ member.phone }} · {{ t('memberDetail.joined') }} {{ member.joinDate }}</p>

    <section v-if="canSeeRisk" class="card">
      <h2>{{ t('memberDetail.risk.title') }} <span class="shadow-tag">{{ t('memberDetail.risk.shadowMode') }}</span></h2>
      <p class="hint">{{ t('memberDetail.risk.hint') }}</p>
      <div v-if="riskScore" class="risk-row">
        <span class="badge" :class="riskScore.band">{{ riskScore.band }}</span>
        <span class="text">{{ (riskScore.pChurn28d * 100).toFixed(1) }}% {{ t('memberDetail.risk.chance') }}</span>
        <span class="date">{{ t('memberDetail.risk.weekOf') }} {{ riskScore.snapshotWeek }} · {{ t('memberDetail.risk.model') }} {{ riskScore.modelVersion }}</span>
      </div>
      <ul v-if="riskScore && riskScore.topReasons.length" class="reasons">
        <li v-for="(r, i) in riskScore.topReasons" :key="i">{{ r }}</li>
      </ul>
      <p v-if="!riskScore" class="empty">{{ t('memberDetail.risk.empty') }}</p>
    </section>

    <section class="card">
      <h2>{{ t('memberDetail.workoutMix.title') }}</h2>
      <p class="hint">{{ t('memberDetail.workoutMix.hint') }}</p>
      <RadarChart v-if="workoutMixHasData" :points="workoutMix.map((w) => ({ label: w.tag, value: w.count }))" />
      <p v-else class="empty">{{ t('memberDetail.workoutMix.empty') }}</p>
    </section>

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
          <button class="ghost" :class="{ active: timelineFilter === '' }" @click="timelineFilter = ''">{{ t('memberDetail.timeline.all') }}</button>
          <button v-for="opt in TIMELINE_FILTERS" :key="opt" class="ghost" :class="{ active: timelineFilter === opt }" @click="timelineFilter = opt">
            {{ t(`memberDetail.timeline.filters.${opt}`) }}
          </button>
        </div>
      </div>
      <ul class="timeline">
        <li v-for="(item, i) in timelineFiltered" :key="i">
          <span class="type" :class="item.type.toLowerCase()">{{ item.type }}</span>
          <span>{{ item.summary }}</span>
          <span class="date">{{ new Date(item.at).toLocaleString() }}</span>
        </li>
        <li v-if="timelineFiltered.length === 0" class="empty">{{ t('memberDetail.timeline.empty') }}</li>
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
.card {
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin-top: -0.25rem;
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
.add-goal {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
  margin-bottom: 1rem;
}
.add-goal button {
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
.forms {
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
.forms li {
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
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: var(--color-bg-soft);
  color: var(--color-text-muted);
  text-transform: capitalize;
}
.badge.red {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.badge.amber {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}
.reasons {
  list-style: disc;
  margin: 0.5rem 0 0 1.25rem;
  padding: 0;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
</style>
