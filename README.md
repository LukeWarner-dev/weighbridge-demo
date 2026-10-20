Weighbridge demo (C# / .NET 8 / SQL Server)

A small weighbridge system I built to learn the domain:

ScaleDemo: reads weights from a simulated indicator over TCP, ignores noise, only accepts stable readings.

WeighbridgeApi: ASP.NET Core minimal API for first and second weighs (gross, tare, nett) on SQL Server Express, with validation, parameterised queries, and a daily tonnage report as JSON or CSV.



 Run it

1\. .NET 8 SDK and SQL Server Express (`localhost\\SQLEXPRESS`).

2\. `cd ScaleDemo` then `dotnet run`

3\. `cd WeighbridgeApi` then `dotnet run`, then send the requests in `requests.http`.



 What I learned

Stable-weight detection, REST status codes, SQL Server from C#, and report queries with GROUP BY.

