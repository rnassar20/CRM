import { type FormEvent, useRef, useState } from 'react'
import { errMsg, fieldError, fieldErrors } from '../api'
import { Badge, Empty, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import type { SaveSettingRequest, SettingDto } from '../types'
import {
  useAllPlans,
  useCreateSetting,
  useDeleteSetting,
  useLinkedPlans,
  useSetPlanLink,
  useSettingCategories,
  useSettings,
  useToggleSetting,
  useUpdateSetting,
} from '../queries'

export default function SettingsPage() {
  const [page, setPage] = useState('')
  const [showAdd, setShowAdd] = useState(false)
  const [editing, setEditing] = useState<SettingDto | null>(null)
  const [planning, setPlanning] = useState<SettingDto | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const { data: categories, error: catsError, isLoading: catsLoading } = useSettingCategories()
  const { data: items, error, isLoading } = useSettings(page)
  const toggle = useToggleSetting()
  const del = useDeleteSetting()

  const filtered = (items ?? []).filter((s) => !page || s.page === page)

  if (catsError && !categories) return <ErrorBox message={errMsg(catsError)} />

  function onToggle(s: SettingDto, active: boolean) {
    setMessage(null)
    toggle.mutate({ page: s.page, pscode: s.pscode, active })
  }

  function onDelete(s: SettingDto) {
    if (!window.confirm(`Delete setting '${s.page}:${s.pscode}'?`)) return
    setMessage(null)
    del.mutate(
      { page: s.page, pscode: s.pscode },
      { onSuccess: () => setMessage(`Deleted '${s.page}:${s.pscode}'.`) },
    )
  }

  return (
    <>
      <div className="page-head">
        <h1>Settings</h1>
        <button className="btn btn-primary" onClick={() => setShowAdd(true)}>+ Add setting</button>
      </div>

      <div className="filters card">
        <label>
          <select value={page} onChange={(e) => { setPage(e.target.value); setMessage(null) }}>
            <option value="">All categories ({categories?.reduce((n, c) => n + c.count, 0) ?? '…'})</option>
            {(categories ?? []).map((c) => (
              <option key={c.page} value={c.page}>{c.page} ({c.count})</option>
            ))}
          </select>
        </label>
        <span className="muted small">Category = the ew_set “page” column.</span>
      </div>

      {message && <div className="success-box">{message}</div>}
      {errMsg(toggle.error) && <ErrorBox message={errMsg(toggle.error)} />}
      {errMsg(del.error) && <ErrorBox message={errMsg(del.error)} />}
      {error && <ErrorBox message={errMsg(error)} />}

      {catsLoading && !categories && <Spinner />}
      {isLoading && !items && <Spinner />}

      {categories && (filtered.length === 0 ? (
        <Empty text={page ? `No settings in category '${page}'.` : 'No settings yet.'} />
      ) : (
        <table className="table card">
          <thead>
            <tr>
              <th>Code</th>
              <th>User code</th>
              <th>Description</th>
              <th>Active</th>
              <th>Used</th>
              <th>Plans</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((s) => (
              <tr key={`${s.page}:${s.pscode}`}>
                <td className="nowrap"><span className="muted">{s.page}:</span><strong>{s.pscode}</strong></td>
                <td>{s.uscode || <span className="muted">-</span>}</td>
                <td>{s.description || <span className="muted">-</span>}</td>
                <td>
                  <label className="check" title="Maps to the Status column (1 = active)">
                    <input
                      type="checkbox"
                      checked={s.active}
                      disabled={toggle.isPending}
                      onChange={(e) => onToggle(s, e.target.checked)}
                    />
                  </label>
                </td>
                <td><Badge value={s.isUsed ? 'Used' : 'free'} /></td>
                <td><button className="btn btn-small" onClick={() => setPlanning(s)}>Manage plans…</button></td>
                <td className="nowrap">
                  <button className="btn btn-small" onClick={() => setEditing(s)}>Edit</button>
                  <button
                    className="btn btn-small"
                    disabled={s.isUsed}
                    title={s.isUsed ? 'In use by one or more plans — unlink them first.' : ''}
                    onClick={() => onDelete(s)}
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ))}

      {showAdd && <SettingModal page={page} onClose={() => setShowAdd(false)} onSaved={() => setShowAdd(false)} />}
      {editing && <SettingModal setting={editing} onClose={() => setEditing(null)} onSaved={() => setEditing(null)} />}
      {planning && <PlanLinkModal setting={planning} onClose={() => setPlanning(null)} />}
    </>
  )
}

function SettingModal({ setting, page, onClose, onSaved }: { setting?: SettingDto; page?: string; onClose: () => void; onSaved: () => void }) {
  const editing = setting != null
  const [pageKey, setPageKey] = useState(setting?.page ?? page ?? '')
  const [pscode, setPscode] = useState(setting?.pscode ?? '')
  const [uscode, setUscode] = useState(setting?.uscode ?? '')
  const [description, setDescription] = useState(setting?.description ?? '')
  const [active, setActive] = useState(setting?.active ?? true)
  const [usref, setUsref] = useState(setting?.usref != null ? String(setting.usref) : '')
  const [descref, setDescref] = useState(setting?.descref != null ? String(setting.descref) : '')
  const { data: categories = [] } = useSettingCategories()

  const create = useCreateSetting()
  const update = useUpdateSetting()
  const mut = editing ? update : create
  const e = mut.error

  function submit(ev: FormEvent) {
    ev.preventDefault()
    const body: SaveSettingRequest = {
      pscode: pscode.trim(),
      uscode: uscode.trim() || null,
      description: description.trim() || null,
      active,
      usref: usref ? Number(usref) : null,
      descref: descref ? Number(descref) : null,
    }
    if (editing) {
      update.mutate({ page: setting.page, pscode: setting.pscode, body }, { onSuccess: onSaved })
    } else {
      create.mutate({ page: pageKey.trim(), body }, { onSuccess: onSaved })
    }
  }

  return (
    <Modal title={editing ? `Edit setting ${setting.page}:${setting.pscode}` : 'Add setting'} onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        {!editing && (
          <Field label="Category (page) *" error={fieldError(e, 'page')}>
            <input
              value={pageKey}
              onChange={(ev) => setPageKey(ev.target.value.toUpperCase())}
              required maxLength={5}
              placeholder="e.g. CURR"
              list="setting-cats"
            />
            <datalist id="setting-cats">
              {categories.map((c) => <option key={c.page} value={c.page} />)}
            </datalist>
          </Field>
        )}
        <Field label="PSCODE *" error={fieldError(e, 'pscode') ?? fieldError(e, 'Pscode')}>
          <input value={pscode} onChange={(ev) => setPscode(ev.target.value.toUpperCase())} required maxLength={5} />
        </Field>
        <Field label="USCODE" error={fieldError(e, 'uscode') ?? fieldError(e, 'Uscode')}>
          <input value={uscode} onChange={(ev) => setUscode(ev.target.value.toUpperCase())} maxLength={5} />
        </Field>
        <Field label="Description" error={fieldError(e, 'description') ?? fieldError(e, 'Description')}>
          <input value={description} onChange={(ev) => setDescription(ev.target.value)} maxLength={255} />
        </Field>
        <Field label="Active" error={fieldError(e, 'active') ?? fieldError(e, 'Active')}>
          <label className="check">
            <input type="checkbox" checked={active} onChange={(ev) => setActive(ev.target.checked)} />
            Active
          </label>
        </Field>
        <Field label="USREF (multilang)" error={fieldError(e, 'usref') ?? fieldError(e, 'Usref')}>
          <input type="number" value={usref} onChange={(ev) => setUsref(ev.target.value)} />
        </Field>
        <Field label="DESCREF (multilang)" error={fieldError(e, 'descref') ?? fieldError(e, 'Descref')}>
          <input type="number" value={descref} onChange={(ev) => setDescref(ev.target.value)} />
        </Field>
        {errMsg(e) && !fieldErrors(e) && <ErrorBox message={errMsg(e)} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={mut.isPending}>{editing ? 'Save' : 'Add'}</button>
        </div>
      </form>
    </Modal>
  )
}

function PlanLinkModal({ setting, onClose }: { setting: SettingDto; onClose: () => void }) {
  const { data: allPlans = [] } = useAllPlans()
  const { data: linked = [], isLoading } = useLinkedPlans(setting.page, setting.pscode)
  const setLink = useSetPlanLink()
  const linkedIds = new Set(linked.map((l) => l.planId))
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [saving, setSaving] = useState(false)

  // Initialize the checklist from the currently linked plans once they load.
  const initialized = useRef(false)
  if (!initialized.current && linked.length > 0) {
    initialized.current = true
    setSelected(linkedIds)
  }

  const dirty = [...selected].sort().join(',') !== [...linkedIds].sort().join(',')

  function flip(id: number, on: boolean) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (on) next.add(id); else next.delete(id)
      return next
    })
  }

  async function save() {
    const toAdd = allPlans.filter((p) => selected.has(p.id) && !linkedIds.has(p.id))
    const toRemove = allPlans.filter((p) => !selected.has(p.id) && linkedIds.has(p.id))
    setSaving(true)
    try {
      for (const p of toAdd)
        await setLink.mutateAsync({ page: setting.page, pscode: setting.pscode, planId: p.id, link: true })
      for (const p of toRemove)
        await setLink.mutateAsync({ page: setting.page, pscode: setting.pscode, planId: p.id, link: false })
      onClose()
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={`Plans using ${setting.page}:${setting.pscode}`} onClose={onClose}>
      <p className="muted small">Tick the plans that should include this setting. “Used” reflects this list.</p>
      {isLoading && !linked ? (
        <Spinner />
      ) : (
        <div className="form-grid">
          {allPlans.map((p) => (
            <label key={p.id} className="check">
              <input
                type="checkbox"
                checked={selected.has(p.id)}
                onChange={(e) => flip(p.id, e.target.checked)}
              />
              {p.name} <span className="muted small">({p.cycle})</span>
            </label>
          ))}
          {allPlans.length === 0 && <Empty text="No plans yet. Create a plan first." />}
        </div>
      )}
      {errMsg(setLink.error) && <ErrorBox message={errMsg(setLink.error)} />}
      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>Close</button>
        <button className="btn btn-primary" disabled={saving || !dirty} onClick={save}>Save</button>
      </div>
    </Modal>
  )
}
