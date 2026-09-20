<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { api, ApiError } from '@/lib/api'
import type { LoginResponse } from '@/stores/auth'

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
    router.push({ name: 'members' })
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Login failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <form class="auth-card" @submit.prevent="submit">
      <h1>BKeeper</h1>
      <p class="subtitle">Sign in</p>
      <label>Email<input v-model="email" type="email" required autofocus /></label>
      <label>Password<input v-model="password" type="password" required /></label>
      <p v-if="error" class="error">{{ error }}</p>
      <button type="submit" :disabled="loading">{{ loading ? 'Signing in…' : 'Sign in' }}</button>
      <RouterLink to="/bootstrap" class="bootstrap-link">First time? Set up your box</RouterLink>
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
  border-radius: 12px;
  border: 1px solid #e2e2e2;
}
h1 {
  margin: 0;
}
.subtitle {
  margin: 0 0 0.5rem;
  color: #666;
}
label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.9rem;
}
input {
  padding: 0.5rem;
  border-radius: 6px;
  border: 1px solid #ccc;
}
button {
  padding: 0.6rem;
  border-radius: 6px;
  border: none;
  background: #1a1a2e;
  color: white;
  cursor: pointer;
}
.error {
  color: #c0392b;
  font-size: 0.85rem;
}
.bootstrap-link {
  text-align: center;
  font-size: 0.85rem;
}
</style>
