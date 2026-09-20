# Changelog

## 2026-09-20 — Global exception handling + logging (ELMS-21)

- **New middleware** — `Middleware/GlobalExceptionMiddleware` catches every
  unhandled request exception centrally (no per-controller try/catch). It logs
  timestamp, HTTP method, path+query, user id/username (from the auth cookie
  claims; null when anonymous), exception type, message, stack trace, and
  correlation id — to the existing `ILogger` pipeline (structured `LogError`,
  Info/Warning logging untouched) and to a new `ExceptionLogs` table in the
  existing SQL Server Express database (`Services/ExceptionLogStore`).
- **Response unchanged** — the middleware rethrows, so the existing
  `UseExceptionHandler("/Home/Error")` flow still renders the generic 500 page
  with no sensitive details. Correlation id uses the same expression as the
  error page's reference (`Activity.Current?.Id ?? TraceIdentifier`), so a
  quoted reference matches the DB row. If DB persistence itself fails, it falls
  back to ILogger-only (logging never breaks the error path).

Changed files:
- `LeaveManagementSystem/Middleware/GlobalExceptionMiddleware.cs` (new)
- `LeaveManagementSystem/Models/Entities/ExceptionLog.cs` (new)
- `LeaveManagementSystem/Services/Interfaces/IExceptionLogStore.cs` (new)
- `LeaveManagementSystem/Services/ExceptionLogStore.cs` (new)
- `LeaveManagementSystem/Data/ApplicationDbContext.cs` (DbSet + config)
- `LeaveManagementSystem/Data/Migrations/20260920073825_AddExceptionLogs.cs` (new)
- `LeaveManagementSystem/Program.cs` (registration + `UseMiddleware`, inside `UseExceptionHandler`)
- `LeaveManagementSystem.Tests/ExceptionMiddlewareTests.cs` (new, 4 tests)

Verified: build clean (0 warn/0 err); 22/22 tests pass (18 existing + 4 new);
migration applied to `.\SQLEXPRESS/LeaveManagementDb`; live intentional
exception (`InvalidOperationException` from a temporary probe endpoint, since
removed) returned the generic error page with zero detail leaked and wrote the
full row to `ExceptionLogs` (all fields + 2.4KB stack trace confirmed, probe
row deleted afterward); both seed logins re-verified after the migration's
hash re-salt; `ExceptionLogs` left at 0 rows.

## 2026-09-20 — Role-based FAQ / Help (ELMS-20)

- **New pages** — `FAQ / Help` in both the Employee and Admin menus
  (`/Employee/Faq`, `/Admin/Faq`), each rendering only its role's FAQs
  (7 employee, 6 admin) behind the controllers' existing `[Authorize(Roles=…)]`.
- **Shared, not duplicated** — one `FaqData` source (`ViewModels/FaqViewModels.cs`),
  one `_WorkflowStrip` partial (Employee → … → Status Updated) and one
  single-open `_FaqAccordion` partial; new `wwwroot/css/faq.css` in the existing
  navy/teal/amber language, responsive down to mobile. Wiring guide:
  `LeaveManagementSystem/FAQ_WIRING.md`.
- **Cancel/modify answer** reflects the current workflow: no self-service
  cancel/modify exists (`LeaveService` is submit + approve/reject only), so the
  answer says so and points the employee to their Admin.

Changed files:
- `LeaveManagementSystem/Controllers/AdminController.cs` (`Faq()` action)
- `LeaveManagementSystem/Controllers/EmployeeController.cs` (`Faq()` action)
- `LeaveManagementSystem/ViewModels/FaqViewModels.cs` (new)
- `LeaveManagementSystem/Views/Admin/Faq.cshtml` (new)
- `LeaveManagementSystem/Views/Employee/Faq.cshtml` (new)
- `LeaveManagementSystem/Views/Shared/_WorkflowStrip.cshtml` (new)
- `LeaveManagementSystem/Views/Shared/_FaqAccordion.cshtml` (new)
- `LeaveManagementSystem/wwwroot/css/faq.css` (new)
- `LeaveManagementSystem/Views/Shared/_Layout.cshtml` (FAQ links + css)
- `LeaveManagementSystem/FAQ_WIRING.md` (new)

Verified: build clean (0 warn/0 err); 18/18 unit tests pass; live smoke on
Development (24/24 checks) — anon bounces to login, both FAQ pages render
200 with role-correct content, cross-role URLs blocked both directions,
`faq.css` serves 200.

