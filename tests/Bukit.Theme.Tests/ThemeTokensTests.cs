using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeTokensTests
{
    [Fact]
    public void Merge_ChildOverridesParent()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000", ["background"] = "#ffffff" }
        };
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        };

        var merged = child.Merge(parent);
        Assert.NotNull(merged.Colors);
        Assert.Equal("#ff0000", merged.Colors["primary"]);
        Assert.Equal("#ffffff", merged.Colors["background"]);
    }

    [Fact]
    public void Merge_ChildNull_UsesParent()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000" }
        };
        var child = new ThemeTokens();

        var merged = child.Merge(parent);
        Assert.NotNull(merged.Colors);
        Assert.Equal("#000000", merged.Colors["primary"]);
    }

    [Fact]
    public void Merge_ParentNull_UsesChild()
    {
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        };

        var merged = child.Merge(new ThemeTokens());
        Assert.NotNull(merged.Colors);
        Assert.Equal("#ff0000", merged.Colors["primary"]);
    }

    [Fact]
    public void LoadWithInheritance_FlattensAndMergesNestedTokenMaps()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-token-deep-" + Guid.NewGuid().ToString("N"));
        try
        {
            var parentDir = Path.Combine(root, "parent");
            var childDir = Path.Combine(root, "child");
            Directory.CreateDirectory(parentDir);
            Directory.CreateDirectory(childDir);
            File.WriteAllText(Path.Combine(parentDir, "tokens.yaml"), """
                colors:
                  brand:
                    primary: "#000000"
                    secondary: "#333333"
                """);
            File.WriteAllText(Path.Combine(childDir, "tokens.yaml"), """
                colors:
                  brand:
                    primary: "#ff0000"
                """);

            var merged = new ThemeTokensLoader().LoadWithInheritance(childDir, parentDir);

            Assert.NotNull(merged?.Colors);
            Assert.Equal("#ff0000", merged!.Colors["brand.primary"]);
            Assert.Equal("#333333", merged.Colors["brand.secondary"]);
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
    public void DeepMerge_MergesNestedTokensCorrectly()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["brand.primary"] = "#000000",
                ["brand.secondary"] = "#333333",
                ["brand.accent.light"] = "#00ff00",
                ["brand.accent.dark"] = "#00aa00",
            }
        };
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["brand.primary"] = "#ff0000",
                ["brand.accent.light"] = "#aaffaa",
            }
        };

        var merged = child.DeepMerge(parent);

        Assert.NotNull(merged.Colors);
        Assert.Equal("#ff0000", merged.Colors!["brand.primary"]);
        Assert.Equal("#333333", merged.Colors["brand.secondary"]);
        Assert.Equal("#aaffaa", merged.Colors["brand.accent.light"]);
        Assert.Equal("#00aa00", merged.Colors["brand.accent.dark"]);
    }

    [Fact]
    public void DeepMerge_ChildLeafReplacesParentBranch()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["brand.primary"] = "#000000",
                ["brand.secondary"] = "#333333",
            }
        };
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["brand"] = "#ff0000",
            }
        };

        var merged = child.DeepMerge(parent);

        Assert.NotNull(merged.Colors);
        Assert.Single(merged.Colors!);
        Assert.Equal("#ff0000", merged.Colors["brand"]);
    }

    [Fact]
    public void DeepMerge_ShallowMergePreservesFlatBehaviorForNonOverlappingKeys()
    {
        var shallow = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        }.Merge(new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000", ["secondary"] = "#333333" }
        });

        var deep = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        }.DeepMerge(new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000", ["secondary"] = "#333333" }
        });

        Assert.Equal(shallow.Colors, deep.Colors);
    }

    [Fact]
    public void DeepMerge_MergesMultipleTokenGroups()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000" },
            Font = new Dictionary<string, string> { ["size.base"] = "16px", ["size.lg"] = "20px" },
        };
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#fff" },
            Font = new Dictionary<string, string> { ["size.base"] = "18px" },
        };

        var merged = child.DeepMerge(parent);

        Assert.Equal("#fff", merged.Colors!["primary"]);
        Assert.Equal("18px", merged.Font!["size.base"]);
        Assert.Equal("20px", merged.Font["size.lg"]);
    }
}
