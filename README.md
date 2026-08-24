<<<<<<< HEAD
# CRM
mydev opencode CRM
=======
# ERP CRM — Web App

A web-based CRM to manage your ERP clients: subscriptions with expiry reminders over WhatsApp,
call logs, support tickets, an agenda/follow-up system, and encrypted license keys that unlock
renewals inside your local VB.NET desktop ERP.

| Layer    | Tech |
|----------|------|
| Backend  | ASP.NET Core (.NET 10) Web API, EF Core, JWT auth, BCrypt |
| Database | PostgreSQL 16 (Docker) |
| Frontend | React 19 + Vite + TypeScript |
| WhatsApp | Provider interface → Logging sender (dev) or **Meta WhatsApp Cloud API** |

```
├── docker-compose.yml      # PostgreSQL 16 (host port 5433!)
├── api/                    # ASP.NET Core Web API
│   ├── Models/             # entities + enums (client types/statuses, tickets…)
│   ├── Data/               # DbContext + demo seeder
│   ├── Dtos/               # request/response contracts
│   ├── Services/
│   │   ├── JwtTokenService.cs
│   │   ├── LicenseKeyService.cs       # encrypted activation keys
│   │   ├── WhatsApp/WhatsAppSenders.cs# Logging + Meta Cloud providers
│   │   └── ReminderWorker.cs          # 30/15/7/1-day expiry cron + agenda reminders
│   └── Controllers/        # auth, clients, plans, subscriptions, tickets,
│                           # interactions (call log), followups (agenda), users, dashboard
└── web/                    # React frontend (Login, Dashboard, Clients, Subscriptions,
                            # Tickets, Agenda, Users)
```

## Quick start

Prereqs: .NET 10 SDK, Node 18+, Docker Desktop.

```powershell
# 1) database
docker compose up -d          # postgres on localhost:5433 (5432 taken by your local PG)

# 2) backend  -> http://localhost:5000/swagger
dotnet run --project api

# 3) frontend -> http://localhost:5173  (proxies /api to :5000)
npm install --prefix web
npm run dev --prefix web
```

First run creates the schema and seeds demo data automatically (`EnsureCreated`).

**Seeded admin:** `admin@crm.local` / `Admin@123`
The first account registered becomes Admin; afterwards only Admins can create users.

Demo data includes *Al-Shifa Pharmacy* with a subscription expiring in ~7 days so you can try
"Mark paid & send key" and the reminder flow immediately.

## Browsing data with pgAdmin

The DB lives in Docker but any client works the same way. In pgAdmin 4:
*Register → Server*, then:

| Field | Value |
|---|---|
| Host | `localhost` |
| Port | **5433** (5432 is your separate local PostgreSQL install) |
| Database | `crm_db` |
| Username | `crm` |
| Password | value of `POSTGRES_PASSWORD` in `docker-compose.yml` |

Tip: enable *Start Docker Desktop when you sign in* — the API expects Postgres to be reachable
at startup and fails its first request otherwise.

## Feature map

| Requirement | Where |
|---|---|
| JWT auth + roles (Admin/Agent) | `api/Controllers/AuthController.cs`, `UsersController.cs` |
| Client CRUD + type (Pharmacy/GiftShop/DoctorClinic…) + status pipeline (Potential→Contacted→Interested→NotInterested→Subscribed) | `ClientsController.cs`, `web/src/pages/ClientsPage.tsx` |
| Subscription tracking (start, expiry, plan, payment status), renewal stacking | `SubscriptionsController.cs` |
| Expiry reminders at 30/15/7/1 days via WhatsApp, de-duplicated | `ReminderWorker.cs` (runs every 6h) |
| Encrypted license key after payment, delivered via WhatsApp | `LicenseKeyService.cs`, `POST /api/subscriptions/{id}/mark-paid`, `/resend-key`, `/validate-key` |
| Support tickets (priority, assignment, comments) | `TicketsController.cs`, `TicketsPage.tsx` |
| Call/request log (Call/WhatsApp/Email/Visit/Sms) + outcome + auto follow-up ("he's interested → call again Tuesday") | `InteractionsController.cs` — posting with `nextFollowUpAt` auto-creates an agenda entry |
| Agenda with overdue/today/upcoming grouping | `FollowUpsController.cs`, `AgendaPage.tsx` |
| New client → schedule first contact immediately | `CreateClientRequest.FirstContactAt` |
| WhatsApp message logging incl. voice/image schema | `WhatsAppMessage` table (MediaType: Text/Image/Voice/Document, MediaUrl, status, provider id) |
| Dashboard KPIs | `DashboardController.cs` |

