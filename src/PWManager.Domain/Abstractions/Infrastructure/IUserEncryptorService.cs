using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IUserEncryptorService
{
    User EncryptUser(User user);

    User DecryptUser(User user);
}
