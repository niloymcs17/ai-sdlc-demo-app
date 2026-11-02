namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through Draft, PendingApproval, Approved, or Rejected.
/// A single Manager or Director decides invoices at or under the Director limit.
/// Amounts above the Director limit stay pending after the first approval and need a second, distinct Director.
/// Every check finishes before the invoice changes.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, Approver approver)
    {
        if (approver is null || string.IsNullOrWhiteSpace(approver.UserId))
        {
            throw new ArgumentException("An approver with a user id is required.", nameof(approver));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);
        EnsureCanApprove(invoice, approver);

        var approvedAt = DateTime.UtcNow;
        invoice.Approvals.Add(new ApprovalRecord(approver.UserId, approver.Role, approvedAt));
        if (invoice.Amount <= ApprovalLimits.DirectorLimit || invoice.Approvals.Count == 2)
        {
            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = approver.UserId;
            invoice.DecidedAt = approvedAt;
        }

        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private static void EnsureCanApprove(Invoice invoice, Approver approver)
    {
        if (invoice.Amount <= 0)
        {
            throw new ApprovalRefusedException(
                ApprovalRefusedException.AmountNotPositiveCode,
                ApprovalRefusedException.AmountNotPositiveMessage);
        }

        if (!IsManager(approver) && !IsDirector(approver))
        {
            throw new ApprovalRefusedException(
                ApprovalRefusedException.RoleNotPermittedCode,
                ApprovalRefusedException.RoleNotPermittedMessage);
        }

        if (invoice.Amount <= ApprovalLimits.DirectorLimit)
        {
            if (IsManager(approver) && invoice.Amount > ApprovalLimits.ManagerLimit)
            {
                throw new ApprovalRefusedException(
                    ApprovalRefusedException.ExceedsManagerLimitCode,
                    ApprovalRefusedException.ExceedsManagerLimitMessage);
            }

            return;
        }

        if (invoice.Approvals.Count == 0)
        {
            return;
        }

        foreach (var record in invoice.Approvals)
        {
            if (string.Equals(record.UserId, approver.UserId, StringComparison.Ordinal))
            {
                throw new ApprovalRefusedException(
                    ApprovalRefusedException.SameApproverCode,
                    ApprovalRefusedException.SameApproverMessage);
            }
        }

        if (!IsDirector(approver))
        {
            throw new ApprovalRefusedException(
                ApprovalRefusedException.SecondApproverMustBeDirectorCode,
                ApprovalRefusedException.SecondApproverMustBeDirectorMessage);
        }
    }

    private static bool IsManager(Approver approver) =>
        string.Equals(approver.Role, Roles.Manager, StringComparison.Ordinal);

    private static bool IsDirector(Approver approver) =>
        string.Equals(approver.Role, Roles.Director, StringComparison.Ordinal);

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