## Configuration (`api/appsettings.json`)

```jsonc
"ConnectionStrings": { "Default": "Host=localhost;Port=5433;..." },
"Jwt":         { "Secret": "...at least 64 chars..." },
"Licensing":   { "Secret": "must match your desktop ERP validator" },
"WhatsApp": {
  "Provider": "Logging",              // switch to "MetaCloud" when you have a token
  "MetaCloud": {
    "AccessToken": "<system-user token>",
    "PhoneNumberId": "<WABA phone number id>",
    "ApiVersion": "v21.0"
  },
  "Templates": { "ExpiryReminder": "...{contact} {plan} {expiry} {days}...", 
                 "LicenseDelivered": "...{client} {plan} {key} {expiry}..." }
}
```

With `Provider: "Logging"` nothing is really sent — messages are stored in `WhatsAppMessages`
and written to the console log, so you can develop safely. Switching to `"MetaCloud"` sends real
messages once `AccessToken` + `PhoneNumberId` are filled (Meta WhatsApp Cloud API, text messages;
media fields already exist in the schema for later).

## License keys ↔ desktop ERP

Format: `Base32( IV ‖ AES-256-CBC("CRM|clientId|subscriptionId|expiryyyyyMMdd") ‖ HMAC-SHA256 )`,
grouped in 5-char blocks with `-`. Keys derive from one master secret
(`Licensing:Secret`) — AES key = SHA256(secret‖"|aes"), MAC key = SHA256(secret‖"|mac").
AES-CBC+HMAC was chosen over AES-GCM so legacy .NET Framework VB.NET can validate without extra packages.

Drop this module into your VB.NET ERP's activation screen:

```vb
' LicenseValidator.vb — needs Imports System.Security.Cryptography, System.Text, System.Collections.Generic
Public Class LicenseValidator
    ' MUST equal api appsettings.json -> Licensing:Secret
    Private Const MasterSecret As String = "DEV_ONLY_license_master_secret_change_me"

    ''' <summary>True if the key belongs to clientId and has not expired.</summary>
    Public Shared Function Validate(encryptedKey As String, expectedClientId As Integer) As Boolean
        Try
            Dim p = DecryptPayload(encryptedKey).Split("|"c)
            If p.Length <> 4 OrElse p(0) <> "CRM" Then Return False
            If Integer.Parse(p(1)) <> expectedClientId Then Return False
            Dim expiry As Date = Date.ParseExact(p(3), "yyyyMMdd", Globalization.CultureInfo.InvariantCulture)
            Return expiry >= Date.Today
        Catch ex As Exception
            Return False   ' tampered/malformed key
        End Try
    End Function

    Public Shared Function ExpiryOf(encryptedKey As String) As Date
        Dim p = DecryptPayload(encryptedKey).Split("|"c)
        Return Date.ParseExact(p(3), "yyyyMMdd", Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Shared Function DecryptPayload(keyText As String) As String
        Dim blob = Base32Decode(keyText)
        If blob.Length < 48 Then Throw New Exception("key too short")

        Dim master As Byte() = Text.Encoding.UTF8.GetBytes(MasterSecret)
        Dim aesKey = SHA256.Create().ComputeHash(ConcatBytes(master, Text.Encoding.ASCII.GetBytes("|aes")))
        Dim macKey = SHA256.Create().ComputeHash(ConcatBytes(master, Text.Encoding.ASCII.GetBytes("|mac")))

        Dim bodyLen As Integer = blob.Length - 32
        Dim body(bodyLen - 1) As Byte : Array.Copy(blob, 0, body, 0, bodyLen)
        Dim mac(31) As Byte : Array.Copy(blob, bodyLen, mac, 0, 32)

        Using h As New HMACSHA256(macKey)
            If Not Convert.ToBase64String(h.ComputeHash(body)) = Convert.ToBase64String(mac) Then
                Throw New Exception("integrity check failed")
            End If
        End Using

        Using aes As New AesManaged With {.KeySize = 256}
            aes.Key = aesKey
            Using dec = aes.CreateDecryptor(aes.Key, Slice(body, 0, 16))
                Dim cipher = Slice(body, 16, body.Length - 16)
                Dim plain = dec.TransformFinalBlock(cipher, 0, cipher.Length)
                Return Text.Encoding.UTF8.GetString(plain)
            End Using
        End Using
    End Function

    Private Shared Function Base32Decode(s As String) As List(Of Byte)
        Const alpha As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
        Dim bitBuffer As Integer = 0, bits As Integer = 0
        Dim outp As New List(Of Byte)
        For Each c As Char In s.ToUpperInvariant()
            Dim v = alpha.IndexOf(c) : If v < 0 Then Continue For   ' ignores dashes/spaces
            bitBuffer = (bitBuffer << 5) Or v : bits += 5
            If bits >= 8 Then
                outp.Add(CByte((bitBuffer >> (bits - 8)) And &HFF)) : bits -= 8
            End If
        Next
        Return outp
    End Function

    Private Shared Function ConcatBytes(a As Byte(), b As Byte()) As Byte()
        Dim r(a.Length + b.Length - 1) As Byte
        a.CopyTo(r, 0) : b.CopyTo(r, a.Length)
        Return r
    End Function

    Private Shared Function Slice(src As Byte(), start As Integer, count As Integer) As Byte()
        Dim r(count - 1) As Byte : Array.Copy(src, start, r, 0, count)
        Return r
    End Function
End Class
```

