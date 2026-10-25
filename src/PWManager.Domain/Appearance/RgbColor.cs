using System.Globalization;

namespace PWManager.Domain.Appearance;

public readonly record struct RgbColor(byte R, byte G, byte B, byte A = 255)
{
    public static RgbColor White { get; } = new(255, 255, 255);

    public static RgbColor Black { get; } = new(0, 0, 0);

    public static bool TryParse(string? hex, out RgbColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var text = hex.Trim();

        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length is not (3 or 6 or 8))
        {
            return false;
        }

        if (text.Length == 3)
        {
            text = string.Concat(text.Select(character => new string(character, 2)));
        }

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = text.Length == 6
            ? new((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : new((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));

        return true;
    }

    public static RgbColor Parse(string hex) =>
        TryParse(hex, out var color) ? color : throw new FormatException($"'{hex}' is not a valid color.");

    public string ToHex() => A == 255
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public RgbColor WithAlpha(byte alpha) => this with { A = alpha };

    public double RelativeLuminance()
    {
        var r = Linearize(R);
        var g = Linearize(G);
        var b = Linearize(B);

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;

        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
