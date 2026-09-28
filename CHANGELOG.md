# Changelog

## 2026-09-27 — Admin backdate fix: Employee + Date selector, role-secured bypass (ELMS-34–35)

### Fix
- Admin `Attendance/Mark` had a raw numeric Employee Id input; it now has an Employee dropdown + Date picker (repopulated on validation failure; selecting an employee is validated). Admin picks Employee + Date, enters Hours + Project + Remarks, saves — any past working date.
- Secure bypass made explicit: employee `Submit` is `[Authorize(Roles="Employee")]` AND today-only in the service; admin `Mark`/`Correct` are `[Authorize(Roles="Admin")]` with NO today-equality rule — the role attribute, not caller honesty, grants the bypass. Service guards (future/weekend/holiday) still apply to admins, so those days keep rendering Weekend/Holiday and leave keeps rendering Leave; a backdated entry flips Absent → Present.
- No schema change; Leave/Attendance functionality and UI otherwise untouched; SQL Server Express only.

### Tests (ELMS-35)
- New `Tests/AdminBackdateTests.cs` (6 tests): endpoint role-separation matrix (Submit=Employee-only; Mark/Correct/Report/Daily/Monthly/EmployeeWise=Admin-only), same-back-date employee-rejected/admin-succeeds, Absent → Present flip with hours/project detail, backdated edit + audit row, Weekend/Holiday/Leave rules hold, future-date rejection.

Changed files: `Controllers/AttendanceController.cs` (IEmployeeService, Mark dropdown data), `Views/Attendance/Mark.cshtml` (select), `ViewModels/AttendanceViewModels.cs` (UserId range validation), `Tests/AdminBackdateTests.cs` (new).

Verified: build clean (0 warn/0 err); 73/73 tests pass (67 existing + 6 new); DB unchanged (no migration; 6 applied; seed users intact); no SQLite reference in any csproj.


## 2026-09-27 — Timesheet rules: no backfill, auto-Absent, audited admin corrections (ELMS-31–33)

### Journey check (existing behavior confirmed, then hardened)
- Employee current-day popup submission, duplicate/past/future/weekend/holiday rejection, missed-day auto-Absent at read time, common-calendar precedence, and admin Correct/Mark all already existed from Phases 8–9. Gaps found and closed: admin corrections were NOT audited, admin Mark allowed weekends/holidays, and the employee Month calendar showed Absent on approved-leave days.

### Employee rules (ELMS-31)
- Timesheet only for the CURRENT date via the Month-calendar popup (Date read-only, Hours, Project, Remarks). `SubmitDailyAttendanceAsync` rejects past/future dates, weekends, holidays, and duplicates — direct API posts with forged dates are rejected the same way (covered by a bypass test).
- A missed working day reads Absent the next day automatically; there is no employee edit/backfill endpoint, so Tuesday cannot fill Monday. Weekends read Weekend, holidays read Holiday, approved leave reads Leave — never Absent.

### Admin rules + audit (ELMS-32)
- Admin views employee-wise calendars (selector + month/year), daily/monthly/employee-wise lists, and the centralized hours/projects Report — unchanged.
- Admin Correct (by record) and Mark/Upsert (past working dates only: weekends, holidays, and future dates rejected) accept Hours + Project + Remarks.
- Every admin add/correction writes an `AuditLogs` row in the same transaction: `AttendanceRecordId`, action Added/Corrected, details “date · status · hours · project”, acting admin id. Schema: `LeaveRequestId` nullable, new `AttendanceRecordId?` (FK, Restrict) + `Details nvarchar(500)?` via migration `20260927123644_AddAttendanceAudit` (SQL Server Express only).
- `Attendance/Month` is now leave-aware: approved days show Leave (Pending for pending), never Absent. Priority everywhere: Weekend > Holiday > Leave > Timesheet > Absent/NoRecord.

