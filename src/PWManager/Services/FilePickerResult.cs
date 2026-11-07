namespace PWManager.Services;

public readonly record struct FilePickerResult(string? Path, bool IsCanceled, string? ErrorMessage)
{
    public bool IsSuccess => Path is not null && ErrorMessage is null;

    public static FilePickerResult Canceled() => new(null, true, null);

    public static FilePickerResult Selected(string path) => new(path, false, null);

    public static FilePickerResult Failed(string message) => new(null, false, message);

    public bool TryGetLocalFile(out string path)
    {
        if (IsSuccess && Path is { Length: > 0 } selected)
        {
            path = selected;
            return true;
        }

        path = string.Empty;
        return false;
    }
}
