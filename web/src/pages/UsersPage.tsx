import { type FormEvent, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { fieldError, errMsg } from '../api'
import { useAuth } from '../AuthContext'
import { Badge, ErrorBox, Field, Modal, Spinner } from '../components/ui'
import { fmtDate, type Role, type UserDto } from '../types'
import { useCreateUser, useResetPassword, useToggleUser, useUsers } from '../queries'

export default function UsersPage() {
  const { user: me } = useAuth()
  const { data: users, error, isLoading } = useUsers()
  const [msg, setMsg] = useState<string | null>(null)
  const [showAdd, setShowAdd] = useState(false)

  const toggle = useToggleUser()
  const reset = useResetPassword()

  function resetPassword(u: UserDto, newPassword: string) {
    setMsg(null)
    reset.mutate(
      { id: u.id, newPassword },
      { onSuccess: () => setMsg(`Password updated for ${u.fullName}.`) },
    )
  }

  if (error) return <ErrorBox message={errMsg(error)} />
  if (isLoading || !users) return <Spinner />

  return (
    <>
      <div className="page-head">
        <h1>Users</h1>
        <button className="btn btn-primary" onClick={() => setShowAdd(true)}>+ Add user</button>
      </div>

      {errMsg(toggle.error) && <ErrorBox message={errMsg(toggle.error)} />}
      {msg && <div className="success-box">{msg}</div>}

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
                    <button className="btn btn-small" onClick={() => toggle.mutate(u.id)}>
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

      {showAdd && <AddUserModal onClose={() => setShowAdd(false)} onSaved={() => setShowAdd(false)} />}
    </>
  )
}

function ResetButton({ user, onSave }: { user: UserDto; onSave: (newPassword: string) => void }) {
  const [open, setOpen] = useState(false)
  const [pw, setPw] = useState('')
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: async () => onSave(pw),
    onSuccess: () => {
      setOpen(false)
      setPw('')
      setError(null)
    },
    onError: (e) => setError(errMsg(e)),
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    if (pw.length < 8) return setError('Min 8 characters.')
    save.mutate()
  }

  return (
    <>
      <button className="btn btn-small" onClick={() => setOpen(true)}>Reset password</button>
      {open && (
        <Modal title={`Reset password - ${user.fullName}`} onClose={() => setOpen(false)}>
          <form onSubmit={submit} className="form-grid">
            <Field label="New password *" error={fieldError(save.error, 'newPassword') ?? fieldError(save.error, 'NewPassword')}>
              <input type="password" value={pw} onChange={(e) => setPw(e.target.value)} required autoFocus minLength={8} />
            </Field>
            {error && <ErrorBox message={error} />}
            <div className="modal-actions">
              <button type="button" className="btn" onClick={() => setOpen(false)}>Cancel</button>
              <button className="btn btn-primary" disabled={save.isPending}>Set password</button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}

function AddUserModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ fullName: '', email: '', password: '', role: 'Agent' as Role })
  const create = useCreateUser()

  function submit(e: FormEvent) {
    e.preventDefault()
    create.mutate(form, { onSuccess: onSaved })
  }

  const e = create.error
  return (
    <Modal title="Add user" onClose={onClose}>
      <form onSubmit={submit} className="form-grid">
        <Field label="Full name *" error={fieldError(e, 'fullName') ?? fieldError(e, 'FullName')}>
          <input value={form.fullName} onChange={(ev) => setForm({ ...form, fullName: ev.target.value })} required />
        </Field>
        <Field label="Email *" error={fieldError(e, 'email') ?? fieldError(e, 'Email')}>
          <input type="email" value={form.email} onChange={(ev) => setForm({ ...form, email: ev.target.value })} required />
        </Field>
        <Field label="Password *" error={fieldError(e, 'password') ?? fieldError(e, 'Password')}>
          <input type="password" value={form.password} onChange={(ev) => setForm({ ...form, password: ev.target.value })} required minLength={8} />
        </Field>
        <Field label="Role *" error={fieldError(e, 'role') ?? fieldError(e, 'Role')}>
          <select value={form.role} onChange={(ev) => setForm({ ...form, role: ev.target.value as Role })}>
            <option value="Agent">Agent</option>
            <option value="Admin">Admin</option>
          </select>
        </Field>
        {errMsg(e) && <ErrorBox message={errMsg(e)} />}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={create.isPending}>Create user</button>
        </div>
      </form>
    </Modal>
  )
}
