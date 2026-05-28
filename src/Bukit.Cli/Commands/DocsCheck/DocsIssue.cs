namespace Bukit.Cli.Commands.DocsCheck;

public enum Severity
{
    Error,
    Warn
}

public enum CheckType
{
    Cli,
    ConfigFields,
    FileRefs,
    Examples,
    Skills
}

public sealed record DocsIssue(
    string FilePath,
    int Line,
    Severity Severity,
    CheckType CheckType,
    string Message);
