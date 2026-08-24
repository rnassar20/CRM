import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { api, errMsg } from '../api'
import { useAuth } from '../AuthContext'
import { Badge, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import { fmtDate, type Role, type UserDto } from '../types'

export default function UsersPage() {
  const { user: me } = useAuth()
  const [users, setUsers] = useState<UserDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [showAdd, setShowAdd] = useState(false)

  const load = useCallback(async () => {
    try {
      const res = await api.get<UserDto[]>('/users')
      setUsers(res.data)
    } catch (e) {
      setError(errMsg(e))
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  async function toggle(u: UserDto) {
    setError(null)
    setMsg(null)
    try {
      await api.patch(`/users/${u.id}/toggle-active`)
      load()
    } catch (e) {
      setError(errMsg(e))
    }
  }

  async function resetPassword(u: UserDto, newPassword: string) {
    setError(null)
    try {
      await api.patch(`/users/${u.id}/reset-password`, { newPassword })
      setMsg(`Password updated for ${u.fullName}.`)
    } catch (e) {
      setError(errMsg(e))
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Users</h1>
        <button className="btn btn-primary" onClick={() => setShowAdd(true)}>+ Add user</button>
      </div>

      {error && <ErrorBox message={error} />}
      {msg && <div className="success-box">{msg}</div>}
      {!users && !error && <Spinner />}

      {users && (
        <table className="table card">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td className="strong">{u.fullName}{me?.id === u.id && <span className="muted"> (you)</span>}</td>
                <td>{u.email}</td>
                <td><Badge value={u.role === 'Admin' ? 'Medium' : 'Low'} /> {u.role}</td>
                <td><Badge value={u.isActive ? 'Active' : 'Cancelled'} /></td>
                <td>{fmtDate(u.createdAt)}</td>
                <td className="nowrap">
                  {me?.id !== u.id && (
                    <>
                      <button className="btn btn-small" onClick={() => toggle(u)}>
                        {u.isActive ? 'Deactivate' : 'Activate'}
                      </button>{' '}
                      <ResetButton user={u} onSave={(pw) => resetPassword(u, pw)} />
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showAdd && <AddUserModal onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); load() }} />}
    </>
  )
}

function ResetButton({ user, onSave }: { user: UserDto; onSave: (newPassword: string) => Promise<void> }) {
  const [open, setOpen] = useState(false)
  const [pw, setPw] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (pw.length < 8) return setError('Min 8 characters.')
    await onSave(pw)
    setOpen(false)
    setPw('')
    setError(null)
  }

  return (
    <>
      <button className="btn btn-small" onClick={() => setOpen(true)}>Reset password</button>
      {open && (
        <Modal title={`Reset password - ${user.fullName}`} onClose={() => setOpen(false)}>
          <form onSubmit={submit} className="form-grid">
            <Field label="New password *">
              <input type="password" value={pw} onChange={(e) => setPw(e.target.value)} required autoFocus minLength={8} />
            </Field>
            {error && <ErrorBox message={error} />}
            <div className="modal-actions">
              <button type="button" className="btn" onClick={() => setOpen(false)}>Cancel</button>
              <button className="btn btn-primary">Set password</button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}

function AddUserModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ fullName: '', email: '', password: '', role: 'Agent' as Role })
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api.post('/users', form)
      onSaved()
    } catch (err) {
      setError(errMsg(err))
    }
  }

  return (
    <Modal title="Add user" onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        <Field label="Full name *">
          <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
        </Field>
        <Field label="Email *">
          <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
        </Field>
        <Field label="Password *">
          <input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required minLength={8} />
        </Field>
        <Field label="Role *">
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
            <option value="Agent">Agent</option>
            <option value="Admin">Admin</option>
          </select>
        </Field>
        {error && <ErrorBox message={error} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary">Create user</button>
        </div>
      </form>
    </Modal>
  )
}
