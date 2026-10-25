using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PWManager.Application.UseCases.Bootstrapping;
using PWManager.Application.UseCases.Unlock;
using PWManager.Composition;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Infra.Configuration;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;
using PWManager.Views;
using AvaloniaApp = Avalonia.Application;

namespace PWManager;

public partial class App : AvaloniaApp
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        MoveRoamingDataIfNeeded(configuration);

        Services = ServiceRegistration.Build(
            configuration,
            () => ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        Services.GetRequiredService<IThemeService>()
            .Apply(Services.GetRequiredService<IColorThemeStore>().LoadActiveScheme());

        var shellViewModel = Services.GetRequiredService<ShellViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShellWindow
            {
                DataContext = shellViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();

        // Vault preparation is deliberately started after the window has been
        // assigned: it must never delay the first frame.
        _ = PrepareVaultAsync(shellViewModel);
    }

    private static async Task PrepareVaultAsync(ShellViewModel shell)
    {
        VaultStatus status;

        try
        {
            status = await Services.GetRequiredService<IVaultBootstrapper>().InitializeAsync();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Vault bootstrap failed: {exception}");
            status = VaultStatus.Existing;
        }

        await Dispatcher.UIThread.InvokeAsync(() => shell.Unlock.Configure(status));
    }

    private static void MoveRoamingDataIfNeeded(IConfiguration configuration)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        DataFolderMigration.MoveIfNeeded(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            configuration["DatabaseConfig:AppFolder"] ?? "PWManager");
    }
}
