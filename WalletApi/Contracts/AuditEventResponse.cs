namespace WalletApi.Contracts;

public record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string Outcome,
    Guid? UserId,
    Guid? TargetId,
    decimal? Amount,
    string? IpAddress,
    string? Details
);
