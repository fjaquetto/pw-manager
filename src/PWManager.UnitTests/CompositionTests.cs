using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PWManager.Composition;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class CompositionTests
{
    [Fact]
    public void RestoreAndImportViewModelsAreAvailableFromTheCompositionRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pwmanager-composition-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseConfig:AppFolder"] = folder,
                ["DatabaseConfig:DbFile"] = "vault.db"
            })
            .Build();
        var provider = (ServiceProvider)ServiceRegistration.Build(configuration, () => null);

        try
        {
            var unlock = provider.GetRequiredService<UnlockViewModel>();
            var security = provider.GetRequiredService<SecuritySettingsViewModel>();

            Assert.NotNull(unlock.Restore);
            Assert.NotNull(security.Import);
        }
        finally
        {
            provider.Dispose();

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
