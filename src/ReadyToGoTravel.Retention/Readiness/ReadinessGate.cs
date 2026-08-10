namespace ReadyToGoTravel.Retention.Readiness;

public enum ImplementationStatus
{
    NotStarted,
    InProgress,
    Complete,
}

public enum VerificationStatus
{
    Unverified,
    LocallyVerified,
    ExternallyVerified,
}

public enum ActivationState
{
    Disabled,
    Enabled,
}

/// <summary>
/// One row of the production-readiness certification, mirroring one row of
/// docs/decisions/review-register.md (the authoritative source - this type is a code-verifiable
/// projection of it, not a replacement for it). A gate that is Blocking must reach
/// VerificationStatus.ExternallyVerified before it can be treated as satisfied - local
/// implementation completeness alone is never sufficient for a Blocking gate, so
/// "implementation complete" can never masquerade as "production ready".
/// </summary>
public sealed record ReadinessGate(
    string Name,
    ImplementationStatus Implementation,
    VerificationStatus Verification,
    string RequiredEvidence,
    string ApprovalOwner,
    ActivationState Activation,
    bool Blocking,
    string Notes)
{
    /// <summary>True only when this specific gate can no longer block a "ready" result.</summary>
    public bool Satisfied => !Blocking || Verification == VerificationStatus.ExternallyVerified;
}
