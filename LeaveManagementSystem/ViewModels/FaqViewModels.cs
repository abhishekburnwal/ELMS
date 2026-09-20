namespace LeaveManagementSystem.ViewModels;

// ELMS-20 — static FAQ content. One item type shared by both roles; each role
// gets its own list so no FAQ text is duplicated except the shared workflow
// strip (rendered by Views/Shared/_WorkflowStrip.cshtml).
public record FaqItem(string Question, string AnswerHtml);

public static class FaqData
{
    // Single source of truth for the leave-workflow strip shown at the top of
    // both FAQ pages: Employee → Apply Leave → Submit Request → Admin
    // Notification → Admin Reviews → Approve/Reject → Employee Notification →
    // Status Updated. bool marks the decision step (amber highlight).
    public static IReadOnlyList<(string Label, bool IsDecision)> WorkflowSteps { get; } =
    [
        ("Employee", false),
        ("Apply Leave", false),
        ("Submit Request", false),
        ("Admin Notification", false),
        ("Admin Reviews", false),
        ("Approve / Reject", true),
        ("Employee Notification", false),
        ("Status Updated", false),
    ];

    public static IReadOnlyList<FaqItem> EmployeeFaqs { get; } =
    [
        new("How do I apply for leave?",
            "Go to <strong>Apply Leave</strong>, select From Date and To Date, enter the reason, and click Submit Request."),
        new("Can I select a past date?",
            "No. Leave can be applied only for today or future dates."),
        new("How are leave days calculated?",
            "Only working days are counted. Saturday and Sunday are excluded."),
        new("What happens after I submit a leave request?",
            "The request is sent to the Admin / Approver for review and remains in <strong>Pending</strong> status until an action is taken."),
        new("How do I know whether my leave is approved?",
            "Check <strong>My History</strong> or the notification section."),
        new("Will I receive a notification when my leave is approved or rejected?",
            "Yes, the system provides a real-time notification when the request status changes."),
        // No self-service cancel/modify exists in the current workflow
        // (LeaveService exposes submit + approve/reject only), so the answer
        // says so and points to the Admin instead of showing controls.
        new("Can I cancel or modify a submitted request?",
            "The current ELMS workflow does not support self-service cancel or modify after submission &mdash; a submitted request stays <strong>Pending</strong> until the Admin approves or rejects it. To change a request, please contact your Admin."),
    ];

    public static IReadOnlyList<FaqItem> AdminFaqs { get; } =
    [
        new("How do I view employee leave requests?",
            "Go to the <strong>Leave Requests / Leave Management</strong> section."),
        new("What happens when an employee submits leave?",
            "The request appears as <strong>Pending</strong> and the Admin receives a notification."),
        new("How do I approve a leave request?",
            "Open the pending request, review the details, and click <strong>Approve</strong>."),
        new("How do I reject a leave request?",
            "Open the request, review the details, provide a reason if required, and click <strong>Reject</strong>."),
        new("Will the employee know when I approve or reject the request?",
            "Yes. The employee receives a real-time notification and the request status is updated."),
        new("Can I see an employee's leave history?",
            "Yes, Admin can view the employee's submitted leave requests and their status."),
    ];
}
