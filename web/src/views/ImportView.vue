<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, ApiError } from '@/lib/api'

const { t } = useI18n()

interface ImportRowIssue {
  sheet: string
  row: number
  message: string
}

interface ImportResult {
  importRunId: string
  membersUpserted: number
  sessionsUpserted: number
  bookingsUpserted: number
  notesUpserted: number
  succeeded: boolean
  issues: ImportRowIssue[]
}

const file = ref<File | null>(null)
const result = ref<ImportResult | null>(null)
const error = ref('')
const loading = ref(false)

function onFileChange(e: Event) {
  const input = e.target as HTMLInputElement
  file.value = input.files?.[0] ?? null
}

async function upload() {
  if (!file.value) return
  error.value = ''
  result.value = null
  loading.value = true
  try {
    const form = new FormData()
    form.append('file', file.value)
    result.value = await api.postForm<ImportResult>('/imports', form)
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : t('import.failed')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <h1>{{ t('import.title') }}</h1>
    <i18n-t keypath="import.hint" tag="p" class="hint">
      <template #members><code>Members</code></template>
      <template #classes><code>Classes</code></template>
      <template #attendance><code>Attendance</code></template>
      <template #notes><code>Notes</code></template>
    </i18n-t>
    <div class="card">
      <input type="file" accept=".xlsx" @change="onFileChange" />
      <button :disabled="!file || loading" @click="upload">{{ loading ? t('import.importing') : t('import.importAction') }}</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="result" class="card">
      <h2>{{ t('import.resultTitle') }}</h2>
      <ul class="summary">
        <li>{{ result.membersUpserted }} {{ t('import.members') }}</li>
        <li>{{ result.sessionsUpserted }} {{ t('import.classes') }}</li>
        <li>{{ result.bookingsUpserted }} {{ t('import.bookings') }}</li>
        <li>{{ result.notesUpserted }} {{ t('import.notes') }}</li>
      </ul>
      <div v-if="result.issues.length" class="issues">
        <h3>{{ result.issues.length }} {{ t('import.rowIssues') }}</h3>
        <ul>
          <li v-for="(issue, i) in result.issues" :key="i">
            {{ issue.sheet }} {{ t('import.row') }} {{ issue.row }}: {{ issue.message }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hint {
  color: var(--color-text-muted);
  max-width: 640px;
}
.hint code {
  background: var(--color-bg-soft);
  padding: 0.1rem 0.3rem;
  border-radius: 4px;
}
.card {
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.card button {
  margin-left: 0.75rem;
}
.summary {
  display: flex;
  gap: 1.5rem;
  list-style: none;
  padding: 0;
  font-variant-numeric: tabular-nums;
}
.issues {
  margin-top: 1rem;
  font-size: 0.85rem;
  color: var(--color-danger);
}
.error {
  color: var(--color-danger);
}
</style>
