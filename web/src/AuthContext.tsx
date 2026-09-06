import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from './api'
import type { AuthResponse, UserDto } from './types'

interface AuthState {
  user: UserDto | null
  /** true until the initial /auth/me call resolves, so the UI doesn't flash the login page */
  loading: boolean
  login: (email: string, password: string) => Promise<AuthResponse>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthState>(null!)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [loading, setLoading] = useState(true)

  // Restore the session from the HttpOnly cookie on first load (the token is not in JS storage).
  useEffect(() => {
    let active = true
    api
      .get<UserDto>('/auth/me')
      .then((res) => {
        if (active) setUser(res.data)
      })
      .catch(() => {
        if (active) setUser(null)
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.post<AuthResponse>('/auth/login', { email, password })
    setUser(res.data.user)
    return res.data
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout')
    } catch {
      // best-effort; clear client state regardless
    }
    setUser(null)
  }, [])

  const value = useMemo(() => ({ user, loading, login, logout }), [user, loading, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  return useContext(AuthContext)
}
