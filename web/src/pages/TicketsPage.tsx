import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { api, errMsg } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import {
  fmtDateTime,
  type AgentDto,
  type PagedResult,
  type TicketCommentDto,
  type TicketDto,
  type TicketPriority,
  type TicketStatus,
} from '../types'

const PRIORITIES: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical']
const STATUSES: TicketStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed']

export default function TicketsPage() {
  const [data, setData] = useState<PagedResult<TicketDto> | null>(null)
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [priority, setPriority] = useState('')
  const [page, setPage] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const [showNew, setShowNew] = useState(false)
  const [openId, setOpenId] = useState<number | null>(null)

  useEffect(() => {
    api.get<AgentDto[]>('/users/agents').then((r) => setAgents(r.data)).catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setError(null)
    try {
      const res = await api.get<PagedResult<TicketDto>>('/tickets', {
        params: { q: q || undefined, status: status || undefined, priority: priority || undefined, page, pageSize: 20 },
      })
      setData(res.data)
    } catch (e) {
      setError(errMsg(e))
    }
  }, [q, status, priority, page])

  useEffect(() => {
    const t = setTimeout(load, q ? 300 : 0)
    return () => clearTimeout(t)
  }, [load, q])

  return (
    <>
      <div className="page-head">
        <h1>Support tickets</h1>
        <button className="btn btn-primary" onClick={() => setShowNew(true)}>+ New ticket</button>
      </div>

      <div className="filters card">
        <input placeholder="Search title…" value={q} onChange={(e) => { setQ(e.target.value); setPage(1) }} />
        <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          <option value="">All statuses</option>
          {STATUSES.map((s) => <option key={s}>{s}</option>)}
        </select>
        <select value={priority} onChange={(e) => { setPriority(e.target.value); setPage(1) }}>
          <option value="">All priorities</option>
          {PRIORITIES.map((p) => <option key={p}>{p}</option>)}
        </select>
      </div>

      {error && <ErrorBox message={error} />}
      {!data && !error && <Spinner />}

      {data && (
        <>
          {data.items.length === 0 ? (
            <Empty text="No tickets found." />
          ) : (
            <table className="table card row-click">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Title</th>
                  <th>Client</th>
                  <th>Priority</th>
                  <th>Status</th>
                  <th>Assigned</th>
                  <th>Updated</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((t) => (
                  <tr key={t.id} onClick={() => setOpenId(t.id)}>
                    <td>{t.id}</td>
                    <td className="strong">{t.title}{t.commentCount > 0 && <span className="muted small"> ({t.commentCount})</span>}</td>
                    <td>{t.clientName}</td>
                    <td><Badge value={t.priority} /></td>
                    <td><Badge value={t.status} /></td>
                    <td>{t.assignedToName ?? <span className="muted">unassigned</span>}</td>
                    <td className="nowrap">{fmtDateTime(t.updatedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <div className="pager">
            <button className="btn" disabled={page <= 1} onClick={() => setPage(page - 1)}>← Prev</button>
            <span className="muted">{data.total} ticket(s)</span>
            <button className="btn" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>Next →</button>
          </div>
        </>
      )}

      {showNew && (
        <NewTicketModal agents={agents} onClose={() => setShowNew(false)} onSaved={() => { setShowNew(false); load() }} />
      )}
      {openId !== null && (
        <TicketDetailModal ticketId={openId} agents={agents} onClose={() => setOpenId(null)} onChanged={load} />
      )}
    </>
  )
}

function NewTicketModal({ agents, onClose, onSaved }: { agents: AgentDto[]; onClose: () => void; onSaved: () => void }) {
  const [clients, setClients] = useState<{ id: number; name: string }[]>([])
  const [form, setForm] = useState({ clientId: '', title: '', description: '', priority: 'Medium', assignedToId: '' })
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<PagedResult<{ id: number; name: string }>>('/clients', { params: { pageSize: 100 } })
      .then((r) => setClients(r.data.items))
      .catch(() => {})
  }, [])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.clientId) return setError('Select a client.')
    setError(null)
    try {
      await api.post('/tickets', {
        clientId: Number(form.clientId),
        title: form.title,
        description: form.description || null,
        priority: form.priority,
        assignedToId: form.assignedToId ? Number(form.assignedToId) : null,
      })
      onSaved()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <Modal title="New ticket" onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        <Field label="Client *">
          <select value={form.clientId} onChange={(e) => setForm({ ...form, clientId: e.target.value })} required>
            <option value="">— select —</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </Field>
        <Field label="Title *">
          <input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
        </Field>
        <Field label="Description">
          <textarea rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        <div className="grid-2">
          <Field label="Priority">
            <select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}>
              {PRIORITIES.map((p) => <option key={p}>{p}</option>)}
            </select>
          </Field>
          <Field label="Assign to">
            <select value={form.assignedToId} onChange={(e) => setForm({ ...form, assignedToId: e.target.value })}>
              <option value="">Unassigned</option>
              {agents.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
            </select>
          </Field>
        </div>
        {error && <ErrorBox message={error} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary">Create ticket</button>
        </div>
      </form>
    </Modal>
  )
}

function TicketDetailModal({ ticketId, agents, onClose, onChanged }: { ticketId: number; agents: AgentDto[]; onClose: () => void; onChanged: () => void }) {
  const [ticket, setTicket] = useState<TicketDto | null>(null)
  const [comments, setComments] = useState<TicketCommentDto[]>([])
  const [comment, setComment] = useState('')
  const [isInternal, setIsInternal] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      const res = await api.get(`/tickets/${ticketId}`)
      setTicket(res.data.ticket)
      setComments(res.data.comments)
    } catch (e) {
      setError(errMsg(e))
    }
  }, [ticketId])

  useEffect(() => {
    load()
  }, [load])

  async function patch(payload: Record<string, unknown>) {
    setError(null)
    try {
      await api.put(`/tickets/${ticketId}`, payload)
      await load()
      onChanged()
    } catch (e) {
      setError(errMsg(e))
    }
  }

  async function addComment(e: FormEvent) {
    e.preventDefault()
    if (!comment.trim()) return
    try {
      await api.post(`/tickets/${ticketId}/comments`, { body: comment, isInternal })
      setComment('')
      await load()
      onChanged()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  if (!ticket) return <Modal title={`Ticket #${ticketId}`} onClose={onClose}><Spinner /></Modal>

  return (
    <Modal title={`#${ticket.id} · ${ticket.title}`} onClose={onClose} wide>
      <p className="muted small">
        Client: <strong>{ticket.clientName}</strong> · opened by {ticket.createdByName} on {fmtDateTime(ticket.createdAt)}
      </p>
      {ticket.description && <p className="wrap">{ticket.description}</p>}

      <div className="grid-3">
        <Field label="Status">
          <select value={ticket.status} onChange={(e) => patch({ status: e.target.value })}>
            {STATUSES.map((s) => <option key={s}>{s}</option>)}
          </select>
        </Field>
        <Field label="Priority">
          <select value={ticket.priority} onChange={(e) => patch({ priority: e.target.value })}>
            {PRIORITIES.map((p) => <option key={p}>{p}</option>)}
          </select>
        </Field>
        <Field label="Assigned to">
          <select
            value={ticket.assignedToId ?? ''}
            onChange={(e) =>
              patch(e.target.value === '' ? { unassign: true } : { assignedToId: Number(e.target.value) })
            }
          >
            <option value="">Unassigned</option>
            {agents.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
          </select>
        </Field>
      </div>

      <h3>Comments ({comments.length})</h3>
      <div className="comments">
        {comments.length === 0 && <Empty text="No comments yet." />}
        {comments.map((c) => (
          <div key={c.id} className={`comment ${c.isInternal ? 'comment-internal' : ''}`}>
            <div className="comment-meta">
              <strong>{c.userName}</strong> · {fmtDateTime(c.createdAt)}
              {c.isInternal && <Badge value="internal" />}
            </div>
            <div>{c.body}</div>
          </div>
        ))}
      </div>

      <form onSubmit={addComment} className="add-comment">
        <textarea
          rows={2}
          placeholder="Write a comment…"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
        />
        <label className="check">
          <input type="checkbox" checked={isInternal} onChange={(e) => setIsInternal(e.target.checked)} /> internal note
        </label>
        <button className="btn btn-primary">Add comment</button>
      </form>
      {error && <ErrorBox message={error} />}
    </Modal>
  )
}
