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
    var innerJson = JsonSerializer.Serialize(new
    {
        openAi,
        github,
        pluginName,
        pluginHook,
        projectRoot,
        outputDir
    });
    Console.Out.Write(JsonSerializer.Serialize(new
    {
        ok = true,
        outputs = new[]
        {
            new
            {
                path = "plugin-output.json",
                contentType = "application/json",
                text = innerJson
            }
        }
    }));
    return;
}

if (mode == "env-allowlist")
{
    var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    var home = Environment.GetEnvironmentVariable("HOME") 
        ?? Environment.GetEnvironmentVariable("USERPROFILE") 
        ?? string.Empty;
    var notion = Environment.GetEnvironmentVariable("NOTION_TOKEN") ?? string.Empty;
    var openAi = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    var pluginName = Environment.GetEnvironmentVariable("BUKIT_PLUGIN_NAME") ?? string.Empty;
    var pluginHook = Environment.GetEnvironmentVariable("BUKIT_PLUGIN_HOOK") ?? string.Empty;
    var customEnv = Environment.GetEnvironmentVariable("BUKIT_TEST_ALLOW_ENV") ?? string.Empty;
    var escapedCustom = customEnv.Replace("\\", "\\\\");
    Console.Out.Write($$"""{"ok":true,"outputs":[{"path":"env-report.json","contentType":"application/json","text":"{\"pathNotEmpty\":{{(path.Length > 0).ToString().ToLowerInvariant()}},\"homeNotEmpty\":{{(home.Length > 0).ToString().ToLowerInvariant()}},\"notionNotEmpty\":{{(notion.Length > 0).ToString().ToLowerInvariant()}},\"openAiNotEmpty\":{{(openAi.Length > 0).ToString().ToLowerInvariant()}},\"pluginName\":\"{{pluginName}}\",\"pluginHook\":\"{{pluginHook}}\",\"customEnv\":\"{{escapedCustom}}\"}"}]}""");
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
    Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"derived-1","title":"Derived 1","slug":"derived-1","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Derived</p>","meta":{"type":"page"},"url":"/derived/derived-1/","outputPath":"derived/derived-1/index.html","template":"pages/page.html"}]}""");
    return;
}

if (mode == "derive-conflict")
{
    Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"derived-conflict","title":"Derived Conflict","slug":"derived-conflict","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Derived Conflict</p>","meta":{"type":"page"},"url":"/blog/post-1/","outputPath":"blog/post-1/index.html","template":"pages/page.html"}]}""");
    return;
}

if (mode == "derive-lastwins")
{
    Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"derived-conflict","title":"Derived Conflict","slug":"derived-conflict","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Derived Conflict</p>","meta":{"type":"page"},"url":"/derived/conflict/","outputPath":"derived/conflict/index.html","template":"pages/page.html"}]}""");
    return;
}

if (mode == "derive-plugin-a")
{
    Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"plugin-a","title":"Plugin A Page","slug":"plugin-a","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Plugin A</p>","meta":{"type":"page"},"url":"/plugin-conflict/page/","outputPath":"plugin-conflict/page/index.html","template":"pages/page.html"}]}""");
    return;
}

if (mode == "derive-plugin-b")
{
    Console.Out.Write("""{"ok":true,"derivedPages":[{"id":"plugin-b","title":"Plugin B Page","slug":"plugin-b","publishAt":"2026-01-01T00:00:00+00:00","contentHtml":"<p>Plugin B</p>","meta":{"type":"page"},"url":"/plugin-conflict/page/","outputPath":"plugin-conflict/page/index.html","template":"pages/page.html"}]}""");
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
    catch (JsonException ex)
    {
        Console.Error.Write($"[plugin] Failed to parse stdin JSON for property '{propertyName}': {ex.Message}");
    }

    return null;
}

#pragma warning disable CS8321
static void Fail(string message)
{
    Console.Out.Write($$"""{"ok":false,"error":{"code":"PLUGIN_ERROR","message":"{{message}}"},"logs":[{"level":"error","message":"{{message}}"}]}""");
    Environment.Exit(1);
}
#pragma warning restore CS8321

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