Usage in the ERP (store your client id somewhere machine-local):
`If LicenseValidator.Validate(txtKey.Text.Trim(), My.Settings.ClientId) Then ... unlock/renew ...`

## API quick reference

Swagger UI: `http://localhost:5000/swagger` (Development). All routes except `/auth/login` and
`/subscriptions/validate-key` require `Authorization: Bearer <token>`.

```
POST /api/auth/login | register | change-password      GET /api/auth/me
GET/POST/PUT/DELETE /api/clients[?q&type&status&page]  GET /api/clients/{id}
PATCH /api/clients/{id}/status
GET/POST /api/plans                                    PUT/DELETE /api/plans/{id}
GET /api/subscriptions[?expiringInDays&paymentStatus]  POST /api/subscriptions (create/renew)
POST /api/subscriptions/{id}/mark-paid                 POST /api/subscriptions/{id}/resend-key
POST /api/subscriptions/validate-key                   (anonymous — used by ERP tooling/tests)
GET/POST /api/tickets                                  GET/PUT /api/tickets/{id}
POST /api/tickets/{id}/comments
GET/POST /api/interactions                             (outcome + nextFollowUpAt → auto-agenda)
GET/POST /api/followups                                PUT /api/followups/{id}
PATCH /api/followups/{id}/complete | /cancel
GET /api/dashboard/stats
GET/POST /api/users (admin)                            PATCH .../toggle-active, /reset-password
GET /api/users/agents                                  (assignment dropdowns)
```

## Production checklist (before real deployment)

- [ ] Change `Jwt:Secret`, `Licensing:Secret`, DB password (`appsettings.Production.json` / env vars).
- [ ] Replace `EnsureCreated()` with EF Core migrations (`dotnet ef migrations add Init`) once schema evolves.
- [ ] HTTPS on the API + restrict CORS origins to your real domain.
- [ ] Set `WhatsApp:Provider=MetaCloud` with a real WABA token; keep `Logging` in dev.
- [ ] Back up Postgres volume (`crm_pgdata`). Consider per-client row filtering if you host multiple tenants.

## Fit with your migration roadmap

This project is deliberately the **seed of the online ERP** described in your plan:

- **1-a / 1-b** — business logic already lives in .NET services/controllers over PostgreSQL; SQL Express SPs get ported into these services as modules migrate.
- **2-a** — your VB.NET WinForms screens can call this same API (HttpClient) instead of SqlConnection; the `/subscriptions/validate-key` endpoint mirrors what an offline validator does.
- **2-b / 2-c** — React pages here become the module pages embedded in WebView2 or served standalone.
- **3-a / 3-b / 3-c** — the API contract is stateless JWT; an offline shell (Electron/Blazor) with SQLite can sync against these same endpoints later.
>>>>>>> 6245033 (Initial commit: CRM web app (ASP.NET Core API + React + PostgreSQL))
