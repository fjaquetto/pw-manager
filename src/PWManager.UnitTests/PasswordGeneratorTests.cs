using System.Linq;
using PWManager.Application.Common;
using Xunit;

namespace PWManager.UnitTests;

public class PasswordGeneratorTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(48)]
    public void GeneratePassword_ReturnsCorrectLength(int length)
    {
        var result = PasswordGenerator.Generate(length, true, true, true, true);

        Assert.Equal(length, result.Length);
    }

    [Fact]
    public void GeneratePassword_OnlyUppercase_ContainsOnlyUppercaseLetters()
    {
        var result = PasswordGenerator.Generate(50, uppercase: true, lowercase: false, numbers: false, symbols: false);

        Assert.True(result.All(char.IsUpper), $"Expected only uppercase letters but got: {result}");
    }

    [Fact]
    public void GeneratePassword_OnlyNumbers_ContainsOnlyDigits()
    {
        var result = PasswordGenerator.Generate(50, uppercase: false, lowercase: false, numbers: true, symbols: false);

        Assert.True(result.All(char.IsDigit), $"Expected only digits but got: {result}");
    }

    [Fact]
    public void GeneratePassword_NoOptionsSelected_FallsBackToLowercase()
    {
        var result = PasswordGenerator.Generate(30, uppercase: false, lowercase: false, numbers: false, symbols: false);

        Assert.Equal(30, result.Length);
        Assert.True(result.All(char.IsLower), $"Expected fallback to lowercase but got: {result}");
    }

    [Fact]
    public void GeneratePassword_AllOptions_ContainsMixedCharacters()
    {
        var result = PasswordGenerator.Generate(100, uppercase: true, lowercase: true, numbers: true, symbols: true);

        Assert.Equal(100, result.Length);
        Assert.Contains(result, char.IsUpper);
        Assert.Contains(result, char.IsLower);
        Assert.Contains(result, char.IsDigit);
        Assert.Contains(result, c => !char.IsLetterOrDigit(c));
    }

    [Theory]
    [InlineData(8, false, false, false, false, 0)]
    [InlineData(8, true, false, false, false, 1)]
    [InlineData(15, true, true, true, true, 4)]
    [InlineData(16, true, true, true, true, 5)]
    [InlineData(23, true, true, true, true, 5)]
    [InlineData(24, true, true, true, true, 6)]
    public void EstimatedStrength_PreservesCategoryAndLengthScoring(
        int length, bool uppercase, bool lowercase, bool numbers, bool symbols, int expectedScore)
    {
        var score = PasswordGenerator.EstimateStrengthScore(length, uppercase, lowercase, numbers, symbols);

        Assert.Equal(expectedScore, score);
    }

    [Fact]
    public void GeneratePassword_CalledTwice_ReturnsDifferentPasswords()
    {
        var first = PasswordGenerator.Generate(32, true, true, true, true);
        var second = PasswordGenerator.Generate(32, true, true, true, true);

        Assert.NotEqual(first, second);
    }
}
