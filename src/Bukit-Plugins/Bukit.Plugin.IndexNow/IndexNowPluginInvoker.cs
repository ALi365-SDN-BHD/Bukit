using Bukit.IndexNow;
using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginInvoker
{
    public static async Task<PluginInvokeResponse> InvokeAsync(PluginInvokeRequest request)
    {
        try
        {
            var invocation = IndexNowPluginOptionsMapper.Map(request);
            var workflow = new IndexNowSubmissionWorkflow();
            var result = await workflow.RunAsync(new IndexNowSubmissionRequest(
                invocation.ChangeSetPath,
                invocation.SnapshotPath,
                invocation.SiteUrl,
                invocation.StateDir,
                invocation.OutputRoot,
                invocation.Key,
                invocation.DryRun));
            return IndexNowPluginResponseMapper.FromResult(request, result, invocation.DryRun);
        }
        catch (IndexNowPluginOptionsException exception)
        {
            return IndexNowPluginResponseMapper.FromOptionsException(request, exception);
        }
        catch
        {
            return IndexNowPluginResponseMapper.FromException(request);
        }
    }
}
