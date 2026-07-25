using System.Text.Json;
using Bukit.Plugin.IndexNow;

var input = await Console.In.ReadToEndAsync();
var response = await IndexNowPluginApp.HandleAsync(input);
Console.Out.Write(response);

try
{
    using var document = JsonDocument.Parse(response);
    return document.RootElement.TryGetProperty("exitCode", out var exitCode)
        ? exitCode.GetInt32()
        : 0;
}
catch
{
    return 1;
}
