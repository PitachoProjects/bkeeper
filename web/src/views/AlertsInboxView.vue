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
  dueAt: string
  ruleCodes: string[]
}

const alerts = ref<AlertListItem[]>([])
const loading = ref(true)

async function load() {
  loading.value = true
  alerts.value = await api.get<AlertListItem[]>('/alerts')
  loading.value = false
}

async function claim(id: string) {
  await api.post(`/alerts/${id}/claim`)
  await load()
}

async function resolve(id: string) {
  const outcome = window.prompt('Outcome (e.g. returned, contacted_no_reply, false_positive_no_action):')
  if (!outcome) return
  await api.post(`/alerts/${id}/resolve`, { outcome })
  await load()
}

onMounted(load)
</script>

<template>
  <div>
    <h1>Alert inbox</h1>
    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>Severity</th>
          <th>Member</th>
          <th>Family</th>
          <th>Rules</th>
          <th>Status</th>
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
          <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
          <td class="actions">
            <button @click="claim(a.id)">Claim</button>
            <button @click="resolve(a.id)">Resolve</button>
          </td>
        </tr>
        <tr v-if="alerts.length === 0">
          <td colspan="7">Nothing open — the daily rule run creates alerts here.</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
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
.actions button {
  padding: 0.3rem 0.6rem;
  border-radius: 6px;
  border: 1px solid #ccc;
  background: white;
  cursor: pointer;
}
</style>
