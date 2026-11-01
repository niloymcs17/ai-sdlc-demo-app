namespace InvoiceApp.Domain;

/// <summary>The signed-in user attempting an approval. Compared by <see cref="UserId"/>.</summary>
public record Approver(string UserId, string Role);
