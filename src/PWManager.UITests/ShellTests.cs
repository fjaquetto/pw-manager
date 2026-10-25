using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PWManager.Application.UseCases.Unlock;
using PWManager.Infra.Security;
using PWManager.Services;
using PWManager.UITests.Fakes;
using PWManager.ViewModels;
using PWManager.Views;

namespace PWManager.UITests;

public class ShellTests
{
    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static int Created;

    private static ShellWindow Open(
        UnlockStub service,
        AppSettingsFake? settings = null,
        Action<ShellViewModel>? configure = null,
        bool configured = true)
    {
        var shell = new ShellViewModel(
            new UnlockViewModel(service, new AppInfo("2.3.1", "3481920")),
            () =>
            {
                Created++;
                return MemoryVault.Sample(3).CreateViewModel();
            },
            new VaultSession(),
            settings ?? new AppSettingsFake(),
            new AppInfo("2.3.1", "3481920"),
            TimeProvider.System);

        if (configured)
        {
            shell.Unlock.Configure(VaultStatus.Existing);
        }

        configure?.Invoke(shell);
        var window = new ShellWindow { DataContext = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void PumpUntil(Func<bool> condition, int attempts = 100)
    {
        for (var i = 0; i < attempts && !condition(); i++)
        {
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "Condition was not reached while pumping the dispatcher.");
    }

    private static (double Width, double Height, int X, int Y) ExpectedVaultGeometry(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window)!;
        var scaling = screen.Scaling > 0 ? screen.Scaling : 1;
        var width = Math.Min(1180, screen.WorkingArea.Width / scaling);
        var height = Math.Min(780, screen.WorkingArea.Height / scaling - 40);
        var x = screen.WorkingArea.X + (screen.WorkingArea.Width - width * scaling) / 2;
        var y = screen.WorkingArea.Y + (screen.WorkingArea.Height - height * scaling) / 2;

        return (width, height, (int)Math.Round(x), (int)Math.Round(y));
    }

    [AvaloniaFact]
    public void Shell_StartsLocked_FocusesPasswordAndNeverCreatesTheVault()
    {
        Created = 0;
        var window = Open(new UnlockStub());

        try
        {
            var password = Find<TextBox>(window, "PasswordBox");
            var shell = (ShellViewModel)window.DataContext!;

            Assert.True(password.IsFocused);
            Assert.Equal('•', password.PasswordChar);
            Assert.True(Find<ContentControl>(window, "UnlockHost").IsEffectivelyVisible);
            Assert.False(Find<ContentControl>(window, "VaultHost").IsEffectivelyVisible);
            Assert.Null(shell.Vault);
            Assert.Equal(0, Created);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_ShowsTheApplicationVersionInTheTitleAndUnlockFooter()
    {
        var window = Open(new UnlockStub());

        try
        {
            Assert.Equal("PWManager 2.3.1", window.Title);
            Assert.Equal("v2.3.1", Find<TextBlock>(window, "VersionLabel").Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_WhilePreparing_BlocksSubmitUntilTheStatusIsConfigured()
    {
        Created = 0;
        var service = new UnlockStub();
        var window = Open(service, configured: false);

        try
        {
            var shell = (ShellViewModel)window.DataContext!;
            var unlock = Find<Button>(window, "UnlockButton");

            Assert.True(shell.Unlock.IsPreparing);
            Assert.False(unlock.IsEffectivelyEnabled);

            window.KeyTextInput("master");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, service.Calls);

            shell.Unlock.Configure(VaultStatus.Existing);
            Dispatcher.UIThread.RunJobs();

            Assert.False(shell.Unlock.IsPreparing);
            Assert.True(unlock.IsEffectivelyEnabled);

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            PumpUntil(() => service.Calls == 1);

            Assert.Equal(1, service.Calls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_UnlockPanel_CentersTheFormAndFitsThePasswordToggle()
    {
        Created = 0;
        var window = Open(new UnlockStub());

        try
        {
            var panel = window.GetVisualDescendants().OfType<UnlockPanel>().Single();
            var password = Find<TextBox>(window, "PasswordBox");
            var unlock = Find<Button>(window, "UnlockButton");
            var visibility = Find<Button>(window, "PasswordVisibility");
            var passwordOrigin = password.TranslatePoint(default, panel)!.Value;
            var visibilityOrigin = visibility.TranslatePoint(default, password)!.Value;
            var label = unlock.GetVisualDescendants().OfType<TextBlock>().First();
            var labelOrigin = label.TranslatePoint(default, unlock)!.Value;

            Assert.Equal(panel.Bounds.Width / 2, passwordOrigin.X + password.Bounds.Width / 2, 1);
            Assert.Equal(unlock.Bounds.Width, password.Bounds.Width, 1);
            Assert.InRange(visibilityOrigin.X, 0, password.Bounds.Width - visibility.Bounds.Width);
            Assert.Equal(password.Bounds.Height / 2, visibilityOrigin.Y + visibility.Bounds.Height / 2, 1);
            Assert.Equal(unlock.Bounds.Height / 2, labelOrigin.Y + label.Bounds.Height / 2, 1.5);
            Assert.NotNull(window.Icon);
            Capture(window, "unlock");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_ShowsProgressWhileUnlocking_ThenShowsTheErrorAndStaysLocked()
    {
        Created = 0;
        var service = new UnlockStub { Pending = new() };
        var window = Open(service);

        try
        {
            var shell = (ShellViewModel)window.DataContext!;

            window.KeyTextInput("incorrect-example");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, service.Calls);
            Assert.True(shell.ShowProgress);
            Assert.False(shell.Unlock.UnlockCommand.CanExecute(null));
            Assert.False(Find<ContentControl>(window, "VaultHost").IsEffectivelyVisible);

            service.Pending.SetResult(UnlockResult.InvalidPassword);
            PumpUntil(() => shell.Unlock.HasError);

            Assert.False(shell.ShowProgress);
            Assert.False(shell.IsUnlocked);
            Assert.True(Find<ContentControl>(window, "UnlockHost").IsEffectivelyVisible);
            Assert.Equal(0, Created);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_SuccessfulUnlock_LoadsRevealsResizesAndFocusesTheVault()
    {
        Created = 0;
        var window = Open(new UnlockStub { Result = UnlockResult.Success });

        try
        {
            var shell = (ShellViewModel)window.DataContext!;
            var expected = ExpectedVaultGeometry(window);

            window.KeyTextInput("master");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            PumpUntil(() => shell.IsUnlocked);
            PumpUntil(() => Math.Abs(window.Width - expected.Width) < 0.0005 && Math.Abs(window.Height - expected.Height) < 0.0005);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, Created);
            Assert.NotNull(shell.Vault);
            Assert.Equal(3, shell.Vault!.Entries.Count);
            Assert.False(Find<ContentControl>(window, "UnlockHost").IsEffectivelyVisible);
            Assert.True(Find<ContentControl>(window, "VaultHost").IsEffectivelyVisible);
            Assert.True(window.GetVisualDescendants().OfType<VaultPanel>().Single().IsEffectivelyVisible);
            Assert.Equal(expected.Width, window.Width, 3);
            Assert.Equal(expected.Height, window.Height, 3);
            Assert.Equal(expected.X, window.Position.X);
            Assert.Equal(expected.Y, window.Position.Y);
            Assert.Equal("v2.3.1", Find<TextBlock>(window, "VaultVersion").Text);
            Assert.True(Find<TextBox>(window, "SearchBox").IsFocused);

            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
            window.KeyRelease(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<TextBox>(window, "SearchBox").IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_UnlockTransition_EndsCenteredInsteadOfAnchoredToTheTopLeft()
    {
        Created = 0;
        var window = Open(new UnlockStub { Result = UnlockResult.Success });

        try
        {
            var shell = (ShellViewModel)window.DataContext!;
            var expected = ExpectedVaultGeometry(window);
            var startPosition = window.Position;

            window.KeyTextInput("master");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

            PumpUntil(() => shell.IsUnlocked);
            PumpUntil(() => Math.Abs(window.Width - expected.Width) < 0.0005 && Math.Abs(window.Height - expected.Height) < 0.0005);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(startPosition, window.Position);
            Assert.Equal(expected.X, window.Position.X);
            Assert.Equal(expected.Y, window.Position.Y);
            Assert.Equal(expected.X + expected.Width / 2, window.Position.X + window.Width / 2, 1);
            Assert.Equal(expected.Y + expected.Height / 2, window.Position.Y + window.Height / 2, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_CtrlL_LocksBackToTheCompactUnlockScreen()
    {
        Created = 0;
        var window = Open(new UnlockStub { Result = UnlockResult.Success });

        try
        {
            var shell = (ShellViewModel)window.DataContext!;

            window.KeyTextInput("master");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            PumpUntil(() => shell.IsUnlocked);
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.L, RawInputModifiers.Control, PhysicalKey.L, null);
            window.KeyRelease(Key.L, RawInputModifiers.Control, PhysicalKey.L, null);
            PumpUntil(() => !shell.IsUnlocked);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(shell.Vault);
            Assert.True(Find<ContentControl>(window, "UnlockHost").IsEffectivelyVisible);
            Assert.False(Find<ContentControl>(window, "VaultHost").IsEffectivelyVisible);
            Assert.True(window.Width <= 700);
            Assert.True(Find<TextBox>(window, "PasswordBox").IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_CreateMode_ShowsConfirmationAndCreatesTheVault()
    {
        Created = 0;
        var window = Open(
            new UnlockStub { Result = UnlockResult.Success },
            configure: shell => shell.Unlock.Configure(VaultStatus.New));

        try
        {
            var shell = (ShellViewModel)window.DataContext!;
            var confirmation = Find<TextBox>(window, "ConfirmPasswordBox");

            Assert.True(confirmation.IsEffectivelyVisible);
            Assert.Equal("Create your vault", shell.Unlock.Heading);

            shell.Unlock.Password = "long-enough-password";
            shell.Unlock.ConfirmPassword = "long-enough-password";
            shell.Unlock.UnlockCommand.Execute(null);
            PumpUntil(() => shell.IsUnlocked);

            Assert.Equal(1, Created);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_SecurityModal_OpensAndClosesWithEscape()
    {
        Created = 0;
        var window = Open(new UnlockStub { Result = UnlockResult.Success });

        try
        {
            var shell = (ShellViewModel)window.DataContext!;

            window.KeyTextInput("master");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            PumpUntil(() => shell.IsUnlocked);
            Dispatcher.UIThread.RunJobs();

            shell.Vault!.OpenSecuritySettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(shell.Vault.IsSecuritySettingsOpen);
            Assert.True(Find<Border>(window, "SecurityLayer").IsEffectivelyVisible);
            Assert.True(Find<NumericUpDown>(window, "SecurityAutoLock").IsEffectivelyVisible);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(shell.Vault.IsSecuritySettingsOpen);
            Assert.False(Find<Border>(window, "SecurityLayer").IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VaultPanel_SecurityModal_KeepsTheRightAlignedFieldsClearOfTheScrollBar()
    {
        var viewModel = MemoryVault.Sample(3).CreateViewModel();
        viewModel.LoadEntriesAsync().GetAwaiter().GetResult();
        var window = new Window
        {
            Content = new VaultPanel
            {
                DataContext = viewModel
            },
            Width = 1180,
            Height = 780
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            viewModel.OpenSecuritySettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var securityView = window.GetVisualDescendants().OfType<SecuritySettingsView>().Single();
            var fields = securityView.GetVisualDescendants().OfType<NumericUpDown>()
                .Where(field => field.IsEffectivelyVisible)
                .ToList();
            Assert.NotEmpty(fields);

            var scroll = fields[0].GetVisualAncestors().OfType<ScrollViewer>().First();
            var scrollBar = scroll.GetVisualDescendants().OfType<ScrollBar>()
                .Single(bar => bar.Name == "PART_VerticalScrollBar" && bar.IsEffectivelyVisible);
            var scrollBarTopLeft = scrollBar.TranslatePoint(default, scroll);

            Assert.NotNull(scrollBarTopLeft);

            foreach (var field in fields)
            {
                var fieldTopLeft = field.TranslatePoint(default, scroll);

                Assert.NotNull(fieldTopLeft);
                Assert.True(fieldTopLeft!.Value.X + field.Bounds.Width <= scrollBarTopLeft!.Value.X,
                    $"The field ending at {fieldTopLeft.Value.X + field.Bounds.Width} overlaps the scroll bar starting at {scrollBarTopLeft.Value.X}.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static RenderTargetBitmap Render(Window window, double scale = 1)
    {
        Dispatcher.UIThread.RunJobs();
        var pixelSize = new PixelSize((int)(window.Bounds.Width * scale), (int)(window.Bounds.Height * scale));
        var resolution = new Vector(96 * scale, 96 * scale);
        var bitmap = new RenderTargetBitmap(pixelSize, resolution);
        bitmap.Render(window);

        return bitmap;
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PWMANAGER_SCREENSHOTS");

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var bitmap = Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"));
    }
}
