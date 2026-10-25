namespace PWManager.Domain.Appearance;

public sealed record ColorScheme
{
    public string Canvas { get; init; } = ColorSchemeDefaults.Canvas;

    public string Surface { get; init; } = ColorSchemeDefaults.Surface;

    public string Raised { get; init; } = ColorSchemeDefaults.Raised;

    public string Field { get; init; } = ColorSchemeDefaults.Field;

    public string Line { get; init; } = ColorSchemeDefaults.Line;

    public string Text { get; init; } = ColorSchemeDefaults.Text;

    public string SecondaryText { get; init; } = ColorSchemeDefaults.SecondaryText;

    public string Title { get; init; } = ColorSchemeDefaults.Title;

    public string ButtonText { get; init; } = ColorSchemeDefaults.ButtonText;

    public string Accent { get; init; } = ColorSchemeDefaults.Accent;

    public string AccentHover { get; init; } = ColorSchemeDefaults.AccentHover;

    public string Focus { get; init; } = ColorSchemeDefaults.Focus;

    public string Selection { get; init; } = ColorSchemeDefaults.Selection;

    public string Success { get; init; } = ColorSchemeDefaults.Success;

    public string Danger { get; init; } = ColorSchemeDefaults.Danger;

    public string Warning { get; init; } = ColorSchemeDefaults.Warning;

    public string Overlay { get; init; } = ColorSchemeDefaults.Overlay;

    public static ColorScheme Default => ColorSchemeDefaults.Create();

    public static ColorScheme From(string canvas, string surface, string raised, string field, string line, string text,
        string secondaryText, string title, string buttonText, string accent, string accentHover, string focus,
        string selection, string success, string danger, string warning, string overlay)
    {
        return new()
        {
            Canvas = canvas,
            Surface = surface,
            Raised = raised,
            Field = field,
            Line = line,
            Text = text,
            SecondaryText = secondaryText,
            Title = title,
            ButtonText = buttonText,
            Accent = accent,
            AccentHover = accentHover,
            Focus = focus,
            Selection = selection,
            Success = success,
            Danger = danger,
            Warning = warning,
            Overlay = overlay
        };
    }

    public IEnumerable<KeyValuePair<string, string>> Tokens()
    {
        yield return new(nameof(Canvas), Canvas);
        yield return new(nameof(Surface), Surface);
        yield return new(nameof(Raised), Raised);
        yield return new(nameof(Field), Field);
        yield return new(nameof(Line), Line);
        yield return new(nameof(Text), Text);
        yield return new(nameof(SecondaryText), SecondaryText);
        yield return new(nameof(Title), Title);
        yield return new(nameof(ButtonText), ButtonText);
        yield return new(nameof(Accent), Accent);
        yield return new(nameof(AccentHover), AccentHover);
        yield return new(nameof(Focus), Focus);
        yield return new(nameof(Selection), Selection);
        yield return new(nameof(Success), Success);
        yield return new(nameof(Danger), Danger);
        yield return new(nameof(Warning), Warning);
        yield return new(nameof(Overlay), Overlay);
    }

    public IReadOnlyList<string> FindInvalidTokens()
    {
        var invalid = new List<string>();

        foreach (var (name, value) in Tokens())
        {
            if (!RgbColor.TryParse(value, out _))
            {
                invalid.Add(name);
            }
        }

        return invalid;
    }

    public bool IsValid => FindInvalidTokens().Count == 0;

    public bool MatchesDefaults()
    {
        var defaults = Default;

        return Tokens().All(token => string.Equals(token.Value, defaults.GetToken(token.Key), StringComparison.OrdinalIgnoreCase));
    }

    public string GetToken(string name)
    {
        foreach (var token in Tokens())
        {
            if (token.Key == name)
            {
                return token.Value;
            }
        }

        throw new ArgumentException($"Unknown color token '{name}'.", nameof(name));
    }
}
