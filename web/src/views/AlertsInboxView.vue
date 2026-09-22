<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, ApiError } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import InfoTip from '@/components/InfoTip.vue'

const { t } = useI18n()

interface AlertListItem {
  id: string
  memberId: string
  memberName: string
  family: string
  severity: string
  status: string
  assignedRole: string
  claimedBy: string | null
  dueAt: string
  ruleCodes: string[]
}

interface RuleCatalogItem {
  code: string
  description: string
}

const TEMPLATE_KEYS = [
  'WELCOME',
  'ONB_NUDGE_D3',
  'MISS_YOU_SOFT',
  'SCHEDULE_NUDGE',
  'MILESTONE_50',
  'ANNIVERSARY',
  'EVAL_REQUEST',
  'EVAL_REMINDER',
  'RENEWAL_REMINDER',
  'WINBACK_D30',
]

const alerts = ref<AlertListItem[]>([])
const outcomes = ref<string[]>([])
const ruleDescriptions = ref<Record<string, string>>({})
const loading = ref(true)
const severityFilter = ref('')
const roleFilter = ref('')
const resolvingId = ref<string | null>(null)
const outcome = ref('')
const outcomeNote = ref('')
const sendingId = ref<string | null>(null)
const sendMode = ref<'template' | 'custom'>('template')
const sendTemplate = ref(TEMPLATE_KEYS[2])
const sendCustomBody = ref('')
const sendError = ref('')

const actionsColumnHint = computed(
  () => `${t('alerts.claim')}: ${t('alerts.claimHint')} ${t('alerts.sendMessage')}: ${t('alerts.sendMessageHint')} ${t('alerts.resolve')}: ${t('alerts.resolveHint')}`,
)

async function load() {
  loading.value = true
  const params = new URLSearchParams()
  if (severityFilter.value) params.set('severity', severityFilter.value)
  if (roleFilter.value) params.set('role', roleFilter.value)
  const query = params.toString() ? `?${params.toString()}` : ''
  alerts.value = await api.get<AlertListItem[]>(`/alerts${query}`)
  loading.value = false
}

async function claim(id: string) {
  await api.post(`/alerts/${id}/claim`)
  await load()
}

function startResolve(id: string) {
  resolvingId.value = id
  outcome.value = outcomes.value[0] ?? ''
  outcomeNote.value = ''
}

async function confirmResolve() {
  if (!resolvingId.value || !outcome.value) return
  await api.post(`/alerts/${resolvingId.value}/resolve`, { outcome: outcome.value, note: outcomeNote.value || null })
  resolvingId.value = null
  await load()
}

async function runEscalation() {
  await api.post('/alerts/escalate/run')
  await load()
}

function startSend(id: string) {
  sendingId.value = id
  sendMode.value = 'template'
  sendTemplate.value = TEMPLATE_KEYS[2]
  sendCustomBody.value = ''
  sendError.value = ''
}

async function confirmSend() {
  if (!sendingId.value) return
  sendError.value = ''
  try {
    await api.post(`/alerts/${sendingId.value}/outreach`, {
      templateKey: sendMode.value === 'template' ? sendTemplate.value : null,
      customBody: sendMode.value === 'custom' ? sendCustomBody.value : null,
    })
    sendingId.value = null
  } catch (e) {
    sendError.value = e instanceof ApiError ? e.message : 'Could not send the message.'
  }
}

onMounted(async () => {
  outcomes.value = await api.get<string[]>('/alerts/outcomes')
  const rules = await api.get<RuleCatalogItem[]>('/rules')
  ruleDescriptions.value = Object.fromEntries(rules.map((r) => [r.code, r.description]))
  await load()
})
</script>

