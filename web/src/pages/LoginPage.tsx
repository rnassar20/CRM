import { type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { fieldError, errMsg } from '../api'
import { useAuth } from '../AuthContext'
import { Field } from '../components/ui'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const mutation = useMutation({
    mutationFn: () => login(email.trim(), password),
    onSuccess: () => navigate('/'),
  })

  const err = errMsg(mutation.error)
  const emailErr = fieldError(mutation.error, 'email') ?? fieldError(mutation.error, 'Email')
  const pwErr = fieldError(mutation.error, 'password') ?? fieldError(mutation.error, 'Password')

  function submit(e: FormEvent) {
    e.preventDefault()
    mutation.mutate()
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={submit}>
        <div className="brand brand-lg">
          ERP<span>CRM</span>
        </div>
        <p className="login-sub">Client & subscription management</p>
        <Field label="Email" error={emailErr}>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
        </Field>
        <Field label="Password" error={pwErr}>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </Field>
        {err && !emailErr && !pwErr && <div className="error-box">{err}</div>}
        <button className="btn btn-primary btn-block" disabled={mutation.isPending}>
          {mutation.isPending ? 'Signing in…' : 'Sign in'}
        </button>
        <p className="login-hint">Seeded dev account: admin@crm.local / Admin@123</p>
      </form>
    </div>
  )
}
