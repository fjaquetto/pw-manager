namespace PWManager.Domain.Appearance;

public static class ColorDerivation
{
    public const byte OverlayAlpha = 0xB3;

    public static RgbColor Hover(RgbColor accent) => ColorMath.Scale(accent, 0.86);

    public static RgbColor Selection(RgbColor accent) => ColorMath.Scale(accent, 0.36);

    public static RgbColor Focus(RgbColor accent) => ColorMath.Lighten(accent, 0.40);

    public static RgbColor Overlay(RgbColor canvas) => canvas.WithAlpha(OverlayAlpha);

    public static ColorScheme Derive(ColorScheme scheme)
    {
        if (!RgbColor.TryParse(scheme.Accent, out var accent) || !RgbColor.TryParse(scheme.Canvas, out var canvas))
        {
            return scheme;
        }

        return scheme with
        {
            AccentHover = Hover(accent).ToHex(),
            Selection = Selection(accent).ToHex(),
            Focus = Focus(accent).ToHex(),
            Overlay = Overlay(canvas).ToHex()
        };
    }

    public static bool MatchesDerived(ColorScheme scheme)
    {
        if (!RgbColor.TryParse(scheme.Accent, out _) || !RgbColor.TryParse(scheme.Canvas, out _))
        {
            return false;
        }

        var derived = Derive(scheme);

        return string.Equals(scheme.AccentHover, derived.AccentHover, StringComparison.OrdinalIgnoreCase)
            && string.Equals(scheme.Selection, derived.Selection, StringComparison.OrdinalIgnoreCase)
            && string.Equals(scheme.Focus, derived.Focus, StringComparison.OrdinalIgnoreCase)
            && string.Equals(scheme.Overlay, derived.Overlay, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCoherent(ColorScheme scheme) => scheme.MatchesDefaults() || MatchesDerived(scheme);
}
