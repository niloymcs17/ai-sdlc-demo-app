using System.Globalization;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        var saved = Assert.Single(repository.Updated);
        Assert.Same(result, saved);
        Assert.Equal(InvoiceStatus.PendingApproval, saved.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var error = Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));

        Assert.Equal("Invoice INV-1 is Approved; only drafts can be submitted.", error.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", new Approver("demo.approver", Roles.Manager));

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        var saved = Assert.Single(repository.Updated);
        Assert.Same(result, saved);
        Assert.Equal(InvoiceStatus.Approved, saved.Status);
        Assert.Equal("demo.approver", saved.DecidedBy);
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        var error = Assert.Throws<KeyNotFoundException>(() =>
            service.Approve("INV-404", new Approver("demo.approver", Roles.Manager)));

        Assert.Equal("Invoice INV-404 was not found.", error.Message);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        var saved = Assert.Single(repository.Updated);
        Assert.Same(result, saved);
        Assert.Equal(InvoiceStatus.Rejected, saved.Status);
        Assert.Equal("Duplicate invoice", saved.DecisionNote);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Reject_InvoiceNotPending_ThrowsAndChangesNothing(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var decisionNoteBefore = invoice.DecisionNote;
        var approvalsBefore = invoice.Approvals.ToList();

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Reject("INV-1", "demo.approver", "Duplicate invoice"));

        Assert.IsNotType<ApprovalRefusedException>(error);
        Assert.Equal($"Invoice INV-1 is {status}; only pending invoices can be decided.", error.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(decisionNoteBefore, invoice.DecisionNote);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var before = Capture(invoice);

        var error = Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));

        Assert.Equal("A reason is required to reject an invoice. (Parameter 'reason')", error.Message);
        Assert.Equal("reason", error.ParamName);
        AssertUnchanged(before, invoice);
        Assert.Empty(repository.Updated);
    }

    public static TheoryData<decimal> ManagerAmountsWithinLimit { get; } = new()
    {
        ApprovalLimits.ManagerLimit,
        100m
    };

    public static TheoryData<string> AmountsOverManagerLimit { get; } = new()
    {
        "0.01",
        "1"
    };

    public static TheoryData<decimal> DirectorAmountsWithinLimit { get; } = new()
    {
        ApprovalLimits.DirectorLimit,
        ApprovalLimits.ManagerLimit + 1m
    };

    public static TheoryData<decimal> AmountsAboveDirectorLimit { get; } = new()
    {
        ApprovalLimits.DirectorLimit + 0.01m,
        ApprovalLimits.DirectorLimit + 1m
    };

    public static TheoryData<string> NonPositiveAmounts { get; } = new()
    {
        "0",
        "-1"
    };

    [Theory]
    [MemberData(nameof(ManagerAmountsWithinLimit))]
    public void Approve_ManagerWithinLimit_ApprovesInvoice(decimal amount)
    {
        var invoice = Pending(amount);

        var result = ServiceFor(invoice).Approve(invoice.Id, Manager());

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Theory]
    [MemberData(nameof(AmountsOverManagerLimit))]
    public void Approve_ManagerAboveLimit_RefusesAndLeavesInvoiceUnchanged(string extra)
    {
        var amount = ApprovalLimits.ManagerLimit + decimal.Parse(extra, CultureInfo.InvariantCulture);
        var invoice = Pending(amount);
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Manager()));

        Assert.Equal(ApprovalRefusedException.ExceedsManagerLimitCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.ExceedsManagerLimitMessage, refused.Message);
        AssertUnchanged(before, invoice);
    }

    [Theory]
    [MemberData(nameof(DirectorAmountsWithinLimit))]
    public void Approve_DirectorWithinLimit_ApprovesInvoice(decimal amount)
    {
        var invoice = Pending(amount);

        var result = ServiceFor(invoice).Approve(invoice.Id, Director());

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_ManagerOnAlreadyApprovedInvoiceAtManagerLimit_Approves()
    {
        var invoice = Pending(ApprovalLimits.ManagerLimit);
        invoice.Approvals.Add(new ApprovalRecord("director.other", Roles.Director, DateTime.UtcNow));
        var service = ServiceFor(invoice);

        var result = service.Approve(invoice.Id, Manager());

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal("director.other", result.Approvals[0].UserId);
        Assert.Equal(Roles.Director, result.Approvals[0].Role);
        Assert.Equal("demo.manager", result.Approvals[1].UserId);
        Assert.Equal(Roles.Manager, result.Approvals[1].Role);
    }

    // Supersedes the Phase 1 D13 placeholder, which refused a Director above the Director limit and recorded no approval.
    [Theory]
    [MemberData(nameof(AmountsAboveDirectorLimit))]
    public void Approve_DirectorAloneAboveDirectorLimit_StaysPendingWithOneRecord(decimal amount)
    {
        var invoice = Pending(amount);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Director("director1"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.NotEqual(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("director1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);
        Assert.NotEqual(default, invoice.Approvals[0].ApprovedAt);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_TwoDirectorsAboveDirectorLimit_ApprovesWithRecordsInOrder()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 0.01m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Director("director1"));
        service.Approve(invoice.Id, Director("director2"));

        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal("director2", invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
        Assert.Equal(2, invoice.Approvals.Count);
        Assert.Equal("director1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);
        Assert.Equal("director2", invoice.Approvals[1].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[1].Role);
    }

    [Fact]
    public void Approve_SecondApproverIsManager_RefusesAndLeavesApprovalsUnchanged()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 0.01m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Director("director1"));

        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("director1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);

        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var refusal = Assert.Throws<ApprovalRefusedException>(() =>
            service.Approve(invoice.Id, Manager("manager1")));

        Assert.Equal(ApprovalRefusedException.SecondApproverMustBeDirectorCode, refusal.ReasonCode);
        Assert.Equal(ApprovalRefusedException.SecondApproverMustBeDirectorMessage, refusal.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("director1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);
    }

    [Fact]
    public void Approve_SameDirectorApprovesTwice_RefusesAndLeavesOneRecord()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 0.01m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Director("director1"));

        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var refusal = Assert.Throws<ApprovalRefusedException>(() =>
            service.Approve(invoice.Id, Director("director1")));

        Assert.Equal(ApprovalRefusedException.SameApproverCode, refusal.ReasonCode);
        Assert.Equal(ApprovalRefusedException.SameApproverMessage, refusal.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("director1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);
    }

    [Fact]
    public void Approve_SameUserIdOnDistinctApproverObjects_RefusesAsSameUser()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 0.01m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, new Approver("d1", Roles.Director));

        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var refusal = Assert.Throws<ApprovalRefusedException>(() =>
            service.Approve(invoice.Id, new Approver("d1", Roles.Director)));

        Assert.Equal(ApprovalRefusedException.SameApproverCode, refusal.ReasonCode);
        Assert.Equal(ApprovalRefusedException.SameApproverMessage, refusal.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("d1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[0].Role);
    }

    [Fact]
    public void Approve_SameManagerApprovesTwice_ReportsSameUserBeforeDirectorCheck()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 1m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Manager("manager1"));

        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var refusal = Assert.Throws<ApprovalRefusedException>(() =>
            service.Approve(invoice.Id, new Approver("manager1", Roles.Manager)));

        Assert.Equal(ApprovalRefusedException.SameApproverCode, refusal.ReasonCode);
        Assert.Equal(ApprovalRefusedException.SameApproverMessage, refusal.Message);
        Assert.NotEqual(ApprovalRefusedException.SecondApproverMustBeDirectorCode, refusal.ReasonCode);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("manager1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Manager, invoice.Approvals[0].Role);
    }

    [Fact]
    public void Approve_TwoManagersAboveDirectorLimit_RefusesBecauseSecondMustBeDirector()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 1m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Manager("manager1"));

        var statusBefore = invoice.Status;
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var approvalsBefore = invoice.Approvals.ToList();

        var refusal = Assert.Throws<ApprovalRefusedException>(() =>
            service.Approve(invoice.Id, Manager("manager2")));

        Assert.Equal(ApprovalRefusedException.SecondApproverMustBeDirectorCode, refusal.ReasonCode);
        Assert.Equal(ApprovalRefusedException.SecondApproverMustBeDirectorMessage, refusal.Message);
        Assert.Equal(statusBefore, invoice.Status);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("manager1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Manager, invoice.Approvals[0].Role);
    }

    [Fact]
    public void Approve_ManagerFirstThenDirectorAboveDirectorLimit_ApprovesOnSecond()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit + 1m);
        var service = ServiceFor(invoice);

        service.Approve(invoice.Id, Manager("manager1"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal(1, invoice.Approvals.Count);
        Assert.Equal("manager1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Manager, invoice.Approvals[0].Role);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);

        service.Approve(invoice.Id, Director("director1"));

        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal("director1", invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
        Assert.Equal(2, invoice.Approvals.Count);
        Assert.Equal("manager1", invoice.Approvals[0].UserId);
        Assert.Equal(Roles.Manager, invoice.Approvals[0].Role);
        Assert.Equal("director1", invoice.Approvals[1].UserId);
        Assert.Equal(Roles.Director, invoice.Approvals[1].Role);
    }

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void Approve_AmountNotPositive_RefusesAndLeavesInvoiceUnchanged(string amountText)
    {
        var invoice = Pending(decimal.Parse(amountText, CultureInfo.InvariantCulture));
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Manager()));

        Assert.Equal(ApprovalRefusedException.AmountNotPositiveCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.AmountNotPositiveMessage, refused.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_NegativeAmountByDirector_RefusesAndLeavesInvoiceUnchanged()
    {
        var invoice = Pending(-1m);
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Director()));

        Assert.Equal(ApprovalRefusedException.AmountNotPositiveCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.AmountNotPositiveMessage, refused.Message);
        AssertUnchanged(before, invoice);
    }

    [Theory]
    [InlineData(Roles.Approver)]
    [InlineData(Roles.Admin)]
    [InlineData("Clerk")]
    [InlineData("")]
    public void Approve_RoleWithoutAuthority_RefusesAndLeavesInvoiceUnchanged(string role)
    {
        var invoice = Pending(100m);
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, new Approver("demo.user", role)));

        Assert.Equal(ApprovalRefusedException.RoleNotPermittedCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.RoleNotPermittedMessage, refused.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_ManagerAtDirectorLimit_RefusesAndLeavesInvoiceUnchanged()
    {
        var invoice = Pending(ApprovalLimits.DirectorLimit);
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Manager()));

        Assert.Equal(ApprovalRefusedException.ExceedsManagerLimitCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.ExceedsManagerLimitMessage, refused.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_BlankUserId_ThrowsArgumentException()
    {
        var invoice = Pending(100m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Capture(invoice);

        var error = Assert.Throws<ArgumentException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, new Approver("  ", Roles.Director)));

        Assert.IsType<ArgumentException>(error);
        Assert.Equal("An approver with a user id is required. (Parameter 'approver')", error.Message);
        Assert.Equal("approver", error.ParamName);
        AssertUnchanged(before, invoice);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public void Approve_NullApprover_ThrowsArgumentException()
    {
        var invoice = Pending(100m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Capture(invoice);

        var error = Assert.Throws<ArgumentException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, null!));

        Assert.IsType<ArgumentException>(error);
        Assert.Equal("An approver with a user id is required. (Parameter 'approver')", error.Message);
        Assert.Equal("approver", error.ParamName);
        AssertUnchanged(before, invoice);
        Assert.Empty(repository.Updated);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var repository = new FakeInvoiceRepository(invoice);
        var decidedByBefore = invoice.DecidedBy;
        var decidedAtBefore = invoice.DecidedAt;
        var decisionNoteBefore = invoice.DecisionNote;
        var approvalsBefore = invoice.Approvals.ToList();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, Manager()));

        Assert.IsNotType<ApprovalRefusedException>(error);
        Assert.Equal(status, invoice.Status);
        Assert.Equal($"Invoice INV-1 is {status}; only pending invoices can be decided.", error.Message);
        Assert.Equal(decidedByBefore, invoice.DecidedBy);
        Assert.Equal(decidedAtBefore, invoice.DecidedAt);
        Assert.Equal(decisionNoteBefore, invoice.DecisionNote);
        Assert.Equal(approvalsBefore, invoice.Approvals);
        Assert.Empty(repository.Updated);
    }

    private static Invoice Pending(decimal amount) =>
        new() { Id = "INV-1", CustomerName = "Test School", Amount = amount, Status = InvoiceStatus.PendingApproval };

    private static Approver Manager(string userId = "demo.manager") => new(userId, Roles.Manager);

    private static Approver Director(string userId = "demo.director") => new(userId, Roles.Director);

    private static InvoiceSnapshot Capture(Invoice invoice) =>
        new(
            invoice.Status,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.Approvals.ToList(),
            invoice.DecisionNote);

    private static void AssertUnchanged(InvoiceSnapshot before, Invoice invoice)
    {
        Assert.Equal(before.Status, invoice.Status);
        Assert.Equal(before.DecidedBy, invoice.DecidedBy);
        Assert.Equal(before.DecidedAt, invoice.DecidedAt);
        Assert.Equal(before.Approvals, invoice.Approvals);
        Assert.Equal(before.DecisionNote, invoice.DecisionNote);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.NotEqual(InvoiceStatus.Rejected, invoice.Status);
    }

    private sealed record InvoiceSnapshot(
        InvoiceStatus Status,
        string? DecidedBy,
        DateTime? DecidedAt,
        List<ApprovalRecord> Approvals,
        string? DecisionNote);
}
