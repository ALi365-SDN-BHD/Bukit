namespace Bukit.Engine.Incremental;

internal sealed class TaxonomyContributor : IRenderDependencyContributor
{
    public string Name => "taxonomy";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var taxonomy = context.Config.Taxonomy;
        writer.AppendLabeledCanonicalValue("taxonomy.outputMode", taxonomy.OutputMode);
        writer.AppendLabeledCanonicalValue("taxonomy.pageSize", taxonomy.PageSize);
        writer.AppendLabeledCanonicalValue("taxonomy.indexEnabled", taxonomy.IndexEnabled);
        writer.AppendLabeledCanonicalValue("taxonomy.pinField", taxonomy.PinField);
        writer.AppendLabeledCanonicalValue("taxonomy.pinOrderField", taxonomy.PinOrderField);
        writer.AppendLabeledCanonicalValue(
            "taxonomy.itemFields",
            taxonomy.ItemFields?.OrderBy(x => x, StringComparer.Ordinal).ToList());

        if (taxonomy.PinFieldBySource is { Count: > 0 })
        {
            foreach (var entry in taxonomy.PinFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("taxonomy.pinFieldBySource.key", entry.Key);
                writer.AppendLabeledCanonicalValue("taxonomy.pinFieldBySource.value", entry.Value);
            }
        }

        if (taxonomy.PinOrderFieldBySource is { Count: > 0 })
        {
            foreach (var entry in taxonomy.PinOrderFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("taxonomy.pinOrderFieldBySource.key", entry.Key);
                writer.AppendLabeledCanonicalValue("taxonomy.pinOrderFieldBySource.value", entry.Value);
            }
        }

        if (taxonomy.Kinds is not { Count: > 0 })
        {
            return;
        }

        foreach (var kind in taxonomy.Kinds.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.AppendLabeledCanonicalValue("taxonomy.kind.key", kind.Key);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.kind", kind.Kind);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.title", kind.Title);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.description", kind.Description);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.singularTitlePrefix", kind.SingularTitlePrefix);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.template", kind.Template);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.indexTemplate", kind.IndexTemplate);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.termTemplate", kind.TermTemplate);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.indexEnabled", kind.IndexEnabled);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.hierarchical", kind.Hierarchical);
            writer.AppendLabeledCanonicalValue("taxonomy.kind.routePrefix", kind.RoutePrefix);
        }
    }
}
