using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Shared;
using Bukit.WechatSyncing;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginInvoker
{
    public static async Task<PluginInvokeResponse> InvokeAsync(PluginInvokeRequest request)
    {
        try
        {
            var invocation = WechatSyncPluginOptionsMapper.Map(request);
            var logger = new ConsoleLogger(LogLevel.Info);
            var context = await WechatSyncInputLoader.LoadAsync(
                invocation.RootDir,
                invocation.OutputDir,
                invocation.ManifestPath,
                invocation.Options.SiteName,
                invocation.Options.SiteUrl,
                invocation.Options.BaseUrl,
                invocation.MediaDownloadDir,
                logger);

            if (invocation.DryRun)
            {
                return WechatSyncPluginResponseMapper.FromDryRun(request, context, invocation);
            }

            var result = await new WechatSyncWorkflow().RunAsync(context, invocation.Options);
            return WechatSyncPluginResponseMapper.FromResult(request, result);
        }
        catch (WechatSyncPluginOptionsException ex)
        {
            return WechatSyncPluginResponseMapper.FromOptionsException(request, ex);
        }
        catch (Exception ex)
        {
            return WechatSyncPluginResponseMapper.FromException(request, ex);
        }
    }
}
