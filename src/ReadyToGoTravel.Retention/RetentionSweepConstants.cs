namespace ReadyToGoTravel.Retention;

/// <summary>Shared tuning constants every module's retention sweep processor uses.</summary>
public static class RetentionSweepConstants
{
    /// <summary>Bounded batch size per record class per sweep cycle, so one cycle cannot run unbounded.</summary>
    public const int BatchSize = 200;
}
