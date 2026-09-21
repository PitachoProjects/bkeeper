<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const auth = useAuthStore()
const canManage = auth.role === 'Owner' || auth.role === 'Manager'

interface CoachItem {
  id: string
  name: string
  email: string | null
  status: string
  applicationUserId: string | null
}

const STATUSES = ['Active', 'Inactive']

const coaches = ref<CoachItem[]>([])
const loading = ref(true)
const showAdd = ref(false)
const newCoach = ref({ name: '', email: '' })
const editingId = ref<string | null>(null)
const editDraft = ref({ name: '', email: '', status: 'Active' })

async function load() {
  loading.value = true
  try {
    coaches.value = await api.get<CoachItem[]>('/coaches')
  } finally {
    loading.value = false
  }
}

async function addCoach() {
  if (!newCoach.value.name.trim()) return
  const coach = await api.post<CoachItem>('/coaches', {
    name: newCoach.value.name.trim(),
    email: newCoach.value.email.trim() || null,
  })
  coaches.value = [...coaches.value, coach].sort((a, b) => a.name.localeCompare(b.name))
  newCoach.value = { name: '', email: '' }
  showAdd.value = false
}

function startEdit(c: CoachItem) {
  editingId.value = c.id
  editDraft.value = { name: c.name, email: c.email ?? '', status: c.status }
}

async function saveEdit(c: CoachItem) {
  if (!editDraft.value.name.trim()) return
  const updated = await api.put<CoachItem>(`/coaches/${c.id}`, {
    name: editDraft.value.name.trim(),
    email: editDraft.value.email.trim() || null,
    status: editDraft.value.status,
    applicationUserId: c.applicationUserId,
  })
  const idx = coaches.value.findIndex((x) => x.id === c.id)
  if (idx !== -1) coaches.value[idx] = updated
  editingId.value = null
}

onMounted(load)
</script>

<template>
  <div>
    <div class="header">
      <h1>{{ t('coaches.title') }}</h1>
      <button v-if="canManage" class="ghost" @click="showAdd = !showAdd">
        {{ showAdd ? t('coaches.cancel') : t('coaches.addCoach') }}
      </button>
    </div>
    <p class="hint">{{ t('coaches.hint') }}</p>

    <form v-if="showAdd" class="add-coach card" @submit.prevent="addCoach">
      <input v-model="newCoach.name" :placeholder="t('coaches.namePlaceholder')" />
      <input v-model="newCoach.email" :placeholder="t('coaches.emailPlaceholder')" />
      <button type="submit">{{ t('coaches.save') }}</button>
    </form>

    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>{{ t('coaches.name') }}</th>
          <th>{{ t('coaches.email') }}</th>
          <th>{{ t('coaches.status') }}</th>
          <th v-if="canManage"></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="c in coaches" :key="c.id">
          <tr v-if="editingId !== c.id">
            <td>{{ c.name }}</td>
            <td>{{ c.email }}</td>
            <td><span class="badge" :class="c.status.toLowerCase()">{{ t(`coaches.statuses.${c.status}`) }}</span></td>
            <td v-if="canManage"><button class="ghost" @click="startEdit(c)">{{ t('coaches.edit') }}</button></td>
          </tr>
          <tr v-else class="editing">
            <td><input v-model="editDraft.name" /></td>
            <td><input v-model="editDraft.email" /></td>
            <td>
              <select v-model="editDraft.status">
                <option v-for="s in STATUSES" :key="s" :value="s">{{ t(`coaches.statuses.${s}`) }}</option>
              </select>
            </td>
            <td><button @click="saveEdit(c)">{{ t('coaches.save') }}</button></td>
          </tr>
        </template>
        <tr v-if="coaches.length === 0">
          <td :colspan="canManage ? 4 : 3">{{ t('coaches.empty') }}</td>
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
  margin-bottom: 0.25rem;
}
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin: 0 0 1.25rem;
}
.add-coach {
  display: flex;
  gap: 0.5rem;
  padding: 1rem 1.25rem;
  margin-bottom: 1rem;
}
.add-coach input {
  flex: 1;
}
tr.editing input,
tr.editing select {
  width: 100%;
}
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: var(--color-success-soft);
  color: var(--color-success);
}
.badge.inactive {
  background: var(--color-bg-soft);
  color: var(--color-text-muted);
}
</style>
