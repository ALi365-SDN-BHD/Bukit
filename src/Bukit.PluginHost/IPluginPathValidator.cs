namespace Bukit.PluginHost;

public interface IPluginPathValidator
{
    PluginPathValidationResult ValidatePluginSource(string projectRoot, string source);

    PluginPathValidationResult ValidatePluginEntry(string projectRoot, string pluginRoot, string entry);
}
