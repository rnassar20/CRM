import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPatch, apiPost, apiPut } from './api'
import type {
  AgentDto,
  ClientDetail,
  ClientListItem,
  DashboardStats,
  FollowUpDto,
  PagedResult,
  PlanDto,
  SubscriptionDto,
  TicketCommentDto,
  TicketDto,
  UserDto,
} from './types'

// ---------- query keys ----------

export const qk = {
  clients: (params: Record<string, unknown>) => ['clients', params] as const,
  client: (id: number) => ['client', id] as const,
  plans: ['plans'] as const,
  agents: ['agents'] as const,
  subscriptions: (params: Record<string, unknown>) => ['subscriptions', params] as const,
  tickets: (params: Record<string, unknown>) => ['tickets', params] as const,
  ticket: (id: number) => ['ticket', id] as const,
  followups: (params: Record<string, unknown>) => ['followups', params] as const,
  dashboard: ['dashboard'] as const,
  users: ['users'] as const,
}

// ---------- misc hooks ----------

/** Debounce a changing value; handy for server-side search filters. */
export function useDebouncedValue<T>(value: T, ms = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return debounced
}

// ---------- clients ----------

export function useClients(params: { q: string; type: string; status: string; page: number; pageSize: number }) {
  return useQuery({
    queryKey: qk.clients(params),
    queryFn: () => apiGet<PagedResult<ClientListItem>>('/clients', { ...params, q: params.q || undefined, type: params.type || undefined, status: params.status || undefined }),
    placeholderData: (prev) => prev,
  })
}

export function useCreateClient() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>('/clients', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['clients'] }),
  })
}

export function useClient(id: number) {
  return useQuery({ queryKey: qk.client(id), queryFn: () => apiGet<ClientDetail>(`/clients/${id}`) })
}

export function useUpdateClient(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPut<void>(`/clients/${id}`, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.client(id) })
      qc.invalidateQueries({ queryKey: ['clients'] })
    },
  })
}

export function useCreateInteraction(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>('/interactions', body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.dashboard })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

// ---------- plans / agents / users ----------

export function usePlans() {
  return useQuery({
    queryKey: qk.plans,
    queryFn: () => apiGet<PlanDto[]>('/plans').then((plans) => plans.filter((p) => p.isActive)),
  })
}

export function useAgents() {
  return useQuery({ queryKey: qk.agents, queryFn: () => apiGet<AgentDto[]>('/users/agents') })
}

export function useUsers() {
  return useQuery({ queryKey: qk.users, queryFn: () => apiGet<UserDto[]>('/users') })
}

export function useToggleUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiPatch<void>(`/users/${id}/toggle-active`),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.users }),
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: ({ id, newPassword }: { id: number; newPassword: string }) =>
      apiPatch<void>(`/users/${id}/reset-password`, { newPassword }),
  })
}

export function useCreateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { fullName: string; email: string; password: string; role: string }) =>
      apiPost<void>('/users', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.users }),
  })
}

// ---------- subscriptions ----------

export function useSubscriptions(params: {
  expiringInDays: string
  paymentStatus: string
  page: number
  pageSize: number
}) {
  return useQuery({
    queryKey: qk.subscriptions(params),
    queryFn: () =>
      apiGet<PagedResult<SubscriptionDto>>('/subscriptions', {
        expiringInDays: params.expiringInDays || undefined,
        paymentStatus: params.paymentStatus || undefined,
        page: params.page,
        pageSize: params.pageSize,
      }),
    placeholderData: (prev) => prev,
  })
}

export interface MarkPaidResult {
  subscription: SubscriptionDto
  whatsappStatus: string
  licenseKey: string
}

export function useMarkPaid(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiPost<MarkPaidResult>(`/subscriptions/${id}/mark-paid`, {}),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['subscriptions'] })
      qc.invalidateQueries({ queryKey: qk.dashboard })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

export function useCreateSubscription(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>('/subscriptions', body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['subscriptions'] })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

export function useResendKey() {
  return useMutation({
    mutationFn: (id: number) => apiPost<{ status?: string; sent?: boolean }>(`/subscriptions/${id}/resend-key`, {}),
  })
}

// ---------- tickets ----------

export function useTickets(params: { q: string; status: string; priority: string; page: number; pageSize: number }) {
  return useQuery({
    queryKey: qk.tickets(params),
    queryFn: () =>
      apiGet<PagedResult<TicketDto>>('/tickets', {
        q: params.q || undefined,
        status: params.status || undefined,
        priority: params.priority || undefined,
        page: params.page,
        pageSize: params.pageSize,
      }),
    placeholderData: (prev) => prev,
  })
}

export function useTicket(id: number) {
  return useQuery({
    queryKey: qk.ticket(id),
    queryFn: () => apiGet<{ ticket: TicketDto; comments: TicketCommentDto[] }>(`/tickets/${id}`),
  })
}

export function useCreateTicket() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>('/tickets', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tickets'] }),
  })
}

export function useUpdateTicket(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: Record<string, unknown>) => apiPut<void>(`/tickets/${id}`, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.ticket(id) })
      qc.invalidateQueries({ queryKey: ['tickets'] })
      qc.invalidateQueries({ queryKey: qk.dashboard })
    },
  })
}

export function useAddComment(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { body: string; isInternal: boolean }) => apiPost<void>(`/tickets/${id}/comments`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.ticket(id) }),
  })
}

// ---------- follow-ups / agenda ----------

/** Lightweight client dropdown options (id + name). */
export function useClientOptions() {
  return useQuery({
    queryKey: ['client-options'] as const,
    queryFn: () => apiGet<PagedResult<{ id: number; name: string }>>('/clients', { pageSize: 100 }).then((r) => r.items),
    staleTime: 60_000,
  })
}

/** Open tickets of a client, for support follow-ups. */
export function useClientTickets(clientId: number | null) {
  return useQuery({
    queryKey: ['client-tickets', clientId] as const,
    queryFn: () =>
      apiGet<PagedResult<TicketDto>>('/tickets', { clientId, pageSize: 100 }).then((r) => r.items),
    enabled: clientId != null,
    staleTime: 30_000,
  })
}

export function useFollowUps(mineOnly: boolean) {
  return useQuery({
    queryKey: qk.followups({ mineOnly }),
    queryFn: () => apiGet<FollowUpDto[]>('/followups', { mineOnly, status: '' }),
  })
}

export function useCreateFollowUp(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>('/followups', body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['followups'] })
      qc.invalidateQueries({ queryKey: qk.dashboard })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

export function useCompleteFollowUp(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiPatch<void>(`/followups/${id}/complete`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['followups'] })
      qc.invalidateQueries({ queryKey: qk.dashboard })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

export function useCancelFollowUp(clientId?: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiPatch<void>(`/followups/${id}/cancel`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['followups'] })
      qc.invalidateQueries({ queryKey: qk.dashboard })
      if (clientId) qc.invalidateQueries({ queryKey: qk.client(clientId) })
    },
  })
}

// ---------- dashboard ----------

export function useDashboard() {
  return useQuery({ queryKey: qk.dashboard, queryFn: () => apiGet<DashboardStats>('/dashboard/stats') })
}

// ---------- secondary contacts ----------

export function useCreateContact(clientId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost<void>(`/clients/${clientId}/contacts`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.client(clientId) }),
  })
}

export function useUpdateContact(clientId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: number; body: Record<string, unknown> }) =>
      apiPut<void>(`/clients/${clientId}/contacts/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.client(clientId) }),
  })
}

export function useDeleteContact(clientId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiDelete<void>(`/clients/${clientId}/contacts/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.client(clientId) }),
  })
}
