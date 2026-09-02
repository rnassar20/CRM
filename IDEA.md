ERP CRM — Web App
A web-based CRM to manage your ERP clients: subscriptions with expiry reminders over WhatsApp,
call logs, support tickets, an agenda/follow-up system, and encrypted license keys that unlock
renewals inside your local VB.NET desktop ERP.
Layer	Tech
Backend	ASP.NET Core (.NET 10) Web API, EF Core, JWT auth, BCrypt
Database	PostgreSQL 16 (Docker)
Frontend	React 19 + Vite + TypeScript
WhatsApp	Provider interface → Logging sender (dev) or **Meta WhatsApp Cloud API** (prod)

Local-time assumption: all DateTime columns are timestamp without time zone. The CRM, the API,
and the desktop VB.NET ERP all treat dates as local office time — no time-zone conversion in the
stack today. If you ever deploy across time zones or add multi-region agents, revisit this (store
ScheduledAtUtc + a TimeZone on users, convert at the boundary).
