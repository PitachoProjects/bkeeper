import { useAuthStore } from '@/stores/auth'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message)
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const auth = useAuthStore()
  const headers = new Headers(options.headers)
  headers.set('Content-Type', headers.get('Content-Type') ?? 'application/json')
  if (auth.token) headers.set('Authorization', `Bearer ${auth.token}`)

  const res = await fetch(`${API_URL}${path}`, { ...options, headers })
  if (res.status === 401) {
    auth.logout()
    throw new ApiError(401, 'Session expired, please log in again.')
  }
  if (!res.ok) {
    const text = await res.text()
    throw new ApiError(res.status, text || res.statusText)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
  postForm: <T>(path: string, form: FormData) => {
    const auth = useAuthStore()
    return fetch(`${API_URL}${path}`, {
      method: 'POST',
      headers: auth.token ? { Authorization: `Bearer ${auth.token}` } : undefined,
      body: form,
    }).then(async (res) => {
      if (!res.ok) throw new ApiError(res.status, await res.text())
      return (await res.json()) as T
    })
  },
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
