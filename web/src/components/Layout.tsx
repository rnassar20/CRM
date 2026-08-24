import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../AuthContext'

const links = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/clients', label: 'Clients' },
  { to: '/subscriptions', label: 'Subscriptions' },
  { to: '/tickets', label: 'Tickets' },
  { to: '/agenda', label: 'Agenda' },
]

export default function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brand">
          ERP<span>CRM</span>
        </div>
        <nav>
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} end={l.end} className={({ isActive }) => (isActive ? 'active' : '')}>
              {l.label}
            </NavLink>
          ))}
          {user?.role === 'Admin' && (
            <NavLink to="/users" className={({ isActive }) => (isActive ? 'active' : '')}>
              Users
            </NavLink>
          )}
        </nav>
        <div className="sidebar-foot">v1.0</div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div />
          <div className="topbar-user">
            <span className="user-name">{user?.fullName}</span>
            <span className={`badge ${user?.role === 'Admin' ? 'badge-purple' : 'badge-gray'}`}>{user?.role}</span>
            <button
              className="btn btn-ghost"
              onClick={() => {
                logout()
                navigate('/login')
              }}
            >
              Log out
            </button>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
