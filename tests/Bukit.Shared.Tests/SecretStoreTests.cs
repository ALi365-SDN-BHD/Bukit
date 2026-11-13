using System.Text;
using Xunit;

namespace Bukit.Shared.Tests;

// All tests touching the process-wide SecretStore live in this class so they run sequentially.
[Collection(SecretStoreCollection.Name)]
public sealed class SecretStoreTests : IDisposable
{
    public void Dispose() => SecretStore.Reset();

    [Fact]
    public void Parse_ValidObject_ReturnsAllSecrets()
    {
        var secrets = SecretStore.Parse("{\"NOTION_TOKEN\":\"fake-notion\",\"GITHUB_TOKEN\":\"fake-gh\"}"u8);

        Assert.Equal(2, secrets.Count);
        Assert.Equal("fake-notion", secrets["NOTION_TOKEN"]);
        Assert.Equal("fake-gh", secrets["GITHUB_TOKEN"]);
    }

    [Fact]
    public void Parse_EmptyObject_IsAllowed()
        => Assert.Empty(SecretStore.Parse("  {}\n"u8));

    [Fact]
    public void Parse_Utf8Bom_IsAccepted()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat("{\"A\":\"b\"}"u8.ToArray()).ToArray();
        Assert.Equal("b", SecretStore.Parse(bytes)["A"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t")]
    public void Parse_Empty_FailsWithoutEcho(string input)
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(Encoding.UTF8.GetBytes(input)));
        Assert.Contains("empty", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"NOTION_TOKEN\":\"secret_leak_value\"")]
    [InlineData("secret_leak_value")]
    [InlineData("{\"NOTION_TOKEN\": secret_leak_value}")]
    [InlineData("{'NOTION_TOKEN':'secret_leak_value'}")]
    public void Parse_BadJson_FailsWithoutEchoingInput(string input)
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(Encoding.UTF8.GetBytes(input)));
        Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_leak_value", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NOTION_TOKEN\"", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"NOTION_TOKEN\":12345}")]
    [InlineData("{\"NOTION_TOKEN\":true}")]
    [InlineData("{\"NOTION_TOKEN\":null}")]
    [InlineData("{\"NOTION_TOKEN\":[\"secret_leak_value\"]}")]
    [InlineData("{\"NOTION_TOKEN\":{\"v\":\"secret_leak_value\"}}")]
    public void Parse_NonStringValue_FailsWithoutEcho(string input)
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(Encoding.UTF8.GetBytes(input)));
        Assert.Contains("must be JSON strings", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_leak_value", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[\"secret_leak_value\"]")]
    [InlineData("\"secret_leak_value\"")]
    [InlineData("42")]
    public void Parse_NonObjectRoot_FailsWithoutEcho(string input)
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(Encoding.UTF8.GetBytes(input)));
        Assert.Contains("must be a JSON object", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_leak_value", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"secret_leak_value=x\":\"v\"}")]
    [InlineData("{\"1BAD\":\"v\"}")]
    [InlineData("{\"\":\"v\"}")]
    public void Parse_InvalidName_FailsWithoutEcho(string input)
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(Encoding.UTF8.GetBytes(input)));
        Assert.Contains("names must be", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_leak_value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EmptyStringValue_Fails()
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse("{\"NOTION_TOKEN\":\"\"}"u8));
        Assert.Contains("must not be empty", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DuplicateName_FailsWithoutEcho()
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse("{\"A\":\"secret_leak_one\",\"A\":\"secret_leak_two\"}"u8));
        Assert.Contains("duplicate", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_leak", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_InvalidUtf8_Fails()
    {
        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.Parse(new byte[] { (byte)'{', 0xC3, 0x28, (byte)'}' }));
        Assert.Contains("UTF-8", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromStream_TooLarge_FailsWithoutEcho()
    {
        var payload = "{\"NOTION_TOKEN\":\"" + new string('s', SecretStore.MaxStdinBytes) + "\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var ex = Assert.Throws<SecretsInputException>(() => SecretStore.LoadFromStream(stream));

        Assert.Contains("64 KiB", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sss", ex.Message, StringComparison.Ordinal);
        Assert.False(SecretStore.HasStdinSecrets);
    }

    [Fact]
    public void LoadFromStream_AtLimit_IsAccepted()
    {
        var prefix = "{\"BIG\":\"";
        var suffix = "\"}";
        var payload = prefix + new string('s', SecretStore.MaxStdinBytes - prefix.Length - suffix.Length) + suffix;
        Assert.Equal(SecretStore.MaxStdinBytes, Encoding.UTF8.GetByteCount(payload));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        SecretStore.LoadFromStream(stream);

        Assert.True(SecretStore.HasStdinSecrets);
    }

    [Fact]
    public void Get_StdinValueWinsOverEnvironment_AndMissingNamesFallBackToEnvironment()
    {
        const string winner = "BUKIT_TEST_SECRET_STORE_WINNER";
        const string fallback = "BUKIT_TEST_SECRET_STORE_FALLBACK";
        var previousWinner = Environment.GetEnvironmentVariable(winner);
        var previousFallback = Environment.GetEnvironmentVariable(fallback);
        try
        {
            Environment.SetEnvironmentVariable(winner, "from-env");
            Environment.SetEnvironmentVariable(fallback, "fallback-env");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"{winner}\":\"from-stdin\"}}"));

            SecretStore.LoadFromStream(stream);

            Assert.Equal("from-stdin", SecretStore.Get(winner));
            Assert.Equal("fallback-env", SecretStore.Get(fallback));
            // Stdin secrets are never written back to the environment, so children cannot inherit them.
            Assert.Equal("from-env", Environment.GetEnvironmentVariable(winner));
        }
        finally
        {
            Environment.SetEnvironmentVariable(winner, previousWinner);
            Environment.SetEnvironmentVariable(fallback, previousFallback);
        }
    }

    [Fact]
    public void GetNotionToken_UsesStdinSecretBeforeEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
            SecretStore.Install(new Dictionary<string, string> { [EnvironmentHelper.NotionTokenKey] = "fake-stdin-notion" });

            Assert.Equal("fake-stdin-notion", EnvironmentHelper.GetNotionToken());
            Assert.Null(Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey));

            SecretStore.Reset();
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, "fake-env-notion");
            Assert.Equal("fake-env-notion", EnvironmentHelper.GetNotionToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, previous);
        }
    }

    [Fact]
    public void Redact_MasksStdinValues_LongestFirst()
    {
        SecretStore.Install(new Dictionary<string, string> { ["A"] = "fake-secret", ["B"] = "fake-secret-longer" });

        Assert.Equal("x *** y *** z", SecretStore.Redact("x fake-secret-longer y fake-secret z"));
        Assert.Equal(string.Empty, SecretStore.Redact(null));
    }

    [Fact]
    public void SerializeStdinSecrets_RoundTripsThroughParse()
    {
        SecretStore.Install(new Dictionary<string, string> { ["NOTION_TOKEN"] = "fake \"quoted\" \\ token" });

        var json = SecretStore.SerializeStdinSecrets();

        Assert.Equal("fake \"quoted\" \\ token", SecretStore.Parse(Encoding.UTF8.GetBytes(json))["NOTION_TOKEN"]);
    }

    [Fact]
    public void ConsoleLogger_RedactsStdinSecrets()
    {
        SecretStore.Install(new Dictionary<string, string> { ["NOTION_TOKEN"] = "fake-logger-secret-value" });
        var originalError = Console.Error;
        using var writer = new StringWriter();
        try
        {
            Console.SetError(writer);
            new ConsoleLogger(LogLevel.Debug).Error("request failed with fake-logger-secret-value");
            new ConsoleLogger(LogLevel.Debug, "json").Error("request failed with fake-logger-secret-value");
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.DoesNotContain("fake-logger-secret-value", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("***", writer.ToString(), StringComparison.Ordinal);
    }
}
