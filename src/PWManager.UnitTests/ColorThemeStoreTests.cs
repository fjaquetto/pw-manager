using System.Text.Json;
using PWManager.Domain.Appearance;
using PWManager.Infra.Configuration;

namespace PWManager.UnitTests;

public class ColorThemeStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pwmanager-themes-" + Guid.NewGuid().ToString("N"));

    private ColorThemeStore Store => new(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void LoadProfiles_IsEmptyBeforeAnythingIsSaved()
    {
        Assert.Empty(Store.LoadProfiles());
        Assert.Null(Store.GetActiveProfileName());
        Assert.True(Store.LoadActiveScheme().MatchesDefaults());
    }

    [Fact]
    public void Save_RoundTripsEveryToken()
    {
        var scheme = ColorScheme.Default with { Accent = "#2563EB", Canvas = "#0B1220", Overlay = "#E60B1220" };

        Store.Save(new ColorProfile { Name = "Ocean", Colors = scheme });

        var profile = Assert.Single(Store.LoadProfiles());

        Assert.Equal("Ocean", profile.Name);
        Assert.Equal(ColorProfile.CurrentVersion, profile.Version);
        Assert.Equal(scheme, profile.Colors);
        Assert.True(File.Exists(Path.Combine(_directory, "ocean.json")));
        Assert.False(File.Exists(Path.Combine(_directory, "ocean.json.tmp")));
    }

    [Fact]
    public void Save_OverwritesTheSameNameRegardlessOfCase()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        Store.Save(new ColorProfile { Name = "ocean", Colors = ColorScheme.Default with { Accent = "#0EA5E9" } });

        var profile = Assert.Single(Store.LoadProfiles());

        Assert.Equal("#0EA5E9", profile.Colors.Accent);
    }

    [Fact]
    public void Save_RejectsNamesThatAreInvalidOrDefault()
    {
        Assert.Throws<ArgumentException>(() => Store.Save(new ColorProfile { Name = "Default" }));
        Assert.Throws<ArgumentException>(() => Store.Save(new ColorProfile { Name = string.Empty }));

        foreach (var preset in ColorSchemePresets.All)
        {
            Assert.Throws<ArgumentException>(() => Store.Save(new ColorProfile { Name = preset.Name }));
        }
    }

    [Fact]
    public void ActiveProfile_ResolvesTheBuiltInPresets()
    {
        var preset = ColorSchemePresets.All[0];
        Store.SetActiveProfile(preset.Name);

        Assert.Equal(preset.Name, Store.GetActiveProfileName());
        Assert.Equal(preset.Colors, Store.LoadActiveScheme());
        Assert.Empty(Store.LoadProfiles());
    }

    [Fact]
    public void Delete_RemovesOnlyTheRequestedProfile()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default });
        Store.Save(new ColorProfile { Name = "Graphite", Colors = ColorScheme.Default });

        Assert.True(Store.Delete("ocean"));

        var remaining = Assert.Single(Store.LoadProfiles());

        Assert.Equal("Graphite", remaining.Name);
        Assert.False(Store.Delete("Default"));
        Assert.False(Store.Delete("Missing"));
    }

    [Fact]
    public void ActiveProfile_IsRememberedAndApplied()
    {
        var scheme = ColorScheme.Default with { Accent = "#2563EB" };
        Store.Save(new ColorProfile { Name = "Ocean", Colors = scheme });

        Store.SetActiveProfile("Ocean");

        Assert.Equal("Ocean", Store.GetActiveProfileName());
        Assert.Equal(scheme.Accent, Store.LoadActiveScheme().Accent);

        Store.SetActiveProfile(null);

        Assert.Null(Store.GetActiveProfileName());
        Assert.True(Store.LoadActiveScheme().MatchesDefaults());
    }

    [Fact]
    public void SetActiveProfile_RejectsInvalidNames()
    {
        Assert.Throws<ArgumentException>(() => Store.SetActiveProfile("Default name/with slash"));
    }

    [Fact]
    public void LoadProfiles_SkipsCorruptAndInvalidFiles()
    {
        Store.Save(new ColorProfile { Name = "Good", Colors = ColorScheme.Default });
        File.WriteAllText(Path.Combine(_directory, "broken.json"), "{ this is not json");
        File.WriteAllText(
            Path.Combine(_directory, "wrong-colors.json"),
            JsonSerializer.Serialize(new { name = "Wrong colors", colors = new { accent = "not-a-color" } }));
        File.WriteAllText(
            Path.Combine(_directory, "protected.json"),
            JsonSerializer.Serialize(new { name = "Default", colors = new { } }));

        var profile = Assert.Single(Store.LoadProfiles());

        Assert.Equal("Good", profile.Name);
    }

    [Fact]
    public void LoadActiveScheme_FallsBackToDefaultWhenTheProfileIsMissing()
    {
        Store.SetActiveProfile("Ghost");

        Assert.True(Store.LoadActiveScheme().MatchesDefaults());
    }

    [Fact]
    public void LoadActiveScheme_FallsBackToDefaultWhenTheActiveFileIsCorrupt()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "active.json"), "garbage");

        Assert.Null(Store.GetActiveProfileName());
        Assert.True(Store.LoadActiveScheme().MatchesDefaults());
    }

    [Fact]
    public void ToSlug_SanitizesNamesForTheFileSystem()
    {
        Assert.Equal("ocean", ColorThemeStore.ToSlug("Ocean"));
        Assert.Equal("work-dark", ColorThemeStore.ToSlug("  Work  dark "));
        Assert.Equal("cafe-a1", ColorThemeStore.ToSlug("Cafe & A1"));
        Assert.Equal("profile", ColorThemeStore.ToSlug("!!!"));
    }

    [Fact]
    public void Profiles_AreStoredInIndependentJsonFilesAwayFromTheVault()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default });

        var file = Path.Combine(_directory, "ocean.json");
        var json = File.ReadAllText(file);

        Assert.Contains("\"name\": \"Ocean\"", json);
        Assert.Contains("\"accent\"", json);
        Assert.DoesNotContain("pwmanager.db", json);
    }
}
