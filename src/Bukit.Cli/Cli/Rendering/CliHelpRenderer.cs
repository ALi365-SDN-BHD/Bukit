using System.Text;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Rendering;

public static class CliHelpRenderer
{
    public static string Render(CliCommandSpec spec, string commandPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine(commandPath);
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.Append("  ").Append(commandPath);

        foreach (var arg in spec.Arguments ?? Array.Empty<CliArgumentSpec>())
        {
            builder.Append(arg.Required ? $" <{arg.Name}>" : $" [{arg.Name}]");
        }

        if ((spec.Options?.Count ?? 0) > 0)
        {
            builder.Append(" [options]");
        }

        builder.AppendLine();

        if ((spec.Arguments?.Count ?? 0) > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Arguments:");
            foreach (var arg in spec.Arguments!)
            {
                builder.Append("  <").Append(arg.Name).Append(">  ").AppendLine(arg.Description);
            }
        }

        if ((spec.Options?.Count ?? 0) > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Options:");
            foreach (var option in spec.Options!)
            {
                var suffix = option.Type == CliOptionType.Flag ? string.Empty : $" <{option.ValueName ?? "value"}>";
                builder.Append("  ").Append(option.Name).Append(suffix).Append("  ").AppendLine(option.Description);
            }
        }

        return builder.ToString();
    }
}
