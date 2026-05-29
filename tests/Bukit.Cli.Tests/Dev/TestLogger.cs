using Bukit.Shared;

namespace Bukit.Cli.Tests.Dev;

internal sealed class TestLogger : ILogger
{
    public List<string> Infos { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warns { get; } = new();
    public List<string> Debugs { get; } = new();

    public void Info(string message) => Infos.Add(message);
    public void Error(string message) => Errors.Add(message);
    public void Warn(string message) => Warns.Add(message);
    public void Debug(string message) => Debugs.Add(message);
}
