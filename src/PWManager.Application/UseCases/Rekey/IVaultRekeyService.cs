
namespace PWManager.Application.UseCases.Rekey;

public interface IVaultRekeyService
{
    Task<RekeyResult> ChangeMasterPasswordAsync(string currentPassword, string newPassword);
}
