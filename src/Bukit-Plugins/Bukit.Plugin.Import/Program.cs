using Bukit.Plugin.Import;

try
{
    string input = await Console.In.ReadToEndAsync();
    Console.Error.WriteLine("bukit-plugin-import invoked");
    Console.Out.Write(await ImportPluginApp.HandleAsync(input));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}
