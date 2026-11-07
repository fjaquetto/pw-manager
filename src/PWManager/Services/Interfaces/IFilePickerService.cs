using System.Threading.Tasks;

namespace PWManager.Services.Interfaces;

public interface IFilePickerService
{
    Task<FilePickerResult> PickSaveFileAsync(string title, string suggestedFileName);

    Task<FilePickerResult> PickOpenFileAsync(string title);
}
