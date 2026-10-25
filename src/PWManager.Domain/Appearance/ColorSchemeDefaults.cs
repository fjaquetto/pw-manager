namespace PWManager.Domain.Appearance;

// Must reproduce the original theme exactly, so existing users see no change
// until they pick a different color.
public static class ColorSchemeDefaults
{
    public const string Canvas = "#090A0B";

    public const string Surface = "#111315";

    public const string Raised = "#1B1F21";

    public const string Field = "#090A0B";

    public const string Line = "#2A3033";

    public const string Text = "#E8ECEB";

    public const string SecondaryText = "#A6B1AD";

    public const string Title = "#E8ECEB";

    public const string ButtonText = "#FFFFFF";

    public const string Accent = "#0F766E";

    public const string AccentHover = "#0D655F";

    public const string Focus = "#7DD3C0";

    public const string Selection = "#102B28";

    public const string Success = "#34D399";

    public const string Danger = "#FB7185";

    public const string Warning = "#FBBF24";

    public const string Overlay = "#B3090A0B";

    public static ColorScheme Create() => new();
}
