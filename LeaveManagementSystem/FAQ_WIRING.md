# ELMS-20 — FAQ / Help wiring guide

Role-scoped help pages. Each role sees only its own FAQs; the only shared
content is the leave-workflow strip.

## Routes (both `[HttpGet]`, role-locked by the controller's `[Authorize]`)

| URL | Action | View | Content |
|---|---|---|---|
| `/Admin/Faq` | `AdminController.Faq()` | `Views/Admin/Faq.cshtml` | `FaqData.AdminFaqs` (6 items) |
| `/Employee/Faq` | `EmployeeController.Faq()` | `Views/Employee/Faq.cshtml` | `FaqData.EmployeeFaqs` (7 items) |

Cross-role access is blocked by authorization (employee → `/Admin/Faq` and
admin → `/Employee/Faq` both bounce to sign-in). Nav links live in
`Views/Shared/_Layout.cshtml` (`FAQ / Help` under each role's menu block).

## Shared pieces (no per-role duplication)

- `ViewModels/FaqViewModels.cs` — `FaqItem` record + static `FaqData`:
  `WorkflowSteps` (single source for the strip), `EmployeeFaqs`, `AdminFaqs`.
  Answers are static authored HTML fragments (no user input) rendered raw.
- `Views/Shared/_WorkflowStrip.cshtml` — `@model string` (subtitle only);
  steps come from `FaqData.WorkflowSteps`. The `Approve / Reject` step carries
  the amber `decision` highlight.
- `Views/Shared/_FaqAccordion.cshtml` — `@model IReadOnlyList<FaqItem>`,
  single-open accordion (`aria-expanded` + `.open`), first item open by
  default. Toggle script is delegated and guarded (`window.__elmsFaqBound`).
- `wwwroot/css/faq.css` — workflow + accordion styles in the existing
  navy/teal/amber language; responsive breakpoint at 760px (steps stack,
  arrows rotate). Loaded globally from `_Layout.cshtml`.

## Content rules

- The cancel/modify answer reflects the current workflow: `LeaveService`
  exposes submit + approve/reject only, so there is no self-service
  cancel/modify — the answer says so and points the employee to their Admin.
  If a cancel/withdraw feature is ever added, update that one `FaqItem` in
  `FaqData.EmployeeFaqs`; no view changes needed.
- To add/edit a question, edit the corresponding list in `FaqData` only.
