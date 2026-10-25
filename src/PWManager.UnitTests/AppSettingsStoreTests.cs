using PWManager.Domain.Settings;
using PWManager.Infra.Configuration;

namespace PWManager.UnitTests;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pwmanager-settings-" + Guid.NewGuid().ToString("N"));

    private AppSettingsStore Store => new(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsSafeDefaults()
    {
        var settings = Store.Load();

        Assert.Equal(AppSettings.DefaultAutoLockMinutes, settings.AutoLockMinutes);
        Assert.Equal(AppSettings.DefaultClipboardClearSeconds, settings.ClipboardClearSeconds);
        Assert.Equal(AppSettings.DefaultHidePasswordsAfterSeconds, settings.HidePasswordsAfterSeconds);
        Assert.False(settings.LockWhenMinimized);
    }

    [Fact]
    public void Save_AndLoad_RoundTripsEveryOption()
    {
        Store.Save(new AppSettings
        {
            AutoLockMinutes = 5,
            LockWhenMinimized = true,
            ClipboardClearSeconds = 10,
            HidePasswordsAfterSeconds = 30
        });

        var settings = Store.Load();

        Assert.Equal(5, settings.AutoLockMinutes);
        Assert.True(settings.LockWhenMinimized);
        Assert.Equal(10, settings.ClipboardClearSeconds);
        Assert.Equal(30, settings.HidePasswordsAfterSeconds);
    }

    [Fact]
    public void Save_NormalizesOutOfRangeValues()
    {
        Store.Save(new AppSettings
        {
            AutoLockMinutes = -5,
            ClipboardClearSeconds = 999_999,
            HidePasswordsAfterSeconds = -1
        });

        var settings = Store.Load();

        Assert.Equal(0, settings.AutoLockMinutes);
        Assert.Equal(3600, settings.ClipboardClearSeconds);
        Assert.Equal(0, settings.HidePasswordsAfterSeconds);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, "{ not json");

        Assert.Equal(AppSettings.DefaultAutoLockMinutes, Store.Load().AutoLockMinutes);
    }
}
