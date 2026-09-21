<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { api, ApiError } from '@/lib/api'
import type { LoginResponse } from '@/stores/auth'

const { t } = useI18n()
const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)
const auth = useAuthStore()
const router = useRouter()

async function submit() {
  error.value = ''
  loading.value = true
  try {
    const data = await api.post<LoginResponse>('/auth/login', { email: email.value, password: password.value })
    auth.setSession(data)
    router.push('/')
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : t('login.failed')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <form class="auth-card" @submit.prevent="submit">
      <h1>BKeeper</h1>
      <p class="subtitle">{{ t('login.signIn') }}</p>
      <label>{{ t('login.email') }}<input v-model="email" type="email" required autofocus /></label>
      <label>{{ t('login.password') }}<input v-model="password" type="password" required /></label>
      <p v-if="error" class="error">{{ error }}</p>
      <button type="submit" :disabled="loading">{{ loading ? t('login.signingIn') : t('login.signIn') }}</button>
      <RouterLink to="/bootstrap" class="bootstrap-link">{{ t('login.bootstrapLink') }}</RouterLink>
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
