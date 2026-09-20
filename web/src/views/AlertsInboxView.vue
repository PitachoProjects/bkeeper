<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '@/lib/api'

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

const alerts = ref<AlertListItem[]>([])
const outcomes = ref<string[]>([])
const loading = ref(true)
const severityFilter = ref('')
const roleFilter = ref('')
const resolvingId = ref<string | null>(null)
const outcome = ref('')
const outcomeNote = ref('')

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

onMounted(async () => {
  outcomes.value = await api.get<string[]>('/alerts/outcomes')
  await load()
})
</script>

<template>
  <div>
    <div class="header">
      <h1>Alert inbox</h1>
      <div class="filters">
        <select v-model="severityFilter" @change="load">
          <option value="">All severities</option>
          <option value="Red">Red</option>
          <option value="Amber">Amber</option>
          <option value="Info">Info</option>
        </select>
        <select v-model="roleFilter" @change="load">
          <option value="">All roles</option>
          <option value="Coach">Coach</option>
          <option value="Manager">Manager</option>
          <option value="Owner">Owner</option>
        </select>
        <button class="ghost" title="Runs the SLA escalation sweep now (also runs every 15 min automatically)" @click="runEscalation">
          Run escalation sweep
        </button>
      </div>
    </div>

    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>Severity</th>
          <th>Member</th>
          <th>Family</th>
          <th>Rules</th>
          <th>Status</th>
          <th>Assigned</th>
          <th>Due</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="a in alerts" :key="a.id">
          <td><span class="badge" :class="a.severity.toLowerCase()">{{ a.severity }}</span></td>
          <td><RouterLink :to="`/members/${a.memberId}`">{{ a.memberName }}</RouterLink></td>
          <td>{{ a.family }}</td>
          <td>{{ a.ruleCodes.join(', ') }}</td>
          <td>{{ a.status }}</td>
          <td>{{ a.assignedRole }}</td>
          <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
          <td class="actions">
            <button v-if="!a.claimedBy" @click="claim(a.id)">Claim</button>
            <button @click="startResolve(a.id)">Resolve</button>
          </td>
        </tr>
        <tr v-if="alerts.length === 0">
          <td colspan="8">Nothing here — the daily rule run (or "Run escalation sweep") creates/updates alerts.</td>
        </tr>
      </tbody>
    </table>

    <div v-if="resolvingId" class="modal-backdrop" @click.self="resolvingId = null">
      <div class="modal">
        <h2>Resolve alert</h2>
        <label>
          Outcome
          <select v-model="outcome">
            <option v-for="o in outcomes" :key="o" :value="o">{{ o.replaceAll('_', ' ') }}</option>
          </select>
        </label>
        <label>
          Note (optional)
          <textarea v-model="outcomeNote" rows="3"></textarea>
        </label>
        <div class="modal-actions">
          <button class="ghost" @click="resolvingId = null">Cancel</button>
          <button @click="confirmResolve">Confirm</button>
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
select {
  padding: 0.45rem;
  border-radius: 6px;
  border: 1px solid #ccc;
}
table {
  width: 100%;
  border-collapse: collapse;
  background: white;
  border-radius: 8px;
  overflow: hidden;
}
th,
td {
  text-align: left;
  padding: 0.6rem 0.8rem;
  border-bottom: 1px solid #eee;
}
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: #eee;
}
.badge.red {
  background: #fbe3e1;
  color: #a12e24;
}
.badge.amber {
  background: #fdf0d5;
  color: #92650b;
}
.badge.info {
  background: #e5eefc;
  color: #1e4d7a;
}
.actions {
  display: flex;
  gap: 0.4rem;
}
button {
  padding: 0.3rem 0.6rem;
  border-radius: 6px;
  border: 1px solid #1a1a2e;
  background: #1a1a2e;
  color: white;
  cursor: pointer;
}
button.ghost {
  background: white;
  color: #1a1a2e;
}
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
}
.modal {
  background: white;
  border-radius: 8px;
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
.modal select,
.modal textarea {
  padding: 0.5rem;
  border-radius: 6px;
  border: 1px solid #ccc;
  font-family: inherit;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
