<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { api, ApiError } from '@/lib/api'
import type { LoginResponse } from '@/stores/auth'

const boxName = ref('')
const ownerName = ref('')
const ownerEmail = ref('')
const ownerPassword = ref('')
const error = ref('')
const loading = ref(false)
const auth = useAuthStore()
const router = useRouter()

async function submit() {
  error.value = ''
  loading.value = true
  try {
    const data = await api.post<LoginResponse>('/auth/bootstrap', {
      boxName: boxName.value,
      ownerName: ownerName.value,
      ownerEmail: ownerEmail.value,
      ownerPassword: ownerPassword.value,
    })
    auth.setSession(data)
    router.push({ name: 'members' })
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Setup failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <form class="auth-card" @submit.prevent="submit">
      <h1>Set up your box</h1>
      <p class="subtitle">This only works once — the first Owner account.</p>
      <label>Box name<input v-model="boxName" required /></label>
      <label>Your name<input v-model="ownerName" required /></label>
      <label>Email<input v-model="ownerEmail" type="email" required /></label>
      <label>Password<input v-model="ownerPassword" type="password" minlength="8" required /></label>
      <p v-if="error" class="error">{{ error }}</p>
      <button type="submit" :disabled="loading">{{ loading ? 'Creating…' : 'Create box' }}</button>
      <RouterLink to="/login" class="bootstrap-link">Back to sign in</RouterLink>
    </form>
  </div>
</template>

<style scoped>
.auth-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
}
.auth-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  width: 320px;
  padding: 2rem;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  box-shadow: var(--shadow-md);
}
h1 {
  margin: 0;
}
.subtitle {
  margin: 0 0 0.5rem;
  color: var(--color-text-muted);
  font-size: 0.85rem;
}
label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.9rem;
}
.error {
  color: var(--color-danger);
  font-size: 0.85rem;
}
.bootstrap-link {
  text-align: center;
  font-size: 0.85rem;
}
</style>
