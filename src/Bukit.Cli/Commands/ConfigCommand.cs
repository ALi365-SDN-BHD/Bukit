using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class ConfigCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(sub))
        {
            PrintUsage();
            return Task.FromResult(2);
        }

        return sub switch
        {
            "check" => Task.FromResult(Check(reader)),
            "schema" => Task.FromResult(Schema(reader)),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static int Check(ArgReader reader)
    {
        try
        {
            var resolved = ConfigPathResolver.Resolve(reader);
            var config = ConfigLoader.Load(resolved.FullConfigPath);

            var siteUrl = reader.GetOption("--site-url");
            if (!string.IsNullOrWhiteSpace(siteUrl))
            {
                config = config with { Site = config.Site with { Url = siteUrl } };
            }

            ConfigValidator.Validate(config);

            Console.WriteLine("✔ Config check passed");
            Console.WriteLine($"  config: {resolved.FullConfigPath}");
            Console.WriteLine($"  root:   {resolved.RootDir}");
            Console.WriteLine($"  site:   {config.Site.Name}");
            Console.WriteLine($"  title:  {config.Site.Title}");
            if (!string.IsNullOrWhiteSpace(config.Site.Url))
            {
                Console.WriteLine($"  siteUrl={config.Site.Url}");
            }

            return 0;
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Config error");
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.WriteLine("✖ Config error");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Schema(ArgReader reader)
    {
        try
        {
            var json = ConfigJsonSchemaGenerator.Generate();
            var output = reader.GetOption("--output");
            if (string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine(json);
                return 0;
            }

            var fullPath = Path.GetFullPath(output);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, json);
            Console.WriteLine($"Config schema written: {fullPath}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.WriteLine("✖ Config schema error");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown config command: {sub}");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  bukit config check [--config <path>] [--site <name>] [--site-url <url>]");
        Console.WriteLine("  bukit config schema [--output <path>]");
    }
}
