# Employee Leave Management System (ELMS)

ASP.NET Core MVC (.NET 8) + SQL Server Express + EF Core Code-First.
Two roles — **Admin** (manages employees, approves/rejects leave) and
**Employee** (applies for leave, tracks own requests). Cookie authentication,
Razor Views + Bootstrap in a navy/teal/amber theme.

## Prerequisites

- .NET 8 SDK or later (`dotnet --version`)
- SQL Server Express, instance `.\SQLEXPRESS`
- EF Core CLI: `dotnet tool install --global dotnet-ef`
  (this repo also pins a local `dotnet-ef` 8.x tool manifest, so
  `dotnet tool run dotnet-ef` works without a global install)

## Setup (fresh clone to running app)

1. **Connection string** — copy the template and point it at your instance:
   - `LeaveManagementSystem/appsettings.json` ships with an **empty**
     `DefaultConnection` on purpose (no secrets in source control).
   - Create `LeaveManagementSystem/appsettings.Development.json` (git-ignored):
     ```json
     {
       "ConnectionStrings": {
         "DefaultConnection": "Server=.\\SQLEXPRESS;Database=LeaveManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
       }
     }
     ```
2. **Apply migrations + seed** (creates both tables and the two test accounts):
   ```bash
   cd LeaveManagementSystem
   dotnet tool run dotnet-ef -- database update
   ```
3. **Run**:
   ```bash
   dotnet run
   ```
   Open the URL shown (e.g. `http://localhost:5000`) → sign in below.

## Default test accounts

| Role | Email | Password |
|---|---|---|
| Admin | admin@example.com | admin123 |
| Employee | employee@example.com | emp123 |

Passwords are stored hashed (`PasswordHasher<User>`); the values above are
seed inputs for testing only.

## Core flow

Employee submits leave → validated (`FromDate ≤ ToDate`, no overlapping dates)
→ `Pending` → Admin approves/rejects (re-decision blocked) → Employee sees the
updated status. Deactivating an employee blocks sign-in but preserves history.
Leave duration counts working days only (Monday–Friday); weekends are excluded
from day counts, balance usage, and exports.

## Project layout

Follows `docs/` (`/home/abhishek/.config/opencode/projects/ELMS/docs`):
`Controllers/`, `Models/Entities+Enums/`, `ViewModels/`, `Services/`,
`Repositories/`, `Data/` (context, `Migrations/`, seed), `Views/`.

## Deploy

See `LeaveManagementSystem` publish flow in the design docs (`DEPLOYMENT.md`):
`dotnet build` → backup DB → `dotnet ef database update` (production
connection string) → `dotnet publish -c Release -o ./publish` → smoke test
(login as Admin and Employee, apply + approve one request).

## Bonus scope (implemented)

- **Help / FAQ** — role-scoped `FAQ / Help` pages (`/Admin/Faq`, `/Employee/Faq`)
  with a shared leave-workflow strip and single-open accordion; each role sees
  only its own FAQs (wiring: `LeaveManagementSystem/FAQ_WIRING.md`).
- **Real-time notifications** — SignalR hub at `/hub/notifications`; the
  employee dashboard shows a toast the moment a request is approved/rejected.
- **Reporting** — the admin leave-requests view filters by status, date range,
  and employee/reason text; **Export to Excel** downloads the current filter
  as `.xlsx` (ClosedXML, streamed, nothing written to disk).
- **Audit logging** — every approve/reject writes an `AuditLogs` row (who,
  what, when) in the same transaction as the status update; reviewer + date
  are shown on the admin list.

Note: there is no department filter — the MVP schema has no Department model
(`DATABASE.md` lists it as optional/future).
