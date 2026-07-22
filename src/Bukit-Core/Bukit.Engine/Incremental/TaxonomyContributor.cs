namespace Bukit.Engine.Incremental;

internal sealed class TaxonomyContributor : IRenderDependencyContributor
{
    public string Name => "taxonomy";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var taxonomy = context.Config.Taxonomy;
        writer.AppendNewline();
        writer.AppendUtf8(taxonomy.OutputMode);
        writer.AppendNewline();
        writer.AppendUtf8(taxonomy.PageSize.ToString());
        writer.AppendNewline();
        writer.AppendUtf8(taxonomy.IndexEnabled.ToString());
        writer.AppendNewline();
        writer.AppendUtf8(taxonomy.PinField);
        writer.AppendNewline();
        writer.AppendUtf8(taxonomy.PinOrderField);
        if (taxonomy.ItemFields is { Count: > 0 })
        {
            foreach (var field in taxonomy.ItemFields.OrderBy(x => x, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(field);
            }
        }

        if (taxonomy.PinFieldBySource is { Count: > 0 })
        {
            foreach (var entry in taxonomy.PinFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(entry.Key);
                writer.AppendNewline();
                writer.AppendUtf8(entry.Value);
            }
        }

        if (taxonomy.PinOrderFieldBySource is { Count: > 0 })
        {
            foreach (var entry in taxonomy.PinOrderFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(entry.Key);
                writer.AppendNewline();
                writer.AppendUtf8(entry.Value);
            }
        }

        if (taxonomy.Kinds is not { Count: > 0 })
        {
            return;
        }

        foreach (var kind in taxonomy.Kinds.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.AppendNewline();
            writer.AppendUtf8(kind.Key);
            writer.AppendNewline();
            writer.AppendUtf8(kind.Kind);
            writer.AppendNewline();
            writer.AppendUtf8(kind.Title);
            writer.AppendNewline();
            writer.AppendUtf8(kind.Description);
            writer.AppendNewline();
            writer.AppendUtf8(kind.SingularTitlePrefix);
            writer.AppendNewline();
            writer.AppendUtf8(kind.Template);
            writer.AppendNewline();
            writer.AppendUtf8(kind.IndexTemplate);
            writer.AppendNewline();
            writer.AppendUtf8(kind.TermTemplate);
            writer.AppendNewline();
            writer.AppendUtf8(kind.IndexEnabled?.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(kind.Hierarchical.ToString());
            writer.AppendNewline();
            writer.AppendUtf8(kind.RoutePrefix);
        }
    }
}
