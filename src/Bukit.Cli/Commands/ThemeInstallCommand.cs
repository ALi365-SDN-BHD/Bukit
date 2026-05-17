namespace Bukit.Cli.Commands;

public static class ThemeInstallCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var source = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(source) || source.StartsWith('-'))
            return Task.FromResult(2);

        return Task.FromResult(2);
    }
}
