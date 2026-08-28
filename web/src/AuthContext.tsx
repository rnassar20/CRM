import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from './api'
import type { AuthResponse, UserDto } from './types'

interface AuthState {
  user: UserDto | null
  login: (email: string, password: string) => Promise<AuthResponse>
  logout: () => void
}

const AuthContext = createContext<AuthState>(null!)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(() => {
    try {
      const raw = localStorage.getItem('crm_user')
      return raw ? (JSON.parse(raw) as UserDto) : null
    } catch {
      return null
    }
  })

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.post<AuthResponse>('/auth/login', { email, password })
    localStorage.setItem('crm_token', res.data.token)
    localStorage.setItem('crm_user', JSON.stringify(res.data.user))
    setUser(res.data.user)
    return res.data
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('crm_token')
    localStorage.removeItem('crm_user')
    setUser(null)
  }, [])

  const value = useMemo(() => ({ user, login, logout }), [user, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  return useContext(AuthContext)
}
