namespace Bukit.Shared;

public class BukitException : Exception
{
    public DiagnosticCode? Code { get; }

    public BukitException(string message) : base(message)
    {
    }

    public BukitException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public BukitException(string message, DiagnosticCode code) : base(message)
    {
        Code = code;
    }

    public BukitException(string message, Exception innerException, DiagnosticCode code) : base(message, innerException)
    {
        Code = code;
    }
}

public sealed class ConfigException : BukitException
{
    public ConfigException(string message) : base(message)
    {
    }

    public ConfigException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConfigException(string message, DiagnosticCode code) : base(message, code)
    {
    }

    public ConfigException(string message, Exception innerException, DiagnosticCode code) : base(message, innerException, code)
    {
    }
}

public sealed class ContentException : BukitException
{
    public ContentException(string message) : base(message)
    {
    }

    public ContentException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ContentException(string message, DiagnosticCode code) : base(message, code)
    {
    }

    public ContentException(string message, Exception innerException, DiagnosticCode code) : base(message, innerException, code)
    {
    }
}

public sealed class RenderException : BukitException
{
    public RenderException(string message) : base(message)
    {
    }

    public RenderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public RenderException(string message, DiagnosticCode code) : base(message, code)
    {
    }

    public RenderException(string message, Exception innerException, DiagnosticCode code) : base(message, innerException, code)
    {
    }
}

public sealed class CommandArgumentException : Exception
{
    public CommandArgumentException(string message) : base(message)
    {
    }

    public CommandArgumentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
