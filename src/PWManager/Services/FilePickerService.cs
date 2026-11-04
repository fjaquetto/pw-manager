using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PWManager.Services.Interfaces;

namespace PWManager.Services;

public sealed class FilePickerService(Func<Window?> windowAccessor) : IFilePickerService
{
    // Safety copies produced by the restore flow use .bak, so the picker must
    // accept them too. Content validation is done by the restore service.
    private static readonly FilePickerFileType BackupFileType = new("PWManager vault or backup")
    {
        Patterns = ["*.db", "*.bak"]
    };

    private static readonly FilePickerFileType AllFiles = new("All files")
    {
        Patterns = ["*"]
    };

    public async Task<FilePickerResult> PickSaveFileAsync(string title, string suggestedFileName)
    {
        var window = windowAccessor();

        if (window is null)
        {
            return FilePickerResult.Failed("The application window is not available. Please try again.");
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "db",
            FileTypeChoices = [BackupFileType]
        });

        if (file is null)
        {
            return FilePickerResult.Canceled();
        }

        return ToResult(file);
    }

    public async Task<FilePickerResult> PickOpenFileAsync(string title)
    {
        var window = windowAccessor();

        if (window is null)
        {
            return FilePickerResult.Failed("The application window is not available. Please try again.");
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [BackupFileType, AllFiles]
        });

        if (files.Count == 0)
        {
            return FilePickerResult.Canceled();
        }

        return ToResult(files[0]);
    }

    private static FilePickerResult ToResult(IStorageFile file)
    {
        var path = file.TryGetLocalPath();

        return path is null
            ? FilePickerResult.Failed("This file location is not accessible. Copy the backup to a local folder and try again.")
            : FilePickerResult.Selected(path);
    }
}
