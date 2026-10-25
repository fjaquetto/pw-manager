using Moq;
using PWManager.Application.UseCases.Unlock;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class UnlockViewModelTests
{
    [Fact]
    public void IsPreparing_DefaultsToTrue_AndBlocksTheCommand()
    {
        var viewModel = new UnlockViewModel(Mock.Of<IUnlockService>(), TestAppInfo.Create());

        Assert.True(viewModel.IsPreparing);
        Assert.False(viewModel.UnlockCommand.CanExecute(null));
    }

    [Fact]
    public void Configure_CompletesPreparationAndEnablesTheCommand()
    {
        var viewModel = new UnlockViewModel(Mock.Of<IUnlockService>(), TestAppInfo.Create());

        viewModel.Configure(VaultStatus.Existing);

        Assert.False(viewModel.IsPreparing);
        Assert.True(viewModel.UnlockCommand.CanExecute(null));
    }

    [Fact]
    public async Task Unlock_WhilePreparing_DoesNotCallTheService()
    {
        var service = new Mock<IUnlockService>();
        var viewModel = new UnlockViewModel(service.Object, TestAppInfo.Create()) { Password = "master" };

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        service.Verify(s => s.UnlockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Configure_SwitchesBetweenUnlockAndCreateModes()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsCreating);
        Assert.Equal("Unlock your vault", viewModel.Heading);
        Assert.Equal("Unlock vault", viewModel.ButtonLabel);

        viewModel.Configure(VaultStatus.New);

        Assert.True(viewModel.IsCreating);
        Assert.Equal("Create your vault", viewModel.Heading);
        Assert.Equal("Create vault", viewModel.ButtonLabel);
        Assert.Contains("no recovery", viewModel.Description);
    }

    [Theory]
    [InlineData("short", "short", "at least")]
    [InlineData("long-enough", "other-password", "do not match")]
    public async Task CreateMode_ValidatesLengthAndConfirmation(string password, string confirmation, string expectedMessage)
    {
        var service = new Mock<IUnlockService>();
        var viewModel = CreateViewModel(service.Object);
        viewModel.Configure(VaultStatus.New);
        viewModel.Password = password;
        viewModel.ConfirmPassword = confirmation;

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, viewModel.ErrorMessage);
        service.Verify(s => s.UnlockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateMode_CreatesTheVaultWithAMatchingConfirmation()
    {
        var service = new Mock<IUnlockService>();
        service.Setup(s => s.UnlockAsync("long-enough")).ReturnsAsync(UnlockResult.Success);
        var viewModel = CreateViewModel(service.Object);
        var unlocked = false;
        viewModel.Unlocked += () => unlocked = true;
        viewModel.Configure(VaultStatus.New);
        viewModel.Password = "long-enough";
        viewModel.ConfirmPassword = "long-enough";

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(unlocked);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ConfirmPassword);
    }

    [Fact]
    public async Task CreateMode_Failure_UsesTheCreationMessage()
    {
        var service = new Mock<IUnlockService>();
        service.Setup(s => s.UnlockAsync(It.IsAny<string>())).ReturnsAsync(UnlockResult.Unavailable);
        var viewModel = CreateViewModel(service.Object);
        viewModel.Configure(VaultStatus.New);
        viewModel.Password = "long-enough";
        viewModel.ConfirmPassword = "long-enough";

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.Contains("Unable to create", viewModel.ErrorMessage);
    }

    [Fact]
    public void Reset_ClearsPasswordsVisibilityMessagesAndNotice()
    {
        var viewModel = CreateViewModel();
        viewModel.Password = "secret";
        viewModel.ConfirmPassword = "secret";
        viewModel.IsPasswordVisible = true;
        viewModel.ErrorMessage = "error";
        viewModel.Notice = "notice";

        viewModel.Reset();

        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ConfirmPassword);
        Assert.False(viewModel.IsPasswordVisible);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasNotice);
    }

    private static UnlockViewModel CreateViewModel(IUnlockService? service = null)
    {
        var viewModel = new UnlockViewModel(service ?? Mock.Of<IUnlockService>(), TestAppInfo.Create());
        viewModel.Configure(VaultStatus.Existing);

        return viewModel;
    }
}
