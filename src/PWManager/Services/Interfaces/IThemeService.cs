using PWManager.Domain.Appearance;

namespace PWManager.Services.Interfaces;

public interface IThemeService
{
    void Apply(ColorScheme scheme);
}
