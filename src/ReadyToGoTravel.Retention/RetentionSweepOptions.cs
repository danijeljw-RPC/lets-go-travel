namespace ReadyToGoTravel.Retention;

/// <summary>
/// Shared activation flag for every module's retention sweep processor, bound from the
/// "Retention" configuration section. Defaults to disabled in every checked-in configuration
/// (Production and Development), matching the exact precedent set by
/// Support:Storage:Enabled/Support:Scanning:ClamAv:Enabled in Slice 6: implemented and fully
/// tested, but not silently active. Legal-hold administration is not gated by this flag - it is
/// protective, not destructive, and separately authorization-gated.
/// </summary>
public sealed class RetentionSweepOptions
{
    public const string SectionName = "Retention";

    public bool Enabled { get; set; }
}
