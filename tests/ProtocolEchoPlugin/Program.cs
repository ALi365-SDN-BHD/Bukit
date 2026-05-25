using System.Text;
using System.Text.Json;

var mode = args.Length == 0 ? "success" : args[0];
var stdin = Console.In.ReadToEnd();
var hook = ReadStringProperty(stdin, "hook");

if (mode == "sleep")
{
    await Task.Delay(1000);
    return;
}

if (mode == "empty")
{
    return;
}

if (mode == "invalid")
{
    Console.Out.Write("not-json");
    return;
}

if (mode == "no-output")
{
    Console.Out.Write("""{"ok":true,"outputs":[]}""");
    return;
}

if (mode == "traversal")
{
    Console.Out.Write("""{"ok":true,"outputs":[{"path":"../escape.json","contentType":"application/json","text":"{}"}]}""");
    return;
}

if (mode == "env")
{
    var openAi = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    var github = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty;
    var pluginName = Environment.GetEnvironmentVariable("BUKIT_PLUGIN_NAME") ?? string.Empty;
    var pluginHook = Environment.GetEnvironmentVariable("BUKIT_PLUGIN_HOOK") ?? string.Empty;
    var projectRoot = Environment.GetEnvironmentVariable("BUKIT_PROJECT_ROOT") ?? string.Empty;
    var outputDir = Environment.GetEnvironmentVariable("BUKIT_OUTPUT_DIR") ?? string.Empty;
    Console.Out.Write($$"""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"openAi\":\"{{openAi}}\",\"github\":\"{{github}}\",\"pluginName\":\"{{pluginName}}\",\"pluginHook\":\"{{pluginHook}}\",\"projectRoot\":\"{{projectRoot.Replace("\\", "\\\\")}}\",\"outputDir\":\"{{outputDir.Replace("\\", "\\\\")}}\"}"}]}""");
    return;
}

if (mode == "large-stdout")
{
    Console.Out.Write(new string('x', 4096));
    return;
}

if (mode == "large-stderr")
{
    Console.Error.Write(new string('x', 4096));
    return;
}

if (mode == "error")
{
    Console.Out.Write("""{"ok":false,"error":{"code":"PLUGIN_ERROR","message":"plugin failed"}}""");
    return;
}

if (mode == "handshake-v2")
{
    if (string.Equals(hook, "handshake", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("""{"ok":true,"negotiatedSchemaVersion":"2"}""");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"version\":\"2\"}"}]}""");
    return;
}

if (mode == "handshake-v1only")
{
    if (string.Equals(hook, "handshake", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("""{"ok":false,"error":{"code":"UNSUPPORTED_SCHEMA_VERSION","message":"v2 unsupported"}}""");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"ok\":true}"}]}""");
    return;
}

if (mode == "handshake-counter")
{
    var counterPath = args.Length > 1 ? args[1] : null;
    if (string.Equals(hook, "handshake", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.IsNullOrWhiteSpace(counterPath))
        {
            var current = 0;
            if (File.Exists(counterPath) && int.TryParse(File.ReadAllText(counterPath).Trim(), out var parsed))
            {
                current = parsed;
            }

            File.WriteAllText(counterPath, (current + 1).ToString());
        }

        Console.Out.Write("""{"ok":true,"negotiatedSchemaVersion":"2"}""");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"version\":\"2\"}"}]}""");
    return;
}

if (mode == "handshake-routedpages")
{
    if (string.Equals(hook, "handshake", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("""{"ok":true,"negotiatedSchemaVersion":"2"}""");
        return;
    }

    var routedPagesCount = ReadRoutedPagesCount(stdin);
    Console.Out.Write($$"""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"routedPagesCount\":{{routedPagesCount}}}"}]}""");
    return;
}

if (mode == "handshake-invalid")
{
    if (string.Equals(hook, "handshake", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("not-json");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"ok\":true}"}]}""");
    return;
}

if (mode == "derive-success")
{
    if (string.Equals(hook, "derive-pages", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"derived-1","title":"Derived 1","slug":"derived-1","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Derived</p>","meta":{"type":"page"},"url":"/derived/derived-1/","outputPath":"derived/derived-1/index.html","template":"pages/page.html"}]}""");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"ok\":true}"}]}""");
    return;
}

if (mode == "derive-conflict")
{
    if (string.Equals(hook, "derive-pages", StringComparison.OrdinalIgnoreCase))
    {
        Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"derived-conflict","title":"Derived Conflict","slug":"derived-conflict","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Derived Conflict</p>","meta":{"type":"page"},"url":"/blog/post-1/","outputPath":"blog/post-1/index.html","template":"pages/page.html"}]}""");
        return;
    }

    Console.Out.Write("""{"ok":true,"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"ok\":true}"}]}""");
    return;
}

Console.OutputEncoding = Encoding.UTF8;
Console.Out.Write("""{"ok":true,"logs":[{"level":"info","message":"ok"}],"outputs":[{"path":"plugin-output.json","contentType":"application/json","text":"{\"ok\":true}"}]}""");

static string? ReadStringProperty(string json, string propertyName)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return null;
    }

    try
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }
    }
    catch
    {
    }

    return null;
}

static int ReadRoutedPagesCount(string json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return 0;
    }

    try
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("afterBuild", out var afterBuild) || afterBuild.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (!afterBuild.TryGetProperty("routedPages", out var routedPages) || routedPages.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return routedPages.GetArrayLength();
    }
    catch
    {
        return 0;
    }
}
