<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { api } from '@/lib/api'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

interface MemberListItem {
  id: string
  name: string
  email: string | null
  status: string
  joinDate: string
}

interface AlertListItem {
  memberId: string
}

const STATUSES = ['Active', 'Frozen', 'Cancelled', 'Lapsed']

// Quick filters over this one page/endpoint, not separate pages — driven by ?filter= so they're
// still deep-linkable and bookmarkable without needing their own nav entry or route. "At risk"/
// "early warning" reuse the (already unrestricted) alerts endpoint client-side; "new"/"inactive"
// reuse fields the members endpoint already returns.
const FILTERS = ['all', 'at-risk', 'early-warning', 'new', 'inactive'] as const
type FilterId = (typeof FILTERS)[number]
const FILTER_I18N_KEY: Record<FilterId, string> = { all: 'all', 'at-risk': 'atRisk', 'early-warning': 'earlyWarning', new: 'new', inactive: 'inactive' }
const NEW_MEMBER_WINDOW_DAYS = 30

const filter = computed<FilterId>(() => {
  const f = route.query.filter as string
  return (FILTERS as readonly string[]).includes(f) ? (f as FilterId) : 'all'
})
const showsStatusFilter = computed(() => filter.value === 'all')
const filterHint = computed(() => (filter.value === 'all' ? '' : t(`members.presets.${FILTER_I18N_KEY[filter.value]}.hint`)))

function filterLabel(f: FilterId) {
  return f === 'all' ? t('members.allStatuses') : t(`members.presets.${FILTER_I18N_KEY[f]}.title`)
}
function selectFilter(f: FilterId) {
  router.push(f === 'all' ? '/members' : { path: '/members', query: { filter: f } })
}

const members = ref<MemberListItem[]>([])
const search = ref('')
const statusFilter = ref('')
const loading = ref(true)

async function loadAlertFiltered(severity: 'Red' | 'Amber') {
  const [alerts, allMembers] = await Promise.all([
    api.get<AlertListItem[]>(`/alerts?severity=${severity}`),
    api.get<MemberListItem[]>('/members'),
  ])
  const flaggedIds = new Set(alerts.map((a) => a.memberId))
  let list = allMembers.filter((m) => flaggedIds.has(m.id))
  if (search.value) list = list.filter((m) => m.name.toLowerCase().includes(search.value.toLowerCase()))
  members.value = list
}

async function load() {
  loading.value = true
  try {
    if (filter.value === 'at-risk' || filter.value === 'early-warning') {
      await loadAlertFiltered(filter.value === 'at-risk' ? 'Red' : 'Amber')
    } else if (filter.value === 'inactive') {
      // Client-side union: the members endpoint filters by a single status, and
      // "inactive" covers both the stored Lapsed and Cancelled statuses.
      const searchQuery = search.value ? `&search=${encodeURIComponent(search.value)}` : ''
      const [lapsed, cancelled] = await Promise.all([
        api.get<MemberListItem[]>(`/members?status=Lapsed${searchQuery}`),
        api.get<MemberListItem[]>(`/members?status=Cancelled${searchQuery}`),
      ])
      members.value = [...lapsed, ...cancelled].sort((a, b) => a.name.localeCompare(b.name))
    } else {
      const params = new URLSearchParams()
      if (search.value) params.set('search', search.value)
      if (showsStatusFilter.value && statusFilter.value) params.set('status', statusFilter.value)
      const query = params.toString() ? `?${params.toString()}` : ''
      let list = await api.get<MemberListItem[]>(`/members${query}`)

      if (filter.value === 'new') {
        const cutoff = Date.now() - NEW_MEMBER_WINDOW_DAYS * 24 * 60 * 60 * 1000
        list = list.filter((m) => new Date(m.joinDate).getTime() >= cutoff)
      }
      members.value = list
    }
  } finally {
    loading.value = false
  }
}

watch(filter, load)

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
        <select v-if="showsStatusFilter" v-model="statusFilter" @change="load">
          <option value="">{{ t('members.allStatuses') }}</option>
          <option v-for="s in STATUSES" :key="s" :value="s">{{ t(`members.statuses.${s}`) }}</option>
        </select>
        <input v-model="search" :placeholder="t('members.searchPlaceholder')" @keyup.enter="load" />
      </div>
    </div>

    <div class="chips">
      <button v-for="f in FILTERS" :key="f" class="chip" :class="{ active: filter === f }" @click="selectFilter(f)">
        {{ filterLabel(f) }}
      </button>
    </div>
    <p v-if="filterHint" class="hint">{{ filterHint }}</p>

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
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin: -0.5rem 0 1.25rem;
}
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
.chips {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}
.chip {
  padding: 0.35rem 0.8rem;
  border-radius: 999px;
  border: 1px solid var(--color-border-strong);
  background: transparent;
  color: var(--color-text-muted);
  font-size: 0.82rem;
  font-weight: 500;
  cursor: pointer;
}
.chip:hover {
  filter: none;
  background: var(--color-bg-soft);
  color: var(--color-text);
}
.chip.active {
  background: var(--color-accent-soft);
  border-color: var(--color-accent);
  color: var(--color-accent);
  font-weight: 600;
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
