namespace PWManager.Domain.Appearance;

public sealed record ColorSchemePreset(string Name, ColorScheme Colors);

public static class ColorSchemePresets
{
    public static ColorSchemePreset MidnightOcean { get; } = new(
        "Midnight Ocean",
        ColorScheme.From(
            canvas: "#0B1220",
            surface: "#111A2C",
            raised: "#1B263B",
            field: "#0B1220",
            line: "#2A3A55",
            text: "#E6EDF7",
            secondaryText: "#9FB0C7",
            title: "#F2F6FC",
            buttonText: "#FFFFFF",
            accent: "#2563EB",
            accentHover: "#1D4ED8",
            focus: "#93C5FD",
            selection: "#172A4D",
            success: "#34D399",
            danger: "#FB7185",
            warning: "#FBBF24",
            overlay: "#B30B1220"));

    public static ColorSchemePreset VioletNebula { get; } = new(
        "Violet Nebula",
        ColorScheme.From(
            canvas: "#0F0A18",
            surface: "#171022",
            raised: "#221731",
            field: "#0F0A18",
            line: "#362A48",
            text: "#ECE6F5",
            secondaryText: "#B0A3C4",
            title: "#F5F1FB",
            buttonText: "#FFFFFF",
            accent: "#7C3AED",
            accentHover: "#6D28D9",
            focus: "#C4B5FD",
            selection: "#241640",
            success: "#34D399",
            danger: "#FB7185",
            warning: "#FBBF24",
            overlay: "#B30F0A18"));

    public static ColorSchemePreset Ember { get; } = new(
        "Ember",
        ColorScheme.From(
            canvas: "#140D08",
            surface: "#1C1310",
            raised: "#2A1D16",
            field: "#140D08",
            line: "#3D2C22",
            text: "#F5EDE6",
            secondaryText: "#C4AFA3",
            title: "#FBF6F1",
            buttonText: "#FFFFFF",
            accent: "#C2410C",
            accentHover: "#9A3412",
            focus: "#FDBA74",
            selection: "#3A1F10",
            success: "#34D399",
            danger: "#FB7185",
            warning: "#FBBF24",
            overlay: "#B3140D08"));

    public static ColorSchemePreset Forest { get; } = new(
        "Forest",
        ColorScheme.From(
            canvas: "#0A120D",
            surface: "#101A13",
            raised: "#1A281E",
            field: "#0A120D",
            line: "#2C3F31",
            text: "#E8F0E9",
            secondaryText: "#A8BCA9",
            title: "#F2F8F2",
            buttonText: "#FFFFFF",
            accent: "#15803D",
            accentHover: "#166534",
            focus: "#86EFAC",
            selection: "#14351D",
            success: "#34D399",
            danger: "#FB7185",
            warning: "#FBBF24",
            overlay: "#B30A120D"));

    public static IReadOnlyList<ColorSchemePreset> All { get; } = new[]
    {
        MidnightOcean,
        VioletNebula,
        Ember,
        Forest
    };

    public static ColorSchemePreset? Find(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return All.FirstOrDefault(preset => string.Equals(preset.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPresetName(string? name) => Find(name) is not null;
}
