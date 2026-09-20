# ELMS Build Progress (persistent memory)

- Project root (Windows): `C:\AbhiLab\ELMS` (WSL: `/mnt/c/AbhiLab/ELMS`)
- Design docs (WSL): `/home/abhishek/.config/opencode/projects/ELMS/docs`
  (`PROJECT.md`, `ARCHITECTURE.md`, `DATABASE.md`, `ENVIRONMENT.md`,
  `DEPLOYMENT.md`, `BACKLOG.md`, `KICKOFF_PROMPT.md`, `UI_Prototype.html`)
- Stack: ASP.NET Core MVC (.NET 8), EF Core Code-First, Razor + Bootstrap, cookie auth
- Database: SQL Server Express ONLY — instance `.\SQLEXPRESS`, db `LeaveManagementDb`
- Visual language: navy/teal/amber palette, Manrope + IBM Plex Sans, left rail nav,
  stat cards, panel tables with status badges (per `UI_Prototype.html`)
- Phase 5 (ELMS-17–19) is BONUS — do NOT start without explicit "start phase 5"

## Ticket checklist

- [x] ELMS-01 — Project scaffold (2026-09-18: MVC net8.0 in `C:\AbhiLab\ELMS\LeaveManagementSystem`, folders per ARCHITECTURE.md §2; Privacy/Index scaffold removed; local dotnet-ef 8.0.11 via tool manifest)
- [x] ELMS-02 — Database context and initial migration (2026-09-18: `Data/Migrations/InitialCreate`; verified CK_LeaveRequests_DateRange + IX_LeaveRequests_UserId_Dates + unique email in DB)
- [x] ELMS-03 — Seed default Admin and Employee (2026-09-18: verified rows admin@example/Role1/Bal0, employee@example/Role2/Bal20, Identity-v3 hashes)
- [x] ELMS-04 — Cookie authentication and login (2026-09-18: smoke-tested — bad creds friendly error, anon bounces to login, logout works; cross-role URLs bounce to own dashboard)
- [x] ELMS-05 — Role-aware dashboard redirect (2026-09-18: admin→/Admin, employee→/Employee/Dashboard verified)
- [x] ELMS-06 — Employee list and search
- [x] ELMS-07 — Add / edit employee
- [x] ELMS-08 — Deactivate employee
- [x] ELMS-09 — Apply for leave (Employee)
- [x] ELMS-10 — Employee leave history
- [x] ELMS-11 — Admin: view and approve/reject leave requests
- [x] ELMS-12 — Admin dashboard summary
- [x] ELMS-13 — Employee dashboard summary
- [x] ELMS-14 — Global error handling
- [x] ELMS-15 — README and setup instructions
- [x] ELMS-16 — Release rehearsal
- [x] ELMS-17 — SignalR real-time notifications
- [x] ELMS-18 — Reporting filters + export
- [x] ELMS-19 — Audit logging
- [x] ELMS-20 — Role-based FAQ / Help

## Session log

- 2026-09-18: Session 1 started. No prior PROGRESS.md found; created this file.
  Starting ELMS-01 (Phase 0).
- 2026-09-18: Phase 0 COMPLETE (ELMS-01–05). Build clean (0 warn/0 err).
  Deviation: `LeaveBalance` has no DB-level DEFAULT 20 (entity initializer `= 20`
  is the default instead) so the seed can insert the Admin's explicit 0 per
  DATABASE.md §4 — an EF HasData/HasDefaultValue sentinel limitation, not a doc gap.
  Code lives in `C:\AbhiLab\ELMS\LeaveManagementSystem`; run with
  `dotnet run` (Development → `.\SQLEXPRESS/LeaveManagementDb`).
  STOPPING here per phase-gate rule. Next: Phase 1 (ELMS-06).
- 2026-09-18: Phase 1 COMPLETE (ELMS-06–08). Build clean.
  Admin/Employees list + search (admin accounts excluded), used/remaining computed
  in one query per DATABASE.md §6; shared EmployeeForm for create/edit; duplicate
  email → field-level error via new ServiceResult.Field; Deactivate/Reactivate via
  IsActive toggle ("Toggle" per ELMS-08). Smoke-tested live: search both ways,
  create+login, duplicate blocked (dbRows=1), edit incl. blank-password keeps login,
  usage row 18/3/15 exact, deactivate blocks login with history preserved
  (IsActive=0, leave row intact), reactivate restores login. Test rows cleaned up.
  STOPPING here per phase-gate rule. Next: Phase 2 (ELMS-09).
- 2026-09-18: Phase 2 COMPLETE (ELMS-09–11). Build clean.
  Employee/Apply + History; Admin/LeaveRequests with status filter and per-row
  Approve/Reject (formaction split) + optional remarks; re-decision blocked in
  LeaveService with TempData error banner. Fixed mid-phase: AdminController now
  uses ILeaveService for decisions (was ILeaveRepository — build error).
  Smoke-tested live: valid→Pending+History redirect, overlap→clear msg/0 rows,
  ToDate<FromDate→model error/0 rows (DB-confirmed), history isolation, admin
  filter, approve sets ReviewedBy=1+date, re-approve blocked, reject path,
  pending empties. Test rows cleaned (0 leaves, 2 users).
  STOPPING here per phase-gate rule. Next: Phase 3 (ELMS-12).
