using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;

namespace PWManager.Infra.Security;

public sealed class UserEncryptorService(IVaultSession session, IVaultCipher cipher) : IUserEncryptorService
{
    public User EncryptUser(User user)
    {
        var key = session.GetKey();

        user.Site = cipher.Encrypt(key, user.Site);
        user.Login = cipher.Encrypt(key, user.Login);
        user.Password = cipher.Encrypt(key, user.Password);
        return user;
    }

    public User DecryptUser(User user)
    {
        var key = session.GetKey();

        user.Site = cipher.Decrypt(key, user.Site);
        user.Login = cipher.Decrypt(key, user.Login);
        user.Password = cipher.Decrypt(key, user.Password);
        return user;
    }
}
