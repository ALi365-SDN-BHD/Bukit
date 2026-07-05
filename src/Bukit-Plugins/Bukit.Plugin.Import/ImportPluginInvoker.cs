using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportPluginInvoker
{
    public static async Task<PluginInvokeResponse> InvokeAsync(PluginInvokeRequest request)
    {
        try
        {
            var options = ImportPluginOptionsMapper.Map(request);
            var capture = await ImportPluginConsoleCapture.CaptureAsync(() =>
                Importing.ImportCommandWorkflow.RunAsync(options));

            if (capture.Exception is not null)
                return ImportPluginResponseMapper.FromException(request, capture.Exception);

            return ImportPluginResponseMapper.FromResult(request, capture.Result!, capture);
        }
        catch (ImportPluginOptionsException ex)
        {
            return ImportPluginResponseMapper.FromOptionsException(request, ex);
        }
        catch (Exception ex)
        {
            return ImportPluginResponseMapper.FromException(request, ex);
        }
    }
}
