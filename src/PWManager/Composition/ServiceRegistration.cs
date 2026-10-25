using System;
using System.IO;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PWManager.Application.UseCases.Bootstrapping;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Migration;
using PWManager.Application.UseCases.Rekey;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.Unlock;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Infra.Configuration;
using PWManager.Infra.Persistence;
using PWManager.Infra.Persistence.Backup;
using PWManager.Infra.Persistence.Repositories;
using PWManager.Infra.Persistence.Schema;
using PWManager.Infra.Security;
using PWManager.Services;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.Composition;

internal static class ServiceRegistration
{
    public static IServiceProvider Build(IConfiguration configuration, Func<Window?> mainWindow)
    {
        var services = new ServiceCollection();
        services.Configure<DatabaseConfig>(options => configuration.GetSection("DatabaseConfig").Bind(options));
        services.AddSingleton<DatabaseConfigurator>();
        services.AddDbContextFactory<PWDbContext>((provider, options) =>
        {
            var databaseConfigurator = provider.GetRequiredService<DatabaseConfigurator>();
            options.UseSqlite(databaseConfigurator.GetConnectionString());
        });

        services.AddSingleton<IVaultBootstrapper, VaultBootstrapper>();
        services.AddSingleton<IColorThemeStore>(provider => new ColorThemeStore(
            Path.Combine(
                provider.GetRequiredService<DatabaseConfigurator>().GetApplicationFolder(),
                ColorThemeStore.ThemesFolderName)));
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton(AppInfo.FromAssembly(typeof(AppInfo).Assembly));
        services.AddSingleton<IVaultSession, VaultSession>();
        services.AddSingleton<IVaultCipher, VaultCipherService>();
        services.AddSingleton<ILegacyVaultCipher, LegacyVaultCipherService>();
        services.AddSingleton<IAppSettingsStore>(provider =>
            new AppSettingsStore(provider.GetRequiredService<DatabaseConfigurator>().GetApplicationFolder()));
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IVaultBackupService>(provider =>
            new VaultBackupService(provider.GetRequiredService<DatabaseConfigurator>().GetDatabaseFilePath()));
        services.AddSingleton<IVaultMaintenanceService, VaultMaintenanceService>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IVaultMetadataRepository, VaultMetadataRepository>();
        services.AddSingleton<IVaultMigrationRepository, VaultMigrationRepository>();
        services.AddSingleton<IVaultKeyService, VaultKeyService>();
        services.AddSingleton<IVaultSchemaMigrator, SqliteVaultSchemaMigrator>();
        services.AddSingleton<IVaultMigrationService, VaultMigrationService>();
        services.AddSingleton<IUserEncryptorService, UserEncryptorService>();
        services.AddTransient<IClipboardService, ClipboardService>();
        services.AddTransient<IVaultRekeyService, VaultRekeyService>();
        services.AddTransient<IUnlockService, UnlockService>();
        services.AddTransient<IVaultEntryService, VaultEntryService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFilePickerService>(_ => new FilePickerService(mainWindow));
        services.AddTransient<ThemeSettingsViewModel>();
        services.AddTransient<SecuritySettingsViewModel>();
        services.AddTransient<UnlockViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddSingleton<Func<MainViewModel>>(provider => provider.GetRequiredService<MainViewModel>);

        return services.BuildServiceProvider();
    }
}
