using System.Net;
using System.Net.Sockets;
using System.Text;

// Pretends to be a weighbridge indicator so you can develop without real hardware.
// It accepts one connection and streams a truck driving on and settling at a target weight.
public static class ScaleSimulator
{
    public static async Task RunAsync(int port, int targetKg, CancellationToken ct)
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
                string status = current == targetKg ? "ST" : "US";
                await writer.WriteLineAsync($"{status},GS,{current:+0000000}kg");

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
