namespace InvoiceApp.Domain;

public record ApprovalRecord(string UserId, string Role, DateTime ApprovedAt);