## 2026-09-18 — Apply Leave date validation (no past dates, dynamic To Date)

- **From Date:** only today and future dates are selectable (`min = today` on
  the date input) and enforced server-side.
- **To Date:** `min` dynamically follows the selected From Date (same-day
  allowed); if From changes so the current To becomes invalid, To is cleared.
- Backend is the source of truth: new `[NoPastDate]` model attribute plus a
  past-date guard in `LeaveService.SubmitRequestAsync` (existing
  `FromDate ≤ ToDate` checks unchanged). Working-day calculation and UI styling
  untouched.

Changed files:
- `LeaveManagementSystem/ViewModels/Validation/NoPastDateAttribute.cs` (new)
- `LeaveManagementSystem/ViewModels/ApplyLeaveViewModel.cs`
- `LeaveManagementSystem/Services/LeaveService.cs`
- `LeaveManagementSystem/Views/Employee/Apply.cshtml`
- `LeaveManagementSystem.Tests/DateValidationTests.cs` (new, 4 tests)

Verified: 18/18 tests pass; live — From min renders as today, posted past
dates rejected with 0 rows inserted, same-day-today submits to Pending.

## 2026-09-18 — SignalR both directions + local client bundle

Real-time notifications now cover both directions over the single existing hub
(`/hub/notifications`): employee submit → admins (`LeaveSubmitted` to the
"Admins" group); admin approve/reject → employee (`LeaveStatusChanged` to the
user). One reusable auto-reconnecting connection lives in the shared layout
for all authenticated pages (toast UI); the old per-page Dashboard script was
removed to avoid a duplicate connection. `signalr.min.js` (8.0.7) is now
served from `wwwroot/lib/signalr/` instead of a CDN — pages work offline and
in no-internet grading environments.

Changed files:
- `LeaveManagementSystem/Hubs/NotificationHub.cs` (Admins group on connect)
- `LeaveManagementSystem/Services/LeaveService.cs` (submit-side push)
- `LeaveManagementSystem/Views/Shared/_Layout.cshtml` (singleton connection,
  toast host, both handlers)
- `LeaveManagementSystem/Views/Employee/Dashboard.cshtml` (per-page script
  removed)
- `LeaveManagementSystem/wwwroot/lib/signalr/signalr.min.js` (new, local)

Verified with a real two-user SignalR rig (cookie-auth connections, no page
refresh): submit→admin PASS, approve→employee PASS; 14/14 unit tests pass.
No `docs/CURRENT_SESSION.md` / `MEMORY.md` / `MASTER_IMPLEMENTATION_PLAN.md`
exist — session memory is `C:\AbhiLab\ELMS\PROGRESS.md`.

## 2026-09-18 — Working-day leave count (Mon–Fri, weekends excluded)

Leave duration is now counted in **working days only**: the inclusive
`FromDate → ToDate` range counts Monday–Friday; Saturday and Sunday never
contribute. Single source of truth: `Services/LeaveDaysCalculator.cs`
(`CountWorkingDays`), used by every calculation site — no duplicated logic.

Changed files:
- `LeaveManagementSystem/Services/LeaveDaysCalculator.cs` (new)
- `LeaveManagementSystem/Services/EmployeeService.cs` (leave usage)
- `LeaveManagementSystem/Controllers/AdminController.cs` (employee list,
  Excel export)
- `LeaveManagementSystem/Views/Employee/History.cshtml` (per-request days)
- `LeaveManagementSystem/Views/Admin/LeaveRequests.cshtml` (per-request days)
- `LeaveManagementSystem/Views/Employee/Dashboard.cshtml` (recent-request days)
- `LeaveManagementSystem/Views/_ViewImports.cshtml` (Services namespace)
- `LeaveManagementSystem.Tests/` (new xUnit project: 14 tests)

Notes:
- No persisted "deduction" exists by design — `LeaveBalance` is the annual
  entitlement and Used/Remaining are computed on read (`DATABASE.md` §6);
  that computation now uses working days, so balance and display always agree.
- Overlap check and `FromDate ≤ ToDate` validation are untouched (ranges, not
  counts). `CountWorkingDays` throws `ArgumentException` on an inverted range
  as a last-resort guard.
- Database remains SQL Server Express only; no schema change, no migration.
- No `docs/CURRENT_SESSION.md`, `docs/MEMORY.md`, or
  `docs/MASTER_IMPLEMENTATION_PLAN.md` exist in this project — session memory
  lives in `C:\AbhiLab\ELMS\PROGRESS.md` (updated alongside this change).
