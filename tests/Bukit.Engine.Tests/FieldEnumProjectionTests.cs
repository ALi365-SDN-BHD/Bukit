using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class FieldEnumProjectionTests
{
    [Fact]
    public void Build_ListsOptionsInOrderWithLabelFallbackAndScopes()
    {
        var schema = new ContentModelSchemaConfig
        {
            CustomFields =
            [
                new CustomFieldDefinitionConfig
                {
                    Name = "audience",
                    Enum = ["family", "couple"],
                    EnumLabels = new Dictionary<string, string> { ["family"] = "亲子" }
                },
                new CustomFieldDefinitionConfig { Name = "plain" }
            ],
            FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>
            {
                ["route"] = [new CustomFieldDefinitionConfig { Name = "pace", Enum = ["slow"] }]
            }
        };

        var result = FieldEnumProjection.Build(schema)!;

        var audience = ((List<object>)result["audience"]).Cast<IDictionary<string, object?>>().ToList();
        Assert.Equal([("family", "亲子"), ("couple", "couple")], audience.Select(option => ((string)option["value"]!, (string)option["label"]!)));
        Assert.False(result.ContainsKey("plain"));
        var scopes = (Dictionary<string, object>)result[FieldEnumProjection.ScopesKey];
        var pace = (Dictionary<string, object>)scopes["route"];
        Assert.Single((List<object>)pace["pace"]);
    }

    [Fact]
    public void Build_NoEnums_ReturnsNull()
    {
        Assert.Null(FieldEnumProjection.Build(null));
        Assert.Null(FieldEnumProjection.Build(new ContentModelSchemaConfig { CustomFields = [new CustomFieldDefinitionConfig { Name = "a" }] }));
    }
}
