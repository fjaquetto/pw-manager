using System.Threading.Tasks;
using PWManager.Services.Interfaces;

namespace PWManager.Services;

public readonly record struct BackupFileSelection(
    bool IsCanceled,
    bool IsSelected,
    string Path,
    string FileName,
    string ErrorMessage)
{
    public static BackupFileSelection Canceled() => new(true, false, string.Empty, string.Empty, string.Empty);

    public static BackupFileSelection Failed(string errorMessage) =>
        new(false, false, string.Empty, string.Empty, errorMessage);

    public static BackupFileSelection Selected(string path) =>
        new(false, true, path, System.IO.Path.GetFileName(path), string.Empty);
}

public static class BackupFileSelector
{
    public static async Task<BackupFileSelection> SelectAsync(IFilePickerService filePicker, string title)
    {
        var result = await filePicker.PickOpenFileAsync(title);

        if (result.IsCanceled)
        {
            return BackupFileSelection.Canceled();
        }

        return result.TryGetLocalFile(out var path)
            ? BackupFileSelection.Selected(path)
            : BackupFileSelection.Failed(result.ErrorMessage ?? "Unable to open the selected file.");
    }
}