### Tests (ELMS-33)
- New `Tests/TimesheetRulesTests.cs` (10 tests, past-relative dates so they hold on any run date): Monday Present/Absent pair, backfill API-bypass rejection, Weekend/Holiday/Leave priority, admin backfill success + audit row, correction audit details, weekend/holiday upsert rejection with zero audit rows, current-day end-to-end, audit schema.

Changed files (new unless noted):
- `Models/Entities/AuditLog.cs` (edit), `Data/ApplicationDbContext.cs` (edit), `Data/Migrations/20260927123644_AddAttendanceAudit.cs` (new)
- `Services/Interfaces/IAuditService.cs` (edit), `Services/AuditService.cs` (edit)
- `Services/AttendanceService.cs` (edit: audit wiring, working-date guard on Upsert), `Services/Interfaces/IAttendanceService.cs` (edit: actor id params)
- `Controllers/AttendanceController.cs` (edit: leave-aware Month, admin id passthrough)
- `ViewModels/AttendanceViewModels.cs` (edit: LeaveByDate), `Views/Attendance/Month.cshtml` (edit: priority rendering)
- `Tests/TimesheetRulesTests.cs` (new), `Tests/FakeInfra.cs` (edit: FakeAudit)
- `CURRENT_SESSION.md`, `MEMORY.md` (edit), `PROGRESS.md` (edit), docs `BACKLOG.md` + `DATABASE.md` (edit)

Verified: build clean (0 warn/0 err); 67/67 tests pass (57 existing + 10 new); migration applied to `.\SQLEXPRESS/LeaveManagementDb` (AuditLogs columns confirmed via INFORMATION_SCHEMA; 6 migrations in history; 2 seed users intact); no SQLite reference in any csproj.


## 2026-09-27 — Click-to-submit attendance with hours & project (ELMS-28–30)

### Employee: click current date → popup → submit (ELMS-28)
- `Attendance/Month` is now interactive: only the current-date cell is clickable (working day + unmarked) and opens a Bootstrap modal with Date (read-only), Working Hours (default 9), Project Name, and optional Remarks → POST `Attendance/Submit`.
- New `AttendanceService.SubmitDailyAttendanceAsync`: current date ONLY — past/future dates rejected; weekends, configured holidays, and duplicate submissions rejected; hours 0.5–24 and project name required. Backend is the source of truth (popup date re-validated server-side).
- Submitted days render Present with hours + project on the employee calendar (e.g. 20 Tue, Present, 9 Hours, Project: ELMS Development).

### Admin: employee-wise calendar + centralized report (ELMS-29)
- `Calendar/Index` for Admins: employee dropdown + month picker (defaults to first employee), per-date detail modals showing status, working hours, project, remarks, check-in/out. Leave/Holiday/Weekend integration unchanged.
- New `Attendance/Report`: centralized month view — per-employee totals (days present, total hours, project list) plus every daily record with hours and project, so the admin sees who worked on which project and for how long.
- `EmployeeWise` gained Total Hours, Projects, and per-employee Calendar-link columns; `Correct`/`Mark` accept hours/project. Report linked from the Daily toolbar.

### Schema, validation, tests (ELMS-30)
- Migration `20260927122735_AddAttendanceHoursAndProject`: `AttendanceRecords.WorkingHours decimal(4,2)?`, `ProjectName nvarchar(200)?` (SQL Server Express only).
- `CalendarDay` carries `WorkingHours`/`ProjectName`/`Remarks`/`AttendanceId`; detail text e.g. “9 Hours · Project: ELMS Development”.
- Validation server-side throughout (`ServiceResult` + DataAnnotations); unhandled exceptions still flow to `GlobalExceptionMiddleware` (ILogger + `ExceptionLogs`).
- New `Tests/DailyAttendanceSubmissionTests.cs` (18 cases): submit success, duplicate/past/future/weekend/holiday rejection, hours + project validation, popup model validation, calendar hours/project display, leave/holiday precedence, role attributes (Submit=Employee, Report=Admin), no-SQLite check.

