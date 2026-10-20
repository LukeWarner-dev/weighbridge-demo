// Lesson 2: talking to weighbridge hardware.
// Run with:  dotnet run
// It starts a fake indicator on port 5050, connects to it, waits for a stable weight and prints it.

// 1. Quick self-checks for the parser (a tiny "unit test" without a test framework).
Check(ScaleParser.Parse("ST,GS,+0024560kg") == new ScaleReading(true, 24560), "stable line parses");
Check(ScaleParser.Parse("US,GS,+0001000kg")?.IsStable == false, "unstable line is flagged");
Check(ScaleParser.Parse("#noise#") is null, "noise is ignored");
Check(ScaleParser.Parse("ST,GS,+00ABCkg") is null, "corrupt weight is ignored");

var space = new SpaceFormat();
Check(space.Parse("  24560 kg G S") == new ScaleReading(true, 24560), "space format: stable line parses");
Check(space.Parse("  18000 kg G M")?.IsStable == false, "space format: motion is flagged");
Check(space.Parse("#noise#") is null, "space format: noise is ignored");

// 2. Start the fake scale, then read from it like real software would.
const int Port = 5050;
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));   // never hang forever
var simulator = ScaleSimulator.RunAsync(Port, targetKg: 24560, cts.Token, spaceFormat: true);

IScaleReader scale = new TcpScaleReader("127.0.0.1", Port, new SpaceFormat());
Console.WriteLine("Truck driving onto the weighbridge...");

try
{
    int grossKg = await scale.ReadStableWeightAsync(cts.Token);
    Console.WriteLine($"\nSTABLE gross weight captured: {grossKg:N0} kg");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Timed out waiting for a stable weight.");
}
catch (IOException ex)
{
    Console.WriteLine($"Lost connection to the scale: {ex.Message}");
}

cts.Cancel();
await simulator;

static void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
}