<template>
  <div>
    <div class="header">
      <h1>{{ t('alerts.title') }}</h1>
      <div class="filters">
        <select v-model="severityFilter" @change="load">
          <option value="">{{ t('alerts.allSeverities') }}</option>
          <option value="Red">{{ t('severity.Red') }}</option>
          <option value="Amber">{{ t('severity.Amber') }}</option>
          <option value="Info">{{ t('severity.Info') }}</option>
        </select>
        <select v-model="roleFilter" @change="load">
          <option value="">{{ t('alerts.allRoles') }}</option>
          <option value="Coach">Coach</option>
          <option value="Manager">Manager</option>
          <option value="Owner">Owner</option>
        </select>
        <button class="ghost" :title="t('alerts.runEscalationHint')" @click="runEscalation">
          {{ t('alerts.runEscalation') }}
        </button>
      </div>
    </div>

    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>{{ t('alerts.severity') }}</th>
          <th>{{ t('alerts.member') }}</th>
          <th>{{ t('alerts.family') }}</th>
          <th>{{ t('alerts.rules') }}</th>
          <th>{{ t('alerts.status') }}</th>
          <th>{{ t('alerts.assigned') }}</th>
          <th>{{ t('alerts.due') }}</th>
          <th>{{ t('alerts.actions') }}<InfoTip :text="actionsColumnHint" /></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="a in alerts" :key="a.id">
          <td><span class="badge" :class="a.severity.toLowerCase()">{{ severityLabel(a.severity) }}</span></td>
          <td><RouterLink :to="`/members/${a.memberId}`">{{ a.memberName }}</RouterLink></td>
          <td>{{ a.family }}</td>
          <td class="rule-chips">
            <span v-for="code in a.ruleCodes" :key="code" class="badge rule-chip">
              {{ code }}<span v-if="ruleDescriptions[code]" class="rule-desc"> — {{ ruleDescriptions[code] }}</span>
            </span>
          </td>
          <td><span class="badge status">{{ a.status }}</span></td>
          <td>{{ a.assignedRole }}</td>
          <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
          <td class="actions">
            <button v-if="!a.claimedBy" @click="claim(a.id)">{{ t('alerts.claim') }}</button>
            <button class="ghost" @click="startSend(a.id)">{{ t('alerts.sendMessage') }}</button>
            <button @click="startResolve(a.id)">{{ t('alerts.resolve') }}</button>
          </td>
        </tr>
        <tr v-if="alerts.length === 0">
          <td colspan="8">{{ t('alerts.empty') }}</td>
        </tr>
      </tbody>
    </table>

    <div v-if="resolvingId" class="modal-backdrop" @click.self="resolvingId = null">
      <div class="modal">
        <h2>{{ t('alerts.resolveTitle') }}</h2>
        <label>
          {{ t('alerts.outcome') }}
          <select v-model="outcome">
            <option v-for="o in outcomes" :key="o" :value="o">{{ o.replaceAll('_', ' ') }}</option>
          </select>
        </label>
        <label>
          {{ t('alerts.note') }}
          <textarea v-model="outcomeNote" rows="3"></textarea>
        </label>
        <div class="modal-actions">
          <button class="ghost" @click="resolvingId = null">{{ t('alerts.cancel') }}</button>
          <button @click="confirmResolve">{{ t('alerts.confirm') }}</button>
        </div>
      </div>
    </div>

    <div v-if="sendingId" class="modal-backdrop" @click.self="sendingId = null">
      <div class="modal">
        <h2>{{ t('alerts.sendTitle') }}</h2>
        <div class="mode-toggle">
          <label><input type="radio" value="template" v-model="sendMode" /> {{ t('alerts.template') }}</label>
          <label><input type="radio" value="custom" v-model="sendMode" /> {{ t('alerts.customText') }}</label>
        </div>
        <label v-if="sendMode === 'template'">
          {{ t('alerts.template') }}
          <select v-model="sendTemplate">
            <option v-for="tk in TEMPLATE_KEYS" :key="tk" :value="tk">{{ tk.replaceAll('_', ' ') }}</option>
          </select>
        </label>
        <label v-else>
          {{ t('alerts.messageLabel') }}
          <textarea v-model="sendCustomBody" rows="4" :placeholder="t('alerts.messagePlaceholder')"></textarea>
        </label>
        <p v-if="sendError" class="error">{{ sendError }}</p>
        <div class="modal-actions">
          <button class="ghost" @click="sendingId = null">{{ t('alerts.cancel') }}</button>
          <button @click="confirmSend">{{ t('alerts.send') }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.filters {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}
.rule-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
.rule-chip {
  font-variant-numeric: tabular-nums;
}
.rule-desc {
  font-weight: normal;
  opacity: 0.75;
}
.badge.status {
  text-transform: none;
}
.actions {
  display: flex;
  gap: 0.4rem;
}
.actions button {
  padding: 0.3rem 0.6rem;
  font-size: 0.8rem;
}
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
}
.modal {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
  padding: 1.5rem;
  width: 360px;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.modal label {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  font-size: 0.9rem;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
.mode-toggle {
  display: flex;
  gap: 1rem;
  font-size: 0.9rem;
}
.mode-toggle label {
  display: flex;
  align-items: center;
  gap: 0.3rem;
}
.error {
  color: var(--color-danger);
  font-size: 0.85rem;
}
</style>
