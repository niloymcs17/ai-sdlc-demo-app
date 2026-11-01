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

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", new Approver("demo.approver", Roles.Manager));

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", new Approver("demo.approver", Roles.Manager)));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() =>
            service.Approve("INV-404", new Approver("demo.approver", Roles.Manager)));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
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

    public static TheoryData<string> AmountsOverDirectorLimit { get; } = new()
    {
        "0.01",
        "1"
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

    [Theory]
    [MemberData(nameof(AmountsOverDirectorLimit))]
    public void Approve_DirectorAboveLimit_RefusesAndLeavesInvoiceUnchanged(string extra)
    {
        var amount = ApprovalLimits.DirectorLimit + decimal.Parse(extra, CultureInfo.InvariantCulture);
        var invoice = Pending(amount);
        var before = Capture(invoice);

        var refused = Assert.Throws<ApprovalRefusedException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Director()));

        Assert.Equal(ApprovalRefusedException.RequiresTwoApproversCode, refused.ReasonCode);
        Assert.Equal(ApprovalRefusedException.RequiresTwoApproversMessage, refused.Message);
        AssertUnchanged(before, invoice);
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
        var before = Capture(invoice);

        Assert.Throws<ArgumentException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, new Approver("  ", Roles.Director)));
        AssertUnchanged(before, invoice);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(invoice).Approve(invoice.Id, Manager()));

        Assert.IsNotType<ApprovalRefusedException>(error);
        Assert.Equal(status, invoice.Status);
        Assert.Equal($"Invoice INV-1 is {status}; only pending invoices can be decided.", error.Message);
    }

    private static Invoice Pending(decimal amount) =>
        new() { Id = "INV-1", CustomerName = "Test School", Amount = amount, Status = InvoiceStatus.PendingApproval };

    private static Approver Manager(string userId = "demo.manager") => new(userId, Roles.Manager);

    private static Approver Director(string userId = "demo.director") => new(userId, Roles.Director);

    private static InvoiceSnapshot Capture(Invoice invoice) =>
        new(invoice.Status, invoice.DecidedBy, invoice.DecidedAt);

    private static void AssertUnchanged(InvoiceSnapshot before, Invoice invoice)
    {
        Assert.Equal(before.Status, invoice.Status);
        Assert.Equal(before.DecidedBy, invoice.DecidedBy);
        Assert.Equal(before.DecidedAt, invoice.DecidedAt);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.NotEqual(InvoiceStatus.Rejected, invoice.Status);
    }

    private sealed record InvoiceSnapshot(InvoiceStatus Status, string? DecidedBy, DateTime? DecidedAt);
}
