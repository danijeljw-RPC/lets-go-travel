using System.Collections.ObjectModel;

namespace ReadyToGoTravel.Retention.Readiness;

/// <summary>
/// Code-verifiable projection of docs/decisions/review-register.md as of Slice 7. This does not
/// replace the review register as the authoritative source, and it must never mark a gate
/// ExternallyVerified without a real recorded approval - the whole point of this type is that
/// "ready" can only ever report reality (see PLAN-0002 Slice 7's explicit requirement that
/// implementation completeness never be presented as production readiness).
/// </summary>
public static class ReadinessCatalog
{
    public static readonly ReadOnlyCollection<ReadinessGate> Gates = new List<ReadinessGate>
    {
        new(
            "OI-0002 LiteAPI commercial terms and merchant-of-record",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Executed merchant, settlement, descriptor, refund, dispute, chargeback, tax and booking-failure allocation.",
            "Finance, Legal/Compliance",
            ActivationState.Disabled,
            Blocking: true,
            "Provider-neutral payment route implemented behind a disabled production capability; readytogo.travel is not modelled as merchant of record. See docs/evidence/liteapi/OI-0002-commercial-mor-review.md."),
        new(
            "OI-0003 Australian carrier production entitlement",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Production entitlement plus dated verify, prebook, book, ticket, retrieve and servicing evidence per enabled carrier.",
            "Product, Supplier Integration",
            ActivationState.Disabled,
            Blocking: true,
            "Carrier-neutral flight search/booking and capability registry implemented; Qantas, Jetstar and Virgin Australia remain observed sandbox carriers only."),
        new(
            "OI-0005 LiteAPI webhook production reliance",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Account subscriptions, secrets, retry settings and controlled production delivery/duplicate/missing-event tests.",
            "Architecture, Security",
            ActivationState.Disabled,
            Blocking: true,
            "Authenticated at-least-once ingress, durable inbox, event_id deduplication and scheduled fallback implemented; webhook ingress defaults disabled."),
        new(
            "OI-0006 Payment SDK / AOC / PCI scope",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Current AOC, responsibility matrix, qualified scope, 3-D Secure and production-domain/payment-method evidence.",
            "Security, Compliance, Finance",
            ActivationState.Disabled,
            Blocking: true,
            "Hosted/SDK payment component, server-side checkout state and idempotent recovery implemented; native payment remains deferred."),
        new(
            "OI-0011 Retention: LiteAPI DPA and Australian legal/privacy approval",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Executed LiteAPI/DPA review, Australian Privacy and Legal/Compliance approval of the record-class schedule, and tested enforcement, per docs/security/data-retention-and-legal-hold.md and closed OI-0011.",
            "Data, Privacy, Legal/Compliance",
            ActivationState.Disabled,
            Blocking: true,
            "Slice 7 implements the approved retention schedule, legal-hold suppression and deletion receipts for every record class with a real persistence store; canonical booking evidence, raw supplier payloads and traveller-field minimisation remain policy-only (see Slice 7 design doc's 'Explicitly scoped out' section) because no live data exists for them yet or because enforcement would require bypassing an existing immutability control."),
        new(
            "Australian market legal and compliance checklist",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Operating entity, executed supplier/customer terms, funds flow, and the formal approval record in docs/australian-market-legal-pack/09-legal-approval-checklist.md.",
            "Legal/Compliance, Product",
            ActivationState.Disabled,
            Blocking: true,
            "Minimum-total pricing, confirmation-state accuracy, APP baseline, intermediary disclosures, no insurance/wallet, adult purchaser and breach-response hooks implemented; launch checklist approvals remain pending."),
        new(
            "Object storage and malware-scanning production activation",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Provisioned Australian-region S3-compatible bucket and credentials, a production ClamAV deployment/monitoring path, and exercised quarantine/failure-mode tests.",
            "Security, Operations",
            ActivationState.Disabled,
            Blocking: true,
            "S3-compatible private attachment storage and fail-closed ClamAV scanning implemented behind disabled-by-default flags (Slice 6); attachments remain quarantined with no registered backend."),
        new(
            "Retention sweep production activation",
            ImplementationStatus.Complete,
            VerificationStatus.LocallyVerified,
            "Retention:Enabled flipped on in a verified production configuration, plus a completed production retention/legal-hold drill.",
            "Data, Security, Operations",
            ActivationState.Disabled,
            Blocking: true,
            "Every concretely-scoped Slice 7 sweep (webhook payload bodies, notification content, abandoned checkout state, support tickets/attachments/audit records, account-closure minimisation) is implemented, legal-hold-aware and locally verified against PostgreSQL (see the Slice 7 outcome report's drill results); Retention:Enabled defaults false in every checked-in configuration until production evidence is recorded, matching the exact precedent set by Support:Storage/Support:Scanning in Slice 6."),
        new(
            "Legal-hold-officer role provisioning",
            ImplementationStatus.Complete,
            VerificationStatus.Unverified,
            "Named legal/compliance staff assigned the legal-hold-officer Keycloak realm role.",
            "Legal/Compliance, Security",
            ActivationState.Disabled,
            Blocking: false,
            "The role, authorization policy and staff-only API are implemented and tested; named staffing is operational evidence, not a design question, matching the existing precedent for support-agent assignment and supplier escalation contacts."),
        new(
            "PostgreSQL / Keycloak / object-storage backup and restore evidence",
            ImplementationStatus.NotStarted,
            VerificationStatus.Unverified,
            "Restore exercises against the stated RPO/RTO objectives in docs/operations/mvp-operational-policy.md, including confirmation that retention tombstones are reapplied after a restore.",
            "Operations, Security",
            ActivationState.Disabled,
            Blocking: true,
            "Backup rotation and restore execution are managed PostgreSQL/object-storage infrastructure controls outside this repository's code; application code cannot self-certify them. Slice 7's live-recomputation sweep design (see design doc) makes tombstone reapplication automatic once a restored environment's worker resumes running, but this has not been exercised against a real restored environment."),
        new(
            "Diagnostic application log retention",
            ImplementationStatus.NotStarted,
            VerificationStatus.Unverified,
            "Confirmation that the log platform enforces a 90-day lifecycle and that logs never contain complete supplier payloads or sensitive traveller/payment data.",
            "Security, Operations",
            ActivationState.Disabled,
            Blocking: true,
            "Log retention is a log-platform (Azure Monitor) configuration concern, not application code; this repository cannot self-certify it."),
    }.AsReadOnly();

    /// <summary>
    /// Fail-closed by construction: true only when every Blocking gate has reached
    /// ExternallyVerified. A gate that is merely LocallyVerified or has Complete implementation
    /// can never satisfy this on its own.
    /// </summary>
    public static bool Ready => Gates.All(gate => gate.Satisfied);

    public static IReadOnlyList<ReadinessGate> BlockingGatesOutstanding =>
        [.. Gates.Where(gate => gate.Blocking && !gate.Satisfied)];
}
