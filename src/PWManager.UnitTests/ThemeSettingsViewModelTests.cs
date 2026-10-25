using Moq;
using PWManager.Domain.Appearance;
using PWManager.Infra.Configuration;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class ThemeSettingsViewModelTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pwmanager-theme-vm-" + Guid.NewGuid().ToString("N"));

    private Mock<IThemeService> ThemeService { get; } = new();

    private ColorThemeStore Store => new(_directory);

    private ThemeSettingsViewModel CreateViewModel() => new(ThemeService.Object, Store);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Open_LoadsTheActiveProfileWithNoWarnings()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        Store.SetActiveProfile("Ocean");
        var viewModel = CreateViewModel();

        viewModel.Open();

        Assert.Equal("Ocean", viewModel.SelectedProfile);
        Assert.Equal("Ocean", viewModel.ProfileName);
        Assert.Equal("#2563EB", viewModel.AccentColor);
        Assert.False(viewModel.AutoDerive);
        Assert.False(viewModel.HasWarnings);
        Assert.False(viewModel.HasValidationMessage);
        Assert.True(viewModel.CanDeleteProfile);
        ThemeService.Verify(service => service.Apply(It.Is<ColorScheme>(scheme => scheme.Accent == "#2563EB")), Times.Once);
    }

    [Fact]
    public void Open_DefaultProfileKeepsAutomaticDerivationOn()
    {
        var viewModel = CreateViewModel();

        viewModel.Open();

        Assert.Equal("Default", viewModel.SelectedProfile);
        Assert.True(viewModel.AutoDerive);
        Assert.False(viewModel.CanDeleteProfile);
    }

    [Fact]
    public void Open_ListsTheBuiltInPresetsBeforeUserProfiles()
    {
        Store.Save(new ColorProfile { Name = "Graphite", Colors = ColorScheme.Default });
        var viewModel = CreateViewModel();

        viewModel.Open();

        var expected = new[] { "Default" }
            .Concat(ColorSchemePresets.All.Select(preset => preset.Name))
            .Append("Graphite");

        Assert.Equal(expected, viewModel.Profiles);
    }

    [Fact]
    public void SelectingAPreset_PreviewsItsColorsAndProtectsIt()
    {
        var preset = ColorSchemePresets.VioletNebula;
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.SelectedProfile = preset.Name;

        Assert.Equal(preset.Colors.Accent, viewModel.AccentColor);
        Assert.Equal(preset.Colors.Canvas, viewModel.CanvasColor);
        Assert.Equal(preset.Name, viewModel.ProfileName);
        Assert.False(viewModel.CanDeleteProfile);
        ThemeService.Verify(service => service.Apply(It.Is<ColorScheme>(scheme => scheme.Equals(preset.Colors))), Times.AtLeastOnce);
    }

    [Fact]
    public void Apply_OnUntouchedPreset_ActivatesIt()
    {
        var preset = ColorSchemePresets.Ember;
        var saved = false;
        var closed = false;
        var viewModel = CreateViewModel();
        viewModel.Saved += () => saved = true;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Open();
        viewModel.SelectedProfile = preset.Name;

        viewModel.ApplyCommand.Execute(null);

        Assert.True(saved);
        Assert.True(closed);
        Assert.False(viewModel.HasValidationMessage);
        Assert.Equal(preset.Name, Store.GetActiveProfileName());
        Assert.Equal(preset.Colors, Store.LoadActiveScheme());
        Assert.Empty(Store.LoadProfiles());
    }

    [Fact]
    public void Apply_OnModifiedPreset_AsksForAName()
    {
        var viewModel = CreateViewModel();
        var closed = false;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Open();
        viewModel.SelectedProfile = "Midnight Ocean";
        viewModel.AccentColor = "#DC2626";

        viewModel.ApplyCommand.Execute(null);

        Assert.False(closed);
        Assert.True(viewModel.HasValidationMessage);
        Assert.Contains("built in", viewModel.ValidationMessage);
        Assert.Empty(Store.LoadProfiles());
    }

    [Fact]
    public void Apply_ForkingAPreset_SavesAUserProfileAndKeepsThePreset()
    {
        var preset = ColorSchemePresets.MidnightOcean;
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.SelectedProfile = preset.Name;
        viewModel.AccentColor = "#DC2626";
        viewModel.ProfileName = "Deep red";

        viewModel.ApplyCommand.Execute(null);

        var profile = Assert.Single(Store.LoadProfiles());
        Assert.Equal("Deep red", profile.Name);
        Assert.Equal("#DC2626", profile.Colors.Accent);
        Assert.Equal("Deep red", Store.GetActiveProfileName());
        Assert.Contains(preset.Name, viewModel.Profiles);
    }

    [Fact]
    public void EditingAccent_DerivesShadesAndPreviewsLive()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.AccentColor = "#2563EB";

        Assert.Equal(ColorDerivation.Hover(RgbColor.Parse("#2563EB")).ToHex(), viewModel.AccentHoverColor);
        Assert.Equal(ColorDerivation.Selection(RgbColor.Parse("#2563EB")).ToHex(), viewModel.SelectionColor);
        Assert.Equal(ColorDerivation.Focus(RgbColor.Parse("#2563EB")).ToHex(), viewModel.FocusColor);
        ThemeService.Verify(
            service => service.Apply(It.Is<ColorScheme>(scheme => scheme.Accent == "#2563EB" && scheme.AccentHover == viewModel.AccentHoverColor)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void EditingTheBackground_DerivesTheOverlay()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.CanvasColor = "#0B1220";

        Assert.Equal(ColorDerivation.Overlay(RgbColor.Parse("#0B1220")).ToHex(), viewModel.OverlayColor);
    }

    [Fact]
    public void EditingADerivedToken_TurnsAutomaticDerivationOff()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        Assert.True(viewModel.AutoDerive);
        viewModel.FocusColor = "#123456";

        Assert.False(viewModel.AutoDerive);
        Assert.Equal("#123456", viewModel.FocusColor);
    }

    [Fact]
    public void Cancel_RestoresTheOriginalScheme()
    {
        var original = ColorScheme.Default with { Accent = "#2563EB", Focus = "#93C5FD" };
        Store.Save(new ColorProfile { Name = "Ocean", Colors = original });
        Store.SetActiveProfile("Ocean");
        var viewModel = CreateViewModel();
        var closed = false;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Open();

        viewModel.AccentColor = "#DC2626";
        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
        ThemeService.Verify(service => service.Apply(It.Is<ColorScheme>(scheme => scheme.Equals(original))), Times.AtLeastOnce);
        Assert.Single(Store.LoadProfiles());
    }

    [Fact]
    public void Apply_SavesANewProfileAndActivatesIt()
    {
        var viewModel = CreateViewModel();
        var saved = false;
        var closed = false;
        viewModel.Saved += () => saved = true;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Open();
        viewModel.AccentColor = "#2563EB";
        viewModel.ProfileName = "Ocean";

        viewModel.ApplyCommand.Execute(null);

        Assert.True(saved);
        Assert.True(closed);
        var profile = Assert.Single(Store.LoadProfiles());
        Assert.Equal("Ocean", profile.Name);
        Assert.Equal("#2563EB", profile.Colors.Accent);
        Assert.Equal("Ocean", Store.GetActiveProfileName());
        Assert.Equal("#2563EB", Store.LoadActiveScheme().Accent);
    }

    [Fact]
    public void Apply_ProtectsTheBuiltInDefaultWhenColorsChanged()
    {
        var viewModel = CreateViewModel();
        var closed = false;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Open();
        viewModel.AccentColor = "#DC2626";
        viewModel.ProfileName = "Default";

        viewModel.ApplyCommand.Execute(null);

        Assert.False(closed);
        Assert.True(viewModel.HasValidationMessage);
        Assert.Empty(Store.LoadProfiles());
    }

    [Fact]
    public void Apply_OnUntouchedDefault_RestoresTheBuiltInPalette()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        Store.SetActiveProfile("Ocean");
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.SelectedProfile = "Default";

        viewModel.ApplyCommand.Execute(null);

        Assert.Null(Store.GetActiveProfileName());
        Assert.True(Store.LoadActiveScheme().MatchesDefaults());
        ThemeService.Verify(service => service.Apply(It.Is<ColorScheme>(scheme => scheme.MatchesDefaults())), Times.AtLeastOnce);
    }

    [Fact]
    public void Apply_RejectsDuplicateNames()
    {
        Store.Save(new ColorProfile { Name = "Graphite", Colors = ColorScheme.Default });
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default });
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.SelectedProfile = "Ocean";
        viewModel.ProfileName = "Graphite";

        viewModel.ApplyCommand.Execute(null);

        Assert.True(viewModel.HasValidationMessage);
        Assert.Equal(2, Store.LoadProfiles().Count);
    }

    [Fact]
    public void Apply_RenamesTheSelectedProfile()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        Store.SetActiveProfile("Ocean");
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.ProfileName = "Pacific";

        viewModel.ApplyCommand.Execute(null);

        var profile = Assert.Single(Store.LoadProfiles());
        Assert.Equal("Pacific", profile.Name);
        Assert.Equal("#2563EB", profile.Colors.Accent);
        Assert.Equal("Pacific", Store.GetActiveProfileName());
    }

    [Fact]
    public void Apply_RejectsInvalidColors()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.TextColor = "not-a-color";

        viewModel.ApplyCommand.Execute(null);

        Assert.True(viewModel.HasValidationMessage);
        Assert.Empty(Store.LoadProfiles());
    }

    [Fact]
    public void Delete_FallsBackToTheDefaultProfile()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        Store.SetActiveProfile("Ocean");
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.DeleteProfileCommand.Execute(null);

        Assert.Empty(Store.LoadProfiles());
        Assert.Null(Store.GetActiveProfileName());
        Assert.Equal("Default", viewModel.SelectedProfile);
        Assert.True(viewModel.AutoDerive);
        ThemeService.Verify(service => service.Apply(It.Is<ColorScheme>(scheme => scheme.MatchesDefaults())), Times.AtLeastOnce);
    }

    [Fact]
    public void Delete_IsIgnoredForTheBuiltInDefault()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.DeleteProfileCommand.Execute(null);

        Assert.Equal("Default", viewModel.SelectedProfile);
    }

    [Fact]
    public void RestoreDefaultColors_ResetsEveryTokenAndDerivation()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.TextColor = "#101010";
        viewModel.AccentColor = "#DC2626";
        viewModel.AutoDerive = false;

        viewModel.RestoreDefaultColorsCommand.Execute(null);

        Assert.Equal(ColorSchemeDefaults.Text, viewModel.TextColor);
        Assert.Equal(ColorSchemeDefaults.Accent, viewModel.AccentColor);
        Assert.Equal(ColorSchemeDefaults.AccentHover, viewModel.AccentHoverColor);
        Assert.True(viewModel.AutoDerive);
        Assert.False(viewModel.HasWarnings);
    }

    [Fact]
    public void ResetToken_RestoresOnlyThatToken()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.AccentColor = "#DC2626";
        viewModel.TextColor = "#101010";

        viewModel.ResetTokenCommand.Execute("AccentColor");

        Assert.Equal(ColorSchemeDefaults.Accent, viewModel.AccentColor);
        Assert.Equal("#101010", viewModel.TextColor);
    }

    [Fact]
    public void LowContrast_ProducesWarningsWithoutBlocking()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.TextColor = "#101010";

        Assert.True(viewModel.HasWarnings);
        Assert.Contains(viewModel.ContrastWarnings, warning => warning.Contains("Body text on background"));

        viewModel.TextColor = ColorSchemeDefaults.Text;

        Assert.False(viewModel.HasWarnings);
    }

    [Fact]
    public void SelectingAProfile_PreviewsItsColorsAndClearsMessages()
    {
        Store.Save(new ColorProfile { Name = "Ocean", Colors = ColorScheme.Default with { Accent = "#2563EB" } });
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.TextColor = "#101010";

        viewModel.SelectedProfile = "Ocean";

        Assert.Equal("#2563EB", viewModel.AccentColor);
        Assert.Equal("Ocean", viewModel.ProfileName);
        Assert.False(viewModel.HasWarnings);
        Assert.False(viewModel.HasValidationMessage);
    }
}
