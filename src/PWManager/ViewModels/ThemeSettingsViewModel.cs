using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Appearance;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

// Profiles are JSON files outside the vault database.
public partial class ThemeSettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IColorThemeStore _store;
    private ColorScheme _original = ColorScheme.Default;
    private bool _suppressUpdates;

    public ThemeSettingsViewModel(IThemeService themeService, IColorThemeStore store)
    {
        _themeService = themeService;
        _store = store;
    }

    public event Action? CloseRequested;

    public event Action? Saved;

    public ObservableCollection<string> Profiles { get; } = new();

    public ObservableCollection<string> ContrastWarnings { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
    private string _selectedProfile = ColorProfile.DefaultProfileName;

    [ObservableProperty]
    private string _profileName = ColorProfile.DefaultProfileName;

    [ObservableProperty]
    private bool _autoDerive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private string _canvasColor = ColorSchemeDefaults.Canvas;

    [ObservableProperty]
    private string _surfaceColor = ColorSchemeDefaults.Surface;

    [ObservableProperty]
    private string _raisedColor = ColorSchemeDefaults.Raised;

    [ObservableProperty]
    private string _fieldColor = ColorSchemeDefaults.Field;

    [ObservableProperty]
    private string _lineColor = ColorSchemeDefaults.Line;

    [ObservableProperty]
    private string _textColor = ColorSchemeDefaults.Text;

    [ObservableProperty]
    private string _secondaryTextColor = ColorSchemeDefaults.SecondaryText;

    [ObservableProperty]
    private string _titleColor = ColorSchemeDefaults.Title;

    [ObservableProperty]
    private string _buttonTextColor = ColorSchemeDefaults.ButtonText;

    [ObservableProperty]
    private string _accentColor = ColorSchemeDefaults.Accent;

    [ObservableProperty]
    private string _accentHoverColor = ColorSchemeDefaults.AccentHover;

    [ObservableProperty]
    private string _focusColor = ColorSchemeDefaults.Focus;

    [ObservableProperty]
    private string _selectionColor = ColorSchemeDefaults.Selection;

    [ObservableProperty]
    private string _successColor = ColorSchemeDefaults.Success;

    [ObservableProperty]
    private string _dangerColor = ColorSchemeDefaults.Danger;

    [ObservableProperty]
    private string _warningColor = ColorSchemeDefaults.Warning;

    [ObservableProperty]
    private string _overlayColor = ColorSchemeDefaults.Overlay;

    public bool CanDeleteProfile => !ColorProfile.IsReservedName(SelectedProfile);

    public bool HasValidationMessage => ValidationMessage.Length > 0;

    public bool HasWarnings => ContrastWarnings.Count > 0;

    public void Open()
    {
        _original = _store.LoadActiveScheme();

        var activeName = _store.GetActiveProfileName() ?? ColorProfile.DefaultProfileName;
        ReloadProfiles();
        var selected = Profiles.Any(name => string.Equals(name, activeName, StringComparison.OrdinalIgnoreCase))
            ? Profiles.First(name => string.Equals(name, activeName, StringComparison.OrdinalIgnoreCase))
            : ColorProfile.DefaultProfileName;

        _suppressUpdates = true;
        LoadScheme(_original);
        AutoDerive = ColorDerivation.IsCoherent(_original);
        ProfileName = selected;
        SelectedProfile = selected;
        _suppressUpdates = false;

        ValidationMessage = string.Empty;
        UpdateWarnings();
        _themeService.Apply(_original);
    }

    [RelayCommand]
    private void Apply()
    {
        var name = ProfileName.Trim();
        var scheme = BuildScheme();

        if (!scheme.IsValid)
        {
            ValidationMessage = "Enter a valid color in #RRGGBB format.";
            return;
        }

        var builtInScheme = FindBuiltInScheme(name);

        if (builtInScheme is not null)
        {
            if (!SameScheme(scheme, builtInScheme))
            {
                ValidationMessage = $"{name} is built in and cannot be changed. Choose a name to save your colors as a profile.";
                return;
            }

            _store.SetActiveProfile(ColorProfile.IsDefaultName(name) ? null : name);
            _themeService.Apply(builtInScheme);
            Saved?.Invoke();
            CloseRequested?.Invoke();
            return;
        }

        if (!ColorProfile.IsValidName(name))
        {
            ValidationMessage = $"Choose a profile name up to {ColorProfile.MaxNameLength} characters.";
            return;
        }

        var conflicts = _store.LoadProfiles().Any(profile =>
            !string.Equals(profile.Name, SelectedProfile, StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

        if (conflicts)
        {
            ValidationMessage = "A profile with this name already exists.";
            return;
        }

        var selectedIsUserProfile = !ColorProfile.IsReservedName(SelectedProfile);
        var isRenaming = selectedIsUserProfile
            && !string.Equals(name, SelectedProfile, StringComparison.OrdinalIgnoreCase);

        if (isRenaming)
        {
            _store.Delete(SelectedProfile);
        }

        _store.Save(new ColorProfile { Name = name, Colors = scheme });
        _store.SetActiveProfile(name);
        _themeService.Apply(scheme);
        Saved?.Invoke();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        _themeService.Apply(_original);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (ColorProfile.IsReservedName(SelectedProfile))
        {
            return;
        }

        var deleted = SelectedProfile;
        _store.Delete(deleted);

        if (string.Equals(_store.GetActiveProfileName(), deleted, StringComparison.OrdinalIgnoreCase))
        {
            _store.SetActiveProfile(null);
        }

        ReloadProfiles();

        _suppressUpdates = true;
        SelectedProfile = ColorProfile.DefaultProfileName;
        ProfileName = ColorProfile.DefaultProfileName;
        LoadScheme(ColorScheme.Default);
        AutoDerive = true;
        _suppressUpdates = false;

        ValidationMessage = string.Empty;
        UpdateWarnings();
        _themeService.Apply(ColorScheme.Default);
    }

    [RelayCommand]
    private void RestoreDefaultColors()
    {
        _suppressUpdates = true;
        LoadScheme(ColorScheme.Default);
        AutoDerive = true;
        _suppressUpdates = false;

        ApplyPreview();
        UpdateWarnings();
    }

    [RelayCommand]
    private void ResetToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        _suppressUpdates = true;

        switch (token)
        {
            case nameof(CanvasColor): CanvasColor = ColorSchemeDefaults.Canvas; break;
            case nameof(SurfaceColor): SurfaceColor = ColorSchemeDefaults.Surface; break;
            case nameof(RaisedColor): RaisedColor = ColorSchemeDefaults.Raised; break;
            case nameof(FieldColor): FieldColor = ColorSchemeDefaults.Field; break;
            case nameof(LineColor): LineColor = ColorSchemeDefaults.Line; break;
            case nameof(TextColor): TextColor = ColorSchemeDefaults.Text; break;
            case nameof(SecondaryTextColor): SecondaryTextColor = ColorSchemeDefaults.SecondaryText; break;
            case nameof(TitleColor): TitleColor = ColorSchemeDefaults.Title; break;
            case nameof(ButtonTextColor): ButtonTextColor = ColorSchemeDefaults.ButtonText; break;
            case nameof(AccentColor): AccentColor = ColorSchemeDefaults.Accent; break;
            case nameof(AccentHoverColor): AccentHoverColor = ColorSchemeDefaults.AccentHover; break;
            case nameof(FocusColor): FocusColor = ColorSchemeDefaults.Focus; break;
            case nameof(SelectionColor): SelectionColor = ColorSchemeDefaults.Selection; break;
            case nameof(SuccessColor): SuccessColor = ColorSchemeDefaults.Success; break;
            case nameof(DangerColor): DangerColor = ColorSchemeDefaults.Danger; break;
            case nameof(WarningColor): WarningColor = ColorSchemeDefaults.Warning; break;
            case nameof(OverlayColor): OverlayColor = ColorSchemeDefaults.Overlay; break;
            default: _suppressUpdates = false; return;
        }

        _suppressUpdates = false;
        ApplyPreview();
        UpdateWarnings();
    }

    private void ReloadProfiles()
    {
        Profiles.Clear();
        Profiles.Add(ColorProfile.DefaultProfileName);

        foreach (var preset in ColorSchemePresets.All)
        {
            Profiles.Add(preset.Name);
        }

        foreach (var profile in _store.LoadProfiles())
        {
            Profiles.Add(profile.Name);
        }
    }

    private void LoadProfile(string name)
    {
        if (_suppressUpdates)
        {
            return;
        }

        var scheme = FindBuiltInScheme(name);

        if (scheme is null)
        {
            var profile = _store.LoadProfiles()
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

            scheme = profile?.Colors ?? ColorScheme.Default;
        }

        _suppressUpdates = true;
        LoadScheme(scheme);
        ProfileName = ColorProfile.IsDefaultName(name) ? ColorProfile.DefaultProfileName : name;
        AutoDerive = ColorDerivation.IsCoherent(scheme);
        _suppressUpdates = false;

        ValidationMessage = string.Empty;
        UpdateWarnings();
        _themeService.Apply(scheme);
    }

    private static ColorScheme? FindBuiltInScheme(string name)
    {
        if (ColorProfile.IsDefaultName(name))
        {
            return ColorScheme.Default;
        }

        return ColorSchemePresets.Find(name)?.Colors;
    }

    private static bool SameScheme(ColorScheme first, ColorScheme second) => first
        .Tokens()
        .All(token => string.Equals(token.Value, second.GetToken(token.Key), StringComparison.OrdinalIgnoreCase));

    private void LoadScheme(ColorScheme scheme)
    {
        CanvasColor = scheme.Canvas;
        SurfaceColor = scheme.Surface;
        RaisedColor = scheme.Raised;
        FieldColor = scheme.Field;
        LineColor = scheme.Line;
        TextColor = scheme.Text;
        SecondaryTextColor = scheme.SecondaryText;
        TitleColor = scheme.Title;
        ButtonTextColor = scheme.ButtonText;
        AccentColor = scheme.Accent;
        AccentHoverColor = scheme.AccentHover;
        FocusColor = scheme.Focus;
        SelectionColor = scheme.Selection;
        SuccessColor = scheme.Success;
        DangerColor = scheme.Danger;
        WarningColor = scheme.Warning;
        OverlayColor = scheme.Overlay;
    }

    private ColorScheme BuildScheme() => ColorScheme.From(
        CanvasColor, SurfaceColor, RaisedColor, FieldColor, LineColor, TextColor, SecondaryTextColor, TitleColor,
        ButtonTextColor, AccentColor, AccentHoverColor, FocusColor, SelectionColor, SuccessColor, DangerColor,
        WarningColor, OverlayColor);

    private void ApplyPreview()
    {
        var scheme = BuildScheme();

        if (!scheme.IsValid)
        {
            ValidationMessage = "Enter a valid color in #RRGGBB format.";
            return;
        }

        ValidationMessage = string.Empty;
        _themeService.Apply(scheme);
    }

    private void UpdateWarnings()
    {
        ContrastWarnings.Clear();
        var scheme = BuildScheme();

        if (scheme.IsValid)
        {
            foreach (var warning in ColorContrast.Audit(scheme))
            {
                ContrastWarnings.Add(warning.Message);
            }
        }

        OnPropertyChanged(nameof(HasWarnings));
    }

    private void RecomputeDerived()
    {
        if (!RgbColor.TryParse(AccentColor, out var accent) || !RgbColor.TryParse(CanvasColor, out var canvas))
        {
            return;
        }

        _suppressUpdates = true;
        AccentHoverColor = ColorDerivation.Hover(accent).ToHex();
        SelectionColor = ColorDerivation.Selection(accent).ToHex();
        FocusColor = ColorDerivation.Focus(accent).ToHex();
        OverlayColor = ColorDerivation.Overlay(canvas).ToHex();
        _suppressUpdates = false;
    }

    private void OnTokenChanged(string token)
    {
        if (_suppressUpdates)
        {
            return;
        }

        if (AutoDerive && token is nameof(AccentHoverColor) or nameof(SelectionColor) or nameof(FocusColor) or nameof(OverlayColor))
        {
            _suppressUpdates = true;
            AutoDerive = false;
            _suppressUpdates = false;
        }

        if (AutoDerive && token is nameof(AccentColor) or nameof(CanvasColor))
        {
            RecomputeDerived();
        }

        ApplyPreview();
        UpdateWarnings();
    }

    partial void OnSelectedProfileChanged(string value) => LoadProfile(value);

    partial void OnAutoDeriveChanged(bool value)
    {
        if (_suppressUpdates || !value)
        {
            return;
        }

        RecomputeDerived();
        ApplyPreview();
        UpdateWarnings();
    }

    partial void OnCanvasColorChanged(string value) => OnTokenChanged(nameof(CanvasColor));

    partial void OnSurfaceColorChanged(string value) => OnTokenChanged(nameof(SurfaceColor));

    partial void OnRaisedColorChanged(string value) => OnTokenChanged(nameof(RaisedColor));

    partial void OnFieldColorChanged(string value) => OnTokenChanged(nameof(FieldColor));

    partial void OnLineColorChanged(string value) => OnTokenChanged(nameof(LineColor));

    partial void OnTextColorChanged(string value) => OnTokenChanged(nameof(TextColor));

    partial void OnSecondaryTextColorChanged(string value) => OnTokenChanged(nameof(SecondaryTextColor));

    partial void OnTitleColorChanged(string value) => OnTokenChanged(nameof(TitleColor));

    partial void OnButtonTextColorChanged(string value) => OnTokenChanged(nameof(ButtonTextColor));

    partial void OnAccentColorChanged(string value) => OnTokenChanged(nameof(AccentColor));

    partial void OnAccentHoverColorChanged(string value) => OnTokenChanged(nameof(AccentHoverColor));

    partial void OnFocusColorChanged(string value) => OnTokenChanged(nameof(FocusColor));

    partial void OnSelectionColorChanged(string value) => OnTokenChanged(nameof(SelectionColor));

    partial void OnSuccessColorChanged(string value) => OnTokenChanged(nameof(SuccessColor));

    partial void OnDangerColorChanged(string value) => OnTokenChanged(nameof(DangerColor));

    partial void OnWarningColorChanged(string value) => OnTokenChanged(nameof(WarningColor));

    partial void OnOverlayColorChanged(string value) => OnTokenChanged(nameof(OverlayColor));
}
