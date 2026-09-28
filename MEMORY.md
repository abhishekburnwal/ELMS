# ELMS — Project Memory

## Stack (never change without a recorded decision)
- ASP.NET Core MVC (.NET 8), EF Core Code-First, Razor + Bootstrap 5.
- Database: SQL Server Express ONLY (`.\SQLEXPRESS/LeaveManagementDb`).
  Never introduce SQLite or any second store.
- Auth: cookie + `[Authorize(Roles=...)]`; Admin and Employee seed accounts
  per `docs/DATABASE.md` §4. Never hard-delete employees (soft `IsActive`).

## Architecture rules
- Controllers stay thin; business rules live in services; EF queries in
  repositories. All service/repository registrations are `Scoped`.
- `WorkingCalendarService` is the ONLY working-day decider (Mon–Fri work,
  Sat/Sun weekend, configured holiday). Never add parallel date math.
- `LeaveDaysCalculator` 2-arg signature is frozen (old tests depend on it);
  extend via overloads.
- `EmployeeService` keeps its 2-arg constructor for older tests; DI uses the
  3-arg holiday-aware overload.
- The common calendar (`EmployeeCalendarService`) only READS leave +
  attendance + holiday records — it never duplicates or snapshots them.
  Precedence: Weekend > Holiday > ApprovedLeave > PendingLeave > Attendance
  > Absent (past) / NoRecord (future). Rejected leave is never shown as leave.
- Validation is server-side first (`ServiceResult` + `ModelState`); client
  checks are UX only. Read-only/disabled form fields are never trusted.
  Employees have no edit/backfill endpoint by design — only admins correct
  past dates, and only on working dates (weekends/holidays/future rejected).
- Exceptions: no per-controller try/catch; `GlobalExceptionMiddleware`
  logs to ILogger + `ExceptionLogs` and renders the generic error page.
- Attendance corrections are audited: `AuditLogs` rows carry
  `AttendanceRecordId` + action (Added/Corrected) + details, written in the
  same transaction as the change. `LeaveRequestId` is nullable for this.

## UI language
- Navy/teal/amber palette, Manrope + IBM Plex Sans, left rail nav, stat
  cards, panel tables, status badges, responsive `.cal-grid` (7→4→2).
  Keep it; extend with the same classes.

## Session log (newest last)
- Phase 0–4 MVP, Phase 5 bonus, working-day rule, SignalR both directions,
  past-date guard, ELMS-20 FAQ, ELMS-21 exception logging — see PROGRESS.md.
- Phase 8 (ELMS-22–27): Attendance + Holiday Calendar + common calendar.
- Phase 9 (ELMS-28–30, 2026-09-27): click-current-date popup submission
  (hours + project), admin employee-wise calendar with detail modals,
  centralized hours/projects report. 57/57 tests; migration
  `20260927122735_AddAttendanceHoursAndProject` applied; seed logins intact.
- Phase 10 (ELMS-31–33, 2026-09-27): Timesheet rules — current-day-only
  submission (no backfill; missed day auto-Absent), Weekend > Holiday >
  Leave > Timesheet > Absent priority, leave-aware Month calendar, admin
  corrections on working dates only with every change audited (`AuditLogs`.
  `AttendanceRecordId` + Details, same transaction). 67/67 tests; migration
  `20260927123644_AddAttendanceAudit` applied; seed logins intact.
- Phase 11 (ELMS-34–35, 2026-09-27): Admin backdate fix — Employee + Date
  selector on Mark; role-separated endpoints are the secure bypass
  (Submit = Employee-only + today-only; Mark/Correct = Admin-only, no
  today-equality). No schema change. 73/73 tests.
