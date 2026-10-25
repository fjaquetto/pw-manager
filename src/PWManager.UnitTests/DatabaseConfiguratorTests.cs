using Microsoft.Extensions.Options;
using PWManager.Infra.Configuration;

namespace PWManager.UnitTests;

[CollectionDefinition(PlatformDataFolderCollection.Name, DisableParallelization = true)]
public sealed class PlatformDataFolderCollection
{
    public const string Name = "platform-data-folder";
}

[Collection(PlatformDataFolderCollection.Name)]
public class DatabaseConfiguratorTests
{
    [Fact]
    public void GetApplicationFolder_CreatesAMissingXdgDataDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // XDG_DATA_HOME is only honored on Linux; it is the source of the
        // local application data folder this test has to control.
        var dataHome = CreateTemporaryRoot();
        var previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);

        try
        {
            var folder = CreateConfigurator(new DatabaseConfig()).GetApplicationFolder();

            Assert.True(Path.IsPathRooted(folder));
            Assert.True(Directory.Exists(folder));
            Assert.Equal(dataHome, Directory.GetParent(folder)!.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
            DeleteDirectory(dataHome);
        }
    }

    [Fact]
    public void GetDatabaseFilePath_IsAbsoluteAndUsesTheConfiguredFileName()
    {
        var path = CreateConfigurator(new DatabaseConfig()).GetDatabaseFilePath();

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("pwmanager.db", Path.GetFileName(path));
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void GetApplicationFolder_CreatesTheDefaultFolderUnderTheGivenRoot()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var folder = CreateConfigurator(new DatabaseConfig()).GetApplicationFolder(root);

            Assert.Equal(Path.Combine(root, "PWManager"), folder);
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void GetApplicationFolder_CreatesTheConfiguredFolderUnderTheGivenRoot()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var folder = CreateConfigurator(new DatabaseConfig { AppFolder = "CustomApp" }).GetApplicationFolder(root);

            Assert.Equal(Path.Combine(root, "CustomApp"), folder);
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DataFolderMigration_MovesTheVaultAndThemesFromRoamingToLocal()
    {
        var roaming = Path.Combine(Path.GetTempPath(), "pwmanager-roaming-" + Guid.NewGuid().ToString("N"));
        var local = Path.Combine(Path.GetTempPath(), "pwmanager-local-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(roaming, "PWManager");
        Directory.CreateDirectory(Path.Combine(source, "themes"));
        File.WriteAllText(Path.Combine(source, "pwmanager.db"), "db");
        File.WriteAllText(Path.Combine(source, "pwmanager.db-wal"), "wal");
        File.WriteAllText(Path.Combine(source, "themes", "ocean.json"), "{}");

        try
        {
            DataFolderMigration.MoveIfNeeded(roaming, local, "PWManager");

            var target = Path.Combine(local, "PWManager");

            Assert.True(File.Exists(Path.Combine(target, "pwmanager.db")));
            Assert.True(File.Exists(Path.Combine(target, "pwmanager.db-wal")));
            Assert.True(File.Exists(Path.Combine(target, "themes", "ocean.json")));
            Assert.False(File.Exists(Path.Combine(source, "pwmanager.db")));

            // Running again is a no-op and never overwrites the local vault.
            File.WriteAllText(Path.Combine(target, "pwmanager.db"), "local-db");
            DataFolderMigration.MoveIfNeeded(roaming, local, "PWManager");

            Assert.Equal("local-db", File.ReadAllText(Path.Combine(target, "pwmanager.db")));
        }
        finally
        {
            DeleteDirectory(roaming);
            DeleteDirectory(local);
        }
    }

    private static DatabaseConfigurator CreateConfigurator(DatabaseConfig config)
    {
        return new DatabaseConfigurator(Options.Create(config));
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), "pwmanager-configurator-" + Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
