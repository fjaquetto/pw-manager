namespace PWManager.Domain.Appearance;

public static class ColorMath
{
    public static double ContrastRatio(RgbColor first, RgbColor second)
    {
        var firstLuminance = first.RelativeLuminance();
        var secondLuminance = second.RelativeLuminance();
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    public static RgbColor Scale(RgbColor color, double factor)
    {
        return new(ScaleChannel(color.R, factor), ScaleChannel(color.G, factor), ScaleChannel(color.B, factor), color.A);
    }

    public static RgbColor Mix(RgbColor from, RgbColor to, double amount)
    {
        return new(
            LerpChannel(from.R, to.R, amount),
            LerpChannel(from.G, to.G, amount),
            LerpChannel(from.B, to.B, amount),
            LerpChannel(from.A, to.A, amount));
    }

    public static RgbColor Lighten(RgbColor color, double amount)
    {
        var (hue, saturation, lightness) = ToHsl(color);

        return FromHsl(hue, saturation, Math.Clamp(lightness + amount, 0d, 1d), color.A);
    }

    public static (double Hue, double Saturation, double Lightness) ToHsl(RgbColor color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;

        if (max == min)
        {
            return (0d, 0d, lightness);
        }

        var delta = max - min;
        var saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        var hue = max == r
            ? 60 * (((g - b) / delta) % 6)
            : max == g
                ? 60 * (((b - r) / delta) + 2)
                : 60 * (((r - g) / delta) + 4);

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue, saturation, lightness);
    }

    public static RgbColor FromHsl(double hue, double saturation, double lightness, byte alpha = 255)
    {
        var normalizedHue = ((hue % 360) + 360) % 360;
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var section = normalizedHue / 60;
        var second = chroma * (1 - Math.Abs((section % 2) - 1));

        var (red, green, blue) = section switch
        {
            < 1 => (chroma, second, 0d),
            < 2 => (second, chroma, 0d),
            < 3 => (0d, chroma, second),
            < 4 => (0d, second, chroma),
            < 5 => (second, 0d, chroma),
            _ => (chroma, 0d, second)
        };

        var match = lightness - (chroma / 2);

        return new(
            ToChannel(red + match),
            ToChannel(green + match),
            ToChannel(blue + match),
            alpha);
    }

    private static byte ScaleChannel(byte channel, double factor) =>
        (byte)Math.Clamp(Math.Round(channel * factor), 0, 255);

    private static byte LerpChannel(byte from, byte to, double amount) =>
        (byte)Math.Clamp(Math.Round(from + ((to - from) * amount)), 0, 255);

    private static byte ToChannel(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
