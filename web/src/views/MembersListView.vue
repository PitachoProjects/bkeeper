<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'

const { t } = useI18n()

interface MemberListItem {
  id: string
  name: string
  email: string | null
  status: string
  joinDate: string
}

const STATUSES = ['Active', 'Frozen', 'Cancelled', 'Lapsed']

const members = ref<MemberListItem[]>([])
const search = ref('')
const statusFilter = ref('')
const loading = ref(true)

async function load() {
  loading.value = true
  const params = new URLSearchParams()
  if (search.value) params.set('search', search.value)
  if (statusFilter.value) params.set('status', statusFilter.value)
  const query = params.toString() ? `?${params.toString()}` : ''
  members.value = await api.get<MemberListItem[]>(`/members${query}`)
  loading.value = false
}

function tenure(joinDate: string) {
  const months = Math.floor((Date.now() - new Date(joinDate).getTime()) / (1000 * 60 * 60 * 24 * 30.44))
  if (months < 1) return '< 1 mo'
  if (months < 24) return `${months} mo`
  return `${Math.floor(months / 12)} yr`
}

onMounted(load)
</script>

<template>
  <div>
    <div class="header">
      <h1>{{ t('members.title') }}</h1>
      <div class="filters">
        <select v-model="statusFilter" @change="load">
          <option value="">{{ t('members.allStatuses') }}</option>
          <option v-for="s in STATUSES" :key="s" :value="s">{{ t(`members.statuses.${s}`) }}</option>
        </select>
        <input v-model="search" :placeholder="t('members.searchPlaceholder')" @keyup.enter="load" />
      </div>
    </div>
    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>{{ t('members.name') }}</th>
          <th>{{ t('members.email') }}</th>
          <th>{{ t('members.status') }}</th>
          <th>{{ t('members.joined') }}</th>
          <th>{{ t('members.tenure') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="m in members" :key="m.id" @click="$router.push(`/members/${m.id}`)">
          <td>{{ m.name }}</td>
          <td>{{ m.email }}</td>
          <td><span class="badge" :class="m.status.toLowerCase()">{{ m.status }}</span></td>
          <td>{{ m.joinDate }}</td>
          <td>{{ tenure(m.joinDate) }}</td>
        </tr>
        <tr v-if="members.length === 0">
          <td colspan="5">{{ t('members.empty') }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
}
.filters {
  display: flex;
  gap: 0.5rem;
}
input {
  width: 240px;
}
tbody tr {
  cursor: pointer;
}
tbody tr:hover {
  background: var(--color-bg-soft);
}
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: var(--color-success-soft);
  color: var(--color-success);
}
.badge.frozen {
  background: var(--color-info-soft);
  color: var(--color-info);
}
.badge.cancelled,
.badge.lapsed {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
</style>
