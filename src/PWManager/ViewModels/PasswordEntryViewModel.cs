using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PWManager.Domain.Vault;

namespace PWManager.ViewModels;

public partial class PasswordEntryViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    private string _site = string.Empty;

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private DateTime _creationDate;

    [ObservableProperty]
    private DateTime _lastUpdated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordMask), nameof(VisibilityLabel))]
    private bool _isPasswordVisible;

    public char PasswordMask => IsPasswordVisible ? '\0' : '•';

    public string VisibilityLabel => IsPasswordVisible ? "Hide password" : "Show password";

    public string Initial => string.IsNullOrWhiteSpace(Site) ? "?" : Site.Trim()[..1].ToUpperInvariant();

    public static PasswordEntryViewModel FromModel(User user) => new()
    {
        Id = user.Id,
        Site = user.Site,
        Login = user.Login,
        Password = user.Password,
        CreationDate = user.CreationDate,
        LastUpdated = user.LastUpdated
    };
}
