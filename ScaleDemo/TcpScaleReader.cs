using System.Net.Sockets;

// Reads a weighbridge indicator that streams readings over TCP/IP.
// It doesn't know any indicator format itself: the IIndicatorFormat it's given does the translating.
public class TcpScaleReader : IScaleReader
{
    private readonly string _host;
    private readonly int _port;
    private readonly IIndicatorFormat _format;
    private const int ReadingsNeededForStable = 3;

    public TcpScaleReader(string host, int port, IIndicatorFormat format)
    {
        _host = host;
        _port = port;
        _format = format;
    }

    public async Task<int> ReadStableWeightAsync(CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, ct);
        using var reader = new StreamReader(client.GetStream());

        int sameStableCount = 0;
        int lastWeight = -1;

        while (true)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (line is null) throw new IOException("Scale disconnected.");

            var reading = _format.Parse(line);
            if (reading is null)
            {
                Console.WriteLine($"  ignored bad line: {line}");
                continue;
            }

            Console.WriteLine($"  scale: {line}");

            // Only accept a weight once the indicator says STABLE several times in a row
            // with the same value. A truck still rolling on must never be recorded.
            if (reading.IsStable && reading.WeightKg == lastWeight) sameStableCount++;
            else sameStableCount = reading.IsStable ? 1 : 0;

            lastWeight = reading.WeightKg;
            if (sameStableCount >= ReadingsNeededForStable) return reading.WeightKg;
        }
    }
}