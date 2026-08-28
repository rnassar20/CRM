import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { fieldError, errMsg } from '../api'
import { Badge, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import {
  daysUntil,
  fmtDate,
  type ClientStatus,
  type ClientType,
} from '../types'
import { useClients, useCreateClient, useDebouncedValue } from '../queries'

const CLIENT_TYPES: ClientType[] = ['Pharmacy', 'GiftShop', 'DoctorClinic', 'Hospital', 'Other']
const CLIENT_STATUSES: ClientStatus[] = ['Potential', 'Contacted', 'Interested', 'NotInterested', 'Subscribed']

export default function ClientsPage() {
  const [q, setQ] = useState('')
  const [type, setType] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [showAdd, setShowAdd] = useState(false)

  const debouncedQ = useDebouncedValue(q, q ? 300 : 0)
  const { data, error, isLoading } = useClients({ q: debouncedQ, type, status, page, pageSize: 20 })

  return (
    <>
      <div className="page-head">
        <h1>Clients</h1>
        <button className="btn btn-primary" onClick={() => setShowAdd(true)}>
          + Add client
        </button>
      </div>

      <div className="filters card">
        <input placeholder="Search name / phone / contact…" value={q} onChange={(e) => { setQ(e.target.value); setPage(1) }} />
        <select value={type} onChange={(e) => { setType(e.target.value); setPage(1) }}>
          <option value="">All types</option>
          {CLIENT_TYPES.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          <option value="">All statuses</option>
          {CLIENT_STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </div>

      {error && <ErrorBox message={errMsg(error)} />}
      {isLoading && !data && <Spinner />}

      {data && (
        <>
          <table className="table card">
            <thead>
              <tr>
                <th>Name</th>
                <th>Type</th>
                <th>Status</th>
                <th>Contact</th>
                <th>Phone</th>
                <th>Subscription</th>
                <th>Expiry</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((c) => (
                <tr key={c.id}>
                  <td>
                    <Link to={`/clients/${c.id}`} className="strong">{c.name}</Link>
                    {c.city && <div className="muted small">{c.city}</div>}
                  </td>
                  <td>{c.type}</td>
                  <td><Badge value={c.status} /></td>
                  <td>{c.contactPerson || '-'}</td>
                  <td className="nowrap">{c.phone}</td>
                  <td>{c.planName ? <span>{c.planName} <Badge value={c.paymentStatus} /></span> : <span className="muted">none</span>}</td>
                  <td>
                    {c.expiryDate ? <ExpiryCell iso={c.expiryDate} /> : '-'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="pager">
            <button className="btn" disabled={page <= 1} onClick={() => setPage(page - 1)}>← Prev</button>
            <span className="muted">{data.total} client(s)</span>
            <button
              className="btn"
              disabled={page * data.pageSize >= data.total}
              onClick={() => setPage(page + 1)}
            >
              Next →
            </button>
          </div>
        </>
      )}

      {showAdd && <AddClientModal onClose={() => setShowAdd(false)} onSaved={() => setShowAdd(false)} />}
    </>
  )
}

function ExpiryCell({ iso }: { iso: string }) {
  const d = daysUntil(iso)
  return (
    <span className="nowrap">
      {fmtDate(iso)}{' '}
      <Badge value={d < 0 ? 'Expired' : d <= 7 ? 'Critical' : d <= 30 ? 'Pending' : 'Active'} />
    </span>
  )
}

function AddClientModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    name: '',
    contactPerson: '',
    phone: '',
    email: '',
    city: '',
    address: '',
    type: 'Pharmacy',
    status: 'Potential',
    notes: '',
    firstContactAt: '',
  })
  const create = useCreateClient()

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm({ ...form, [k]: e.target.value })

  function submit(e: FormEvent) {
    e.preventDefault()
    create.mutate(
      {
        ...form,
        email: form.email || null,
        firstContactAt: form.firstContactAt ? new Date(form.firstContactAt).toISOString() : null,
      },
      { onSuccess: onSaved },
    )
  }

  const e = create.error
  const fe = (f: string) => fieldError(e, f) ?? fieldError(e, f.charAt(0).toUpperCase() + f.slice(1))

  return (
    <Modal title="Add client" onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        <Field label="Name *" error={fe('name')}>
          <input value={form.name} onChange={set('name')} required />
        </Field>
        <Field label="Contact person" error={fe('contactPerson')}>
          <input value={form.contactPerson} onChange={set('contactPerson')} />
        </Field>
        <Field label="Phone (WhatsApp) *" error={fe('phone')}>
          <input value={form.phone} onChange={set('phone')} placeholder="+20xxxxxxxxxx" required />
        </Field>
        <Field label="Email" error={fe('email')}>
          <input type="email" value={form.email} onChange={set('email')} />
        </Field>
        <Field label="Type *" error={fe('type')}>
          <select value={form.type} onChange={set('type')}>
            {CLIENT_TYPES.map((t) => (
              <option key={t}>{t}</option>
            ))}
          </select>
        </Field>
        <Field label="Status *" error={fe('status')}>
          <select value={form.status} onChange={set('status')}>
            {CLIENT_STATUSES.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
        </Field>
        <Field label="City">
          <input value={form.city} onChange={set('city')} />
        </Field>
        <Field label="Address">
          <input value={form.address} onChange={set('address')} />
        </Field>
        <Field label="Schedule first contact (agenda)">
          <input type="datetime-local" value={form.firstContactAt} onChange={set('firstContactAt')} />
        </Field>
        <Field label="Notes">
          <textarea rows={2} value={form.notes} onChange={set('notes')} />
        </Field>
        {errMsg(e) && <ErrorBox message={errMsg(e)} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={create.isPending}>{create.isPending ? 'Saving…' : 'Save client'}</button>
        </div>
      </form>
    </Modal>
  )
}
