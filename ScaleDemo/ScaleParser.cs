using System.Globalization;

// One reading from a weighbridge indicator.
public record ScaleReading(bool IsStable, int WeightKg);

public static class ScaleParser
{
    // Indicators send a short text line many times a second, for example:
    //   "ST,GS,+0024560kg"
    //   ST = stable (US = unstable, still moving), GS = gross weight, then the weight.
    // Real formats differ per brand (check the indicator's manual), but they all look like this.
    public static ScaleReading? Parse(string line)
    {
        var parts = line.Trim().Split(',');
        if (parts.Length != 3) return null;          // not a weight line

        bool stable = parts[0] == "ST";
        string raw = parts[2].Replace("kg", "", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int weight))
            return null;                              // corrupted line: ignore it, don't crash

        return new ScaleReading(stable, weight);
    }
}
