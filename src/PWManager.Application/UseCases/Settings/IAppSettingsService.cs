using PWManager.Domain.Settings;

namespace PWManager.Application.UseCases.Settings;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    void Update(AppSettings settings);
}
