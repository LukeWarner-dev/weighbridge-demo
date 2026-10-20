// Lessons 3 and 4: a small REST API for a weighbridge, with a daily report, on SQL Server Express.
// Run with:  dotnet run      then try the requests in requests.http
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
Db.Init();

app.MapGet("/", () => "Weighbridge API is running");

// FIRST WEIGH: a loaded truck arrives. Records the gross weight and returns the ticket.
app.MapPost("/weighings", (FirstWeighRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Registration) || string.IsNullOrWhiteSpace(req.Product))
        return Results.BadRequest("Registration and product are required.");
    if (req.GrossKg <= 0 || req.GrossKg > 80_000)
        return Results.BadRequest("Gross weight must be between 1 and 80 000 kg.");

    int id = Db.InsertFirstWeigh(req);
    return Results.Created($"/weighings/{id}", Db.GetTicket(id));
});

// SECOND WEIGH: the same truck leaves empty. Records tare, completing the ticket (nett = gross - tare).
app.MapPost("/weighings/{id:int}/second", (int id, SecondWeighRequest req) =>
{
    int? gross = Db.GetOpenGross(id);
    if (gross is null)
        return Results.NotFound($"No open weighing with id {id} (wrong id, or already completed).");
    if (req.TareKg <= 0 || req.TareKg >= gross)
        return Results.BadRequest($"Tare must be more than 0 and less than the gross of {gross} kg.");

    Db.CompleteSecondWeigh(id, req.TareKg);
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

app.Run();

static bool TryGetDay(string? date, out DateTime day)
{
    if (string.IsNullOrWhiteSpace(date)) { day = DateTime.Today; return true; }
    return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
