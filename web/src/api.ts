import axios from 'axios'

export const api = axios.create({ baseURL: '/api' })

api.interceptors.request.use((cfg) => {
  const token = localStorage.getItem('crm_token')
  if (token) cfg.headers.Authorization = `Bearer ${token}`
  return cfg
})

api.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401 && window.location.pathname !== '/login') {
      localStorage.removeItem('crm_token')
      localStorage.removeItem('crm_user')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  },
)

/** Extract a human-readable message from an axios error (empty string when there is no error). */
export function errMsg(e: unknown): string {
  if (!e) return ''
  if (axios.isAxiosError(e)) {
    const data = e.response?.data
    if (typeof data === 'string' && data.trim()) return data
    if (data?.error) return String(data.error)
    if (data?.title) return String(data.title)
    return e.message
  }
  return String(e)
}

// ---------- typed request helpers (unwraps res.data) ----------

export async function apiGet<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const res = await api.get<T>(url, { params })
  return res.data
}

export async function apiPost<T = void>(url: string, body?: unknown): Promise<T> {
  const res = await api.post<T>(url, body)
  return res.data
}

export async function apiPut<T = void>(url: string, body?: unknown): Promise<T> {
  const res = await api.put<T>(url, body)
  return res.data
}

export async function apiPatch<T = void>(url: string, body?: unknown): Promise<T> {
  const res = await api.patch<T>(url, body)
  return res.data
}

export async function apiDelete<T = void>(url: string): Promise<T> {
  const res = await api.delete<T>(url)
  return res.data
}

// ---------- server-side validation ----------

/** Map of field name → error messages, as returned by ASP.NET ValidationProblemDetails. */
export type FieldErrors = Record<string, string[]>

/** Extract per-field validation errors from a failed request (RFC 7807 `errors` bag). */
export function fieldErrors(e: unknown): FieldErrors | null {
  if (!axios.isAxiosError(e)) return null
  const data = e.response?.data
  if (data && typeof data === 'object' && data.errors && typeof data.errors === 'object') {
    return data.errors as FieldErrors
  }
  return null
}

/** First message for a field, if any server validation error exists. */
export function fieldError(e: unknown, field: string): string | null {
  return fieldErrors(e)?.[field]?.[0] ?? null
}
