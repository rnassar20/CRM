import { type FormEvent, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { fieldError, errMsg, fieldErrors } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import { daysUntil, fmtDate, type PlanDto, type SubscriptionDto } from '../types'
import { useCreateSubscription, useMarkPaid, usePlans, useResendKey, useSubscriptions, useClientOptions } from '../queries'

export default function SubscriptionsPage() {
  const [params] = useSearchParams()
  const [expiring, setExpiring] = useState(params.get('expiring') ?? '')
  const [unpaidOnly, setUnpaidOnly] = useState(params.get('unpaid') === '1')
  const [expiredOnly, setExpiredOnly] = useState(params.get('expired') === '1')
  const [page, setPage] = useState(1)
  const [showNew, setShowNew] = useState(false)
  const [keyResult, setKeyResult] = useState<{ licenseKey: string; whatsappStatus: string } | null>(null)

  const { data, error, isLoading } = useSubscriptions({
    expiringInDays: expiring,
    paymentStatus: unpaidOnly ? 'Unpaid' : '',
    page,
    pageSize: 25,
  })
  const { data: plans = [] } = usePlans()
  const markPaid = useMarkPaid()

  const items = (data?.items ?? []).filter((s) => !expiredOnly || daysUntil(s.expiryDate) < 0)

  function markPaidClick(s: SubscriptionDto) {
    markPaid.mutate(s.id, {
      onSuccess: (res) =>
        setKeyResult({
          licenseKey: res.licenseKey ?? '',
          whatsappStatus: res.whatsappStatus,
        }),
    })
  }

  return (
    <>
      <div className="page-head">
        <h1>Subscriptions</h1>
        <button className="btn btn-primary" disabled={plans.length === 0} onClick={() => setShowNew(true)}>
          + New / renew
        </button>
      </div>

      <div className="filters card">
        <select value={expiring} onChange={(e) => { setExpiring(e.target.value); setPage(1) }}>
          <option value="">All time</option>
          <option value="7">Expiring in 7 days</option>
          <option value="15">Expiring in 15 days</option>
          <option value="30">Expiring in 30 days</option>
          <option value="60">Expiring in 60 days</option>
        </select>
        <label className="check">
          <input type="checkbox" checked={unpaidOnly} onChange={(e) => { setUnpaidOnly(e.target.checked); setPage(1) }} />
          Unpaid only
        </label>
        <label className="check">
          <input type="checkbox" checked={expiredOnly} onChange={(e) => { setExpiredOnly(e.target.checked); setPage(1) }} />
          Expired only
        </label>
      </div>

      {error && <ErrorBox message={errMsg(error)} />}
      {errMsg(markPaid.error) && <ErrorBox message={errMsg(markPaid.error)} />}
      {isLoading && !data && <Spinner />}

      {data && (
        <>
          {items.length === 0 ? (
            <Empty text="No subscriptions match." />
          ) : (
            <table className="table card">
              <thead>
                <tr>
                  <th>Client</th>
                  <th>Plan</th>
                  <th>Period</th>
                  <th>Price</th>
                  <th>Payment</th>
                  <th>Key</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((s) => {
                  const d = daysUntil(s.expiryDate)
                  return (
                    <tr key={s.id}>
                      <td><Link to={`/clients/${s.clientId}`} className="strong">{s.clientName}</Link><div className="muted small">{s.clientPhone}</div></td>
                      <td>{s.planName}</td>
                      <td className="nowrap">
                        {fmtDate(s.startDate)} → {fmtDate(s.expiryDate)}{' '}
                        <Badge value={d < 0 ? 'Expired' : d <= 7 ? 'Critical' : d <= 30 ? 'Pending' : 'Active'} />
                      </td>
                      <td>{Number(s.price).toLocaleString()}</td>
                      <td><Badge value={s.paymentStatus} /></td>
                      <td>{s.licenseKey ? <span title={s.licenseKey}>✓ issued</span> : '-'}</td>
                      <td className="nowrap">
                        {s.paymentStatus === 'Unpaid' ? (
                          <button className="btn btn-small btn-primary" onClick={() => markPaidClick(s)}>Mark paid & send key</button>
                        ) : (
                          s.licenseKey && <ResendInline subId={s.id} />
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
          <div className="pager">
            <button className="btn" disabled={page <= 1} onClick={() => setPage(page - 1)}>← Prev</button>
            <span className="muted">{data.total} subscription(s)</span>
            <button className="btn" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>Next →</button>
          </div>
        </>
      )}

      {showNew && <NewSubModal plans={plans} onClose={() => setShowNew(false)} onSaved={() => setShowNew(false)} />}

      {keyResult && (
        <Modal title="Activation key generated" onClose={() => setKeyResult(null)}>
          <p>WhatsApp delivery status: <strong>{keyResult.whatsappStatus}</strong></p>
          <pre className="key-box">{keyResult.licenseKey}</pre>
          <p className="muted small">Client enters this key in the desktop ERP (Help → Activate Subscription).</p>
        </Modal>
      )}
    </>
  )
}

function ResendInline({ subId }: { subId: number }) {
  const resend = useResendKey()
  const msg = resend.isPending ? 'sending…' : resend.isSuccess ? 'resent ✓' : resend.isError ? errMsg(resend.error) : null
  return (
    <>
      <button className="btn btn-small" onClick={() => resend.mutate(subId)}>
        Resend key
      </button>
      {msg && <div className="muted small">{msg}</div>}
    </>
  )
}

function NewSubModal({ plans, onClose, onSaved }: { plans: PlanDto[]; onClose: () => void; onSaved: () => void }) {
  const { data: clients = [] } = useClientOptions()
  const [clientId, setClientId] = useState('')
  const [planId, setPlanId] = useState(plans[0]?.id ?? 0)
  const [price, setPrice] = useState(String(plans[0]?.price ?? ''))
  const [startDate, setStartDate] = useState('')
  const [notes, setNotes] = useState('')
  const create = useCreateSubscription()

  function onPlanChange(id: number) {
    setPlanId(id)
    const p = plans.find((x) => x.id === id)
    if (p) setPrice(String(p.price))
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!clientId) return
    create.mutate(
      {
        clientId: Number(clientId),
        planId,
        price: price ? Number(price) : null,
        startDate: startDate ? new Date(startDate).toISOString() : null,
        notes: notes || null,
      },
      { onSuccess: onSaved },
    )
  }

  const e = create.error
  return (
    <Modal title="Create / renew subscription" onClose={onClose}>
      <p className="muted small">Without a start date the renewal stacks after the client's current expiry.</p>
      <form onSubmit={submit} className="form-grid">
        <Field label="Client *" error={fieldError(e, 'clientId')}>
          <select value={clientId} onChange={(ev) => setClientId(ev.target.value)} required>
            <option value="">— select —</option>
            {clients.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Plan *" error={fieldError(e, 'planId')}>
          <select value={planId} onChange={(ev) => onPlanChange(Number(ev.target.value))}>
            {plans.map((p) => (
              <option key={p.id} value={p.id}>{p.name} ({p.cycle})</option>
            ))}
          </select>
        </Field>
        <Field label="Price" error={fieldError(e, 'price') ?? fieldError(e, 'Price')}>
          <input type="number" step="0.01" value={price} onChange={(ev) => setPrice(ev.target.value)} />
        </Field>
        <Field label="Start date (optional)">
          <input type="date" value={startDate} onChange={(ev) => setStartDate(ev.target.value)} />
        </Field>
        <Field label="Notes">
          <textarea rows={2} value={notes} onChange={(ev) => setNotes(ev.target.value)} />
        </Field>
        {errMsg(e) && !fieldErrors(e) && <ErrorBox message={errMsg(e)} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={create.isPending}>Create</button>
        </div>
      </form>
    </Modal>
  )
}
