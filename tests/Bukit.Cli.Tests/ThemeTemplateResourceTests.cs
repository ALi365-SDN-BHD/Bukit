using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeTemplateResourceTests
{
    [Fact]
    public void Get_StyleCss_ReturnsNonEmptyCss()
    {
        var result = ThemeTemplateResource.Get("StyleCss");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(":root", result);
        Assert.Contains("--primary: #0b5fff;", result);
    }

    [Fact]
    public void Get_BaseLayout_ReturnsHtmlTemplate()
    {
        var result = ThemeTemplateResource.Get("BaseLayout");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<html", result);
    }

    [Fact]
    public void Get_Nonexistent_ReturnsEmpty()
    {
        var result = ThemeTemplateResource.Get("Nonexistent");

        Assert.Equal("", result);
    }

    [Fact]
    public void Get_ListCardPartial_ReturnsTemplate()
    {
        var result = ThemeTemplateResource.Get("ListCardPartial");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Get_HeaderPartial_ReturnsTemplate()
    {
        var result = ThemeTemplateResource.Get("HeaderPartial");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ApplyColorOverrides_ReplacesPrimaryAndAccent()
    {
        var css = ThemeTemplateResource.Get("StyleCss");
        Assert.Contains("--primary: #0b5fff;", css);
        Assert.Contains("--accent: #0f7b6c;", css);

        var result = ThemeTemplateResource.ApplyColorOverrides(css, "#ff0000", "#00ff00");

        Assert.DoesNotContain("--primary: #0b5fff;", result);
        Assert.DoesNotContain("--accent: #0f7b6c;", result);
        Assert.Contains("--primary: #ff0000;", result);
        Assert.Contains("--accent: #00ff00;", result);
    }

    [Fact]
    public void ApplyColorOverrides_OnlyPrimary_ReplacesPrimaryOnly()
    {
        var css = ThemeTemplateResource.Get("StyleCss");

        var result = ThemeTemplateResource.ApplyColorOverrides(css, "#ff0000", null);

        Assert.DoesNotContain("--primary: #0b5fff;", result);
        Assert.Contains("--primary: #ff0000;", result);
        Assert.Contains("--accent: #0f7b6c;", result);
    }

    [Fact]
    public void ApplyColorOverrides_OnlyAccent_ReplacesAccentOnly()
    {
        var css = ThemeTemplateResource.Get("StyleCss");

        var result = ThemeTemplateResource.ApplyColorOverrides(css, null, "#00ff00");

        Assert.Contains("--primary: #0b5fff;", result);
        Assert.DoesNotContain("--accent: #0f7b6c;", result);
        Assert.Contains("--accent: #00ff00;", result);
    }

    [Fact]
    public void ProcessPlaceholders_ReplacesPlaceholders()
    {
        var template = "default: {{-- bukit:brand --}}\ndefault: \"{{-- bukit:primary_color --}}\"";
        var replacements = new Dictionary<string, string>
        {
            ["brand"] = "TestBrand",
            ["primary_color"] = "#abcdef",
        };

        var result = ThemeTemplateResource.ProcessPlaceholders(template, replacements);

        Assert.Contains("TestBrand", result);
        Assert.Contains("#abcdef", result);
        Assert.DoesNotContain("{{-- bukit:brand --}}", result);
        Assert.DoesNotContain("{{-- bukit:primary_color --}}", result);
    }

    [Fact]
    public void ProcessPlaceholders_NoReplacements_ReturnsOriginal()
    {
        var template = "default: {{-- bukit:brand --}}";

        var result = ThemeTemplateResource.ProcessPlaceholders(template, new Dictionary<string, string>());

        Assert.Equal(template, result);
    }

    [Fact]
    public void ProcessPlaceholders_NullDictionary_ReturnsOriginal()
    {
        var template = "default: {{-- bukit:brand --}}";

        var result = ThemeTemplateResource.ProcessPlaceholders(template, null!);

        Assert.Equal(template, result);
    }
}
