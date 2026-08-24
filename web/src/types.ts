export type Role = 'Admin' | 'Agent'
export type ClientType = 'Pharmacy' | 'GiftShop' | 'DoctorClinic' | 'Hospital' | 'Other'
export type ClientStatus = 'Potential' | 'Contacted' | 'Interested' | 'NotInterested' | 'Subscribed'
export type PaymentStatus = 'Unpaid' | 'Paid'
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
export type InteractionType = 'Call' | 'WhatsApp' | 'Email' | 'Visit' | 'Sms'
export type InteractionOutcome =
  | 'NoAnswer'
  | 'CallbackRequested'
  | 'Interested'
  | 'NotInterested'
  | 'DealClosed'
  | 'InfoOnly'
export type FollowUpStatus = 'Pending' | 'Done' | 'Missed' | 'Cancelled'

export interface UserDto {
  id: number
  fullName: string
  email: string
  role: Role
  isActive: boolean
  createdAt: string
}

export interface AgentDto {
  id: number
  fullName: string
  role: Role
}

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  user: UserDto
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface ClientListItem {
  id: number
  name: string
  contactPerson: string
  phone: string
  email: string | null
  city: string | null
  type: ClientType
  status: ClientStatus
  subscriptionId: number | null
  planName: string | null
  expiryDate: string | null
  paymentStatus: PaymentStatus | null
}

export interface SubscriptionDto {
  id: number
  clientId: number
  clientName: string
  clientPhone: string
  planId: number
  planName: string
  startDate: string
  expiryDate: string
  price: number
  paymentStatus: PaymentStatus
  paymentMethod: string | null
  paidAt: string | null
  licenseKey: string | null
  licenseKeyIssuedAt: string | null
  notes: string | null
  createdAt: string
}

export interface PlanDto {
  id: number
  name: string
  durationDays: number
  price: number
  isActive: boolean
}

export interface InteractionDto {
  id: number
  clientId: number
  clientName: string
  type: InteractionType
  outcome: InteractionOutcome
  notes: string | null
  nextFollowUpAt: string | null
  userId: number
  userName: string
  createdAt: string
}

export interface FollowUpDto {
  id: number
  clientId: number
  clientName: string
  title: string
  description: string | null
  scheduledAt: string
  status: FollowUpStatus
  assignedToId: number
  assignedToName: string
  reminderSentAt: string | null
  createdAt: string
}

export interface TicketDto {
  id: number
  clientId: number
  clientName: string
  title: string
  description: string | null
  priority: TicketPriority
  status: TicketStatus
  assignedToId: number | null
  assignedToName: string | null
  createdByName: string
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  commentCount: number
}

export interface TicketCommentDto {
  id: number
  userId: number
  userName: string
  body: string
  isInternal: boolean
  createdAt: string
}

export interface DashboardStats {
  clientsTotal: number
  clientsByStatus: Record<string, number>
  clientsByType: Record<string, number>
  subscriptionsActive: number
  subscriptionsExpiringIn30: number
  subscriptionsExpired: number
  subscriptionsUnpaidActive: number
  ticketsByStatus: Record<string, number>
  ticketsOpen: number
  followUpsToday: number
  followUpsOverdue: number
  whatsAppSentLast30Days: number
  upcomingFollowUps: FollowUpDto[]
  recentInteractions: InteractionDto[]
}

// ---------- helpers ----------

export function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** days from now until iso date (negative = overdue) */
export function daysUntil(iso: string): number {
  const target = new Date(iso)
  target.setHours(0, 0, 0, 0)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return Math.round((target.getTime() - today.getTime()) / 86_400_000)
}

/** format a Date for <input type="datetime-local"> */
export function toLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function toLocalInputPlusDays(days: number, hour = 10): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  d.setHours(hour, 0, 0, 0)
  return toLocalInput(d)
}
