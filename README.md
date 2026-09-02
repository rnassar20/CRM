# ERP CRM — Web App

A web-based CRM to manage your ERP clients: subscriptions with expiry reminders over WhatsApp,
call logs, support tickets, an agenda/follow-up system, and encrypted license keys that unlock
renewals inside your local VB.NET desktop ERP.

| Layer    | Tech |
|----------|------|
| Backend  | ASP.NET Core (.NET 10) Web API, EF Core, JWT auth, BCrypt |
| Database | PostgreSQL 16 (Docker) |
| Frontend | React 19 + Vite + TypeScript |
| WhatsApp | Provider interface → Logging sender (dev) or **Meta WhatsApp Cloud API** (prod) |
| Jobs     | Hangfire + Postgres storage (same DB, `hangfire.*` schema) |

## Getting started

```bash
# 1. Copy .env.example → .env and fill in the real values (never commit .env).
cp .env.example .env

# 2. Start Postgres.
docker compose up -d

# 3. Run the API (Kestrel on :5000 by default).
cd api && dotnet run

# 4. Run the web app (Vite on :5173, proxies /api → localhost:5000).
cd web && npm run dev
```

The API refuses to start if required secrets are missing or still placeholders — see
`api/Program.cs::ValidateRequiredSecrets`.

## Configuration (required secrets)

All secrets are injected via environment variables / `.env` (gitignored). The minimum set to
start the API:

| Variable | Purpose | Notes |
|----------|---------|-------|
| `ConnectionStrings__Default` | Postgres connection | `Host=...;Database=crm_db;Username=crm;Password=...` |
| `Jwt__Secret` | JWT signing key | ≥ 64 chars, hex or base64 recommended |
| `Licensing__Secret` | License-key master secret | ≥ 16 chars; used to derive AES + HMAC keys |

Optional (only needed when `WhatsApp__Provider=MetaCloud`):

| Variable | Purpose |
|----------|---------|
| `WhatsApp__MetaCloud__AccessToken` | Meta Cloud API access token |
| `WhatsApp__MetaCloud__PhoneNumberId` | Phone number ID from Meta developer portal |
| `WhatsApp__MetaCloud__ApiVersion` | API version (default `v21.0`) |

`WhatsApp__Provider` defaults to `Logging` (dev) — messages are written to the `WhatsAppMessages`
table but never sent. Set it to `MetaCloud` and provide the three MetaCloud vars above to send
real messages. The API validates at startup that the MetaCloud credentials are present when the
provider is set to MetaCloud.

## Time zone assumption

All `DateTime` columns in the database are `timestamp without time zone` (local time). The CRM,
the API, and the desktop VB.NET ERP all treat dates as local office time — there is no
time-zone conversion anywhere in the stack today.

This is deliberate: the domain concerns expiry days, agenda times, and follow-ups, which are
expressed in local time. **If you ever deploy the API in a different time zone from the office,
or add multi-region agents, this assumption must be revisited** (store `ScheduledAtUtc` + a
`TimeZone` on users, convert at the boundary).

## ERP integration (license keys)

Paid subscriptions get an encrypted activation key delivered over WhatsApp and returned once in
the `POST /api/subscriptions/{id}/mark-paid` response. The desktop ERP validates a key by calling:

```
POST /api/subscriptions/validate-key
Body: { "key": "<entered key>" }
```

Response: `{ "valid": true, "clientId": ..., "subscriptionId": ..., "expiryDate": "yyyy-MM-dd", ... }`.

Key format: `Base32( IV(16) || AES-256-CBC(payload) || HMAC-SHA256(iv+payload) )`, grouped in
5-char blocks separated by `-`. Payload: `CRM|{clientId}|{subscriptionId}|{expiry:yyyyMMdd}`.
The crypto is AES-CBC + HMAC (not AES-GCM) so the legacy .NET Framework VB.NET ERP can decrypt
with plain `AesManaged`/`HMACSHA256` — see `api/Services/LicenseKeyService.cs`.

## API

- Swagger UI: `https://localhost:5001/swagger` (dev only).
- Hangfire dashboard: `https://localhost:5001/hangfire` (Admin cookie auth).
- Dashboard login: `https://localhost:5001/dashboard/login`.

## Seed data (dev only)

On first run in development, `DbSeeder` creates:
- Admin user: `admin@crm.local` / `Admin@123`
- 4 subscription plans (Basic/Pro/Premium yearly + Basic monthly)
- 3 demo clients (Al-Shifa Pharmacy, Sunshine Gifts, Dr. Sara Clinic)
- Demo contacts, interactions, follow-ups, and an open ticket

Seeding is **disabled in production** (`Program.cs` gates it to `IsDevelopment()`). Never rely on
seed data for prod — create real accounts via `POST /api/auth/register` (Admin only after the
first account exists).