Changed files (new unless noted):
- `Models/Entities/AttendanceRecord.cs` (edit), `Data/ApplicationDbContext.cs` (edit), `Data/Migrations/20260927122735_AddAttendanceHoursAndProject.cs` (new)
- `Services/Interfaces/IAttendanceService.cs` (edit: Submit + optional hours/project on Correct/Upsert), `Services/AttendanceService.cs` (edit)
- `Services/Interfaces/IEmployeeCalendarService.cs` (edit: CalendarDay fields), `Services/EmployeeCalendarService.cs` (edit)
- `Controllers/AttendanceController.cs` (edit: Month state + Submit + Report), `Controllers/CalendarController.cs` (edit: employee selector + attendance details)
- `ViewModels/AttendanceViewModels.cs` (edit: Submit form, Month state, Report rows, calendar attendance map)
- `Views/Attendance/Month.cshtml` (interactive + modal), `Views/Calendar/Index.cshtml` (selector + detail modals), `Views/Attendance/Report.cshtml` (new), `Views/Attendance/EmployeeWise.cshtml`, `Correct.cshtml`, `Mark.cshtml`, `Daily.cshtml` (edit)
- `wwwroot/css/site.css` (edit: clickable cells + details)
- `Tests/DailyAttendanceSubmissionTests.cs` (new); `CURRENT_SESSION.md`, `MEMORY.md` (new)

Verified: build clean (0 warn/0 err); 57/57 tests pass (39 existing + 18 new); migration applied to `.\SQLEXPRESS/LeaveManagementDb` (WorkingHours decimal + ProjectName nvarchar confirmed via INFORMATION_SCHEMA; 5 migrations in history; 2 seed users intact); no SQLite reference in any csproj.


## 2026-09-27 — Attendance, Holiday Calendar & Leave integration (ELMS-22–27)

### Centralized Working Day / Calendar service (ELMS-22)
- **Single decider** — `Services/WorkingCalendarService.cs` (`IWorkingCalendarService`): Monday–Friday = working day, Saturday/Sunday = weekend, configured holiday = holiday. Leave and Attendance both call it; no separate calculations.
- `Services/LeaveDaysCalculator.cs` keeps its 2-arg signature (all 22 old tests untouched) plus a holiday-aware overload used through the service.
- `Services/EmployeeService.cs` keeps its 2-arg constructor plus a holiday-aware 3-arg overload wired in `Program.cs`; `AdminController` employees list + Excel export and the History/Dashboard/LeaveRequests views count through the shared service (`ViewBag.Holidays`).

### Attendance module (ELMS-23)
- **New tables via migration `20260927112906_AddAttendanceAndHolidays`** (SQL Server Express only): `AttendanceRecords` (unique `IX_AttendanceRecords_UserId_Date`, `IX_AttendanceRecords_Date`; FK → Users, Restrict).
- Employee: `Attendance/Today` (check-in → Present, check-out stamps time; blocked on weekends/holidays with a clear message), `History` (last 60 days), `Month` grid.
- Admin: `Daily` (by date), `Monthly` report, `EmployeeWise` summary, `Correct` (by record id), `Mark` (manual upsert; future dates rejected server-side).
- Statuses: Present, Absent, Half Day, WFH (plus Leave/Holiday/Weekend markers for corrections).

### Holiday Calendar (ELMS-24)
- **New `Holidays` table** (unique `IX_Holidays_Date`, SQL Server Express only).
- Admin: add/edit/delete (`Holiday/Form`), import CSV/`.xlsx` (header `Name,Date,Description`; weekends + duplicates skipped with imported/skipped counts; 5 MB limit; extension whitelist), export to Excel (ClosedXML, streamed).
- Weekends can never be added manually (Sat/Sun = automatic weekend, enforced server-side). Both roles view `Holiday/Index` (by year) and export; management endpoints are `[Authorize(Roles="Admin")]` (attribute-tested, not just UI-hidden).

