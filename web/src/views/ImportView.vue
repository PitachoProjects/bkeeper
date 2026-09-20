<script setup lang="ts">
import { ref } from 'vue'
import { api, ApiError } from '@/lib/api'

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
    error.value = e instanceof ApiError ? e.message : 'Import failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <h1>Import</h1>
    <p class="hint">
      Excel workbook with sheets <code>Members</code>, <code>Classes</code>, <code>Attendance</code>, and optionally
      <code>Notes</code> (member_id, note). Re-uploading the same file is safe — it updates existing rows instead of
      duplicating them.
    </p>
    <div class="card">
      <input type="file" accept=".xlsx" @change="onFileChange" />
      <button :disabled="!file || loading" @click="upload">{{ loading ? 'Importing…' : 'Import' }}</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="result" class="card">
      <h2>Result</h2>
      <ul class="summary">
        <li>{{ result.membersUpserted }} members</li>
        <li>{{ result.sessionsUpserted }} classes</li>
        <li>{{ result.bookingsUpserted }} bookings</li>
        <li>{{ result.notesUpserted }} notes</li>
      </ul>
      <div v-if="result.issues.length" class="issues">
        <h3>{{ result.issues.length }} row issue(s)</h3>
        <ul>
          <li v-for="(issue, i) in result.issues" :key="i">
            {{ issue.sheet }} row {{ issue.row }}: {{ issue.message }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hint {
  color: #666;
  max-width: 640px;
}
.card {
  background: white;
  border-radius: 8px;
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.card button {
  margin-left: 0.75rem;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  border: none;
  background: #1a1a2e;
  color: white;
  cursor: pointer;
}
.summary {
  display: flex;
  gap: 1.5rem;
  list-style: none;
  padding: 0;
}
.issues {
  margin-top: 1rem;
  font-size: 0.85rem;
  color: #a12e24;
}
.error {
  color: #c0392b;
}
</style>
