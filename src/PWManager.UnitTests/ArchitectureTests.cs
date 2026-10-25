using PWManager.Application.UseCases.Unlock;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence.Backup;

namespace PWManager.UnitTests;

public class ArchitectureTests
{
    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        var references = typeof(UnlockService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("PWManager.Infra", references);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApplication()
    {
        var references = typeof(VaultBackupService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("PWManager.Application", references);
    }

    [Fact]
    public void Domain_DoesNotReferenceApplicationOrInfrastructure()
    {
        var references = typeof(User).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("PWManager.Application", references);
        Assert.DoesNotContain("PWManager.Infra", references);
    }
}
