namespace Bukit.Importing;

public sealed class ImportCommandOutput
{
    private readonly List<ImportCommandMessage> _messages = [];

    public IReadOnlyList<ImportCommandMessage> Messages => _messages;

    public void Info(string message) => _messages.Add(new ImportCommandMessage("info", message));

    public void Warn(string message) => _messages.Add(new ImportCommandMessage("warning", message));

    public void Error(string message) => _messages.Add(new ImportCommandMessage("error", message));
}
