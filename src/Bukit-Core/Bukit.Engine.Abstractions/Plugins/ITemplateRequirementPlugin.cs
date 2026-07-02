namespace Bukit.Engine.Abstractions.Plugins;

public interface ITemplateRequirementPlugin
{
    IReadOnlyList<string> GetTemplateRequirementKinds(BuildContext context);
}
