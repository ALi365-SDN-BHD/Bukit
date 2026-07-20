namespace Bukit.PublicApiDrift.FormatterFixture;

public class AccessorBase
{
    private EventHandler? _changed;

    public virtual string Mixed { get; protected set; } = string.Empty;

    public virtual event EventHandler? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}

public sealed class AccessorDerived : AccessorBase
{
    private EventHandler? _changed;

    public sealed override string Mixed { get; protected set; } = string.Empty;

    public sealed override event EventHandler? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}

public enum FixtureEnum
{
    None = 0,
    Ready = 1
}
