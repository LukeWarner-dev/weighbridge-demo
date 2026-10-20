// Request bodies (JSON in) and responses (JSON out).
public record FirstWeighRequest(string Registration, string Product, int GrossKg, string User, bool ManualEntry = false);
public record SecondWeighRequest(int TareKg, string User, bool ManualEntry = false);


public record Ticket(
    int Id,
    string Registration,
    string Product,
    int GrossKg,
    int? TareKg,
    int? NetKg,              // null until the second weigh is done
    string FirstWeighAt,
    string? SecondWeighAt);

public record ProductTotal(string Product, int Loads, long NetKg);

// One row of the audit log.
public record AuditEntry(int Id, string User, string Action, string? Details, string At);