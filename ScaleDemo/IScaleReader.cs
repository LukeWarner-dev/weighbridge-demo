// Abstraction: the rest of the program only knows "give me a stable weight".
// It doesn't care whether the scale is on a serial cable, TCP, or a fake for testing.
public interface IScaleReader
{
    Task<int> ReadStableWeightAsync(CancellationToken ct);
}
