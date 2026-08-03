namespace Api.Tests;

/// <summary>Settable clock for exercising day-rollover behavior deterministically.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset initialUtcNow)
    {
        _utcNow = initialUtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;
}
