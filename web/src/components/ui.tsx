import type { ReactNode } from 'react'
import { useEffect } from 'react'

const badgeClassMap: Record<string, string> = {
  // greens
  Subscribed: 'badge-green',
  Paid: 'badge-green',
  Sent: 'badge-green',
  Done: 'badge-green',
  DealClosed: 'badge-green',
  Active: 'badge-green',
  Interested: 'badge-green',
  Resolved: 'badge-green',
  // reds
  NotInterested: 'badge-red',
  Failed: 'badge-red',
  Cancelled: 'badge-red',
  Critical: 'badge-red',
  Expired: 'badge-red',
  Missed: 'badge-red',
  // ambers
  Unpaid: 'badge-amber',
  Pending: 'badge-amber',
  InProgress: 'badge-blue',
  High: 'badge-amber',
  Overdue: 'badge-red',
  // blues
  Open: 'badge-blue',
  Contacted: 'badge-blue',
  CallbackRequested: 'badge-blue',
  Medium: 'badge-purple',
  Queued: 'badge-gray',
  Low: 'badge-gray',
  InfoOnly: 'badge-gray',
  NoAnswer: 'badge-gray',
  Potential: 'badge-gray',
}

export function Badge({ value }: { value: string | null | undefined }) {
  if (!value) return <span className="badge badge-gray">-</span>
    const cls = badgeClassMap[value] ?? 'badge-gray'
  return <span className={`badge ${cls}`}>{value}</span>
}

export function Modal({
  title,
  onClose,
  children,
  wide,
}: {
  title: string
  onClose: () => void
  children: ReactNode
  wide?: boolean
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="modal-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={`modal ${wide ? 'modal-wide' : ''}`}>
        <div className="modal-head">
          <h3>{title}</h3>
          <button className="btn btn-ghost" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  )
}

export function Field({ label, children, error }: { label: string; children: ReactNode; error?: string | null }) {
  return (
    <label className={`field ${error ? 'field-error' : ''}`}>
      <span>{label}</span>
      {children}
      {error && <span className="field-err">{error}</span>}
    </label>
  )
}

export function ErrorBox({ message }: { message: string | null }) {
  if (!message) return null
  return <div className="error-box">{message}</div>
}

export function Spinner() {
  return <div className="spinner" />
}

export function Empty({ text }: { text: string }) {
  return <div className="empty">{text}</div>
}
