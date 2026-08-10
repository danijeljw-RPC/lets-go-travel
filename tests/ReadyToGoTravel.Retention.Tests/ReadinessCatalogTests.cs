using ReadyToGoTravel.Retention.Readiness;

namespace ReadyToGoTravel.Retention.Tests;

public sealed class ReadinessCatalogTests
{
    [Fact]
    public void TheRealCatalogIsNotReadyTodayBecauseExternalProductionGatesRemainOutstanding()
    {
        // This is the load-bearing assertion: every production capability in this repository
        // (LiteAPI commercial terms, carrier entitlement, webhook reliance, PCI scope, Australian
        // legal/privacy approval, object storage, retention activation, backup/restore evidence)
        // is genuinely still gated as of Slice 7. If this ever flips to true without a real
        // recorded approval, that is a false readiness claim, not a passing test.
        Assert.False(ReadinessCatalog.Ready);
        Assert.NotEmpty(ReadinessCatalog.BlockingGatesOutstanding);
    }

    [Fact]
    public void ReadyRequiresNoOutstandingBlockingGate()
    {
        var allPassed = new[]
        {
            new ReadinessGate("A", ImplementationStatus.Complete, VerificationStatus.ExternallyVerified, "evidence", "owner", ActivationState.Enabled, Blocking: true, "notes"),
            new ReadinessGate("B", ImplementationStatus.Complete, VerificationStatus.Unverified, "evidence", "owner", ActivationState.Disabled, Blocking: false, "notes"),
        };

        Assert.True(allPassed.All(gate => gate.Satisfied));
    }

    [Fact]
    public void ASingleOutstandingBlockingGateMakesTheSetNotReady()
    {
        var gates = new[]
        {
            new ReadinessGate("A", ImplementationStatus.Complete, VerificationStatus.ExternallyVerified, "evidence", "owner", ActivationState.Enabled, Blocking: true, "notes"),
            new ReadinessGate("B", ImplementationStatus.Complete, VerificationStatus.LocallyVerified, "evidence", "owner", ActivationState.Disabled, Blocking: true, "notes"),
        };

        Assert.False(gates.All(gate => gate.Satisfied));
    }

    [Fact]
    public void ANonBlockingOutstandingGateDoesNotAffectReadiness()
    {
        var gates = new[]
        {
            new ReadinessGate("A", ImplementationStatus.Complete, VerificationStatus.ExternallyVerified, "evidence", "owner", ActivationState.Enabled, Blocking: true, "notes"),
            new ReadinessGate("B", ImplementationStatus.NotStarted, VerificationStatus.Unverified, "evidence", "owner", ActivationState.Disabled, Blocking: false, "notes"),
        };

        Assert.True(gates.All(gate => gate.Satisfied));
    }

    [Fact]
    public void ImplementationCompleteAloneNeverSatisfiesABlockingGate()
    {
        var gate = new ReadinessGate(
            "A", ImplementationStatus.Complete, VerificationStatus.LocallyVerified, "evidence", "owner", ActivationState.Disabled, Blocking: true, "notes");

        Assert.False(gate.Satisfied);
    }

    [Fact]
    public void EveryGateInTheRealCatalogHasNonEmptyEvidenceAndOwnerFields()
    {
        foreach (var gate in ReadinessCatalog.Gates)
        {
            Assert.False(string.IsNullOrWhiteSpace(gate.Name));
            Assert.False(string.IsNullOrWhiteSpace(gate.RequiredEvidence));
            Assert.False(string.IsNullOrWhiteSpace(gate.ApprovalOwner));
            Assert.False(string.IsNullOrWhiteSpace(gate.Notes));
        }
    }

    [Fact]
    public void NoGateInTheRealCatalogIsMarkedExternallyVerifiedWithoutRecordedEvidence()
    {
        // Guards against ever silently flipping a gate to "done" as a shortcut: every current gate
        // in this repository has genuinely unresolved external evidence.
        Assert.DoesNotContain(ReadinessCatalog.Gates, gate => gate.Verification == VerificationStatus.ExternallyVerified);
    }

    [Fact]
    public void ActivationStateIsDisabledForEveryGateInTheRealCatalog()
    {
        // No production capability accidentally enables itself.
        Assert.All(ReadinessCatalog.Gates, gate => Assert.Equal(ActivationState.Disabled, gate.Activation));
    }
}
