import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, errMsg } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import {
  daysUntil,
  fmtDate,
  fmtDateTime,
  toLocalInputPlusDays,
  type ClientContact,
  type ClientType,
  type ClientStatus,
  type FollowUpType,
  type InteractionOutcome,
  type InteractionType,
  type PaymentInfo,
  type PlanDto,
  type SubscriptionDto,
} from '../types'

const CLIENT_TYPES: ClientType[] = ['Pharmacy', 'GiftShop', 'DoctorClinic', 'Hospital', 'Other']
const CLIENT_STATUSES: ClientStatus[] = ['Potential', 'Contacted', 'Interested', 'NotInterested', 'Subscribed']
const INTERACTION_TYPES: InteractionType[] = ['Call', 'WhatsApp', 'Email', 'Visit', 'Sms']
const OUTCOMES: InteractionOutcome[] = ['NoAnswer', 'CallbackRequested', 'Interested', 'NotInterested', 'DealClosed', 'InfoOnly']

type ClientDetail = {
  id: number
  name: string
  contactPerson: string
  phone: string
  email: string | null
  address: string | null
  city: string | null
  type: ClientType
  status: ClientStatus
  notes: string | null
  createdAt: string
  contacts: ClientContact[]
  payments: PaymentInfo[]
  subscriptions: SubscriptionDto[]
  interactions: import('../types').InteractionDto[]
  tickets: import('../types').TicketDto[]
  followUps: import('../types').FollowUpDto[]
}

export default function ClientDetailPage() {
  const { id } = useParams()
  const [client, setClient] = useState<ClientDetail | null>(null)
  const [plans, setPlans] = useState<PlanDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<'interactions' | 'agenda' | 'subscriptions' | 'payments' | 'contacts' | 'tickets'>('interactions')

  const load = useCallback(async () => {
    try {
      const res = await api.get<ClientDetail>(`/clients/${id}`)
      setClient(res.data)
    } catch (e) {
      setError(errMsg(e))
    }
  }, [id])

  useEffect(() => {
    load()
    api.get<PlanDto[]>('/plans').then((r) => setPlans(r.data.filter((p) => p.isActive))).catch(() => {})
  }, [load])

  if (error) return <ErrorBox message={error} />
  if (!client) return <Spinner />

  return (
    <>
      <div className="page-head">
        <div>
          <Link to="/clients" className="muted small">← All clients</Link>
          <h1>
            {client.name} <Badge value={client.status} /> <span className="badge badge-gray">{client.type}</span>
          </h1>
          <p className="muted">
            {client.contactPerson || '-'} · {client.phone}
            {client.email ? ` · ${client.email}` : ''} {client.city ? `· ${client.city}` : ''}
          </p>
        </div>
        <EditClientButton client={client} onSaved={load} />
      </div>

      <div className="tabs">
        {(['interactions', 'agenda', 'subscriptions', 'payments', 'contacts', 'tickets'] as const).map((t) => (
          <button key={t} className={`tab ${tab === t ? 'tab-active' : ''}`} onClick={() => setTab(t)}>
            {t === 'interactions' ? `Call log (${client.interactions.length})` : t.charAt(0).toUpperCase() + t.slice(1)}
            {t === 'agenda' && ` (${client.followUps.length})`}
            {t === 'subscriptions' && ` (${client.subscriptions.length})`}
            {t === 'payments' && ` (${client.payments.length})`}
            {t === 'contacts' && ` (${client.contacts.length})`}
            {t === 'tickets' && ` (${client.tickets.length})`}
          </button>
        ))}
      </div>

      {tab === 'interactions' && <InteractionsTab client={client} reload={load} />}
      {tab === 'agenda' && <AgendaTab client={client} reload={load} />}
      {tab === 'subscriptions' && <SubscriptionsTab client={client} plans={plans} reload={load} />}
      {tab === 'payments' && <PaymentsTab client={client} />}
      {tab === 'contacts' && <ContactsTab client={client} reload={load} />}
      {tab === 'tickets' && <TicketsTab client={client} />}
    </>
  )
}