### Common Employee Calendar (ELMS-25)
- `Services/EmployeeCalendarService.cs` merges existing records only (never duplicates) with precedence Weekend > Holiday > ApprovedLeave > PendingLeave > Attendance > Absent (past working day) / NoRecord (future). Rejected leaves fall through and are never shown as leave.
- `Calendar/Index` responsive month grid (7→4→2 columns) with legend badges in the ELMS navy/teal/amber language; Admins may pass `userId` to inspect one employee.
- Pending→Approved/Rejected updates appear on next read (no snapshot table).

### UI wiring (ELMS-26)
- Rail nav extended per role; `site.css` appendix (status badges, `.cal-grid`, `.linklike`); existing Leave layout untouched except the holiday-aware Days cell.

### Tests (ELMS-27)
- New `LeaveManagementSystem.Tests/AttendanceHolidayCalendarTests.cs` (17 tests + `FakeHolidayRepository`/`FakeAttendanceRepository`): weekend rule, Fri–Mon=2, week-minus-weekend=5, holiday exclusion, manual-holiday validation, CSV import skips, admin CRUD round-trip, full Mon→Mon integration scenario, status-change updates, working-day check-in, role attributes, SQL-Express-only check.

Changed files (new unless noted):
- `LeaveManagementSystem/Models/Entities/Holiday.cs`, `AttendanceRecord.cs`
- `LeaveManagementSystem/Models/Enums/AttendanceStatus.cs`, `CalendarDayStatus.cs`
- `LeaveManagementSystem/Repositories/Interfaces/IHolidayRepository.cs`, `IAttendanceRepository.cs`
- `LeaveManagementSystem/Repositories/HolidayRepository.cs`, `AttendanceRepository.cs`
- `LeaveManagementSystem/Services/Interfaces/IWorkingCalendarService.cs`, `IHolidayService.cs`, `IAttendanceService.cs`, `IEmployeeCalendarService.cs`
- `LeaveManagementSystem/Services/WorkingCalendarService.cs`, `HolidayService.cs`, `AttendanceService.cs`, `EmployeeCalendarService.cs`
- `LeaveManagementSystem/Services/LeaveDaysCalculator.cs` (edit: overload), `EmployeeService.cs` (edit: overload + holiday-aware usage)
- `LeaveManagementSystem/Controllers/HolidayController.cs`, `AttendanceController.cs`, `CalendarController.cs`
- `LeaveManagementSystem/Controllers/AdminController.cs` (edit: shared-service counting + ViewBag holidays), `EmployeeController.cs` (edit: ViewBag holidays)
- `LeaveManagementSystem/ViewModels/HolidayViewModels.cs`, `AttendanceViewModels.cs`
- `LeaveManagementSystem/Views/Holiday/*.cshtml`, `Views/Attendance/*.cshtml`, `Views/Calendar/Index.cshtml`
- `LeaveManagementSystem/Views/Shared/_Layout.cshtml` (edit: nav), `wwwroot/css/site.css` (edit: appendix)
- `LeaveManagementSystem/Views/Employee/History.cshtml`, `Dashboard.cshtml`, `Views/Admin/LeaveRequests.cshtml` (edit: holiday-aware Days cell)
- `LeaveManagementSystem/Data/ApplicationDbContext.cs` (edit: DbSets + config)
- `LeaveManagementSystem/Data/Migrations/20260927112906_AddAttendanceAndHolidays.cs` (new)
- `LeaveManagementSystem/Program.cs` (edit: DI)
- `LeaveManagementSystem.Tests/AttendanceHolidayCalendarTests.cs` (new)

Verified: build clean (0 warn/0 err); 39/39 tests pass (22 existing + 17 new); migration applied to `.\SQLEXPRESS/LeaveManagementDb` (sqlcmd: Holidays 0 rows, AttendanceRecords 0 rows, Users admin@example.com/Role1 + employee@example.com/Role2 intact); no SQLite reference in any csproj; Razor views compile with the build.



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
