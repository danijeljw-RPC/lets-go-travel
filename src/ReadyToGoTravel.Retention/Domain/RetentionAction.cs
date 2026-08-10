namespace ReadyToGoTravel.Retention.Domain;

/// <summary>End-of-life action a retention policy applies once a record expires and is not held.</summary>
public enum RetentionAction
{
    /// <summary>Destroy the record, or the specific field/blob it names, entirely.</summary>
    Delete,

    /// <summary>Irreversibly remove or mask the personal fields while the remainder is retained as evidence.</summary>
    DeIdentify,

    /// <summary>
    /// The trigger/expiry calculation is implemented and tested, but no live sweep exists yet -
    /// either because no persistence store exists for this class, or because enforcement would
    /// require bypassing an existing immutability control that this slice does not unilaterally
    /// change. See the Slice 7 design document's "Explicitly scoped out" section.
    /// </summary>
    PolicyOnlyNoLiveSweep,
}
