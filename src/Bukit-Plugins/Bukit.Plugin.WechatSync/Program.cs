using System.Text.Json;
using Bukit.Plugin.WechatSync;

string input = await Console.In.ReadToEndAsync();
Console.Error.WriteLine("bukit-plugin-wechat-sync invoked");
var response = await WechatSyncPluginApp.HandleAsync(input);
Console.Out.Write(response);

var exitCode = 0;
try
{
    using var doc = JsonDocument.Parse(response);
    if (doc.RootElement.TryGetProperty("exitCode", out var ec) && ec.ValueKind == JsonValueKind.Number)
    {
        exitCode = ec.GetInt32();
    }
}
catch
{
    // If we cannot parse the response, default to the already-set exit code
}

return exitCode;