- 2026-09-18: Phase 3 COMPLETE (ELMS-12–13). Build clean.
  Admin/Index counts computed on read (2 queries); Employee/Dashboard uses
  GetLeaveUsageAsync (DATABASE.md §6) + history breakdown + Take(5) recent.
  Fixed mid-phase: duplicate CurrentUserId helper + dropped GET Apply restored;
  typo classobj in view. Smoke-tested live: admin stats 2,1,2,1 matched manual
  SQL counts exactly; emp dash 20/3/17 + breakdown 1,1,1 + 3 recent rows;
  zero-states render (1,0,0,0 / 20 remaining). Test rows cleaned.
  STOPPING here per phase-gate rule. Next: Phase 4 (ELMS-14).
- 2026-09-18: Phase 4 COMPLETE (ELMS-14–16). MVP DONE — all Phase 0–4 tickets green.
  ELMS-14: themed generic Error page (no trace/dev hints); verified in Production —
  direct /Home/Error generic, and a genuine 500 (unreachable DB) returned the
  friendly page (status 500) with zero leaked detail. ServiceResult pattern
  already covered expected failures (Phases 1–2).
  ELMS-15: C:\AbhiLab\ELMS\README.md (setup, credentials, migrations, deploy).
  ELMS-16 rehearsal per DEPLOYMENT.md: Release build clean → DB backup to
  C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\LeaveManagementDb_20260918_pre16.bak
  (copy-out blocked by service ACL — backup verified in place instead) →
  `database update` idempotent no-op → publish to LeaveManagementSystem\publish →
  full §7 smoke on published bits (both logins, apply→approve→visible) → cleaned.
  Final DB state: 2 users, 0 leaves. STOPPING — Phase 5 (ELMS-17–19) BLOCKED
  until explicit "start phase 5".
- 2026-09-18: Phase 5 COMPLETE (ELMS-17–19, go-ahead received). Build clean.
  ELMS-17: NotificationHub (/hub/notifications, [Authorize]), LeaveService pushes
  LeaveStatusChanged to the employee after save (best-effort, never rolls back);
  dashboard toast via SignalR CDN client. Verified: auth'd negotiate→connectionId,
  anon→login bounce, approve flow unaffected.
  ELMS-18: status/date/search filters on Admin/LeaveRequests (GetFilteredAsync) +
  Export to Excel via ClosedXML (streamed). Verified filters narrow correctly;
  export content-type/filename ok and unzipped .xlsx contains headers + row data.
  No department filter — MVP schema has no Department model (DATABASE.md optional).
  ELMS-19: AuditLog entity + AddAuditLog migration (table verified in DB) +
  IAuditService recorded in the same SaveChanges transaction as the decision;
  Reviewed By/Date columns on admin list. Verified: Approved/Rejected rows with
  ActionBy=1 + dates set; blocked re-decision writes no audit row.
  Note: each `migrations add` re-salting seed hashes is a harmless cosmetic quirk
  (logins re-verified after AddAuditLog). All test rows cleaned (0 leaves, 0 audits).
  APPLICATION COMPLETE — ELMS-01 through ELMS-19 all green.
- 2026-09-18: Working-day rule implemented (all ELMS tickets still green).
  New shared Services/LeaveDaysCalculator.CountWorkingDays (Mon–Fri inclusive,
  throws on inverted range); replaced calendar math in EmployeeService usage,
  Admin Employees list + Excel export, and History/LeaveRequests/Dashboard views
  (via _ViewImports). No schema change; SQL Express only; overlap/date validation
  untouched. New LeaveManagementSystem.Tests (xUnit): 14/14 pass — all 10 required
  cases (incl. Fri→Mon=2, Sat→Sun=0, Mon→Sun week=5, invalid range) + usage math
  (7 used/13 remaining incl. a 0-day weekend approval) + submit/overlap validation.
  Live-verified on real DB rows: Fri→Mon approved shows Used=2 (old logic: 4);
  History row shows 2 days. Test artifacts cleaned. CHANGELOG.md created.
- 2026-09-18: SignalR audit+fix (no ticket — health check on ELMS-17).
  Rig-tested with real cookie-auth hub connections: approve→employee already
  worked (hub/auth/transport proven); submit→admin was missing by design.
  Fixed with the same hub (no duplication): Admins group in OnConnectedAsync,
  LeaveSubmitted push after submit save, one auto-reconnecting connection in
  _Layout for all authed pages (toast host moved there, per-page Dashboard
  script deleted), signalr.min.js 8.0.7 vendored locally (was CDN-only).
  Re-tested: submit→admin PASS, approve→employee PASS; unit tests 14/14;
  /lib/signalr/signalr.min.js serves 200.   Rig + test rows removed.
- 2026-09-18: Apply Leave date validation (no ticket — rule change).
  Backend: [NoPastDate] attribute on ApplyLeaveViewModel.FromDate +
  past-date guard in LeaveService.SubmitRequestAsync (FromDate≤ToDate already
  covered). Frontend: From min=today, To min follows From via syncToDate()
  (clears invalid To, same-day allowed). Working-day calc + styling untouched.
  Tests 18/18 (4 new); live: min renders, past POST blocked with 0 rows,
  same-day-today → Pending. Artifacts cleaned.
- 2026-09-20: ELMS-20 Role-based FAQ / Help DONE. `Faq()` on both controllers
  (role-locked by existing `[Authorize]`), `FaqData` (7 employee + 6 admin
  items, shared workflow steps), `_WorkflowStrip` + `_FaqAccordion` partials,
  `Admin/Faq` + `Employee/Faq` views, `faq.css`, FAQ links in both menus,
  `FAQ_WIRING.md`. Cancel/modify answer states no self-service cancel exists
  (submit + approve/reject only) and points to Admin. Verified: build clean,
  18/18 tests, 24/24 live Development checks (auth gates, role-correct
  rendering, cross-role blocks, css 200). Temp smoke scripts removed.
