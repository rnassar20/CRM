import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, errMsg } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import { daysUntil, fmtDateTime, toLocalInputPlusDays, type AgentDto, type FollowUpDto, type FollowUpType, type PagedResult, type TicketDto } from '../types'

const FOLLOWUP_TYPES: FollowUpType[] = ['Marketing', 'Internal', 'Support']

export default function AgendaPage() {
  const [items, setItems] = useState<FollowUpDto[] | null>(null)
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [mineOnly, setMineOnly] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showNew, setShowNew] = useState(false)

  const load = useCallback(async () => {
    setError(null)
    try {
      const res = await api.get<FollowUpDto[]>('/followups', {
        params: { mineOnly, status: '' },
      })
      setItems(res.data)
    } catch (e) {
      setError(errMsg(e))
    }
  }, [mineOnly])

  useEffect(() => {
    load()
  }, [load])

  useEffect(() => {
    api.get<AgentDto[]>('/users/agents').then((r) => setAgents(r.data)).catch(() => {})
  }, [])

  async function act(id: number, action: 'complete' | 'cancel') {
    try {
      await api.patch(`/followups/${id}/${action}`)
      load()
    } catch (e) {
      setError(errMsg(e))
    }
  }

  const groups = splitGroups(items ?? [])

  return (
    <>
      <div className="page-head">
        <h1>Agenda</h1>
        <div className="head-actions">
          <label className="check">
            <input type="checkbox" checked={mineOnly} onChange={(e) => setMineOnly(e.target.checked)} /> only mine
          </label>
          <button className="btn btn-primary" onClick={() => setShowNew(true)}>+ Schedule follow-up</button>
        </div>
      </div>

      {error && <ErrorBox message={error} />}
      {!items && !error && <Spinner />}

      {items && (
        <>
          <Section title="Overdue" tone="red" rows={groups.overdue} onAct={act} />
          <Section title="Today" tone="blue" rows={groups.today} onAct={act} />
          <Section title="Upcoming" rows={groups.upcoming} onAct={act} />
          <Section title="Done / cancelled / missed" muted rows={groups.closed} onAct={act} />
        </>
      )}

      {showNew && <NewFollowUpModal agents={agents} onClose={() => setShowNew(false)} onSaved={() => { setShowNew(false); load() }} />}
    </>
  )
}

type Group = { overdue: FollowUpDto[]; today: FollowUpDto[]; upcoming: FollowUpDto[]; closed: FollowUpDto[] }

function splitGroups(rows: FollowUpDto[]): Group {
  const g: Group = { overdue: [], today: [], upcoming: [], closed: [] }
  for (const r of rows) {
    if (r.status !== 'Pending') g.closed.push(r)
    else if (daysUntil(r.scheduledAt) < 0) g.overdue.push(r)
    else if (daysUntil(r.scheduledAt) === 0) g.today.push(r)
    else g.upcoming.push(r)
  }
  return g
}

