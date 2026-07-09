using Bukit.Plugin.WechatSync;

string input = await Console.In.ReadToEndAsync();
Console.Error.WriteLine("bukit-plugin-wechat-sync invoked");
Console.Out.Write(await WechatSyncPluginApp.HandleAsync(input));
return 0;
