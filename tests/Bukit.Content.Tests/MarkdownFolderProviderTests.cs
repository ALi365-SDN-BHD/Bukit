using System.Reflection;
using System.Text.RegularExpressions;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Markdown;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MarkdownFolderProviderTests
{
    private static readonly Type MfpType = typeof(MarkdownFolderProvider);
    private static readonly Type FmType = typeof(MarkdownFrontMatterParser);
    private static readonly Type FieldType = typeof(MarkdownFieldBuilder);
    private static readonly Type TextType = typeof(MarkdownTextHelper);

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var method = MfpType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Method '{methodName}' not found on MarkdownFolderProvider.");
        return (T)method.Invoke(null, args)!;
    }

    private static object InvokePrivateStatic(string methodName, params object[] args)
    {
        var method = MfpType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Method '{methodName}' not found on MarkdownFolderProvider.");
        return method.Invoke(null, args)!;
    }

    private static T InvokeFromType<T>(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}.");
        return (T)method.Invoke(null, args)!;
    }

    private static object InvokeFromType(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}.");
        return method.Invoke(null, args)!;
    }

#pragma warning disable xUnit2008
    [Fact]
    public void BuildGlobRegex_SingleAsterisk_MatchesWithinDirectory()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "posts/*.md");

        Assert.True(regex.IsMatch("posts/hello.md"));
        Assert.False(regex.IsMatch("posts/sub/hello.md"));
    }

    [Fact]
    public void BuildGlobRegex_DoubleAsterisk_MatchesAnyDepth()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "**/*.md");

        Assert.True(regex.IsMatch("a/b/hello.md"));
        Assert.True(regex.IsMatch("a/b/c/hello.md"));
        Assert.False(regex.IsMatch("a/b/hello.txt"));
    }

    [Fact]
    public void BuildGlobRegex_QuestionMark_MatchesSingleChar()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "page-?.md");

        Assert.True(regex.IsMatch("page-1.md"));
        Assert.True(regex.IsMatch("page-a.md"));
        Assert.False(regex.IsMatch("page-12.md"));
        Assert.False(regex.IsMatch("page-.md"));
    }

    [Fact]
    public void BuildGlobRegex_LiteralChars_ExactMatch()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "docs/api.md");

        Assert.True(regex.IsMatch("docs/api.md"));
        Assert.True(regex.IsMatch("docs/API.md"));
    }

    [Fact]
    public void BuildGlobRegex_EscapedChars_RegexSafe()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "file[1].md");

        Assert.True(regex.IsMatch("file[1].md"));
        Assert.False(regex.IsMatch("file1.md"));
    }

    [Fact]
    public void BuildGlobRegex_CaseInsensitive()
    {
        var regex = InvokePrivateStatic<Regex>("BuildGlobRegex", "Posts/Hello.md");

        Assert.True(regex.IsMatch("posts/hello.md"));
        Assert.True(regex.IsMatch("POSTS/HELLO.MD"));
    }
