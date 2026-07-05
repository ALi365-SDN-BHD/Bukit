using Bukit.Plugin.Import;

string input = await Console.In.ReadToEndAsync();
Console.Error.WriteLine("bukit-plugin-import invoked");
Console.Out.Write(await ImportPluginApp.HandleAsync(input));
return 0;
