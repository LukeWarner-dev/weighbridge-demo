using Microsoft.Data.SqlClient;

// All database access lives here (single responsibility).
// Uses your local SQL Server Express. Open SSMS, connect to .\SQLEXPRESS and you'll see the
// "Weighbridge" database appear after the first run.
public static class Db
{
    // If your instance has a different name, change "SQLEXPRESS" (SSMS shows it on the connect screen).
    private const string Server = @"localhost\SQLEXPRESS";
    private const string ConnectionString =
        $"Server={Server};Database=Weighbridge;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string MasterConnectionString =
        $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

    public static SqlConnection Open()
    {
        var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    // Creates the database and table the first time you run the app.
    public static void Init()
    {
        using (var master = new SqlConnection(MasterConnectionString))
        {
            master.Open();
            using var create = master.CreateCommand();
            create.CommandText = "IF DB_ID('Weighbridge') IS NULL CREATE DATABASE Weighbridge;";
            create.ExecuteNonQuery();
        }

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF OBJECT_ID('dbo.Weighings') IS NULL
            CREATE TABLE dbo.Weighings (
                Id            INT IDENTITY(1,1) PRIMARY KEY,
                Registration  VARCHAR(20)  NOT NULL,
                Product       VARCHAR(50)  NOT NULL,
                GrossKg       INT          NOT NULL,
                TareKg        INT          NULL,
                FirstWeighAt  DATETIME2    NOT NULL,
                SecondWeighAt DATETIME2    NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public static int InsertFirstWeigh(FirstWeighRequest req)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        // Parameters (@reg etc.), never string concatenation: this prevents SQL injection.
        cmd.CommandText = """
            INSERT INTO dbo.Weighings (Registration, Product, GrossKg, FirstWeighAt)
            OUTPUT INSERTED.Id
            VALUES (@reg, @prod, @gross, SYSDATETIME());
            """;
        cmd.Parameters.AddWithValue("@reg", req.Registration.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@prod", req.Product.Trim());
        cmd.Parameters.AddWithValue("@gross", req.GrossKg);
        return (int)cmd.ExecuteScalar()!;
    }

    // Returns the gross weight of a weighing still waiting for its second weigh, or null.
    public static int? GetOpenGross(int id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT GrossKg FROM dbo.Weighings WHERE Id = @id AND TareKg IS NULL;";
        cmd.Parameters.AddWithValue("@id", id);
        var result = cmd.ExecuteScalar();
        return result is null ? null : (int)result;
    }

    public static void CompleteSecondWeigh(int id, int tareKg)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Weighings SET TareKg = @tare, SecondWeighAt = SYSDATETIME() WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@tare", tareKg);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public static Ticket? GetTicket(int id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Registration, Product, GrossKg, TareKg, FirstWeighAt, SecondWeighAt
            FROM dbo.Weighings WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        int gross = r.GetInt32(3);
        int? tare = r.IsDBNull(4) ? null : r.GetInt32(4);
        return new Ticket(
            r.GetInt32(0), r.GetString(1), r.GetString(2),
            gross, tare, tare is null ? null : gross - tare,
            r.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss"),
            r.IsDBNull(6) ? null : r.GetDateTime(6).ToString("yyyy-MM-dd HH:mm:ss"));
    }

    // The report: total net weight per product for one day.
    public static List<ProductTotal> DailyTotals(DateTime day)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Product,
                   COUNT(*)                              AS Loads,
                   SUM(CAST(GrossKg - TareKg AS BIGINT)) AS NetKg
            FROM dbo.Weighings
            WHERE TareKg IS NOT NULL
              AND CAST(SecondWeighAt AS DATE) = @day
            GROUP BY Product
            ORDER BY NetKg DESC;
            """;
        cmd.Parameters.AddWithValue("@day", day.Date);
        using var r = cmd.ExecuteReader();
        var totals = new List<ProductTotal>();
        while (r.Read())
            totals.Add(new ProductTotal(r.GetString(0), r.GetInt32(1), r.GetInt64(2)));
        return totals;
    }
}
