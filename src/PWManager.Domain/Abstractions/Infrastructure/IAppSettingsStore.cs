using PWManager.Domain.Settings;

namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
