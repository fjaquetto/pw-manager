namespace PWManager.Infra.Configuration;

// Moves the data out of Roaming AppData, which would replicate the vault
// through roaming profiles and split its WAL side files.
public static class DataFolderMigration
{
    public static void MoveIfNeeded(string roamingRoot, string localRoot, string appFolderName)
    {
        var source = Path.Combine(roamingRoot, appFolderName);
        var target = Path.Combine(localRoot, appFolderName);

        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(file));

            if (!File.Exists(destination))
            {
                File.Move(file, destination);
            }
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(directory));

            if (!Directory.Exists(destination))
            {
                Directory.Move(directory, destination);
            }
        }
    }
}
