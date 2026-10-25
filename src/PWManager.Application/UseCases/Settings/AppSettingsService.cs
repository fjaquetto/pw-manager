
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Settings;

namespace PWManager.Application.UseCases.Settings;

public sealed class AppSettingsService(IAppSettingsStore store) : IAppSettingsService
{
    public AppSettings Current { get; private set; } = store.Load();

    public void Update(AppSettings settings)
    {
        Current = settings.Normalized();
        store.Save(Current);
    }
}
