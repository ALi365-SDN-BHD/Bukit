using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using System.Text;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DataFilesPluginTests
{
    [Fact]
    public void DerivePages_DirectorySymlinkOutsideDataRoot_IsIgnored()
    {
        var root = GetTempDir();
        var externalRoot = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(externalRoot);
            File.WriteAllText(Path.Combine(dataDir, "safe.json"), "{}");
            File.WriteAllText(Path.Combine(externalRoot, "secret.json"), "{}");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(dataDir, "linked"), externalRoot);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                return;
            }

            var context = CreateContext(root);
            new DataFilesPlugin(CreateConfig()).DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            Assert.Contains("safe", data.Keys);
            Assert.DoesNotContain("linked", data.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(externalRoot)) Directory.Delete(externalRoot, true);
        }
    }

    [Fact]
    public void DerivePages_ConfiguredLanguageSymlinkOutsideDataRoot_IsIgnored()
    {
        var root = GetTempDir();
        var externalRoot = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(externalRoot);
            File.WriteAllText(Path.Combine(dataDir, "safe.json"), "{}");
            File.WriteAllText(Path.Combine(externalRoot, "secret.json"), "{}");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(dataDir, "en"), externalRoot);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                return;
            }

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Languages = new[] { "en" }
                },
                Content = TestContent.Markdown()
            };
            var context = CreateContext(root);

            new DataFilesPlugin(config).DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            Assert.Contains("safe", data.Keys);
            Assert.DoesNotContain("en", data.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(externalRoot)) Directory.Delete(externalRoot, true);
        }
    }

    [Fact]
    public void DerivePages_DataRootSymlink_IsIgnored()
    {
        var root = GetTempDir();
        var externalRoot = GetTempDir();
        try
        {
            Directory.CreateDirectory(externalRoot);
            File.WriteAllText(Path.Combine(externalRoot, "secret.json"), "{}");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "data"), externalRoot);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                return;
            }

            var context = CreateContext(root);

            new DataFilesPlugin(CreateConfig()).DerivePages(context);

            Assert.DoesNotContain("__data_files", context.Data.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(externalRoot)) Directory.Delete(externalRoot, true);
        }
    }

    [Fact]
    public async Task DerivePagesAsync_PreCanceled_StopsBeforeEnumeration()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "data"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePagesAsync(
                    CreateContext(root),
                    cancellation.Token));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_EntryLimitExceeded_FailsClosedWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "a.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "b.json"), "{}");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxEntries: 1).DerivePages(CreateContext(root)));

            Assert.Equal("Data directory contains more than 1 entries.", exception.Message);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DepthLimitExceeded_FailsClosedWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var nested = Path.Combine(root, "data", "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "value.json"), "{}");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxDepth: 0).DerivePages(CreateContext(root)));

            Assert.Contains("depth exceeds the maximum of 0", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/nested", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_FileSizeLimitExceeded_FailsClosedBeforeParsing()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "large.json"), "12345");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxFileSizeBytes: 4)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("maximum file size of 4 bytes", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/large.json", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_TotalSizeLimitAcrossLanguages_FailsClosed()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataDir, "en"));
            Directory.CreateDirectory(Path.Combine(dataDir, "fr"));
            File.WriteAllText(Path.Combine(dataDir, "en", "a.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "fr", "b.json"), "{}");
            var config = CreateConfig() with
            {
                Site = CreateConfig().Site with { Languages = ["en", "fr"] }
            };

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(config, maxTotalSizeBytes: 3)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("total size limit of 3 bytes", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/fr/b.json", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_SupportedBomEncodings_PreserveUtf8MultibyteText()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            const string json = "{\"value\":\"雪山\"}";
            var encodings = new (string Name, Encoding Encoding)[]
            {
                ("utf8", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)),
                ("utf8-bom", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true)),
                ("utf16-le", new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true)),
                ("utf16-be", new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true))
            };
            foreach (var (name, encoding) in encodings)
            {
                File.WriteAllBytes(
                    Path.Combine(dataDir, $"{name}.json"),
                    [.. encoding.GetPreamble(), .. encoding.GetBytes(json)]);
            }

            var context = CreateContext(root);
            new DataFilesPlugin(CreateConfig()).DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            foreach (var (name, _) in encodings)
            {
                var document = Assert.IsType<Dictionary<string, object>>(data[name]);
                Assert.Equal("雪山", document["value"]);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("malformed-utf8.yaml", new byte[] { 0x76, 0x3A, 0x20, 0x22, 0xC3, 0x28, 0x22 })]
    [InlineData("malformed-utf16-le.yaml", new byte[] { 0xFF, 0xFE, 0x76, 0x00, 0x3A, 0x00, 0x20, 0x00, 0x22, 0x00, 0x00, 0xD8, 0x22, 0x00 })]
    [InlineData("malformed-utf16-be.yaml", new byte[] { 0xFE, 0xFF, 0x00, 0x76, 0x00, 0x3A, 0x00, 0x20, 0x00, 0x22, 0xD8, 0x00, 0x00, 0x22 })]
    public void DerivePages_MalformedEncoding_FailsClosedWithRelativePath(
        string fileName,
        byte[] content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllBytes(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("Malformed data file encoding", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_UnsupportedUtf32Bom_FailsClosedWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            var encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
            File.WriteAllBytes(
                Path.Combine(dataDir, "unsupported.yaml"),
                [.. encoding.GetPreamble(), .. encoding.GetBytes("value: safe\n")]);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("Unsupported data file encoding", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/unsupported.yaml", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(0x38)]
    [InlineData(0x39)]
    [InlineData(0x2B)]
    [InlineData(0x2F)]
    public void DerivePages_UnsupportedUtf7Bom_FailsClosedWithRelativePath(int variant)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllBytes(
                Path.Combine(dataDir, "unsupported-utf7.yaml"),
                [0x2B, 0x2F, 0x76, checked((byte)variant), 0x2D, .. Encoding.UTF8.GetBytes("value: safe\n")]);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("Unsupported data file encoding", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/unsupported-utf7.yaml", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DecodedCharacterLimit_PreemptsLaterSyntaxError()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "decoded.yaml"), "value: abcdef\ninvalid: [\n");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(
                    CreateConfig(),
                    maxFileSizeBytes: 128,
                    maxDecodedChars: 8)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("decodes to more than 8 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/decoded.yaml", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Failed to parse", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("scalar.json", "{\"value\":\"abcdef\"}")]
    [InlineData("scalar.yaml", "value: abcdef\n")]
    public void DerivePages_ScalarLimit_FailsDuringParsing(
        string fileName,
        string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(
                    CreateConfig(),
                    maxFileSizeBytes: 128,
                    maxDecodedChars: 128,
                    maxScalarChars: 5)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("scalar longer than 5 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_JsonPropertyNameScalarLimit_FailsWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(
                Path.Combine(dataDir, "property.json"),
                "{\"\\u0061\\u0062\\u0063\\u0064\\u0065\\u0066\":1}");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(
                    CreateConfig(),
                    maxFileSizeBytes: 128,
                    maxDecodedChars: 128,
                    maxScalarChars: 5)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("scalar longer than 5 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/property.json", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_JsonPropertyNameScalarLimit_CountsDecodedCharacters()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "property.json"), "{\"\\u0061\":1}");

            var context = CreateContext(root);
            new DataFilesPlugin(
                CreateConfig(),
                maxFileSizeBytes: 128,
                maxDecodedChars: 128,
                maxScalarChars: 1)
                .DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            var document = Assert.IsType<Dictionary<string, object>>(data["property"]);
            Assert.Equal(1L, document["a"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_JsonNumberScalarLimit_SpansReaderChunksAndFailsWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            var content = $"{{\"number\":{new string('1', 5000)}}}";
            File.WriteAllText(Path.Combine(dataDir, "number.json"), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(
                    CreateConfig(),
                    maxFileSizeBytes: content.Length + 16L,
                    maxDecodedChars: content.Length + 16L,
                    maxScalarChars: 4096)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("scalar longer than 4096 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/number.json", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_ScalarLimit_StopsControlledInputBeforeTrailingPayload()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            var path = Path.Combine(dataDir, "controlled.yaml");
            File.WriteAllText(path, "placeholder");
            var payload = Encoding.UTF8.GetBytes(
                $"value: {new string('a', 32)}\ntrailing: {new string('b', 1024 * 1024)}\n");
            var controlled = new ControlledReadStream(payload);
            var openCount = 0;

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(
                    CreateConfig(),
                    maxFileSizeBytes: payload.Length + 1L,
                    maxTotalSizeBytes: payload.Length + 1L,
                    maxDecodedChars: payload.Length + 1L,
                    maxScalarChars: 16,
                    openDataFile: _ =>
                    {
                        openCount++;
                        return controlled;
                    })
                    .DerivePages(CreateContext(root)));

            Assert.Contains("scalar longer than 16 characters", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, openCount);
            Assert.InRange(controlled.BytesRead, 1, payload.Length - 1);
            Assert.Equal(0, controlled.SeekCount);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_LanguageFilesCountOnceAgainstTotalSizeLimit()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataDir, "en"));
            Directory.CreateDirectory(Path.Combine(dataDir, "fr"));
            File.WriteAllText(Path.Combine(dataDir, "en", "a.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "fr", "b.json"), "{}");
            var config = CreateConfig() with
            {
                Site = CreateConfig().Site with { Languages = ["en", "fr"] }
            };
            var context = CreateContext(root);

            new DataFilesPlugin(config, maxTotalSizeBytes: 4).DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            Assert.Equal(["en", "fr"], data.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("nested.json", "{\"a\":{\"b\":1}}")]
    [InlineData("nested.yaml", "a:\n  b:\n    c: 1\n")]
    public void DerivePages_DocumentDepthLimitExceeded_FailsClosed(
        string fileName,
        string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxDocumentDepth: 1)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("document depth exceeds the maximum of 1", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("[null,null]")]
    public void DerivePages_DocumentNodeLimitExceeded_FailsClosed(string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "nodes.json"), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxDocumentNodes: 2)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 2 nodes", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/nodes.json", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("nodes.json", "[1,2,3,")]
    [InlineData("nodes.yaml", "- 1\n- 2\n- [\n")]
    public void DerivePages_DocumentNodeLimit_PreemptsLaterSyntaxError(
        string fileName,
        string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxDocumentNodes: 2)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 2 nodes", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Failed to parse", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_NoDataDir_ReturnsEmpty()
    {
        var config = CreateConfig();
        var ctx = new BuildContext
        {
            RootDir = Path.Combine(Path.GetTempPath(), "bukit_nonexistent_" + Guid.NewGuid().ToString("N")),
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new DataFilesPlugin(config).DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_LoadsYamlDataFile()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "authors.yaml"),
                "john:\n  name: John Doe\n  email: john@example.com\n");

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            var derived = new DataFilesPlugin(config).DerivePages(ctx);
            Assert.Empty(derived);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            var authors = Assert.IsType<Dictionary<string, object>>(dict["authors"]);
            var john = Assert.IsType<Dictionary<string, object>>(authors["john"]);
            Assert.Equal("John Doe", john["name"]);
            Assert.Equal("john@example.com", john["email"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_YamlComplexMappingKey_PreservesLegacyTextProjection()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "complex.yaml"), "? [a, b]\n: value\n");
            var context = CreateContext(root);

            new DataFilesPlugin(CreateConfig()).DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            var complex = Assert.IsType<Dictionary<string, object>>(data["complex"]);
            Assert.Equal("value", complex["[ a, b ]"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_LoadsJsonDataFile()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "nav.json"),
                """{"items":[{"name":"Home","url":"/"},{"name":"Blog","url":"/blog/"}]}""");

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            var nav = Assert.IsType<Dictionary<string, object>>(dict["nav"]);
            var items = Assert.IsType<List<object>>(nav["items"]);
            var home = Assert.IsType<Dictionary<string, object>>(items[0]);
            Assert.Equal("Home", home["name"]);
            Assert.Equal("/", home["url"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_LoadsMultiLanguageData()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataDir, "zh-CN"));
            Directory.CreateDirectory(Path.Combine(dataDir, "en"));
            File.WriteAllText(Path.Combine(dataDir, "zh-CN", "strings.yaml"), "hello: 你好\n");
            File.WriteAllText(Path.Combine(dataDir, "en", "strings.yaml"), "hello: Hello\n");

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Languages = new[] { "zh-CN", "en" }
                },
                Content = TestContent.Markdown()
            };
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            Assert.Contains("zh-CN", dict.Keys);
            Assert.Contains("en", dict.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_NestedDirectories_LoadsRecursively()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var subDir = Path.Combine(dataDir, "team");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "members.yaml"), "devs:\n  - Alice\n  - Bob\n");

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            Assert.Contains("team", dict.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_TomlFile_ThrowsUnsupportedFormatWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "settings.toml"), "title = \"Bukit\"");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data/settings.toml", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("broken.json", "{\"items\": [}")]
    [InlineData("broken.yaml", "items: [")]
    public void DerivePages_MalformedSupportedFile_ThrowsWithRelativePath(string fileName, string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DuplicateLogicalKeyAcrossFormats_Throws()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "nav.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "nav.yaml"), "items: []");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nav", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DifferentCreationOrder_ProducesSameOrdinalEnumerationOrder()
    {
        var firstRoot = GetTempDir();
        var secondRoot = GetTempDir();
        try
        {
            CreateOrderedFixture(firstRoot, reverse: false);
            CreateOrderedFixture(secondRoot, reverse: true);
            var firstContext = CreateContext(firstRoot);
            var secondContext = CreateContext(secondRoot);

            new DataFilesPlugin(CreateConfig()).DerivePages(firstContext);
            new DataFilesPlugin(CreateConfig()).DerivePages(secondContext);

            var first = Assert.IsType<Dictionary<string, object>>(firstContext.Data["__data_files"]);
            var second = Assert.IsType<Dictionary<string, object>>(secondContext.Data["__data_files"]);
            Assert.Equal(new[] { "a", "z", "b", "y" }, first.Keys);
            Assert.Equal(first.Keys, second.Keys);
        }
        finally
        {
            if (Directory.Exists(firstRoot)) Directory.Delete(firstRoot, true);
            if (Directory.Exists(secondRoot)) Directory.Delete(secondRoot, true);
        }
    }

    [Fact]
    public void DerivePages_EntryLimitExceeded_ThrowsDeterministicDiagnostic()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            // Create more entries than the limit (maxEntries=3)
            for (var i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(dataDir, $"item-{i:D2}.json"), "{}");
            }

            var context = CreateContext(root);
            var plugin = new DataFilesPlugin(CreateConfig(), maxEntries: 3);

            var ex = Assert.Throws<ConfigException>(() => plugin.DerivePages(context));
            Assert.Contains("more than 3 entries", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_EntryLimit_RespectsDeterministicOrder()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            // Create entries in reverse order
            for (var i = 9; i >= 0; i--)
            {
                File.WriteAllText(Path.Combine(dataDir, $"item-{i:D2}.json"), $"{{\"v\":{i}}}");
            }

            var context = CreateContext(root);
            // maxEntries=10 should succeed since we have exactly 10 files
            var plugin = new DataFilesPlugin(CreateConfig(), maxEntries: 10);
            plugin.DerivePages(context);

            var data = Assert.IsType<Dictionary<string, object>>(context.Data["__data_files"]);
            Assert.Equal(10, data.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TakeBoundedSorted_StopsAfterLimitItems()
    {
        static IEnumerable<string> Entries()
        {
            yield return "/data/b.json";
            yield return "/data/a.json";
            throw new InvalidOperationException("enumerated beyond the bound");
        }

        var result = DataFilesPlugin.TakeBoundedSorted(Entries(), limit: 2);

        Assert.Equal(["/data/a.json", "/data/b.json"], result);
    }

    [Fact]
    public void TakeBoundedSortedWithinEntryBudget_OverflowDiagnosticIsEnumerationOrderIndependent()
    {
        static ConfigException Overflow(params string[] entries)
            => Assert.Throws<ConfigException>(() =>
                DataFilesPlugin.TakeBoundedSortedWithinEntryBudget(
                    entries,
                    remainingEntries: 1,
                    maxEntries: 1));

        var reverseOrder = Overflow("/data/z.json", "/data/a.json");
        var forwardOrder = Overflow("/data/a.json", "/data/z.json");

        Assert.Equal("Data directory contains more than 1 entries.", reverseOrder.Message);
        Assert.Equal(reverseOrder.Message, forwardOrder.Message);
    }

    [Theory]
    [InlineData("values.json", "{\"a\":1,\"b\":2}")]
    [InlineData("values.yaml", "a: 1\nb: 2\n")]
    public void DerivePages_ProjectedEntryLimit_CountsMapEntries(
        string fileName,
        string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxProjectedEntries: 1)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 1 collection entries", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("json", "{\"k\":\"ab\"}")]
    [InlineData("yaml", "k: ab\n")]
    public void DerivePages_ProjectedCharacterLimit_IsCumulativeAcrossFiles(
        string extension,
        string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, $"a.{extension}"), content);
            File.WriteAllText(Path.Combine(dataDir, $"b.{extension}"), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxProjectedChars: 5)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 5 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"data/b.{extension}", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_ProjectedEntryLimit_CountsFileMapEntries()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "a.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "b.json"), "{}");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxProjectedEntries: 1)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 1 collection entries", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/b.json", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_ProjectedCharacterLimit_CountsFileMapKeys()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "long-name.json"), "{}");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig(), maxProjectedChars: 4)
                    .DerivePages(CreateContext(root)));

            Assert.Contains("more than 4 characters", exception.Message, StringComparison.Ordinal);
            Assert.Contains("data/long-name.json", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_data_test_" + Guid.NewGuid().ToString("N"));

    private static BuildContext CreateContext(string root) => new()
    {
        RootDir = root,
        OutputDir = Path.Combine(root, "out"),
        BaseUrl = "/",
        LayoutsDir = Path.Combine(root, "layouts"),
        RoutedDocuments = Array.Empty<RoutedContentDocument>(),
        Logger = new ConsoleLogger(LogLevel.Error)
    };

    private static void CreateOrderedFixture(string root, bool reverse)
    {
        var dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        var entries = new (string Path, string Content)[]
        {
            (Path.Combine(dataDir, "z.json"), "{}"),
            (Path.Combine(dataDir, "a.json"), "{}"),
            (Path.Combine(dataDir, "y", "entry.json"), "{}"),
            (Path.Combine(dataDir, "b", "entry.json"), "{}")
        };
        foreach (var entry in reverse ? entries.Reverse() : entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.Path)!);
            File.WriteAllText(entry.Path, entry.Content);
        }
    }

    private static AppConfig CreateConfig() => new()
    {
        Site = new SiteConfig { Name = "t", Title = "t" },
        Content = TestContent.Markdown()
    };

    private sealed class ControlledReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        internal int BytesRead { get; private set; }
        internal int SeekCount { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            SeekCount++;
            throw new NotSupportedException();
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
