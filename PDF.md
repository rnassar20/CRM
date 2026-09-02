[NetLFI WG]
Severity: 3 - Functional/Policy gap (not a crash or data-loss bug)
Area: Program.cs startup (ValidateRequiredSecrets, DbSeeder, middleware order)
Trigger: any deploy/first-run with WhatsApp:Provider=MetaCloud, or prod DB with seed logic reachable

Findings:
- ValidateRequiredSecrets checks JWT + Licensing secrets, but does NOT validate WhatsApp MetaCloud
  credentials when Provider=MetaCloud. A mis-configured provider silently falls back to Logging,
  so a deployment can "work" while sending zero real messages.
- DbSeeder.Seed runs on every first-run (any environment) because it's guarded only by
  "if (db.Users.Any())". On a fresh prod DB it creates admin@crm.local / Admin@123 plus demo
  clients/subscriptions/tickets. Not a leak today (guarded), but unsafe intent for prod.
- All DateTime columns are timestamp without time zone (Npgsql.EnableLegacyTimestampBehavior=true).
  The app+ERP agree on local time, but this assumption is not documented anywhere.

Fixes landed in this commit:
- Program.cs: gate DbSeeder.Seed to IsDevelopment().
- Program.cs: validate WhatsApp MetaCloud secrets at startup when Provider=MetaCloud.
- Program.cs: add X-RequestId middleware + log scope for correlation.
- README.md: document local-time assumption + WhatsApp provider config + secrets.
- IDEA.md: mirror the local-time note.
