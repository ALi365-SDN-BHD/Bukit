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
#if ENUM_VALUE_V2
    Ready = 2
#else
    Ready = 1
#endif
}

public class ClassConstraint<T> where T : class;

public class NullableClassConstraint<T> where T : class?;

public class StructConstraint<T> where T : struct;

public class UnmanagedConstraint<T> where T : unmanaged;

public class Unconstrained<T>;

public class NotNullConstraint<T> where T : notnull;
