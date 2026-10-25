namespace PWManager.Domain.Appearance;

public sealed record ContrastWarning(string Description, double Ratio, double Required)
{
    public string Message => $"{Description}: {Ratio:F1}:1 — recommended at least {Required:F1}:1";
}

public static class ColorContrast
{
    public const double NormalText = 4.5;

    public const double LargeText = 3.0;

    public const double UiComponent = 3.0;

    private static readonly (string Description, string Foreground, string Background, double Required)[] Checks =
    {
        ("Body text on background", nameof(ColorScheme.Text), nameof(ColorScheme.Canvas), NormalText),
        ("Body text on panels", nameof(ColorScheme.Text), nameof(ColorScheme.Surface), NormalText),
        ("Body text on raised surfaces", nameof(ColorScheme.Text), nameof(ColorScheme.Raised), NormalText),
        ("Body text in fields", nameof(ColorScheme.Text), nameof(ColorScheme.Field), NormalText),
        ("Secondary text on background", nameof(ColorScheme.SecondaryText), nameof(ColorScheme.Canvas), NormalText),
        ("Secondary text on panels", nameof(ColorScheme.SecondaryText), nameof(ColorScheme.Surface), NormalText),
        ("Titles on background", nameof(ColorScheme.Title), nameof(ColorScheme.Canvas), NormalText),
        ("Titles on panels", nameof(ColorScheme.Title), nameof(ColorScheme.Surface), NormalText),
        ("Button text on primary action", nameof(ColorScheme.ButtonText), nameof(ColorScheme.Accent), NormalText),
        ("Focus indicator on background", nameof(ColorScheme.Focus), nameof(ColorScheme.Canvas), UiComponent),
        ("Focus indicator on panels", nameof(ColorScheme.Focus), nameof(ColorScheme.Surface), UiComponent),
        ("Error text on background", nameof(ColorScheme.Danger), nameof(ColorScheme.Canvas), NormalText),
        ("Error text on panels", nameof(ColorScheme.Danger), nameof(ColorScheme.Surface), NormalText),
        ("Success text on panels", nameof(ColorScheme.Success), nameof(ColorScheme.Surface), NormalText),
        ("Warning text on panels", nameof(ColorScheme.Warning), nameof(ColorScheme.Surface), NormalText)
    };

    public static double Ratio(RgbColor first, RgbColor second) => ColorMath.ContrastRatio(first, second);

    public static IReadOnlyList<ContrastWarning> Audit(ColorScheme scheme)
    {
        var warnings = new List<ContrastWarning>();

        foreach (var (description, foregroundToken, backgroundToken, required) in Checks)
        {
            if (!RgbColor.TryParse(scheme.GetToken(foregroundToken), out var foreground)
                || !RgbColor.TryParse(scheme.GetToken(backgroundToken), out var background))
            {
                continue;
            }

            var ratio = ColorMath.ContrastRatio(foreground, background);

            if (ratio < required)
            {
                warnings.Add(new(description, ratio, required));
            }
        }

        return warnings;
    }
}