function Section({
  title,
  rows,
  tone,
  muted,
  onAct,
}: {
  title: string
  rows: FollowUpDto[]
  tone?: string
  muted?: boolean
  onAct: (id: number, action: 'complete' | 'cancel') => void
}) {
  if (rows.length === 0 && !muted) return null
  return (
    <section className={`card ${tone ? `card-${tone}` : ''}`}>
      <div className="card-head">
        <h2>{title}</h2>
        <span className="muted">{rows.length}</span>
      </div>
      {rows.length === 0 ? (
        <Empty text="Nothing here." />
      ) : (
        <table className="table">
          <tbody>
            {rows.map((f) => (
              <tr key={f.id}>
                <td className="nowrap strong">{fmtDateTime(f.scheduledAt)}</td>
                <td>
                  <Link to={`/clients/${f.clientId}`} className="strong">{f.clientName}</Link>
                  <span> — {f.title}</span>{' '}
                  <span className="badge badge-gray">{f.type}</span>
                  {f.ticketId && (
                    <span className="muted small" title={f.ticketTitle ?? ''}> · ticket #{f.ticketId}</span>
                  )}
                  {f.description && <div className="muted small wrap">{f.description}</div>}
                </td>
                <td>{f.assignedToName}</td>
                <td><Badge value={f.status} /></td>
                <td className="nowrap">
                  {f.status === 'Pending' && (
                    <>
                      <button className="btn btn-small" onClick={() => onAct(f.id, 'complete')}>✓ Done</button>{' '}
                      <button className="btn btn-small" onClick={() => onAct(f.id, 'cancel')}>✕ Cancel</button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

function NewFollowUpModal({ agents, onClose, onSaved }: { agents: AgentDto[]; onClose: () => void; onSaved: () => void }) {
  const [clients, setClients] = useState<{ id: number; name: string }[]>([])
  const [tickets, setTickets] = useState<TicketDto[]>([])
  const [form, setForm] = useState({
    clientId: '',
    title: '',
    description: '',
    scheduledAt: toLocalInputPlusDays(1),
    assignedToId: '',
    type: 'Marketing' as FollowUpType,
    ticketId: '',
  })
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<PagedResult<{ id: number; name: string }>>('/clients', { params: { pageSize: 100 } })
      .then((r) => setClients(r.data.items))
      .catch(() => {})
  }, [])

  // support follow-ups need a ticket to reference - load the client's tickets
  useEffect(() => {
    setTickets([])
    setForm((f) => ({ ...f, ticketId: '' }))
    if (form.type === 'Support' && form.clientId) {
      api
        .get<PagedResult<TicketDto>>('/tickets', { params: { clientId: Number(form.clientId), pageSize: 100 } })
        .then((r) => setTickets(r.data.items))
        .catch(() => {})
    }
  }, [form.type, form.clientId])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.clientId) return setError('Select a client.')
    if (form.type === 'Support' && !form.ticketId) return setError('Support follow-ups need a ticket.')
    setError(null)
    try {
      await api.post('/followups', {
        clientId: Number(form.clientId),
        title: form.title,
        description: form.description || null,
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        assignedToId: form.assignedToId ? Number(form.assignedToId) : undefined,
        type: form.type,
        ticketId: form.type === 'Support' && form.ticketId ? Number(form.ticketId) : null,
      })
      onSaved()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <Modal title="Schedule follow-up" onClose={onClose}>
      <p className="muted small">
        Marketing = sales/outreach · Internal = build/version work · Support = linked to an open ticket.
      </p>
      <form onSubmit={submit} className="form-grid">
        <Field label="Client *">
          <select value={form.clientId} onChange={(e) => setForm({ ...form, clientId: e.target.value })} required>
            <option value="">— select —</option>
            {clients.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Type">
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value as FollowUpType })}>
            {FOLLOWUP_TYPES.map((t) => (
              <option key={t}>{t}{t === 'Internal' ? ' (build/version)' : t === 'Support' ? ' (ticket)' : ''}</option>
            ))}
          </select>
        </Field>
        {form.type === 'Support' && (
          <Field label="Ticket *">
            <select value={form.ticketId} onChange={(e) => setForm({ ...form, ticketId: e.target.value })}>
              <option value="">— select ticket —</option>
              {tickets.map((t) => (
                <option key={t.id} value={t.id}>#{t.id} · {t.title} ({t.status})</option>
              ))}
            </select>
          </Field>
        )}
        <Field label="Title *">
          <input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
        </Field>
        <Field label="When *">
          <input
            type="datetime-local"
            value={form.scheduledAt}
            onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })}
            required
          />
        </Field>
        <Field label="Assign to">
          <select value={form.assignedToId} onChange={(e) => setForm({ ...form, assignedToId: e.target.value })}>
            <option value="">Me</option>
            {agents.map((a) => (
              <option key={a.id} value={a.id}>{a.fullName}</option>
            ))}
          </select>
        </Field>
        <Field label="Description">
          <textarea rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        {error && <ErrorBox message={error} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary">Schedule</button>
        </div>
      </form>
    </Modal>
  )
}
