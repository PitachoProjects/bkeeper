import { defineStore } from 'pinia'
import { ref } from 'vue'

interface LoginResponse {
  token: string
  displayName: string
  role: string
  boxId: string
}

const STORAGE_KEY = 'bkeeper.auth'

function loadStored(): LoginResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as LoginResponse) : null
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', () => {
  const stored = loadStored()
  const token = ref<string | null>(stored?.token ?? null)
  const displayName = ref<string | null>(stored?.displayName ?? null)
  const role = ref<string | null>(stored?.role ?? null)
  const boxId = ref<string | null>(stored?.boxId ?? null)

  function setSession(data: LoginResponse) {
    token.value = data.token
    displayName.value = data.displayName
    role.value = data.role
    boxId.value = data.boxId
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data))
  }

  function logout() {
    token.value = null
    displayName.value = null
    role.value = null
    boxId.value = null
    localStorage.removeItem(STORAGE_KEY)
  }

  return { token, displayName, role, boxId, setSession, logout }
})

export type { LoginResponse }
