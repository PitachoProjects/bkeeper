<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

interface Question {
  key: string
  label: string
  type: 'Scale1To5' | 'Nps0To10' | 'Text' | 'YesNo' | 'MultiSelect'
  showIfQuestionKey: string | null
  showIfEquals: string | null
}

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'
const route = useRoute()
const token = route.params.token as string

const loading = ref(true)
const error = ref('')
const submitted = ref(false)
const questions = ref<Question[]>([])
const answers = ref<Record<string, string>>({})

function shouldShow(q: Question): boolean {
  if (!q.showIfQuestionKey) return true
  return (answers.value[q.showIfQuestionKey] ?? '').toLowerCase() === (q.showIfEquals ?? '').toLowerCase()
}

const visibleQuestions = computed(() => questions.value.filter(shouldShow))

async function load() {
  loading.value = true
  try {
    const res = await fetch(`${API_URL}/f/${token}`)
    if (!res.ok) {
      error.value = 'This link is invalid, expired, or already used.'
      return
    }
    const data = await res.json()
    questions.value = data.questions
  } catch {
    error.value = 'Could not load the form. Please try again later.'
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  try {
    const res = await fetch(`${API_URL}/f/${token}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ answers: answers.value }),
    })
    if (!res.ok) {
      error.value = 'Could not submit — the link may have expired.'
      return
    }
    submitted.value = true
  } catch {
    error.value = 'Could not submit. Please try again.'
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="card">
      <div v-if="loading">Loading…</div>
      <div v-else-if="submitted" class="thankyou">
        <h1>Thank you! 🎉</h1>
        <p>Your answers have been recorded.</p>
      </div>
      <div v-else-if="error">
        <h1>Oops</h1>
        <p>{{ error }}</p>
      </div>
      <form v-else @submit.prevent="submit">
        <h1>A quick check-in</h1>
        <div v-for="q in visibleQuestions" :key="q.key" class="question">
          <label>{{ q.label }}</label>

          <div v-if="q.type === 'Scale1To5'" class="scale">
            <label v-for="n in 5" :key="n" class="scale-option">
              <input type="radio" :name="q.key" :value="String(n)" v-model="answers[q.key]" required />
              {{ n }}
            </label>
          </div>

          <div v-else-if="q.type === 'Nps0To10'" class="scale">
            <label v-for="n in 11" :key="n - 1" class="scale-option">
              <input type="radio" :name="q.key" :value="String(n - 1)" v-model="answers[q.key]" required />
              {{ n - 1 }}
            </label>
          </div>

          <div v-else-if="q.type === 'YesNo'" class="scale">
            <label class="scale-option"><input type="radio" :name="q.key" value="yes" v-model="answers[q.key]" required /> Yes</label>
            <label class="scale-option"><input type="radio" :name="q.key" value="no" v-model="answers[q.key]" required /> No</label>
          </div>

          <textarea v-else v-model="answers[q.key]" rows="3"></textarea>
        </div>

        <button type="submit">Submit</button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  background: var(--color-bg);
}
.card {
  width: 100%;
  max-width: 480px;
  padding: 2rem;
}
h1 {
  margin-bottom: 1rem;
}
.question {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.25rem;
}
.question > label:first-child {
  font-weight: 600;
  font-size: 0.92rem;
}
textarea {
  width: 100%;
}
.scale {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}
.scale-option {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.85rem;
  font-weight: 400;
}
button[type='submit'] {
  width: 100%;
}
.thankyou {
  text-align: center;
}
</style>
