using System.Security.Cryptography;
using System.Text;

namespace PWManager.Application.Common;

public static class PasswordGenerator
{
    private const string UppercaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseCharacters = "abcdefghijklmnopqrstuvwxyz";
    private const string NumericCharacters = "0123456789";
    private const string SymbolCharacters = "!@#$%^&*()-+=._:;,";

    public static string Generate(int length, bool uppercase, bool lowercase, bool numbers, bool symbols)
    {
        var characterSet = BuildCharacterSet(uppercase, lowercase, numbers, symbols);
        var password = new char[length];
        var randomBytes = new byte[sizeof(uint)];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        for (var index = 0; index < length; index++)
        {
            randomNumberGenerator.GetBytes(randomBytes);
            var characterIndex = BitConverter.ToUInt32(randomBytes, 0) % (uint)characterSet.Length;
            password[index] = characterSet[(int)characterIndex];
        }

        return new string(password);
    }

    public static int EstimateStrengthScore(int length, bool uppercase, bool lowercase, bool numbers, bool symbols)
    {
        var enabledCategories = new[] { uppercase, lowercase, numbers, symbols }.Count(enabled => enabled);
        var lengthScore = (length >= 16 ? 1 : 0) + (length >= 24 ? 1 : 0);

        return enabledCategories + lengthScore;
    }

    private static string BuildCharacterSet(bool uppercase, bool lowercase, bool numbers, bool symbols)
    {
        var characters = new StringBuilder();

        if (uppercase)
            characters.Append(UppercaseCharacters);

        if (lowercase)
            characters.Append(LowercaseCharacters);

        if (numbers)
            characters.Append(NumericCharacters);

        if (symbols)
            characters.Append(SymbolCharacters);

        return characters.Length == 0 ? LowercaseCharacters : characters.ToString();
    }
}
