using System.Text.Json.Nodes;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class MiniProgramConfigTests
{
    private const string Sources = """
        content:
          sources:
            - type: markdown
              name: blog
              markdown:
                dir: content/blog
            - type: markdown
              name: modules
              mode: data
              markdown:
                dir: data/modules
            - type: markdown
              name: settings
              mode: data
              markdown:
                dir: data/settings
              dataIndex:
                scopeField: scope
        """;

    [Fact]
    public void Load_WithoutSection_IsDisabled()
    {
        var config = Load(Sources);

        Assert.False(config.MiniProgram.Enabled);
        Assert.Equal("mp", config.MiniProgram.OutputDir);
        Assert.Empty(config.MiniProgram.Datasets);
    }

    [Fact]
    public void Load_ParsesAllDatasetKinds()
    {
        var config = Load(Sources + """

            miniprogram:
              enabled: true
              outputDir: app-data
              maxListItems: 50
              datasets:
                - name: articles
                  kind: collection
                  source: blog
                  listFields: [title, summary, cover]
                  detailFields: [title, body]
                  types: { cover: image }
                  required: [title]
                - name: banners
                  kind: module
                  source: modules
                  module: banner
                  fields: [title, image, order]
                  sort: { field: order, order: desc }
                - name: settings
                  kind: dataIndex
                  source: settings
                  scope: app
                - name: tags
                  kind: taxonomy
                  taxonomy: tags
                - name: nav
                  kind: menu
                  menu: main
            """, menus: true);

        var mini = config.MiniProgram;
        Assert.True(mini.Enabled);
        Assert.Equal("app-data", mini.OutputDir);
        Assert.Equal(50, mini.MaxListItems);
        Assert.Equal(["articles", "banners", "settings", "tags", "nav"], mini.Datasets.Select(x => x.Name));
        var articles = mini.Datasets[0];
        Assert.Equal(["title", "summary", "cover"], articles.ListFields);
        Assert.Equal("image", articles.Types!["cover"]);
        Assert.Equal(["title"], articles.Required);
        Assert.Equal(new MiniProgramSortConfig("order", true), mini.Datasets[1].Sort);
        Assert.Equal("main", mini.Datasets[4].Menu);
    }

    [Fact]
    public void Load_RejectsUnknownSectionField()
    {
        var ex = Assert.Throws<ConfigException>(() => Load(Sources + "\nminiprogram:\n  enabled: true\n  foo: 1\n"));

        Assert.Contains("miniprogram.foo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsUnknownDatasetField()
    {
        var ex = Assert.Throws<ConfigException>(() => Load(Sources + "\nminiprogram:\n  enabled: true\n  datasets:\n    - { name: x, kind: taxonomy, taxonomy: tags, extra: 1 }\n"));

        Assert.Contains("miniprogram.datasets[0].extra", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ name: a, kind: collection, source: shop, listFields: [title], detailFields: [title] }", "miniprogram.datasets[0].source 'shop'")]
    [InlineData("{ name: a, kind: collection, source: modules, listFields: [title], detailFields: [title] }", "must use mode: content")]
    [InlineData("{ name: a, kind: module, source: blog, module: banner, fields: [title], sort: { field: order } }", "must use mode: data")]
    [InlineData("{ name: a, kind: module, source: modules, module: banner, fields: [title] }", "miniprogram.datasets[0].sort is required")]
    [InlineData("{ name: a, kind: dataIndex, source: modules, scope: app }", "has no dataIndex")]
    [InlineData("{ name: a, kind: menu, menu: footer }", "miniprogram.datasets[0].menu 'footer'")]
    [InlineData("{ name: a, kind: menu, menu: main, source: blog }", "source is not allowed")]
    [InlineData("{ name: Site, kind: taxonomy, taxonomy: tags }", "must match")]
    [InlineData("{ name: site, kind: taxonomy, taxonomy: tags }", "reserved")]
    [InlineData("{ name: a, kind: page }", "kind must be")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [lat], sort: { field: lat }, types: { lat: float } }", "types.lat must be")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [a, a], sort: { field: a } }", "more than once")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [name], sort: { field: name }, required: [phone] }", "miniprogram.datasets[0].required names 'phone'")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [photo], sort: { field: photo }, types: { photos: image } }", "miniprogram.datasets[0].types.photos names 'photos'")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [name], listFields: [phone], sort: { field: name } }", "miniprogram.datasets[0].listFields is not allowed for kind records")]
    [InlineData("{ name: a, kind: collection, source: blog, fields: [title], listFields: [title], detailFields: [title] }", "miniprogram.datasets[0].fields is not allowed for kind collection")]
    [InlineData("{ name: a, kind: taxonomy, taxonomy: tags }", "declare at least one kind: collection dataset")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [body], sort: { field: body }, types: { body: string } }", "types.body is not allowed")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [d], sort: { field: id }, types: { d: { type: string, fields: { x: string } } } }", "types.d.fields is only allowed for type object or object[]")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [d], sort: { field: id }, types: { d: { type: 'object[]', fields: { x: float } } } }", "types.d.fields.x must be")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [d], sort: { field: id }, types: { d: { type: object, extra: 1 } } }", "types.d.extra")]
    [InlineData("{ name: a, kind: records, source: modules, fields: [d], sort: { field: id }, types: { d: { fields: { x: string } } } }", "types.d.type")]
    public void Validate_RejectsInvalidDatasets(string dataset, string expected)
    {
        var ex = Assert.Throws<ConfigException>(() => Load(Sources + $"\nminiprogram:\n  enabled: true\n  datasets:\n    - {dataset}\n", menus: true));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DuplicateNames_Fail()
    {
        var ex = Assert.Throws<ConfigException>(() => Load(Sources + """

            miniprogram:
              enabled: true
              datasets:
                - { name: tags, kind: menu, menu: main }
                - { name: tags, kind: menu, menu: main }
            """, menus: true));

        Assert.Contains("miniprogram.datasets[1].name 'tags'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DisabledSection_SkipsDatasetRules()
    {
        var config = Load(Sources + "\nminiprogram:\n  datasets:\n    - { name: a, kind: menu, menu: missing }\n");

        Assert.False(config.MiniProgram.Enabled);
    }

    [Fact]
    public void Validate_InvalidOutputDir_Fails()
    {
        var ex = Assert.Throws<ConfigException>(() => Load(Sources + "\nminiprogram:\n  enabled: true\n  outputDir: ../escape\n"));

        Assert.Contains("miniprogram.outputDir", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ParsesObjectTypesWithNestedFields()
    {
        var config = Load(Sources + """

            miniprogram:
              enabled: true
              datasets:
                - name: routes
                  kind: records
                  source: modules
                  fields: [title, days, hotel]
                  sort: { field: title }
                  types:
                    hotel: object
                    days:
                      type: object[]
                      fields:
                        title: string
                        sights: string[]
                        photo: image
                        meals: { type: object, fields: { lunch: boolean } }
                - name: settings
                  kind: dataIndex
                  source: settings
                  scope: app
                  types: { contact: object }
            """);

        var routes = config.MiniProgram.Datasets[0];
        Assert.Equal("object[]", routes.Types!["days"]);
        Assert.Equal("object", routes.Types["hotel"]);
        var days = routes.ObjectFields!["days"];
        Assert.Equal(new[] { "meals", "photo", "sights", "title" }, days.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("image", days["photo"].Type);
        Assert.Equal("boolean", days["meals"].Fields!["lunch"].Type);
        Assert.False(routes.ObjectFields.ContainsKey("hotel"));
        Assert.Equal("object", config.MiniProgram.Datasets[1].Types!["contact"]);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("object")]
    public void Load_RejectsObjectsDeeperThanTheLimit(string leaf)
    {
        // A ninth object level, declared with fields or as a bare type.
        var nested = leaf == "object" ? "object" : $"{{ type: object, fields: {{ n: {leaf} }} }}";
        for (var i = 0; i < MiniProgramKinds.MaxObjectDepth; i++)
        {
            nested = $"{{ type: object, fields: {{ n: {nested} }} }}";
        }

        var ex = Assert.Throws<ConfigException>(() => Load(Sources + ObjectDataset(nested)));

        Assert.Contains("exceeds the maximum object nesting depth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AcceptsScalarFieldsInsideTheDeepestObjectLevel()
    {
        // Eight object levels, the eighth declaring a scalar field, as the runtime converter accepts.
        var nested = "{ type: object, fields: { n: string } }";
        for (var i = 1; i < MiniProgramKinds.MaxObjectDepth; i++)
        {
            nested = $"{{ type: object, fields: {{ n: {nested} }} }}";
        }

        var config = Load(Sources + ObjectDataset(nested));

        Assert.Equal("object", config.MiniProgram.Datasets[0].Types!["d"]);
    }

    private static string ObjectDataset(string type)
        => $"\nminiprogram:\n  enabled: true\n  datasets:\n    - {{ name: a, kind: records, source: modules, fields: [d], sort: {{ field: id }}, types: {{ d: {type} }} }}\n";

    [Fact]
    public void Schema_DescribesObjectTypes()
    {
        var schema = JsonNode.Parse(ConfigJsonSchemaGenerator.Generate())!;
        var fieldType = schema["$defs"]!["miniProgramFieldType"]!["oneOf"]!.AsArray();

        Assert.Contains("object[]", fieldType[0]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("#/$defs/miniProgramFieldType", (string)fieldType[1]!["properties"]!["fields"]!["additionalProperties"]!["$ref"]!);
    }

    [Fact]
    public void Schema_DescribesMiniProgramSection()
    {
        var schema = JsonNode.Parse(ConfigJsonSchemaGenerator.Generate())!;
        var section = schema["properties"]!["miniprogram"]!;

        Assert.False(section["additionalProperties"]!.GetValue<bool>());
        var dataset = section["properties"]!["datasets"]!["items"]!;
        Assert.Equal(["name", "kind"], dataset["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("menu", dataset["properties"]!["kind"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Validate_NotionDataBodyWithoutRenderContent_Fails()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig
            {
                Sources = [new ContentSourceConfig { Type = "notion", Name = "faq", Mode = "data", Notion = new NotionConfig { DatabaseId = "db" } }]
            },
            MiniProgram = new MiniProgramConfig
            {
                Enabled = true,
                Datasets = [new MiniProgramDatasetConfig { Name = "faq", Kind = "module", Source = "faq", Module = "faq", Fields = ["title", "body"], Sort = new MiniProgramSortConfig("title", false) }]
            }
        };

        var ex = Assert.Throws<ConfigException>(() => MiniProgramConfigValidator.Validate(config));

        Assert.Contains("notion.renderContent: true", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("http://example.com", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("https://example.com/?ref=a", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("https://example.com/#top", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("https://deploy:token@example.com", DiagnosticCode.ConfigInvalidValue)]
    public void ValidateSiteUrl_RejectsNonHttpsUrl(string url, DiagnosticCode code)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S", Url = url },
            Content = new ContentConfig(),
            MiniProgram = new MiniProgramConfig { Enabled = true }
        };

        var ex = Assert.Throws<ConfigException>(() => MiniProgramConfigValidator.ValidateSiteUrl(config, required: false));

        Assert.Equal(code, ex.Code);
        Assert.DoesNotContain("token", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSiteUrl_MissingUrlFailsOnlyWhenRequired()
    {
        var config = new AppConfig { Site = new SiteConfig { Name = "s", Title = "S" }, Content = new ContentConfig(), MiniProgram = new MiniProgramConfig { Enabled = true } };

        Assert.Null(MiniProgramConfigValidator.ValidateSiteUrl(config, required: false));
        Assert.Throws<ConfigException>(() => MiniProgramConfigValidator.ValidateSiteUrl(config, required: true));
    }

    private static AppConfig Load(string body, bool menus = false)
    {
        var site = menus
            ? """
              site:
                name: myblog
                title: My Blog
                menus:
                  main:
                    - identifier: home
                      name: Home
                      url: /
              """
            : """
              site:
                name: myblog
                title: My Blog
              """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-miniprogram-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, site + "\n" + body);
        try
        {
            var config = ConfigLoader.Load(path);
            ConfigValidator.Validate(config);
            return config;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