#pragma warning restore xUnit2008

    [Fact]
    public async Task LoadAsync_WhenContentDirMissing_ThrowsContentException()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-md-missing-" + Guid.NewGuid().ToString("N"));
        var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(dir));

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadRawAsync());

        Assert.Contains("ContentDir not found", ex.Message);
        Assert.Contains(dir, ex.Message);
    }

    [Fact]
    public async Task LoadAsync_WithIncludePathsAndGlobs_FiltersMarkdownFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-md-provider-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "posts"));
            await File.WriteAllTextAsync(Path.Combine(root, "about.md"), "# About");
            await File.WriteAllTextAsync(Path.Combine(root, "posts", "first.md"), "# First");
            await File.WriteAllTextAsync(Path.Combine(root, "posts", "second.md"), "# Second");

            var byPath = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(
                root,
                IncludePaths: new[] { "about" }));
            var pathResult = await byPath.LoadRawAsync();

            var pathItem = Assert.Single(pathResult.Documents);
            Assert.Equal("about", pathItem.Slug);

            var byGlob = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(
                root,
                IncludeGlobs: new[] { "posts/*.md" },
                MaxItems: 1));
            var globResult = await byGlob.LoadRawAsync();

            var globItem = Assert.Single(globResult.Documents);
            Assert.Equal("first", globItem.Slug);
            Assert.Equal("markdown", ContentFieldReader.GetText(globItem.CustomFields, "source"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_AddsTableOfContentsMetadataFromHeadings()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-md-toc-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "toc.md"), """
            ---
            title: TOC
            ---
            # Main Title

            ## First Section

            ### Deep Section

            ## First Section
            """);

            var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(root));
            var result = await provider.LoadRawAsync();

            Assert.True(ContentFieldReader.TryGetField(result.Documents[0].CustomFields, "tableOfContents", out var tocField));
            var toc = Assert.IsAssignableFrom<IReadOnlyList<TableOfContentsEntry>>(tocField.Value);
            Assert.Equal(4, toc.Count);
            Assert.Equal(1, toc[0].Level);
            Assert.Equal("Main Title", toc[0].Text);
            Assert.Equal("main-title", toc[0].Id);
            Assert.Equal("first-section-1", toc[3].Id);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WithCollectionFrontMatter_DoesNotInjectDefaultType()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-md-collection-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "article.md"), """
            ---
            title: Article
            collection: post
            ---
            Body
            """);

            var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(root, DefaultType: "page"));
            var result = await provider.LoadRawAsync();

            var item = Assert.Single(result.Documents);
            Assert.Equal("post", ContentFieldReader.GetText(item.CustomFields, "collection"));
            Assert.False(ContentFieldReader.TryGetField(item.CustomFields, "type", out _));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_StripsHtmlDecodesEntitiesAndTruncates()
    {
        var summary = MarkdownTextHelper.ExtractSummaryFromMarkdown("""
        # Title
        This is a short &amp; useful paragraph with extra words.
        """, maxLength: 24);

        Assert.Equal("Title This is a short &…", summary);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_WithNonPositiveLength_ReturnsEmpty()
    {
        var summary = MarkdownTextHelper.ExtractSummaryFromMarkdown("# Title\nBody", maxLength: 0);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public async Task RenderHtmlFromFileAsync_StripsFrontMatterBeforeRendering()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-md-render-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "page.md");
            await File.WriteAllTextAsync(path, """
            ---
            title: Hidden
            ---
            # Visible
            Body text
            """);

            var html = await MarkdownTextHelper.RenderHtmlFromFileAsync(path, CancellationToken.None);

            Assert.Contains("<h1 id=\"visible\">Visible</h1>", html);
            Assert.Contains("<p>Body text</p>", html);
            Assert.DoesNotContain("title: Hidden", html);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TryExtractFrontMatter_ValidYaml_ExtractsCorrectly()
    {
        var markdown = """
                       ---
                       title: Hello World
                       tags: [a, b]
                       ---
                       # Content

                       Body text
                       """;

        var args = new object[] { markdown, string.Empty, string.Empty };
        var ok = (bool)InvokeFromType(FmType, "TryExtractFrontMatter", args);

        Assert.True(ok);
        Assert.Contains("title: Hello World", (string)args[1]);
        Assert.Contains("# Content", (string)args[2]);
        Assert.Contains("Body text", (string)args[2]);
    }

    [Fact]
    public void TryExtractFrontMatter_NoFrontMatter_ReturnsFalse()
    {
        var markdown = "# Just Content\n\nNo front matter here.";

        var args = new object[] { markdown, string.Empty, string.Empty };
        var ok = (bool)InvokeFromType(FmType, "TryExtractFrontMatter", args);

        Assert.False(ok);
    }

    [Fact]
    public void TryExtractFrontMatter_Malformed_ReturnsFalse()
    {
        var args = new object[] { "---\ntitle: incomplete", string.Empty, string.Empty };
        var ok = (bool)InvokeFromType(FmType, "TryExtractFrontMatter", args);

        Assert.False(ok);
    }

    [Fact]
    public void TryExtractFrontMatter_MissingClosing_ReturnsFalse()
    {
        var markdown = """
                       ---
                       title: NoEnd
                       tags: [x]

                       Body content without closing ---
                       """;

        var args = new object[] { markdown, string.Empty, string.Empty };
        var ok = (bool)InvokeFromType(FmType, "TryExtractFrontMatter", args);

        Assert.False(ok);
    }

    [Fact]
    public void ParseFrontMatter_BasicValues_ReturnsDictionary()
    {
        var yaml = "title: Test\ncount: 42\nactive: true";

        var dict = InvokeFromType<IReadOnlyDictionary<string, object>>(FmType, "ParseFrontMatter", yaml);

        Assert.Equal("Test", dict["title"]);
        Assert.Equal("42", dict["count"]);
        Assert.True(bool.Parse(dict["active"].ToString()!));
    }

    [Fact]
    public void ParseFrontMatter_Sequence_ReturnsList()
    {
        var yaml = "tags: [one, two, three]";

        var dict = InvokeFromType<IReadOnlyDictionary<string, object>>(FmType, "ParseFrontMatter", yaml);

        Assert.True(dict["tags"] is IEnumerable<object>);
        var list = ((IEnumerable<object>)dict["tags"]).Select(x => x.ToString()).ToList();
        Assert.Contains("one", list);
        Assert.Contains("two", list);
        Assert.Contains("three", list);
    }

    [Fact]
    public void ParseFrontMatter_NestedMapping_ReturnsDictionary()
    {
        var yaml = "author:\n  name: Alice\n  email: a@b.com";

        var dict = InvokeFromType<IReadOnlyDictionary<string, object>>(FmType, "ParseFrontMatter", yaml);

        Assert.True(dict["author"] is IReadOnlyDictionary<string, object>);
    }

    [Fact]
    public void TryConvertToField_Bool_ReturnsBoolField()
    {
        var args = new object[] { true, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("bool", field.Type);
        Assert.True((bool)field.Value!);
    }

    [Fact]
    public void TryConvertToField_Int_ReturnsNumberField()
    {
        var args = new object[] { 42, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_Long_ReturnsNumberField()
    {
        var args = new object[] { 42L, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_Float_ReturnsNumberField()
    {
        var args = new object[] { 3.14f, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_Double_ReturnsNumberField()
    {
        var args = new object[] { 3.14d, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_Decimal_ReturnsNumberField()
    {
        var args = new object[] { 3.14m, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_DateTime_ReturnsDateField()
    {
        var dt = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var args = new object[] { dt, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("date", field.Type);
    }

    [Fact]
    public void TryConvertToField_DateTimeOffset_ReturnsDateField()
    {
        var dto = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.FromHours(8));
        var args = new object[] { dto, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("date", field.Type);
    }

    [Fact]
    public void TryConvertToField_PlainText_ReturnsTextField()
    {
        var args = new object[] { "Hello World", null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("text", field.Type);
        Assert.Equal("Hello World", field.Value);
    }

    [Fact]
    public void TryConvertToField_DateString_ReturnsDateField()
    {
        var args = new object[] { "2025-06-15T10:30:00Z", null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("date", field.Type);
    }

    [Fact]
    public void TryConvertToField_BoolString_ReturnsBoolField()
    {
        var args = new object[] { "True", null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("bool", field.Type);
        Assert.True((bool)field.Value!);
    }

    [Fact]
    public void TryConvertToField_NumberString_ReturnsNumberField()
    {
        var args = new object[] { "123", null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
        Assert.Equal(123L, (long)field.Value!);
    }

    [Fact]
    public void TryConvertToField_DoubleString_ReturnsNumberField()
    {
        var args = new object[] { "9.99", null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void TryConvertToField_NullValue_ReturnsTextField()
    {
        var args = new object[] { null!, null! };

        Assert.Throws<TargetInvocationException>(() => InvokeFromType(FieldType, "TryConvertToField", args));
    }

    [Fact]
    public void TryConvertToField_UnknownObject_ReturnsTextField()
    {
        var obj = new object();
        var args = new object[] { obj, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("text", field.Type);
        Assert.Contains("System.Object", field.Value!.ToString());
    }

    [Fact]
    public void TryConvertToField_List_ReturnsListField()
    {
        var list = new List<object> { "a", "b" };
        var args = new object[] { list, null! };
        var ok = (bool)InvokeFromType(FieldType, "TryConvertToField", args);

        Assert.True(ok);
        var field = (ContentField)args[1];
        Assert.Equal("list", field.Type);
    }

    [Fact]
    public void NormalizeTaxonomy_CommaSeparatedString_SplitToList()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "alpha, beta, gamma"
        };
        InvokeFromType(FmType, "NormalizeTaxonomy", dict, "tags");

        var result = dict["tags"];
        Assert.IsAssignableFrom<IEnumerable<object>>(result);
        var list = ((IEnumerable<object>)result).Select(x => x.ToString()).ToList();
        Assert.Equal(3, list.Count);
        Assert.Contains("alpha", list);
        Assert.Contains("beta", list);
        Assert.Contains("gamma", list);
    }

    [Fact]
    public void NormalizeTaxonomy_Sequence_KeepsAsList()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["categories"] = new List<object> { "News", "Tech" }
        };
        InvokeFromType(FmType, "NormalizeTaxonomy", dict, "categories");

        var result = dict["categories"];
        Assert.IsAssignableFrom<IEnumerable<object>>(result);
        var list = ((IEnumerable<object>)result).Select(x => x.ToString()).ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains("News", list);
        Assert.Contains("Tech", list);
    }

    [Fact]
    public void NormalizeTaxonomy_EmptyString_SplitsToEmpty()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = ""
        };
        InvokeFromType(FmType, "NormalizeTaxonomy", dict, "tags");

        var result = dict["tags"];
        Assert.IsAssignableFrom<IEnumerable<object>>(result);
        Assert.Empty((IEnumerable<object>)result);
    }

    [Fact]
    public void NormalizeTaxonomy_KeyMissing_NoError()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        InvokeFromType(FmType, "NormalizeTaxonomy", dict, "tags");

        Assert.Empty(dict);
    }

    [Fact]
    public void ExtractTitle_WithHeading_ReturnsTitle()
    {
        var title = InvokeFromType<string?>(TextType, "ExtractTitle", "# Hello World\n\nContent here.");

        Assert.NotNull(title);
        Assert.Equal("Hello World", title);
    }

    [Fact]
    public void ExtractTitle_WithoutHeading_ReturnsNull()
    {
        var title = InvokeFromType<string?>(TextType, "ExtractTitle", "Just content\nNo heading.");

        Assert.Null(title);
    }

    [Fact]
    public void ExtractTitle_FirstHeading_IgnoresSubsequent()
    {
        var title = InvokeFromType<string?>(TextType, "ExtractTitle", "# First\n## Second\n# Third");

        Assert.NotNull(title);
        Assert.Equal("First", title);
    }

    [Fact]
    public void StripHtmlToText_Basic_RemovesTags()
    {
        var text = InvokeFromType<string>(TextType, "StripHtmlToText", "<p>Hello <b>World</b></p>");

        Assert.Equal("Hello World", text);
    }

    [Fact]
    public void StripHtmlToText_WithMultipleTags_CollapsesWhitespace()
    {
        var text = InvokeFromType<string>(TextType, "StripHtmlToText", "<div><p>Line 1</p><p>Line 2</p></div>");

        Assert.Contains("Line 1", text);
        Assert.Contains("Line 2", text);
    }

    [Fact]
    public void StripHtmlToText_EmptyString_ReturnsEmpty()
    {
        var text = InvokeFromType<string>(TextType, "StripHtmlToText", "");

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void StripHtmlToText_HtmlEntities_Decoded()
    {
        var text = InvokeFromType<string>(TextType, "StripHtmlToText", "Hello &amp; World &lt;3");

        Assert.Equal("Hello & World <3", text);
    }

    [Fact]
    public void BuildFields_TagsAndCategories_Injected()
    {
        var projectedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new List<object> { "go", "rust" },
            ["categories"] = new List<object> { "programming" },
            ["count"] = 42
        };

        var fields = InvokeFromType<IReadOnlyDictionary<string, ContentField>>(FieldType, "BuildFields", projectedValues);

        Assert.Contains("tags", fields.Keys);
        Assert.Contains("categories", fields.Keys);
        Assert.Contains("count", fields.Keys);
        Assert.Equal("list", fields["tags"].Type);
        Assert.Equal("list", fields["categories"].Type);
        Assert.Equal("number", fields["count"].Type);
    }

    [Fact]
    public void BuildFields_ReservedKeys_Filtered()
    {
        var projectedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "My Page",
            ["slug"] = "my-page",
            ["type"] = "post",
            ["custom_field"] = "visible"
        };

        var fields = InvokeFromType<IReadOnlyDictionary<string, ContentField>>(FieldType, "BuildFields", projectedValues);

        Assert.DoesNotContain("title", fields.Keys);
        Assert.DoesNotContain("slug", fields.Keys);
        Assert.DoesNotContain("type", fields.Keys);
        Assert.Contains("custom_field", fields.Keys);
    }

    [Fact]
    public void BuildFields_Summary_InjectedAsText()
    {
        var projectedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = "A short summary."
        };

        var fields = InvokeFromType<IReadOnlyDictionary<string, ContentField>>(FieldType, "BuildFields", projectedValues);

        Assert.Contains("summary", fields.Keys);
        Assert.Equal("text", fields["summary"].Type);
        Assert.Equal("A short summary.", fields["summary"].Value);
    }
}
