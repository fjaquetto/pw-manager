using Microsoft.Extensions.Time.Testing;
using Moq;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;

namespace PWManager.UnitTests;

public class VaultEntryServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserEncryptorService> _encryptor = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly VaultEntryService _service;

    public VaultEntryServiceTests()
    {
        _encryptor.Setup(encryptor => encryptor.EncryptUser(It.IsAny<User>()))
            .Returns((User entry) =>
            {
                entry.Site = $"encrypted:{entry.Site}";
                entry.Login = $"encrypted:{entry.Login}";
                entry.Password = $"encrypted:{entry.Password}";

                return entry;
            });

        _encryptor.Setup(encryptor => encryptor.DecryptUser(It.IsAny<User>()))
            .Returns((User entry) =>
            {
                entry.Site = entry.Site["encrypted:".Length..];
                entry.Login = entry.Login["encrypted:".Length..];
                entry.Password = entry.Password["encrypted:".Length..];

                return entry;
            });

        _service = new VaultEntryService(_userRepository.Object, _encryptor.Object, _time);
    }

    [Fact]
    public async Task Load_DecryptsCopiesWithoutChangingStoredObjects()
    {
        var storedEntry = CreateEncryptedEntry();
        _userRepository.Setup(repository => repository.GetAllAsync()).ReturnsAsync([storedEntry]);

        var entries = await _service.LoadAsync();

        var loadedEntry = Assert.Single(entries);

        Assert.NotSame(storedEntry, loadedEntry);
        Assert.Equal("  exact secret  ", loadedEntry.Password);
        Assert.Equal("encrypted:  exact secret  ", storedEntry.Password);
        Assert.Equal(storedEntry.Id, loadedEntry.Id);
        Assert.Equal(storedEntry.CreationDate, loadedEntry.CreationDate);
    }

    [Fact]
    public async Task Create_EncryptsStoredCopyAndReturnsExactDraftWithTimestamps()
    {
        User? persistedEntry = null;
        _userRepository.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .Callback<User>(entry => persistedEntry = entry)
            .Returns(Task.CompletedTask);

        var savedEntry = await _service.SaveAsync(null, "Free-form site", "login", "  exact secret  ");

        Assert.NotNull(savedEntry);
        Assert.NotNull(persistedEntry);
        Assert.NotSame(savedEntry, persistedEntry);
        Assert.Equal(savedEntry.Id, persistedEntry.Id);
        Assert.Equal("Free-form site", savedEntry.Site);
        Assert.Equal("  exact secret  ", savedEntry.Password);
        Assert.Equal("encrypted:  exact secret  ", persistedEntry.Password);
        Assert.Equal(_time.GetLocalNow().DateTime, savedEntry.CreationDate);
        Assert.Equal(savedEntry.CreationDate, savedEntry.LastUpdated);
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_PreservesIdentityAndCreationWithoutMutatingStoredObject()
    {
        var storedEntry = CreateEncryptedEntry();
        _userRepository.Setup(repository => repository.GetUserByIdAsync(storedEntry.Id)).ReturnsAsync(storedEntry);
        _userRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        var savedEntry = await _service.SaveAsync(storedEntry.Id, "Updated site", "new login", "new password");

        Assert.NotNull(savedEntry);
        Assert.Equal(storedEntry.Id, savedEntry.Id);
        Assert.Equal(storedEntry.CreationDate, savedEntry.CreationDate);
        Assert.Equal(_time.GetLocalNow().DateTime, savedEntry.LastUpdated);
        Assert.Equal("new password", savedEntry.Password);
        Assert.Equal("encrypted:  exact secret  ", storedEntry.Password);
        _userRepository.Verify(repository => repository.UpdateAsync(It.Is<User>(entry =>
            entry.Id == storedEntry.Id && entry.Password == "encrypted:new password")), Times.Once);
        _userRepository.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_MissingEntryDoesNotInsertOrReportSuccess()
    {
        _userRepository.Setup(repository => repository.GetUserByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var savedEntry = await _service.SaveAsync(Guid.NewGuid(), "Site", "login", "password");

        Assert.Null(savedEntry);
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
        _userRepository.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task FailedUpdate_PropagatesFailureAndPreservesStoredObject()
    {
        var storedEntry = CreateEncryptedEntry();
        _userRepository.Setup(repository => repository.GetUserByIdAsync(storedEntry.Id)).ReturnsAsync(storedEntry);
        _userRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new IOException("Persistence is unavailable."));

        await Assert.ThrowsAsync<IOException>(() =>
            _service.SaveAsync(storedEntry.Id, "Updated", "updated login", "updated password"));

        Assert.Equal("encrypted:Site", storedEntry.Site);
        Assert.Equal("encrypted:  exact secret  ", storedEntry.Password);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ReportsWhetherTheEntryExisted(bool exists)
    {
        var storedEntry = CreateEncryptedEntry();
        _userRepository.Setup(repository => repository.GetUserByIdAsync(storedEntry.Id))
            .ReturnsAsync(exists ? storedEntry : null);
        _userRepository.Setup(repository => repository.DeleteAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        var deleted = await _service.DeleteAsync(storedEntry.Id);

        Assert.Equal(exists, deleted);
        _userRepository.Verify(repository => repository.DeleteAsync(storedEntry), exists ? Times.Once : Times.Never);
    }

    private static User CreateEncryptedEntry()
    {
        return new User("encrypted:Site", "encrypted:login", "encrypted:  exact secret  ")
        {
            CreationDate = new DateTime(2025, 2, 18, 9, 30, 0),
            LastUpdated = new DateTime(2025, 8, 28, 14, 15, 0)
        };
    }
}
