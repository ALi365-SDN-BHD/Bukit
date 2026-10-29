using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Engine.Tests.Infrastructure;
using Xunit;

namespace Bukit.Cli.Tests;

internal static class ReportContractAssertions
{
    public static string Write(Action<string> write, string fileName)
    {
        var output = Path.Combine(Path.GetTempPath(), $"bukit-cli-report-{Guid.NewGuid():N}");
        try
        {
            write(output);
            return File.ReadAllText(Path.Combine(output, ".bukit", fileName));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    public static void Schema(string json, string schemaFile, string invalidField)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bukit-core.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Unable to locate the repository root.");

        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "docs", "schemas", schemaFile)));
        var report = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(schema.RootElement.GetProperty("$id").GetString(), report["schema"]!.GetValue<string>());
        LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(report), schema.RootElement);

        report[invalidField] = "wrong type";
        var error = Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(report), schema.RootElement));
        Assert.Contains(invalidField, error.Message);
    }
}
