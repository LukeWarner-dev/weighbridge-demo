// A "translator": each indicator brand gets its own class that turns its text into a ScaleReading.
public interface IIndicatorFormat
{
    ScaleReading? Parse(string line);
}

// Brand 1: "ST,GS,+0024560kg" (reuses the parser we already have)
public class CommaFormat : IIndicatorFormat
{
    public ScaleReading? Parse(string line) => ScaleParser.Parse(line);
}

// Brand 2: "  24560 kg G S"  -> weight, unit, G = gross, S = stable / M = motion
public class SpaceFormat : IIndicatorFormat
{
    public ScaleReading? Parse(string line)
    {
        var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length != 4 || p[1] != "kg") return null;   // not a weight line
        if (!int.TryParse(p[0], out int kg)) return null; // corrupted weight
        return new ScaleReading(p[3] == "S", kg);
    }
}