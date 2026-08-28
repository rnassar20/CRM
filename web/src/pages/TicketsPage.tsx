import { type FormEvent, useState } from 'react'
import { fieldError, errMsg } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import {
  fmtDateTime,
  type AgentDto,
  type TicketCommentDto,
  type TicketDto,
  type TicketPriority,
  type TicketStatus,
} from '../types'
import {
  useAddComment,
  useAgents,
  useClientOptions,
  useCreateTicket,
  useTicket,
  useTickets,
  useUpdateTicket,
  useDebouncedValue,
} from '../queries'

const PRIORITIES: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical']
const STATUSES: TicketStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed']

export default function TicketsPage() {
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [priority, setPriority] = useState('')
  const [page, setPage] = useState(1)
  const [showNew, setShowNew] = useState(false)
  const [openId, setOpenId] = useState<number | null>(null)

  const debouncedQ = useDebouncedValue(q)
  const { data, error, isLoading } = useTickets({ q: debouncedQ, status, priority, page, pageSize: 20 })
  const { data: agents = [] } = useAgents()

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

      {error && <ErrorBox message={errMsg(error)} />}
      {isLoading && !data && <Spinner />}

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
                  <th>Fixed in</th>
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
                    <td className="mono">{t.resolvedVersion ?? '-'}</td>
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
        <NewTicketModal agents={agents} onClose={() => setShowNew(false)} onSaved={() => setShowNew(false)} />
      )}
      {openId !== null && (
        <TicketDetailModal ticketId={openId} agents={agents} onClose={() => setOpenId(null)} />
      )}
    </>
  )
}

function NewTicketModal({ agents, onClose, onSaved }: { agents: AgentDto[]; onClose: () => void; onSaved: () => void }) {
  const { data: clients = [] } = useClientOptions()
  const [form, setForm] = useState({ clientId: '', title: '', description: '', priority: 'Medium', assignedToId: '' })
  const create = useCreateTicket()

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.clientId) return
    create.mutate(
      {
        clientId: Number(form.clientId),
        title: form.title,
        description: form.description || null,
        priority: form.priority,
        assignedToId: form.assignedToId ? Number(form.assignedToId) : null,
      },
      { onSuccess: onSaved },
    )
  }

  const e = create.error
  return (
    <Modal title="New ticket" onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        <Field label="Client *" error={fieldError(e, 'clientId')}>
          <select value={form.clientId} onChange={(ev) => setForm({ ...form, clientId: ev.target.value })} required>
            <option value="">— select —</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </Field>
        <Field label="Title *" error={fieldError(e, 'title') ?? fieldError(e, 'Title')}>
          <input value={form.title} onChange={(ev) => setForm({ ...form, title: ev.target.value })} required />
        </Field>
        <Field label="Description">
          <textarea rows={3} value={form.description} onChange={(ev) => setForm({ ...form, description: ev.target.value })} />
        </Field>
        <div className="grid-2">
          <Field label="Priority" error={fieldError(e, 'priority') ?? fieldError(e, 'Priority')}>
            <select value={form.priority} onChange={(ev) => setForm({ ...form, priority: ev.target.value })}>
              {PRIORITIES.map((p) => <option key={p}>{p}</option>)}
            </select>
          </Field>
          <Field label="Assign to" error={fieldError(e, 'assignedToId')}>
            <select value={form.assignedToId} onChange={(ev) => setForm({ ...form, assignedToId: ev.target.value })}>
              <option value="">Unassigned</option>
              {agents.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
            </select>
          </Field>
        </div>
        {errMsg(e) && <ErrorBox message={errMsg(e)} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={create.isPending}>Create ticket</button>
        </div>
      </form>
    </Modal>
  )
}

function TicketDetailModal({ ticketId, agents, onClose }: { ticketId: number; agents: AgentDto[]; onClose: () => void }) {
  const { data, error, isLoading } = useTicket(ticketId)

  if (error) return <Modal title={`Ticket #${ticketId}`} onClose={onClose}><ErrorBox message={errMsg(error)} /></Modal>
  if (isLoading || !data) return <Modal title={`Ticket #${ticketId}`} onClose={onClose}><Spinner /></Modal>

  return (
    <TicketBody
      key={ticketId}
      ticket={data.ticket}
      comments={data.comments}
      agents={agents}
      ticketId={ticketId}
      onClose={onClose}
    />
  )
}

function TicketBody({
  ticketId,
  ticket,
  comments,
  agents,
  onClose,
}: {
  ticketId: number
  ticket: TicketDto
  comments: TicketCommentDto[]
  agents: AgentDto[]
  onClose: () => void
}) {
  const [comment, setComment] = useState('')
  const [isInternal, setIsInternal] = useState(false)
  const [version, setVersion] = useState(ticket.resolvedVersion ?? '')
  const patch = useUpdateTicket(ticketId)
  const addComment = useAddComment(ticketId)

  function patchTicket(payload: Record<string, unknown>) {
    patch.mutate(payload)
  }

  function submitComment(e: FormEvent) {
    e.preventDefault()
    if (!comment.trim()) return
    addComment.mutate(
      { body: comment, isInternal },
      { onSuccess: () => setComment('') },
    )
  }

  return (
    <Modal title={`#${ticket.id} · ${ticket.title}`} onClose={onClose} wide>
      <p className="muted small">
        Client: <strong>{ticket.clientName}</strong> · opened by {ticket.createdByName} on {fmtDateTime(ticket.createdAt)}
      </p>
      {ticket.description && <p className="wrap">{ticket.description}</p>}

      <div className="grid-3">
        <Field label="Status">
          <select value={ticket.status} onChange={(e) => patchTicket({ status: e.target.value })}>
            {STATUSES.map((s) => <option key={s}>{s}</option>)}
          </select>
        </Field>
        <Field label="Priority">
          <select value={ticket.priority} onChange={(e) => patchTicket({ priority: e.target.value })}>
            {PRIORITIES.map((p) => <option key={p}>{p}</option>)}
          </select>
        </Field>
        <Field label="Assigned to">
          <select
            value={ticket.assignedToId ?? ''}
            onChange={(e) =>
              patchTicket(e.target.value === '' ? { unassign: true } : { assignedToId: Number(e.target.value) })
            }
          >
            <option value="">Unassigned</option>
            {agents.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
          </select>
        </Field>
      </div>

      {(ticket.status === 'Resolved' || ticket.status === 'Closed' || ticket.resolvedVersion) && (
        <div className="grid-2" style={{ marginTop: '0.5rem' }}>
          <Field label="Fixed in ERP build/version (recorded with the resolution)">
            <input
              placeholder="e.g. v2.4.1"
              value={version}
              onChange={(e) => setVersion(e.target.value)}
            />
          </Field>
          <div style={{ alignSelf: 'end' }}>
            <button
              className="btn btn-small btn-primary"
              onClick={() => patchTicket({ resolvedVersion: version })}
              type="button"
            >
              Save version
            </button>
          </div>
        </div>
      )}

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

      <form onSubmit={submitComment} className="add-comment">
        <textarea
          rows={2}
          placeholder="Write a comment…"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
        />
        <label className="check">
          <input type="checkbox" checked={isInternal} onChange={(e) => setIsInternal(e.target.checked)} /> internal note
        </label>
        <button className="btn btn-primary" disabled={addComment.isPending}>Add comment</button>
      </form>
      {errMsg(patch.error) && <ErrorBox message={errMsg(patch.error)} />}
    </Modal>
  )
}
