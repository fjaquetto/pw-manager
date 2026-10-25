using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace PWManager.Infra.Configuration;

public class DatabaseConfigurator(IOptions<DatabaseConfig> databaseConfigOptions)
{
    public string GetDatabaseFilePath()
    {
        return Path.Combine(GetApplicationFolder(), databaseConfigOptions.Value.DbFile);
    }

    public string GetConnectionString()
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = GetDatabaseFilePath()
        }.ToString();
    }

    public string GetApplicationFolder()
    {
        // Without SpecialFolderOption.Create an absent data directory resolves
        // to an empty string, which would silently turn the application folder
        // into a path relative to the working directory.
        var dataFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        return GetApplicationFolder(dataFolder);
    }

    public string GetApplicationFolder(string dataFolder)
    {
        var applicationFolder = Path.Combine(dataFolder, databaseConfigOptions.Value.AppFolder);
        Directory.CreateDirectory(applicationFolder);
        return applicationFolder;
    }
}
