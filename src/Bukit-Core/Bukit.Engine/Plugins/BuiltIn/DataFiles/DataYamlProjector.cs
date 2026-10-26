using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Bukit.Engine.Plugins.BuiltIn.DataFiles;

/// <summary>
/// Projects the first document of a YAML data file into dictionaries and lists,
/// resolving anchors and charging every node against the budgets.
/// </summary>
internal static class DataYamlProjector
{
    internal static object? Parse(
        TextReader content,
        DocumentTraversalState traversal)
    {
        var parser = new Parser(content);
        if (!parser.MoveNext() || parser.Current is not StreamStart)
        {
            throw new InvalidDataException("YAML stream start was not found.");
        }

        if (!parser.MoveNext()) throw new InvalidDataException("Unexpected end of YAML stream.");
        object? firstDocument = null;
        var hasDocument = false;
        while (parser.Current is not StreamEnd)
        {
            if (parser.Current is not DocumentStart)
            {
                throw new InvalidDataException("YAML document start was not found.");
            }

            MoveNextYaml(parser);
            var anchors = new Dictionary<string, object?>(StringComparer.Ordinal);
            var document = ParseYamlNode(
                parser,
                traversal,
                depth: 0,
                project: !hasDocument,
                anchors);
            if (parser.Current is not DocumentEnd)
            {
                throw new InvalidDataException("YAML document end was not found.");
            }

            if (!hasDocument)
            {
                firstDocument = document;
                hasDocument = true;
            }

            MoveNextYaml(parser);
        }

        return firstDocument;
    }

    private static object? ParseYamlNode(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        bool project,
        Dictionary<string, object?> anchors)
    {
        switch (parser.Current)
        {
            case MappingStart mappingStart:
                {
                    traversal.Visit(depth);
                    var result = project
                        ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        : null;
                    MoveNextYaml(parser);
                    while (parser.Current is not MappingEnd)
                    {
                        var key = ParseYamlKey(parser, traversal, depth + 1, anchors);
                        if (project)
                        {
                            traversal.VisitResultEntry();
                            traversal.VisitResultString(key.Length);
                        }

                        var value = ParseYamlNode(parser, traversal, depth + 1, project, anchors);
                        result?.Add(key, value ?? string.Empty);
                    }

                    MoveNextYaml(parser);
                    RegisterYamlAnchor(mappingStart, result, anchors);
                    return result;
                }
            case SequenceStart sequenceStart:
                {
                    traversal.Visit(depth);
                    var result = project ? new List<object>() : null;
                    MoveNextYaml(parser);
                    while (parser.Current is not SequenceEnd)
                    {
                        if (project) traversal.VisitResultEntry();
                        var value = ParseYamlNode(parser, traversal, depth + 1, project, anchors);
                        result?.Add(value ?? string.Empty);
                    }

                    MoveNextYaml(parser);
                    RegisterYamlAnchor(sequenceStart, result, anchors);
                    return result;
                }
            case Scalar scalar:
                {
                    traversal.Visit(depth);
                    traversal.VisitScalar(scalar.Value?.Length ?? 0);
                    var value = project ? ConvertYamlScalar(scalar.Value, traversal) : null;
                    RegisterYamlAnchor(scalar, value, anchors);
                    MoveNextYaml(parser);
                    return value;
                }
            case AnchorAlias alias:
                {
                    traversal.Visit(depth);
                    var anchor = alias.Value.Value;
                    if (!anchors.TryGetValue(anchor, out var value))
                    {
                        throw new AnchorNotFoundException($"Anchor '{anchor}' was not found.");
                    }

                    MoveNextYaml(parser);
                    return project ? CloneYamlProjection(value, traversal) : null;
                }
            default:
                throw new InvalidDataException($"Unexpected YAML event {parser.Current?.GetType().Name}.");
        }
    }

