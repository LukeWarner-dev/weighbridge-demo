// Lessons 3 and 4: a small REST API for a weighbridge, with a daily report, on SQL Server Express.
// Run with:  dotnet run      then try the requests in requests.http
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
Db.Init();
// Role permission: only these users may type a weight in by hand (W8Suite's "weight override").
var canOverride = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "supervisor" };
app.MapGet("/", () => "Weighbridge API is running");

app.MapPost("/weighings", (FirstWeighRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User))
        return Results.BadRequest("User is required.");
    if (string.IsNullOrWhiteSpace(req.Registration) || string.IsNullOrWhiteSpace(req.Product))
        return Results.BadRequest("Registration and product are required.");
    if (req.GrossKg <= 0 || req.GrossKg > 80_000)
        return Results.BadRequest("Gross weight must be between 1 and 80 000 kg.");
    if (req.ManualEntry && !canOverride.Contains(req.User))
    {
        Db.WriteAudit(req.User, "OVERRIDE_DENIED", $"Tried manual gross {req.GrossKg} kg for {req.Registration}");
        return Results.Problem("Only a supervisor may type a weight in manually.", statusCode: 403);
    }

    int id = Db.InsertFirstWeigh(req);
    Db.WriteAudit(req.User, req.ManualEntry ? "FIRST_WEIGH_OVERRIDE" : "FIRST_WEIGH",
        $"Ticket {id}: {req.Registration}, {req.Product}, gross {req.GrossKg} kg");
    return Results.Created($"/weighings/{id}", Db.GetTicket(id));
});

app.MapPost("/weighings/{id:int}/second", (int id, SecondWeighRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User))
        return Results.BadRequest("User is required.");
    int? gross = Db.GetOpenGross(id);
    if (gross is null)
        return Results.NotFound($"No open weighing with id {id} (wrong id, or already completed).");
    if (req.TareKg <= 0 || req.TareKg >= gross)
        return Results.BadRequest($"Tare must be more than 0 and less than the gross of {gross} kg.");
    if (req.ManualEntry && !canOverride.Contains(req.User))
    {
        Db.WriteAudit(req.User, "OVERRIDE_DENIED", $"Tried manual tare {req.TareKg} kg on ticket {id}");
        return Results.Problem("Only a supervisor may type a weight in manually.", statusCode: 403);
    }

    Db.CompleteSecondWeigh(id, req.TareKg);
    Db.WriteAudit(req.User, req.ManualEntry ? "SECOND_WEIGH_OVERRIDE" : "SECOND_WEIGH",
        $"Ticket {id}: tare {req.TareKg} kg, nett {gross - req.TareKg} kg");
    return Results.Ok(Db.GetTicket(id));
});
// Look up one ticket.
app.MapGet("/weighings/{id:int}", (int id) =>
    Db.GetTicket(id) is Ticket t ? Results.Ok(t) : Results.NotFound());

// REPORT: nett kg per product for a day (defaults to today). e.g. /reports/daily?date=2026-10-20
app.MapGet("/reports/daily", (string? date) =>
{
    if (!TryGetDay(date, out var day)) return Results.BadRequest("Use date=yyyy-MM-dd");
    return Results.Ok(Db.DailyTotals(day));
});

// The same report as CSV, which opens straight in Excel.
app.MapGet("/reports/daily.csv", (string? date) =>
{
    if (!TryGetDay(date, out var day)) return Results.BadRequest("Use date=yyyy-MM-dd");
    var csv = new StringBuilder("Product,Loads,NetKg\n");
    foreach (var row in Db.DailyTotals(day))
        csv.AppendLine($"{row.Product},{row.Loads},{row.NetKg}");
    return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"daily-report-{day:yyyy-MM-dd}.csv");
});
// AUDIT: the 50 most recent actions, newest first.
app.MapGet("/audit", () => Results.Ok(Db.RecentAudit()));

app.Run();

static bool TryGetDay(string? date, out DateTime day)
{
    if (string.IsNullOrWhiteSpace(date)) { day = DateTime.Today; return true; }
    return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
