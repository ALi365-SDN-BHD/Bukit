using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class SiteDefaultsApplier
{
    internal static DeployConfig? ReadDeployConfig(YamlMappingNode? deployNode)
    {
        if (deployNode is null)
        {
            return null;
        }

        return new DeployConfig
        {
            Provider = ConfigYamlHelpers.GetOptionalString(deployNode, "provider"),
            Branch = ConfigYamlHelpers.GetOptionalString(deployNode, "branch") ?? "gh-pages",
            Message = ConfigYamlHelpers.GetOptionalString(deployNode, "message") ?? "bukit deploy",
            Cname = ConfigYamlHelpers.GetOptionalString(deployNode, "cname"),
            KeepHistory = ConfigYamlHelpers.GetOptionalBool(deployNode, "keepHistory") ?? false
        };
    }

    internal static TaxonomyConfig ReadTaxonomyConfig(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return new TaxonomyConfig();
        }

        return new TaxonomyConfig
        {
            Kinds = ReadTaxonomyKinds(taxonomyNode),
            OutputMode = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "outputMode") ?? "both",
            ItemFields = ConfigYamlHelpers.ReadStringList(taxonomyNode, "itemFields"),
            PageSize = ConfigYamlHelpers.GetOptionalIntStrict(taxonomyNode, "pageSize") ?? 10,
            IndexEnabled = ConfigYamlHelpers.GetOptionalBool(taxonomyNode, "indexEnabled") ?? true,
            PinField = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "pinField") ?? "pinned",
            PinOrderField = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "pinOrderField"),
            PinFieldBySource = ConfigYamlHelpers.ReadStringMap(taxonomyNode, "pinFieldBySource"),
            PinOrderFieldBySource = ConfigYamlHelpers.ReadStringMap(taxonomyNode, "pinOrderFieldBySource")
        };
    }

    internal static IReadOnlyList<TaxonomyKindConfig>? ReadTaxonomyKinds(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return null;
        }

        var kindsNode = ConfigYamlHelpers.GetOptionalSequence(taxonomyNode, "kinds");
        if (kindsNode is null)
        {
            return null;
        }

        var kinds = new List<TaxonomyKindConfig>();
        foreach (var n in kindsNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("taxonomy.kinds items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            kinds.Add(new TaxonomyKindConfig
            {
                Key = ConfigYamlHelpers.GetRequiredString(m, "key"),
                Kind = ConfigYamlHelpers.GetOptionalString(m, "kind"),
                Title = ConfigYamlHelpers.GetOptionalString(m, "title"),
                Description = ConfigYamlHelpers.GetOptionalString(m, "description"),
                SingularTitlePrefix = ConfigYamlHelpers.GetOptionalString(m, "singularTitlePrefix"),
                Template = ConfigYamlHelpers.GetOptionalString(m, "template"),
                IndexTemplate = ConfigYamlHelpers.GetOptionalString(m, "indexTemplate"),
                TermTemplate = ConfigYamlHelpers.GetOptionalString(m, "termTemplate"),
                IndexEnabled = ConfigYamlHelpers.GetOptionalBool(m, "indexEnabled"),
                Hierarchical = ConfigYamlHelpers.GetOptionalBool(m, "hierarchical") ?? false,
                RoutePrefix = ConfigYamlHelpers.GetOptionalString(m, "routePrefix")
            });
        }

        return kinds;
    }
}
