namespace Bukit.Shared;

public class BukitException : Exception
{
    public BukitException(string message) : base(message)
    {
    }

    public BukitException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ConfigException : BukitException
{
    public ConfigException(string message) : base(message)
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
}

public sealed class RenderException : BukitException
{
    public RenderException(string message) : base(message)
    {
    }

    public RenderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

