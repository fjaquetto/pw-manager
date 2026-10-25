using PWManager.Domain.Appearance;

namespace PWManager.UnitTests;

public class ColorSchemeTests
{
    [Theory]
    [InlineData("#0F766E", 0x0F, 0x76, 0x6E, 0xFF)]
    [InlineData("#B3090A0B", 0x09, 0x0A, 0x0B, 0xB3)]
    [InlineData("FFF", 0xFF, 0xFF, 0xFF, 0xFF)]
    [InlineData("2A3033", 0x2A, 0x30, 0x33, 0xFF)]
    public void Parse_ReadsChannelsInArgbOrder(string hex, byte red, byte green, byte blue, byte alpha)
    {
        Assert.True(RgbColor.TryParse(hex, out var color));
        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
        Assert.Equal(alpha, color.A);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#NOTACOLOR")]
    public void Parse_RejectsInvalidValues(string hex)
    {
        Assert.False(RgbColor.TryParse(hex, out _));
    }

    [Fact]
    public void ToHex_RoundTripsOpaqueAndTranslucentColors()
    {
        Assert.Equal("#0F766E", RgbColor.Parse("#0F766E").ToHex());
        Assert.Equal("#B3090A0B", RgbColor.Parse("#B3090A0B").ToHex());
        Assert.Equal("#090A0B", RgbColor.Parse("#B3090A0B").WithAlpha(0xFF).ToHex());
    }

    [Fact]
    public void DefaultScheme_MatchesTheOriginalPaletteExactly()
    {
        var scheme = ColorScheme.Default;

        Assert.Equal("#090A0B", scheme.Canvas);
        Assert.Equal("#111315", scheme.Surface);
        Assert.Equal("#1B1F21", scheme.Raised);
        Assert.Equal("#090A0B", scheme.Field);
        Assert.Equal("#2A3033", scheme.Line);
        Assert.Equal("#E8ECEB", scheme.Text);
        Assert.Equal("#A6B1AD", scheme.SecondaryText);
        Assert.Equal("#E8ECEB", scheme.Title);
        Assert.Equal("#FFFFFF", scheme.ButtonText);
        Assert.Equal("#0F766E", scheme.Accent);
        Assert.Equal("#0D655F", scheme.AccentHover);
        Assert.Equal("#7DD3C0", scheme.Focus);
        Assert.Equal("#102B28", scheme.Selection);
        Assert.Equal("#34D399", scheme.Success);
        Assert.Equal("#FB7185", scheme.Danger);
        Assert.Equal("#FBBF24", scheme.Warning);
        Assert.Equal("#B3090A0B", scheme.Overlay);
        Assert.True(scheme.IsValid);
        Assert.True(scheme.MatchesDefaults());
    }

    [Fact]
    public void DefaultScheme_PassesEveryContrastTarget()
    {
        Assert.Empty(ColorContrast.Audit(ColorScheme.Default));
    }

    [Fact]
    public void ContrastRatio_MatchesKnownWcagValues()
    {
        Assert.Equal(21, ColorMath.ContrastRatio(RgbColor.Black, RgbColor.White), 1);
        Assert.Equal(1, ColorMath.ContrastRatio(RgbColor.White, RgbColor.White), 3);
        Assert.True(ColorContrast.Ratio(RgbColor.Parse("#E8ECEB"), RgbColor.Parse("#090A0B")) > 12);
    }

    [Fact]
    public void Derivation_ReproducesDefaultHoverAndOverlay()
    {
        Assert.Equal("#0D655F", ColorDerivation.Hover(RgbColor.Parse(ColorSchemeDefaults.Accent)).ToHex());
        Assert.Equal("#B3090A0B", ColorDerivation.Overlay(RgbColor.Parse(ColorSchemeDefaults.Canvas)).ToHex());
    }

    [Fact]
    public void Derivation_KeepsShadesCoherentWithTheAccent()
    {
        var accent = RgbColor.Parse("#2563EB");

        Assert.True(ColorMath.ContrastRatio(ColorDerivation.Hover(accent), accent) < 2);
        Assert.True(ColorDerivation.Hover(accent).RelativeLuminance() < accent.RelativeLuminance());
        Assert.True(ColorDerivation.Selection(accent).RelativeLuminance() < accent.RelativeLuminance());
        Assert.True(ColorDerivation.Focus(accent).RelativeLuminance() > accent.RelativeLuminance());
    }

    [Fact]
    public void Derive_RewritesOnlyTheDerivedTokens()
    {
        var custom = ColorScheme.Default with { Accent = "#2563EB", Canvas = "#0B1220" };
        var derived = ColorDerivation.Derive(custom);

        Assert.Equal(custom.Text, derived.Text);
        Assert.Equal(ColorDerivation.Hover(RgbColor.Parse("#2563EB")).ToHex(), derived.AccentHover);
        Assert.Equal(ColorDerivation.Overlay(RgbColor.Parse("#0B1220")).ToHex(), derived.Overlay);
        Assert.NotEqual(ColorSchemeDefaults.AccentHover, derived.AccentHover);
        Assert.NotEqual(ColorSchemeDefaults.Overlay, derived.Overlay);
    }

    [Fact]
    public void Coherence_RecognizesDefaultsAndDerivedSchemes()
    {
        Assert.True(ColorDerivation.IsCoherent(ColorScheme.Default));

        var derived = ColorDerivation.Derive(ColorScheme.Default with { Accent = "#2563EB" });

        Assert.True(ColorDerivation.IsCoherent(derived));

        var overridden = derived with { AccentHover = "#123456" };

        Assert.False(ColorDerivation.IsCoherent(overridden));
    }

    [Fact]
    public void Audit_FlagsUnreadablePairsWithActionableMessages()
    {
        var unreadable = ColorScheme.Default with { Text = "#101010", Title = "#101010" };
        var warnings = ColorContrast.Audit(unreadable);

        Assert.Contains(warnings, warning => warning.Description == "Body text on background");
        Assert.All(warnings, warning => Assert.Contains("recommended at least", warning.Message));
    }

    [Fact]
    public void FindInvalidTokens_ReportsEveryBrokenValue()
    {
        var broken = ColorScheme.Default with { Accent = "blue", Overlay = "transparent" };

        var invalid = broken.FindInvalidTokens();

        Assert.Equal(2, invalid.Count);
        Assert.Contains(nameof(ColorScheme.Accent), invalid);
        Assert.Contains(nameof(ColorScheme.Overlay), invalid);
    }

    [Fact]
    public void Tokens_ExposeEveryPropertyOnce()
    {
        var tokens = ColorScheme.Default.Tokens().ToList();

        Assert.Equal(17, tokens.Count);
        Assert.Equal(tokens.Count, tokens.Select(token => token.Key).Distinct().Count());

        foreach (var (name, value) in tokens)
        {
            Assert.Equal(value, ColorScheme.Default.GetToken(name));
        }
    }

    [Fact]
    public void ProfileName_ValidationProtectsTheBuiltInDefault()
    {
        Assert.False(ColorProfile.IsValidName("Default"));
        Assert.False(ColorProfile.IsValidName("default"));
        Assert.False(ColorProfile.IsValidName("  DEFAULT  "));
        Assert.False(ColorProfile.IsValidName(string.Empty));
        Assert.False(ColorProfile.IsValidName(new string('a', ColorProfile.MaxNameLength + 1)));
        Assert.False(ColorProfile.IsValidName("bad/name"));
        Assert.True(ColorProfile.IsValidName("Ocean"));
        Assert.True(ColorProfile.IsValidName("Work dark"));
        Assert.True(ColorProfile.IsDefaultName(" default "));
    }

    [Fact]
    public void Presets_AreDistinctWellFormedAndReserved()
    {
        Assert.Equal(4, ColorSchemePresets.All.Count);
        Assert.Equal(ColorSchemePresets.All.Count, ColorSchemePresets.All.Select(preset => preset.Name).Distinct().Count());

        foreach (var preset in ColorSchemePresets.All)
        {
            Assert.True(preset.Colors.IsValid, $"Preset '{preset.Name}' has invalid tokens.");
            Assert.True(ColorSchemePresets.IsPresetName(preset.Name));
            Assert.True(ColorSchemePresets.IsPresetName(preset.Name.ToUpperInvariant()));
            Assert.False(ColorProfile.IsDefaultName(preset.Name));
            Assert.False(ColorProfile.IsValidName(preset.Name));
            Assert.True(ColorProfile.IsWellFormedName(preset.Name));
            Assert.True(ColorProfile.IsReservedName(preset.Name));
        }

        Assert.Null(ColorSchemePresets.Find("Missing"));
    }

    [Fact]
    public void Presets_PassEveryContrastTarget()
    {
        foreach (var preset in ColorSchemePresets.All)
        {
            var warnings = ColorContrast.Audit(preset.Colors);

            Assert.True(warnings.Count == 0, $"Preset '{preset.Name}' has contrast warnings: {string.Join("; ", warnings.Select(warning => warning.Message))}");
        }
    }
}
