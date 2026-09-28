# ELMS — Current Session (2026-09-27, Phase 11)

## Last verified state
- Phase 10 complete (ELMS-31–33): Timesheet rules, audited admin corrections.
- Phase 11 complete (ELMS-34–35): Admin backdate fix — Admin can fill/edit
  any past working date via Employee + Date selector; employee restriction
  (current-day only) enforced by role-separated endpoints, bypassed securely
  by Admin-only actions. Weekends/holidays stay Weekend/Holiday; leave stays
  Leave; backdated entry flips Absent → Present.
- Build: clean (0 warn/0 err). Tests: 73/73 pass.
- DB `.\SQLEXPRESS/LeaveManagementDb`: unchanged since Phase 10 (6 migrations,
  latest `20260927123644_AddAttendanceAudit`; no schema change in Phase 11).
  Seed logins intact (`admin@example.com` / `employee@example.com`).

## Prior phases (history)
- Phase 9 (ELMS-28–30): click-current-date popup with Working Hours
  + Project Name; admin employee-wise calendar; centralized report.
- Phase 10 (ELMS-31–33): Timesheet rules — current-day-only submission,
  auto-Absent, Weekend > Holiday > Leave priority, leave-aware Month
  calendar, working-dates-only admin corrections, every change audited
  (67/67 tests at the time).

## Journey (verified against code + tests)
- Employee: current date → click → popup (Date read-only, Hours, Project,
  Remarks) → Submit → Present with hours/project. Past/future, weekends,
  holidays, duplicates rejected server-side (`SubmitDailyAttendanceAsync`).
- Missed day: next day it reads Absent automatically (no record + past
  working day); employee has no edit/backfill endpoint, so Tuesday cannot
  fill Monday.
- Priority everywhere: Weekend > Holiday > ApprovedLeave > PendingLeave >
  Timesheet > Absent (past) / NoRecord (future). Rejected leave never shows
  as leave.
- Admin: Daily/Monthly/EmployeeWise/Report views; Correct (by record id) and
  Mark/Upsert (past working dates only — weekends/holidays/future rejected);
  hours + project + remarks editable. Each change writes an `AuditLogs` row
  (`AttendanceRecordId`, action Added/Corrected, details "date · status ·
  hours · project") in the same transaction.

## What changed in Phase 11 (Admin backdate fix)
- `AttendanceController` takes `IEmployeeService`; `Mark` GET/POST populate
  an Employee dropdown (`ViewBag.Employees`, repopulated on validation
  failure); `AttendanceUpsertViewModel.UserId` requires a selected employee.
- Secure bypass design (made explicit + tested): employee `Submit` is
  Employee-only AND today-only; admin `Mark`/`Correct`/`Upsert` are
  Admin-only with NO today-equality rule — the `[Authorize(Roles=...)]`
  attribute, not caller honesty, grants the bypass. Service guards
  (future/weekend/holiday) still apply to admins so those days keep
  rendering Weekend/Holiday.
- `Tests/AdminBackdateTests.cs` (6 tests): role-separation matrix,
  same-back-date employee-rejected/admin-succeeds, Absent → Present flip,
  backdated edit + audit, Weekend/Holiday/Leave rules hold, future rejected.

## What changed in Phase 10
- `AuditLogs`: `LeaveRequestId` nullable, new `AttendanceRecordId?` (FK,
  Restrict) + `Details nvarchar(500)?` (migration above; Express only).
- `IAuditService.RecordAttendanceChangeAsync`; `AuditService` implements it
  on the shared DbContext.
- `AttendanceService` takes `IAuditService` (optional ctor param, Null Object
  fallback so older tests compile); `CorrectAsync`/`UpsertAsync` accept the
  acting admin id and audit Added/Corrected with details. `UpsertAsync` now
  rejects weekends/holidays. Controllers pass the signed-in admin id.
- `Attendance/Month` is leave-aware (`LeaveByDate`: approved → Leave badge,
  pending → Pending badge; never Absent on a leave day).
- `Tests/TimesheetRulesTests.cs` (10 tests): Monday Present/Absent pair,
  no-backfill API-bypass rejection, Weekend/Holiday/Leave priority, admin
  backfill success + audit row, correction audit details, weekend/holiday
  upsert rejection with zero audit rows, current-day end-to-end, audit schema.
- `Tests/FakeInfra.cs`: `FakeAudit` implements the new audit method.

## Pending / next steps
- No open defects. Next phase only on explicit go-ahead.
- Before any future phase: read this file, `MEMORY.md`, `PROGRESS.md`, then
  inspect the actual source; continue from this verified state.