    private static string ParseYamlKey(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        if (parser.Current is Scalar scalar)
        {
            traversal.Visit(depth);
            var key = scalar.Value ?? string.Empty;
            traversal.VisitScalar(key.Length);
            RegisterYamlAnchor(scalar, key, anchors);
            MoveNextYaml(parser);
            return key;
        }

        if (parser.Current is AnchorAlias alias)
        {
            traversal.Visit(depth);
            var anchor = alias.Value.Value;
            if (!anchors.TryGetValue(anchor, out var value))
            {
                throw new AnchorNotFoundException($"Anchor '{anchor}' was not found.");
            }

            MoveNextYaml(parser);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return ParseYamlComplexKey(parser, traversal, depth, anchors);
    }

    private static string ParseYamlComplexKey(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        var result = new StringBuilder();
        AppendYamlDisplayNode(result, parser, traversal, depth, anchors);
        return result.ToString();
    }

    private static void AppendYamlDisplayNode(
        StringBuilder result,
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        switch (parser.Current)
        {
            case Scalar scalar:
                traversal.Visit(depth);
                var scalarText = scalar.Value ?? string.Empty;
                traversal.VisitScalar(scalarText.Length);
                result.Append(scalarText);
                RegisterYamlAnchor(scalar, scalarText, anchors);
                MoveNextYaml(parser);
                return;
            case SequenceStart sequenceStart:
                traversal.Visit(depth);
                result.Append("[ ");
                MoveNextYaml(parser);
                var firstItem = true;
                while (parser.Current is not SequenceEnd)
                {
                    if (!firstItem) result.Append(", ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    firstItem = false;
                }

                result.Append(" ]");
                MoveNextYaml(parser);
                if (!sequenceStart.Anchor.IsEmpty)
                {
                    anchors.Add(sequenceStart.Anchor.Value, result.ToString());
                }

                return;
            case MappingStart mappingStart:
                traversal.Visit(depth);
                result.Append("{ ");
                MoveNextYaml(parser);
                var firstPair = true;
                while (parser.Current is not MappingEnd)
                {
                    if (!firstPair) result.Append(", ");
                    result.Append("{ ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    result.Append(", ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    result.Append(" }");
                    firstPair = false;
                }

                result.Append(" }");
                MoveNextYaml(parser);
                if (!mappingStart.Anchor.IsEmpty)
                {
                    anchors.Add(mappingStart.Anchor.Value, result.ToString());
                }

                return;
            case AnchorAlias alias:
                traversal.Visit(depth);
                if (!anchors.TryGetValue(alias.Value.Value, out var value))
                {
                    throw new AnchorNotFoundException($"Anchor '{alias.Value.Value}' was not found.");
                }

                AppendYamlDisplayValue(result, value);
                MoveNextYaml(parser);
                return;
            default:
                throw new InvalidDataException($"Unexpected YAML event {parser.Current?.GetType().Name}.");
        }
    }

    private static void AppendYamlDisplayValue(StringBuilder result, object? value)
    {
        switch (value)
        {
            case Dictionary<string, object> map:
                result.Append("{ ");
                var firstPair = true;
                foreach (var (key, item) in map)
                {
                    if (!firstPair) result.Append(", ");
                    result.Append("{ ").Append(key).Append(", ");
                    AppendYamlDisplayValue(result, item);
                    result.Append(" }");
                    firstPair = false;
                }

                result.Append(" }");
                break;
            case List<object> list:
                result.Append("[ ");
                for (var index = 0; index < list.Count; index++)
                {
                    if (index > 0) result.Append(", ");
                    AppendYamlDisplayValue(result, list[index]);
                }

                result.Append(" ]");
                break;
            default:
                result.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void RegisterYamlAnchor(
        NodeEvent node,
        object? value,
        Dictionary<string, object?> anchors)
    {
        if (!node.Anchor.IsEmpty) anchors.Add(node.Anchor.Value, value);
    }

    private static object? CloneYamlProjection(
        object? value,
        DocumentTraversalState traversal)
    {
        switch (value)
        {
            case Dictionary<string, object> map:
                var mapClone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, item) in map)
                {
                    traversal.VisitResultEntry();
                    traversal.VisitResultString(key.Length);
                    mapClone.Add(key, CloneYamlProjection(item, traversal) ?? string.Empty);
                }

                return mapClone;
            case List<object> list:
                var listClone = new List<object>(list.Count);
                foreach (var item in list)
                {
                    traversal.VisitResultEntry();
                    listClone.Add(CloneYamlProjection(item, traversal) ?? string.Empty);
                }

                return listClone;
            case string text:
                return traversal.TrackResultString(text);
            default:
                return value;
        }
    }

    private static void MoveNextYaml(Parser parser)
    {
        if (!parser.MoveNext())
        {
            throw new InvalidDataException("Unexpected end of YAML stream.");
        }
    }

    private static object? ConvertYamlScalar(
        string? value,
        DocumentTraversalState traversal)
    {
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return traversal.TrackResultString(value);
    }
}
