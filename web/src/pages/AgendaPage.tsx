import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { fieldError, errMsg, fieldErrors } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import { daysUntil, fmtDateTime, toLocalInputPlusDays, type AgentDto, type FollowUpDto, type FollowUpType } from '../types'
import {
  useAgents,
  useCancelFollowUp,
  useClientOptions,
  useClientTickets,
  useCompleteFollowUp,
  useCreateFollowUp,
  useFollowUps,
} from '../queries'

const FOLLOWUP_TYPES: FollowUpType[] = ['Marketing', 'Internal', 'Support']

export default function AgendaPage() {
  const [mineOnly, setMineOnly] = useState(false)
  const [showNew, setShowNew] = useState(false)
  const { data: items, error, isLoading } = useFollowUps(mineOnly)
  const { data: agents = [] } = useAgents()

  const complete = useCompleteFollowUp()
  const cancel = useCancelFollowUp()

  function act(id: number, action: 'complete' | 'cancel') {
    if (action === 'complete') complete.mutate(id)
    else cancel.mutate(id)
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

      {error && <ErrorBox message={errMsg(error)} />}
      {errMsg(complete.error) && <ErrorBox message={errMsg(complete.error)} />}
      {isLoading && !items && <Spinner />}

      {items && (
        <>
          <Section title="Overdue" tone="red" rows={groups.overdue} onAct={act} />
          <Section title="Today" tone="blue" rows={groups.today} onAct={act} />
          <Section title="Upcoming" rows={groups.upcoming} onAct={act} />
          <Section title="Done / cancelled / missed" muted rows={groups.closed} onAct={act} />
        </>
      )}

      {showNew && <NewFollowUpModal agents={agents} onClose={() => setShowNew(false)} onSaved={() => setShowNew(false)} />}
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
  const { data: clients = [] } = useClientOptions()
  const [form, setForm] = useState({
    clientId: '',
    title: '',
    description: '',
    scheduledAt: toLocalInputPlusDays(1),
    assignedToId: '',
    type: 'Marketing' as FollowUpType,
    ticketId: '',
  })
  const { data: tickets = [] } = useClientTickets(form.type === 'Support' ? Number(form.clientId) || null : null)
  const create = useCreateFollowUp()

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.clientId) return
    if (form.type === 'Support' && !form.ticketId) return
    create.mutate(
      {
        clientId: Number(form.clientId),
        title: form.title,
        description: form.description || null,
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        assignedToId: form.assignedToId ? Number(form.assignedToId) : undefined,
        type: form.type,
        ticketId: form.type === 'Support' && form.ticketId ? Number(form.ticketId) : null,
      },
      { onSuccess: onSaved },
    )
  }

  const e = create.error
  const globalErr = fieldErrors(e) ? null : errMsg(e)

  return (
    <Modal title="Schedule follow-up" onClose={onClose}>
      <p className="muted small">
        Marketing = sales/outreach · Internal = build/version work · Support = linked to an open ticket.
      </p>
      <form onSubmit={submit} className="form-grid">
        <Field label="Client *" error={fieldError(e, 'clientId')}>
          <select value={form.clientId} onChange={(ev) => setForm({ ...form, clientId: ev.target.value, ticketId: '' })} required>
            <option value="">— select —</option>
            {clients.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Type">
          <select value={form.type} onChange={(ev) => setForm({ ...form, type: ev.target.value as FollowUpType, ticketId: '' })}>
            {FOLLOWUP_TYPES.map((t) => (
              <option key={t}>{t}{t === 'Internal' ? ' (build/version)' : t === 'Support' ? ' (ticket)' : ''}</option>
            ))}
          </select>
        </Field>
        {form.type === 'Support' && (
          <Field label="Ticket *" error={fieldError(e, 'ticketId')}>
            <select value={form.ticketId} onChange={(ev) => setForm({ ...form, ticketId: ev.target.value })}>
              <option value="">— select ticket —</option>
              {tickets.map((t) => (
                <option key={t.id} value={t.id}>#{t.id} · {t.title} ({t.status})</option>
              ))}
            </select>
          </Field>
        )}
        <Field label="Title *" error={fieldError(e, 'title') ?? fieldError(e, 'Title')}>
          <input value={form.title} onChange={(ev) => setForm({ ...form, title: ev.target.value })} required />
        </Field>
        <Field label="When *" error={fieldError(e, 'scheduledAt') ?? fieldError(e, 'ScheduledAt')}>
          <input
            type="datetime-local"
            value={form.scheduledAt}
            onChange={(ev) => setForm({ ...form, scheduledAt: ev.target.value })}
            required
          />
        </Field>
        <Field label="Assign to">
          <select value={form.assignedToId} onChange={(ev) => setForm({ ...form, assignedToId: ev.target.value })}>
            <option value="">Me</option>
            {agents.map((a) => (
              <option key={a.id} value={a.id}>{a.fullName}</option>
            ))}
          </select>
        </Field>
        <Field label="Description">
          <textarea rows={2} value={form.description} onChange={(ev) => setForm({ ...form, description: ev.target.value })} />
        </Field>
        {globalErr && <ErrorBox message={globalErr} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={create.isPending}>Schedule</button>
        </div>
      </form>
    </Modal>
  )
}
