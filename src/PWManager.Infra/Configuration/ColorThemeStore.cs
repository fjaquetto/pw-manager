using System.Text;
using System.Text.Json;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Appearance;

namespace PWManager.Infra.Configuration;

// Appearance profiles live as JSON files next to the vault, never inside it.
// The built-in Default profile is implicit and is never written to disk.
public sealed class ColorThemeStore : IColorThemeStore
{
    public const string ThemesFolderName = "themes";

    private const string ActiveFileName = "active.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directory;

    public ColorThemeStore(string themesDirectory)
    {
        _directory = themesDirectory;
    }

    public IReadOnlyList<ColorProfile> LoadProfiles()
    {
        if (!Directory.Exists(_directory))
        {
            return Array.Empty<ColorProfile>();
        }

        var profiles = new List<ColorProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(_directory, "*.json")
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (string.Equals(Path.GetFileName(file), ActiveFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var profile = TryReadProfile(file);

            if (profile is null || !seen.Add(profile.Name.Trim()))
            {
                continue;
            }

            profiles.Add(profile with { Name = profile.Name.Trim() });
        }

        return profiles
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Save(ColorProfile profile)
    {
        if (!ColorProfile.IsValidName(profile.Name))
        {
            throw new ArgumentException($"'{profile.Name}' is not a valid profile name.", nameof(profile));
        }

        Directory.CreateDirectory(_directory);
        var normalized = profile with
        {
            Name = profile.Name.Trim(),
            Version = ColorProfile.CurrentVersion
        };

        WriteAtomically(PathFor(normalized.Name), JsonSerializer.Serialize(normalized, JsonOptions));
    }

    public bool Delete(string name)
    {
        if (ColorProfile.IsDefaultName(name))
        {
            return false;
        }

        var path = PathFor(name);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);

        return true;
    }

    public string? GetActiveProfileName()
    {
        var path = Path.Combine(_directory, ActiveFileName);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var file = JsonSerializer.Deserialize<ActiveProfileFile>(File.ReadAllText(path), JsonOptions);
            var name = file?.Name?.Trim();

            return string.IsNullOrEmpty(name) || ColorProfile.IsDefaultName(name) ? null : name;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SetActiveProfile(string? name)
    {
        var normalized = string.IsNullOrEmpty(name) || ColorProfile.IsDefaultName(name) ? null : name.Trim();

        if (normalized is not null && !ColorProfile.IsWellFormedName(normalized))
        {
            throw new ArgumentException($"'{name}' is not a valid profile name.", nameof(name));
        }

        Directory.CreateDirectory(_directory);
        WriteAtomically(
            Path.Combine(_directory, ActiveFileName),
            JsonSerializer.Serialize(new ActiveProfileFile { Name = normalized }, JsonOptions));
    }

    public ColorScheme LoadActiveScheme()
    {
        var activeName = GetActiveProfileName();

        if (activeName is null)
        {
            return ColorScheme.Default;
        }

        var preset = ColorSchemePresets.Find(activeName);

        if (preset is not null)
        {
            return preset.Colors;
        }

        var profile = LoadProfiles()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, activeName, StringComparison.OrdinalIgnoreCase));

        return profile?.Colors ?? ColorScheme.Default;
    }

    public static string ToSlug(string name)
    {
        var builder = new StringBuilder();

        foreach (var character in name.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var slug = builder.ToString().Trim('-');

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Length == 0 ? "profile" : slug;
    }

    private string PathFor(string name) => Path.Combine(_directory, ToSlug(name) + ".json");

    private static ColorProfile? TryReadProfile(string file)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<ColorProfile>(File.ReadAllText(file), JsonOptions);

            if (profile is null || !ColorProfile.IsValidName(profile.Name) || !profile.Colors.IsValid)
            {
                return null;
            }

            return profile;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed record ActiveProfileFile
    {
        public string? Name { get; init; }
    }
}