/* ---------------- Call log ---------------- */

function InteractionsTab({ client, reload }: { client: ClientDetail; reload: () => void }) {
  const [form, setForm] = useState({
    type: 'Call',
    outcome: 'Interested',
    notes: '',
    nextFollowUpAt: '',
    newClientStatus: '',
  })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.post('/interactions', {
        clientId: client.id,
        type: form.type,
        outcome: form.outcome,
        notes: form.notes || null,
        nextFollowUpAt: form.nextFollowUpAt ? new Date(form.nextFollowUpAt).toISOString() : null,
        newClientStatus: form.newClientStatus || null,
      })
      setForm({ ...form, notes: '', nextFollowUpAt: '', newClientStatus: '' })
      reload()
    } catch (err) {
      setError(errMsg(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <section className="card">
        <h2>Log interaction</h2>
        <form onSubmit={submit} className="form-grid form-grid-4">
          <Field label="Type">
            <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
              {INTERACTION_TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </Field>
          <Field label="Outcome">
            <select value={form.outcome} onChange={(e) => setForm({ ...form, outcome: e.target.value })}>
              {OUTCOMES.map((o) => (
                <option key={o}>{o}</option>
              ))}
            </select>
          </Field>
          <Field label="Call back at (creates agenda entry)">
            <input
              type="datetime-local"
              value={form.nextFollowUpAt}
              onChange={(e) => setForm({ ...form, nextFollowUpAt: e.target.value })}
            />
          </Field>
          <Field label="Move status to">
            <select value={form.newClientStatus} onChange={(e) => setForm({ ...form, newClientStatus: e.target.value })}>
              <option value="">(auto)</option>
              {CLIENT_STATUSES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </Field>
          <Field label="Notes">
            <textarea rows={2} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
          </Field>
          {error && <ErrorBox message={error} />}
          <div className="modal-actions">
            <button className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save interaction'}</button>
          </div>
        </form>
      </section>

      <section className="card">
        <h2>History</h2>
        {client.interactions.length === 0 ? (
          <Empty text="No interactions yet." />
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Type</th>
                <th>Outcome</th>
                <th>Next follow-up</th>
                <th>Notes</th>
                <th>By</th>
              </tr>
            </thead>
            <tbody>
              {client.interactions.map((i) => (
                <tr key={i.id}>
                  <td className="nowrap">{fmtDateTime(i.createdAt)}</td>
                  <td>{i.type}</td>
                  <td><Badge value={i.outcome} /></td>
                  <td className="nowrap">{i.nextFollowUpAt ? fmtDateTime(i.nextFollowUpAt) : '-'}</td>
                  <td className="wrap">{i.notes}</td>
                  <td>{i.userName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  )
}

/* ---------------- Agenda ---------------- */

function AgendaTab({ client, reload }: { client: ClientDetail; reload: () => void }) {
  const [form, setForm] = useState({
    title: '',
    description: '',
    scheduledAt: toLocalInputPlusDays(1),
    type: 'Marketing' as FollowUpType,
    ticketId: '',
  })
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (form.type === 'Support' && !form.ticketId) return setError('Support follow-ups need a ticket.')
    setError(null)
    try {
      await api.post('/followups', {
        clientId: client.id,
        title: form.title,
        description: form.description || null,
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        type: form.type,
        ticketId: form.type === 'Support' && form.ticketId ? Number(form.ticketId) : null,
      })
      setForm({ ...form, title: '', description: '', ticketId: '' })
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  async function act(id: number, action: 'complete' | 'cancel') {
    try {
      await api.patch(`/followups/${id}/${action}`)
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <>
      <section className="card">
        <h2>Schedule follow-up</h2>
        <p className="muted small">
          Marketing = sales/outreach · Internal = build/version work · Support = linked to one of this client's tickets.
        </p>
        <form onSubmit={submit} className="form-grid form-grid-4">
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
          <Field label="Type">
            <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value as FollowUpType })}>
              {(['Marketing', 'Internal', 'Support'] as FollowUpType[]).map((t) => (
                <option key={t}>{t}{t === 'Internal' ? ' (build/version)' : t === 'Support' ? ' (ticket)' : ''}</option>
              ))}
            </select>
          </Field>
          {form.type === 'Support' && (
            <Field label="Ticket *">
              <select value={form.ticketId} onChange={(e) => setForm({ ...form, ticketId: e.target.value })}>
                <option value="">— select ticket —</option>
                {client.tickets.map((t) => (
                  <option key={t.id} value={t.id}>#{t.id} · {t.title} ({t.status})</option>
                ))}
              </select>
            </Field>
          )}
          <Field label="Description">
            <textarea rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })}
              placeholder={form.type === 'Internal' ? 'Build/version notes…' : undefined} />
          </Field>
          {error && <ErrorBox message={error} />}
          <div className="modal-actions">
            <button className="btn btn-primary">Add to agenda</button>
          </div>
        </form>
      </section>

      <section className="card">
        <h2>Entries</h2>
        {client.followUps.length === 0 ? (
          <Empty text="Nothing scheduled." />
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>When</th>
                <th>Title</th>
                <th>Type</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {client.followUps.map((f) => (
                <tr key={f.id} className={f.status === 'Pending' && daysUntil(f.scheduledAt) < 0 ? 'row-overdue' : ''}>
                  <td className="nowrap">{fmtDateTime(f.scheduledAt)}</td>
                  <td className="wrap">
                    {f.title}
                    {f.ticketId && <span className="muted small"> · ticket #{f.ticketId}</span>}
                    {f.description && <div className="muted small">{f.description}</div>}
                  </td>
                  <td><span className="badge badge-gray">{f.type}</span></td>
                  <td><Badge value={f.status} /></td>
                  <td className="nowrap">
                    {f.status === 'Pending' && (
                      <>
                        <button className="btn btn-small" onClick={() => act(f.id, 'complete')}>✓ Done</button>{' '}
                        <button className="btn btn-small" onClick={() => act(f.id, 'cancel')}>Cancel</button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  )
}

/* ---------------- Payment history ---------------- */

function PaymentsTab({ client }: { client: ClientDetail }) {
  return client.payments.length === 0 ? (
    <Empty text="No payments recorded yet. Mark a subscription as paid to build the history." />
  ) : (
    <>
      <div className="page-head">
        <h2 style={{ margin: 0 }}>Payment history</h2>
        <span className="muted">
          Total collected:{' '}
          <strong>{client.payments.reduce((sum, p) => sum + Number(p.amount), 0).toLocaleString()}</strong>
        </span>
      </div>
      <table className="table card">
        <thead>
          <tr>
            <th>Paid at</th>
            <th>Plan</th>
            <th>Cycle</th>
            <th>Period</th>
            <th>Amount</th>
            <th>Method</th>
            <th>License key</th>
          </tr>
        </thead>
        <tbody>
          {client.payments.map((p) => (
            <tr key={p.subscriptionId}>
              <td className="nowrap strong">{fmtDateTime(p.paidAt)}</td>
              <td>{p.planName}</td>
              <td><span className="badge badge-gray">{p.cycle}</span></td>
              <td className="nowrap">{fmtDate(p.startDate)} → {fmtDate(p.expiryDate)}</td>
              <td className="strong">{Number(p.amount).toLocaleString()}</td>
              <td>{p.paymentMethod ?? '-'}</td>
              <td className="mono small wrap">{p.licenseKey ?? '-'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  )
}

/* ---------------- Secondary contacts ---------------- */

type ContactForm = { name: string; phone: string; email: string; notes: string; allowWhatsApp: boolean }
const EMPTY_CONTACT: ContactForm = { name: '', phone: '', email: '', notes: '', allowWhatsApp: false }

function ContactsTab({ client, reload }: { client: ClientDetail; reload: () => void }) {
  const [form, setForm] = useState<ContactForm>(EMPTY_CONTACT)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    const payload = { ...form, email: form.email || null }
    try {
      if (editingId) {
        await api.put(`/clients/${client.id}/contacts/${editingId}`, payload)
      } else {
        await api.post(`/clients/${client.id}/contacts`, payload)
      }
      setForm(EMPTY_CONTACT)
      setEditingId(null)
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  async function remove(id: number) {
    try {
      await api.delete(`/clients/${client.id}/contacts/${id}`)
      if (editingId === id) { setEditingId(null); setForm(EMPTY_CONTACT) }
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  async function toggleWhatsApp(c: ClientContact) {
    try {
      await api.put(`/clients/${client.id}/contacts/${c.id}`, {
        name: c.name,
        phone: c.phone,
        email: c.email,
        notes: c.notes,
        allowWhatsApp: !c.allowWhatsApp,
      })
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  function startEdit(c: ClientContact) {
    setEditingId(c.id)
    setForm({ name: c.name, phone: c.phone, email: c.email ?? '', notes: c.notes ?? '', allowWhatsApp: c.allowWhatsApp })
  }

  return (
    <>
      <section className="card">
        <h2>{editingId ? 'Edit contact' : 'Add secondary contact'}</h2>
        <p className="muted small">Contacts with "send WhatsApp" checked also receive expiry reminders and license keys.</p>
        <form onSubmit={submit} className="form-grid form-grid-4">
          <Field label="Name *">
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
          </Field>
          <Field label="Phone *">
            <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} required placeholder="+20…" />
          </Field>
          <Field label="Email">
            <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          </Field>
          <Field label="Notes">
            <input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} placeholder="e.g. accountant, pharmacist…" />
          </Field>
          <label className="check" style={{ alignSelf: 'center' }}>
            <input
              type="checkbox"
              checked={form.allowWhatsApp}
              onChange={(e) => setForm({ ...form, allowWhatsApp: e.target.checked })}
            />
            Send WhatsApp notifications
          </label>
          {error && <ErrorBox message={error} />}
          <div className="modal-actions">
            {editingId && (
              <button type="button" className="btn" onClick={() => { setEditingId(null); setForm(EMPTY_CONTACT) }}>
                Cancel edit
              </button>
            )}
            <button className="btn btn-primary">{editingId ? 'Save changes' : 'Add contact'}</button>
          </div>
        </form>
      </section>

      <section className="card">
        <h2>Contact persons ({client.contacts.length})</h2>
        {client.contacts.length === 0 ? (
          <Empty text="No secondary contacts yet." />
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Phone</th>
                <th>Email</th>
                <th>Notes</th>
                <th>WhatsApp</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {client.contacts.map((c) => (
                <tr key={c.id}>
                  <td className="strong">{c.name}</td>
                  <td className="nowrap">{c.phone}</td>
                  <td>{c.email ?? '-'}</td>
                  <td className="wrap">{c.notes}</td>
                  <td>
                    <label className="check" title="Toggle WhatsApp notifications for this contact">
                      <input type="checkbox" checked={c.allowWhatsApp} onChange={() => toggleWhatsApp(c)} />
                      {c.allowWhatsApp ? 'receives' : 'off'}
                    </label>
                  </td>
                  <td className="nowrap">
                    <button className="btn btn-small" onClick={() => startEdit(c)}>✎ Edit</button>{' '}
                    <button className="btn btn-small" onClick={() => remove(c.id)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  )
}

/* ---------------- Subscriptions ---------------- */

function SubscriptionsTab({ client, plans, reload }: { client: ClientDetail; plans: PlanDto[]; reload: () => void }) {
  const [showNew, setShowNew] = useState(false)
  const [keyResult, setKeyResult] = useState<{ licenseKey: string; whatsappStatus: string } | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function markPaid(subId: number) {
    setError(null)
    try {
      const res = await api.post<{ subscription: SubscriptionDto; whatsappStatus: string; licenseKey: string }>(
        `/subscriptions/${subId}/mark-paid`,
        {},
      )
      setKeyResult({ licenseKey: res.data.licenseKey ?? res.data.subscription.licenseKey ?? '', whatsappStatus: res.data.whatsappStatus })
      reload()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <>
      <div className="page-head">
        <h2 style={{ margin: 0 }}>Subscriptions</h2>
        <button className="btn btn-primary" disabled={plans.length === 0} onClick={() => setShowNew(true)}>
          + New / renew subscription
        </button>
      </div>
      {error && <ErrorBox message={error} />}

      {client.subscriptions.length === 0 ? (
        <Empty text="No subscriptions yet." />
      ) : (
        <table className="table card">
          <thead>
            <tr>
              <th>Plan</th>
              <th>Period</th>
              <th>Price</th>
              <th>Payment</th>
              <th>License key</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {client.subscriptions.map((s) => {
              const d = daysUntil(s.expiryDate)
              return (
                <tr key={s.id}>
                  <td>{s.planName}</td>
                  <td className="nowrap">
                    {fmtDate(s.startDate)} → {fmtDate(s.expiryDate)}{' '}
                    <Badge value={d < 0 ? 'Expired' : d <= 7 ? 'Critical' : d <= 30 ? 'Pending' : 'Active'} />
                  </td>
                  <td>{Number(s.price).toLocaleString()}</td>
                  <td><Badge value={s.paymentStatus} /></td>
                  <td className="mono wrap">{s.licenseKey ?? '-'}</td>
                  <td className="nowrap">
                    {s.paymentStatus === 'Unpaid' && (
                      <button className="btn btn-small btn-primary" onClick={() => markPaid(s.id)}>Mark paid & send key</button>
                    )}
                    {s.licenseKey && s.paymentStatus === 'Paid' && (
                      <ResendButton subId={s.id} />
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {showNew && (
        <NewSubscriptionModal clientId={client.id} plans={plans} onClose={() => setShowNew(false)} onSaved={() => { setShowNew(false); reload() }} />
      )}
      {keyResult && (
        <Modal title="Payment received - activation key" onClose={() => setKeyResult(null)}>
          <p>The encrypted activation key was generated{keyResult.whatsappStatus.startsWith('Sent') ? ' and sent via WhatsApp (' + keyResult.whatsappStatus + ')' : ` (WhatsApp: ${keyResult.whatsappStatus})`}.</p>
          <pre className="key-box">{keyResult.licenseKey}</pre>
          <p className="muted small">The client enters this key in the desktop ERP to activate/renew. Copy it now for manual fallback.</p>
        </Modal>
      )}
    </>
  )
}

function ResendButton({ subId }: { subId: number }) {
  const [msg, setMsg] = useState<string | null>(null)
  return (
    <>
      <button
        className="btn btn-small"
        onClick={async () => {
          try {
            const r = await api.post<{ status?: string; sent?: boolean }>(`/subscriptions/${subId}/resend-key`, {})
            setMsg(`Key resent (${r.data.sent ? r.data.status : 'no phone'})`)
          } catch (e) {
            setMsg(errMsg(e))
          }
        }}
      >
        Resend key
      </button>
      {msg && <div className="muted small">{msg}</div>}
    </>
  )
}

function NewSubscriptionModal({
  clientId,
  plans,
  onClose,
  onSaved,
}: {
  clientId: number
  plans: PlanDto[]
  onClose: () => void
  onSaved: () => void
}) {
  const [planId, setPlanId] = useState(plans[0]?.id ?? 0)
  const [price, setPrice] = useState(String(plans[0]?.price ?? ''))
  const [startDate, setStartDate] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const p = plans.find((x) => x.id === planId)
    if (p) setPrice(String(p.price))
  }, [planId, plans])

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api.post('/subscriptions', {
        clientId,
        planId,
        price: price ? Number(price) : null,
        startDate: startDate ? new Date(startDate).toISOString() : null,
        notes: notes || null,
      })
      onSaved()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <Modal title="Create / renew subscription" onClose={onClose}>
      <p className="muted small">Without a start date the renewal stacks after the current expiry.</p>
      <form onSubmit={submit} className="form-grid">
        <Field label="Plan *">
          <select value={planId} onChange={(e) => setPlanId(Number(e.target.value))}>
            {plans.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.cycle})
              </option>
            ))}
          </select>
        </Field>
        <Field label="Price">
          <input type="number" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
        </Field>
        <Field label="Start date (optional)">
          <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </Field>
        <Field label="Notes">
          <textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
        {error && <ErrorBox message={error} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary">Create</button>
        </div>
      </form>
    </Modal>
  )
}

/* ---------------- Tickets mini-tab ---------------- */

function TicketsTab({ client }: { client: ClientDetail }) {
  return client.tickets.length === 0 ? (
    <Empty text="No tickets for this client. Create one from the Tickets page." />
  ) : (
    <table className="table card">
      <thead>
        <tr>
          <th>#</th>
          <th>Title</th>
          <th>Priority</th>
          <th>Status</th>
          <th>Updated</th>
        </tr>
      </thead>
      <tbody>
        {client.tickets.map((t) => (
          <tr key={t.id}>
            <td>{t.id}</td>
            <td><Link to="/tickets">{t.title}</Link></td>
            <td><Badge value={t.priority} /></td>
            <td><Badge value={t.status} /></td>
            <td className="nowrap">{fmtDateTime(t.updatedAt)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/* ---------------- Edit client ---------------- */

function EditClientButton({ client, onSaved }: { client: ClientDetail; onSaved: () => void }) {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({
    name: client.name,
    contactPerson: client.contactPerson,
    phone: client.phone,
    email: client.email ?? '',
    address: client.address ?? '',
    city: client.city ?? '',
    type: client.type,
    status: client.status,
    notes: client.notes ?? '',
  })
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api.put(`/clients/${client.id}`, { ...form, email: form.email || null })
      setOpen(false)
      onSaved()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm({ ...form, [k]: e.target.value })

  return (
    <>
      <button className="btn" onClick={() => setOpen(true)}>✎ Edit</button>
      {open && (
        <Modal title={`Edit ${client.name}`} onClose={() => setOpen(false)}>
          <form onSubmit={submit} className="form-grid">
            <Field label="Name *"><input value={form.name} onChange={set('name')} required /></Field>
            <Field label="Contact person"><input value={form.contactPerson} onChange={set('contactPerson')} /></Field>
            <Field label="Phone *"><input value={form.phone} onChange={set('phone')} required /></Field>
            <Field label="Email"><input type="email" value={form.email} onChange={set('email')} /></Field>
            <Field label="Type *">
              <select value={form.type} onChange={set('type')}>
                {CLIENT_TYPES.map((t) => <option key={t}>{t}</option>)}
              </select>
            </Field>
            <Field label="Status *">
              <select value={form.status} onChange={set('status')}>
                {CLIENT_STATUSES.map((s) => <option key={s}>{s}</option>)}
              </select>
            </Field>
            <Field label="City"><input value={form.city} onChange={set('city')} /></Field>
            <Field label="Address"><input value={form.address} onChange={set('address')} /></Field>
            <Field label="Notes"><textarea rows={2} value={form.notes} onChange={set('notes')} /></Field>
            {error && <ErrorBox message={error} />}
            <div className="modal-actions">
              <button type="button" className="btn" onClick={() => setOpen(false)}>Cancel</button>
              <button className="btn btn-primary">Save changes</button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}
