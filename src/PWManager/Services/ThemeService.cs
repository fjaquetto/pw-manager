using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using PWManager.Domain.Appearance;
using PWManager.Services.Interfaces;
using AvaloniaApp = Avalonia.Application;

namespace PWManager.Services;

// Every view shares the brush instances from Styles/Colors.axaml, so mutating
// their color updates the live UI without replacing resources.
public sealed class ThemeService : IThemeService
{
    private static readonly IReadOnlyList<(string ResourceKey, Func<ColorScheme, string> Token)> BrushMap = new (string, Func<ColorScheme, string>)[]
    {
        ("CanvasBrush", scheme => scheme.Canvas),
        ("SurfaceBrush", scheme => scheme.Surface),
        ("RaisedBrush", scheme => scheme.Raised),
        ("FieldBrush", scheme => scheme.Field),
        ("LineBrush", scheme => scheme.Line),
        ("TextBrush", scheme => scheme.Text),
        ("SecondaryTextBrush", scheme => scheme.SecondaryText),
        ("TitleBrush", scheme => scheme.Title),
        ("PrimaryButtonTextBrush", scheme => scheme.ButtonText),
        ("AccentBrush", scheme => scheme.Accent),
        ("AccentHoverBrush", scheme => scheme.AccentHover),
        ("FocusBrush", scheme => scheme.Focus),
        ("AccentSurfaceBrush", scheme => scheme.Selection),
        ("SuccessBrush", scheme => scheme.Success),
        ("DangerBrush", scheme => scheme.Danger),
        ("WarningBrush", scheme => scheme.Warning),
        ("ModalOverlayBrush", scheme => scheme.Overlay)
    };

    public void Apply(ColorScheme scheme)
    {
        if (AvaloniaApp.Current is not { } application)
        {
            return;
        }

        foreach (var (resourceKey, token) in BrushMap)
        {
            if (RgbColor.TryParse(token(scheme), out var color)
                && application.TryFindResource(resourceKey, out var resource)
                && resource is SolidColorBrush brush)
            {
                brush.Color = ToColor(color);
            }
        }

        ApplyAccent(application, scheme.Accent);
    }

    private static void ApplyAccent(AvaloniaApp application, string accentHex)
    {
        if (!RgbColor.TryParse(accentHex, out var color))
        {
            return;
        }

        var accent = ToColor(color);

        foreach (var style in application.Styles)
        {
            if (style is not FluentTheme fluentTheme)
            {
                continue;
            }

            foreach (var palette in fluentTheme.Palettes.Values)
            {
                palette.Accent = accent;
            }
        }
    }

    private static Color ToColor(RgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}
