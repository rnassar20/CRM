import { Link, useNavigate } from 'react-router-dom'
import { useDashboard } from '../queries'
import { Badge, Empty, ErrorBox, Spinner } from '../components/ui'
import {
  daysUntil,
  fmtDateTime,
} from '../types'

export default function DashboardPage() {
  const { data: stats, error, isLoading } = useDashboard()
  const navigate = useNavigate()

  if (error) return <ErrorBox message={error.message} />
  if (isLoading || !stats) return <Spinner />

  const cards: { label: string; value: number; tone?: string; to?: string }[] = [
    { label: 'Clients', value: stats.clientsTotal, to: '/clients' },
    { label: 'Active subscriptions', value: stats.subscriptionsActive, tone: 'green', to: '/subscriptions' },
    { label: 'Expiring ≤30d', value: stats.subscriptionsExpiringIn30, tone: 'amber', to: '/subscriptions?expiring=30' },
    { label: 'Unpaid (active)', value: stats.subscriptionsUnpaidActive, tone: 'amber', to: '/subscriptions?unpaid=1' },
    { label: 'Expired', value: stats.subscriptionsExpired, tone: 'red', to: '/subscriptions?expired=1' },
    { label: 'Open tickets', value: stats.ticketsOpen, to: '/tickets' },
    { label: "Today's follow-ups", value: stats.followUpsToday, tone: 'blue', to: '/agenda' },
    { label: 'Overdue follow-ups', value: stats.followUpsOverdue, tone: 'red', to: '/agenda' },
  ]

  return (
    <>
      <h1>Dashboard</h1>

      <div className="cards-grid">
        {cards.map((c) => (
          <div key={c.label} className={`card stat-card ${c.to ? 'clickable' : ''}`} onClick={() => c.to && navigate(c.to)}>
            <div className="stat-value">{c.value}</div>
            <div className="stat-label">{c.label}</div>
          </div>
        ))}
      </div>

      <div className="two-col">
        <section className="card">
          <div className="card-head">
            <h2>Upcoming follow-ups (7 days)</h2>
            <Link to="/agenda">View agenda →</Link>
          </div>
          {stats.upcomingFollowUps.length === 0 ? (
            <Empty text="Nothing scheduled for the next week." />
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Client</th>
                  <th>Title</th>
                  <th>Assigned</th>
                </tr>
              </thead>
              <tbody>
                {stats.upcomingFollowUps.map((f) => (
                  <tr key={f.id}>
                    <td className="nowrap">{fmtDateTime(f.scheduledAt)}</td>
                    <td>
                      <Link to={`/clients/${f.clientId}`}>{f.clientName}</Link>
                    </td>
                    <td>{f.title}</td>
                    <td>{f.assignedToName}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <section className="card">
          <div className="card-head">
            <h2>Recent interactions</h2>
          </div>
          {stats.recentInteractions.length === 0 ? (
            <Empty text="No interactions logged yet." />
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Client</th>
                  <th>Type</th>
                  <th>Outcome</th>
                  <th>Next follow-up</th>
                  <th>By</th>
                </tr>
              </thead>
              <tbody>
                {stats.recentInteractions.map((i) => (
                  <tr key={i.id}>
                    <td>
                      <Link to={`/clients/${i.clientId}`}>{i.clientName}</Link>
                    </td>
                    <td>{i.type}</td>
                    <td>
                      <Badge value={i.outcome} />
                    </td>
                    <td className="nowrap">
                      {i.nextFollowUpAt ? (
                        <span>
                          {fmtDateTime(i.nextFollowUpAt)}{' '}
                          {daysUntil(i.nextFollowUpAt) < 0 && <Badge value="Overdue" />}
                        </span>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td>{i.userName}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>

      <section className="card">
        <div className="card-head">
          <h2>Pipeline by status</h2>
        </div>
        <div className="chip-row">
          {Object.entries(stats.clientsByStatus).map(([k, v]) => (
            <span key={k} className="chip">
              <Badge value={k} /> {v}
            </span>
          ))}
          {Object.keys(stats.clientsByStatus).length === 0 && <Empty text="No clients yet." />}
        </div>
      </section>
    </>
  )
}
