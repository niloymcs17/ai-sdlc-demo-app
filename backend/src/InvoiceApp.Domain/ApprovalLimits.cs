namespace InvoiceApp.Domain;

/// <summary>
/// Inclusive approval ceilings. This is the only place these amounts are defined.
/// </summary>
public static class ApprovalLimits
{
    public const decimal ManagerLimit = 50_000m;
    public const decimal DirectorLimit = 500_000m;
}
