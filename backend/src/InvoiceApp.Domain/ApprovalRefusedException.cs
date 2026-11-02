using System.Globalization;

namespace InvoiceApp.Domain;

/// <summary>
/// An approval attempt was refused. The invoice is left unchanged.
/// This is not a rejection: only the explicit reject action sets Rejected.
/// </summary>
public class ApprovalRefusedException : InvalidOperationException
{
    public const string ExceedsManagerLimitCode = "exceeds-manager-limit";
    public const string RoleNotPermittedCode = "role-not-permitted";
    public const string AmountNotPositiveCode = "amount-not-positive";
    public const string RequiresTwoApproversCode = "requires-two-approvers";
    public const string SameApproverCode = "same-approver";
    public const string SecondApproverMustBeDirectorCode = "second-approver-must-be-director";

    public const string ExceedsManagerLimitMessage = "Amount exceeds the Manager approval limit.";
    public const string RoleNotPermittedMessage = "Only a Manager or a Director can approve an invoice.";
    public const string AmountNotPositiveMessage = "Invoice amount must be greater than zero.";
    public const string SameApproverMessage = "The same user cannot approve this invoice twice.";
    public const string SecondApproverMustBeDirectorMessage = "The second approver must be a Director.";

    public static readonly string RequiresTwoApproversMessage = string.Create(
        CultureInfo.InvariantCulture,
        $"Invoices above {ApprovalLimits.DirectorLimit:N2} require two approvers.");

    public ApprovalRefusedException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }

    public string ReasonCode { get; }
}
