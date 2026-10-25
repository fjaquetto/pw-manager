using PWManager.Domain.Appearance;

namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IColorThemeStore
{
    IReadOnlyList<ColorProfile> LoadProfiles();

    void Save(ColorProfile profile);

    bool Delete(string name);

    string? GetActiveProfileName();

    void SetActiveProfile(string? name);

    ColorScheme LoadActiveScheme();
}
