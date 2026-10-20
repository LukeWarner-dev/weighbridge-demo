using System.Net;
using System.Net.Sockets;
using System.Text;

// Pretends to be a weighbridge indicator so you can develop without real hardware.
// spaceFormat = false: brand 1 "ST,GS,+0024560kg"   spaceFormat = true: brand 2 "  24560 kg G S"
public static class ScaleSimulator
{
    public static async Task RunAsync(int port, int targetKg, CancellationToken ct, bool spaceFormat = false)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            var random = new Random();
            int current = 0;

            while (!ct.IsCancellationRequested)
            {
                if (current < targetKg) current = Math.Min(targetKg, current + random.Next(3000, 8000));
                bool stable = current == targetKg;

                string line = spaceFormat
                    ? $"{current,7} kg G {(stable ? "S" : "M")}"
                    : $"{(stable ? "ST" : "US")},GS,{current:+0000000}kg";
                await writer.WriteLineAsync(line);

                if (random.Next(8) == 0) await writer.WriteLineAsync("#noise#");   // real cables are noisy

                await Task.Delay(300, ct);
            }
        }
        catch (OperationCanceledException) { }   // normal shutdown
        catch (IOException) { }                  // reader hung up: normal once it has its weight
        finally
        {
            listener.Stop();
        }
    }
}