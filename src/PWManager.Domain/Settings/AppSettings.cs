namespace PWManager.Domain.Settings;

// Stored outside the vault database. Zero disables the numeric timeouts.
public sealed record AppSettings
{
    public const int DefaultAutoLockMinutes = 15;

    public const int DefaultClipboardClearSeconds = 25;

    public const int DefaultHidePasswordsAfterSeconds = 20;

    public int AutoLockMinutes { get; init; } = DefaultAutoLockMinutes;

    public bool LockWhenMinimized { get; init; }

    public int ClipboardClearSeconds { get; init; } = DefaultClipboardClearSeconds;

    public int HidePasswordsAfterSeconds { get; init; } = DefaultHidePasswordsAfterSeconds;

    public AppSettings Normalized() => this with
    {
        AutoLockMinutes = Math.Clamp(AutoLockMinutes, 0, 24 * 60),
        ClipboardClearSeconds = Math.Clamp(ClipboardClearSeconds, 0, 3600),
        HidePasswordsAfterSeconds = Math.Clamp(HidePasswordsAfterSeconds, 0, 3600)
    };
}
