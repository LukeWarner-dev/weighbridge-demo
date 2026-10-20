// Request bodies (JSON in) and responses (JSON out).
public record FirstWeighRequest(string Registration, string Product, int GrossKg);
public record SecondWeighRequest(int TareKg);

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
