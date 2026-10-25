using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.Common;

namespace PWManager.ViewModels;

public partial class PasswordGeneratorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _generatedPassword = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private int _length = 20;

    [ObservableProperty]
    private bool _useUppercase = true;

    [ObservableProperty]
    private bool _useLowercase = true;

    [ObservableProperty]
    private bool _useNumbers = true;

    [ObservableProperty]
    private bool _useSymbols = true;

    [ObservableProperty]
    private int _strengthValue;

    [ObservableProperty]
    private string _strengthLabel = string.Empty;

    public bool IsWeak => StrengthLabel == "Weak";

    public bool IsFair => StrengthLabel == "Fair";

    public PasswordGeneratorViewModel() => Generate();

    [RelayCommand]
    private void Generate()
    {
        GeneratedPassword = PasswordGenerator.Generate(Length, UseUppercase, UseLowercase, UseNumbers, UseSymbols);
        UpdateStrength();
        OnPropertyChanged(nameof(UsesFallback));
    }

    public char PasswordMask => IsPasswordVisible ? '\0' : '•';

    public string VisibilityLabel => IsPasswordVisible ? "Hide password" : "Show password";

    public bool UsesFallback => !UseUppercase && !UseLowercase && !UseNumbers && !UseSymbols;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordMask));
        OnPropertyChanged(nameof(VisibilityLabel));
    }

    partial void OnLengthChanged(int value)
    {
        if (value < 8 || value > 48)
        {
            Length = Math.Clamp(value, 8, 48);
            return;
        }

        Generate();
    }

    partial void OnUseUppercaseChanged(bool value) => Generate();

    partial void OnUseLowercaseChanged(bool value) => Generate();

    partial void OnUseNumbersChanged(bool value) => Generate();

    partial void OnUseSymbolsChanged(bool value) => Generate();

    private void UpdateStrength()
    {
        var score = PasswordGenerator.EstimateStrengthScore(
            Length, UseUppercase, UseLowercase, UseNumbers, UseSymbols);

        StrengthValue = (int)((score / 6.0) * 100);
        StrengthLabel = score switch
        {
            <= 2 => "Weak",
            3 => "Fair",
            4 => "Strong",
            _ => "Very Strong"
        };
        OnPropertyChanged(nameof(IsWeak));
        OnPropertyChanged(nameof(IsFair));
    }
}
