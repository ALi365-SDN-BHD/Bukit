using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class PublishCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        return SeoCommand.RunAsync(command, "Publish", "publish");
    }
}
