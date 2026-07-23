namespace Bukit.Engine.Output;

internal class OutputPathSecurityException : InvalidOperationException
{
    public OutputPathSecurityException(string message) : base(message) { }
}
