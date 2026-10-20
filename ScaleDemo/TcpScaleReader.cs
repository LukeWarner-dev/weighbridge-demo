using System.Net.Sockets;

// Reads a weighbridge indicator that streams readings over TCP/IP (common on modern indicators).
// A serial (RS-232) indicator would be a SerialScaleReader using System.IO.Ports.SerialPort,
// implementing the same IScaleReader interface: that's polymorphism.
public class TcpScaleReader : IScaleReader
{
    private readonly string _host;
    private readonly int _port;
    private const int ReadingsNeededForStable = 3;

    public TcpScaleReader(string host, int port)
    {
        _host = host;
        _port = port;
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

            var reading = ScaleParser.Parse(line);
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
