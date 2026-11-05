using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

public static class ImportThemeSelectionService
{
    public static Task<int> SetThemeAsync(
        string name,
        string fullConfigPath,
        string rootDir,
        string? brand,
        string? primaryColor,
        string? accentColor)
    {
        var themesDir = Path.Combine(rootDir, "themes");
        var themeRoot = Path.Combine(themesDir, name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        if (!File.Exists(fullConfigPath))
        {
            Console.Error.WriteLine($"Config not found: {fullConfigPath}");
            return Task.FromResult(2);
        }

        var settings = new List<(string Key, string Value)> { ("name", name) };
        if (!string.IsNullOrWhiteSpace(brand))
        {
            settings.Add(("brand", brand));
            settings.Add(("footer_text", brand));
        }

        if (!string.IsNullOrWhiteSpace(primaryColor))
            settings.Add(("primary_color", primaryColor));
        if (!string.IsNullOrWhiteSpace(accentColor))
            settings.Add(("accent_color", accentColor));

        var text = File.ReadAllText(fullConfigPath);
        if (!TryEditThemeBlock(text, settings, out var updated, out var changed))
        {
            // Flow-style theme mapping: replace only that block, keep the rest of the file.
            updated = RewriteFlowThemeBlock(text, settings);
            changed = true;
            Console.Error.WriteLine("  Note: the theme block used flow style and was rewritten as a block mapping.");
        }

        if (changed)
        {
            if (!IsValidYaml(updated))
            {
                Console.Error.WriteLine($"Refusing to write invalid YAML to {fullConfigPath}.");
                return Task.FromResult(1);
            }

            ImportWriteGuard.WriteAllTextAtomic(fullConfigPath, updated);
        }

        Console.WriteLine($"Theme set: {name}");
        return Task.FromResult(0);
    }

    /// <summary>
    /// Edits only the theme block, line by line, so comments, blank lines and anchors
    /// elsewhere in the config survive. Returns false for flow-style theme mappings.
    /// </summary>
    private static bool TryEditThemeBlock(
        string text,
        IReadOnlyList<(string Key, string Value)> settings,
        out string updated,
        out bool changed)
    {
        updated = text;
        changed = false;

        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

        var themeIndex = FindTopLevelKey(lines, "theme");
        if (themeIndex < 0)
        {
            if (lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            lines.Add("");
            lines.Add("theme:");
            lines.Add($"  name: {FormatScalar(settings.First(setting => setting.Key == "name").Value)}");
            var parameters = settings.Where(setting => setting.Key != "name").ToList();
            if (parameters.Count > 0)
            {
                lines.Add("  params:");
                lines.AddRange(parameters.Select(setting => $"    {setting.Key}: {FormatScalar(setting.Value)}"));
            }

            changed = true;
        }
        else
        {
            var themeLine = lines[themeIndex];
            var colon = themeLine.IndexOf(':');
            var rest = colon < 0 ? "" : themeLine[(colon + 1)..].Trim();
            if (rest.Length > 0 && !rest.StartsWith('#'))
                return false; // theme: { ... }

            var (themeStart, themeEnd) = BlockRange(lines, themeIndex);
            var indent = ChildIndent(lines, themeStart, themeEnd);

            var nameSetting = settings.First(setting => setting.Key == "name");
            changed |= UpsertKey(lines, themeStart, themeEnd, indent, nameSetting.Key, nameSetting.Value, out themeEnd);

            var parameterSettings = settings.Where(setting => setting.Key != "name").ToList();
            if (parameterSettings.Count > 0)
            {
                var paramsIndex = FindChildKey(lines, themeStart, themeEnd, "params", out _, out _);
                if (paramsIndex < 0)
                {
                    var insertAt = EndOfBlock(lines, themeStart, themeEnd);
                    lines.Insert(insertAt, indent + "params:");
                    insertAt++;
                    foreach (var setting in parameterSettings)
                    {
                        lines.Insert(insertAt, indent + "  " + setting.Key + ": " + FormatScalar(setting.Value));
                        insertAt++;
                    }

                    changed = true;
                    themeEnd = insertAt;
                }
                else
                {
                    var (paramsStart, paramsEnd) = BlockRange(lines, paramsIndex);
                    var paramsIndent = ChildIndent(lines, paramsStart, paramsEnd);
                    foreach (var setting in parameterSettings)
                    {
                        changed |= UpsertKey(lines, paramsStart, paramsEnd, paramsIndent,
                            setting.Key, setting.Value, out paramsEnd);
                    }
                }
            }
        }

        if (changed)
            updated = string.Join(newLine, lines);

        return true;
    }

    /// <summary>Upserts "indent + key: value" in [start, end); end is exclusive.</summary>
    private static bool UpsertKey(
        List<string> lines,
        int start,
        int end,
        string indent,
        string key,
        string value,
        out int newEnd)
    {
        var existing = FindChildKey(lines, start, end, key, out _, out var lineIndent);
        if (existing >= 0)
        {
            var line = lines[existing];
            var colon = line.IndexOf(':');
            var rest = line[(colon + 1)..];
            var comment = TrailingComment(rest);
            var current = StripComment(rest).Trim().Trim('"', '\'');

            if (current == value)
            {
                newEnd = end;
                return false;
            }

            lines[existing] = line[..(colon + 1)] + " " + FormatScalar(value) +
                              (comment.Length > 0 ? " " + comment : "");
            newEnd = end;
            return true;
        }

        var insertAt = EndOfBlock(lines, start, end);
        lines.Insert(insertAt, lineIndent + key + ": " + FormatScalar(value));
        newEnd = insertAt + 1;
        return true;
    }

    private static int FindTopLevelKey(List<string> lines, string key)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith('#'))
                continue;

            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim() == key)
                return i;
        }

        return -1;
    }

    /// <summary>Start is the key line; End is the first line that is not part of the block.</summary>
    private static (int Start, int End) BlockRange(List<string> lines, int keyIndex)
    {
        var end = keyIndex + 1;
        while (end < lines.Count)
        {
            var line = lines[end];
            if (line.Length == 0)
            {
                end++;
                continue;
            }

            if (!char.IsWhiteSpace(line[0]))
                break;

            end++;
        }

        return (keyIndex + 1, end);
    }

    /// <summary>Index just past the last non-empty line of the block.</summary>
    private static int EndOfBlock(List<string> lines, int start, int end)
    {
        var index = end - 1;
        while (index >= start && string.IsNullOrWhiteSpace(lines[index]))
            index--;
        return index + 1;
    }

    private static int FindChildKey(
        List<string> lines,
        int start,
        int end,
        string key,
        out int keyIndex,
        out string indent)
    {
        keyIndex = -1;
        var directIndent = ChildIndent(lines, start, end);
        indent = directIndent;
        for (var i = start; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var trimmed = line.TrimStart();
            var lineIndent = line[..(line.Length - trimmed.Length)];
            // Only direct children count; a nested "name" under params is a different key.
            if (!string.Equals(lineIndent, directIndent, StringComparison.Ordinal))
                continue;
            if (!trimmed.StartsWith(key + ":", StringComparison.Ordinal))
                continue;

            keyIndex = i;
            indent = lineIndent;
            return i;
        }

        return -1;
    }

    private static string ChildIndent(List<string> lines, int start, int end)
    {
        foreach (var line in lines.Skip(start).Take(end - start))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            return line[..(line.Length - line.TrimStart().Length)];
        }

        return "  ";
    }

    private static string TrailingComment(string remainder)
    {
        for (var i = 0; i < remainder.Length; i++)
        {
            if (remainder[i] != '#')
                continue;
            if (i == 0 || char.IsWhiteSpace(remainder[i - 1]))
                return remainder[i..].TrimEnd();
        }

        return "";
    }

    private static string StripComment(string remainder)
    {
        var comment = TrailingComment(remainder);
        return comment.Length == 0 ? remainder : remainder[..remainder.IndexOf(comment, StringComparison.Ordinal)];
    }

    private static string FormatScalar(string value)
    {
        var plain = value.Length > 0 &&
                    value.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '/') &&
                    !IsYamlKeyword(value);
        if (plain)
            return value;

        return "'" + value.Replace("'", "''") + "'";
    }

    private static bool IsYamlKeyword(string value)
        => value.ToLowerInvariant() is "true" or "false" or "null" or "~" or "yes" or "no" or "on" or "off";

    private static bool IsValidYaml(string text)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            return stream.Documents.Count > 0 &&
                   stream.Documents[0].RootNode is YamlMappingNode;
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces a one-line "theme: { ... }" mapping with a block mapping. Comments and
    /// content outside the theme block are kept as they are.
    /// </summary>
    private static string RewriteFlowThemeBlock(
        string text,
        IReadOnlyList<(string Key, string Value)> settings)
    {
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n').ToList();
        var themeIndex = FindTopLevelKey(lines, "theme");
        if (themeIndex < 0)
            return text;

        var (_, themeEnd) = BlockRange(lines, themeIndex);

        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            root.Children[new YamlScalarNode("theme")] is not YamlMappingNode themeNode)
        {
            return text;
        }

        foreach (var (key, value) in settings)
        {
            if (key is "brand" or "footer_text" or "primary_color" or "accent_color")
            {
                GetOrCreateMapping(themeNode, "params").Children[new YamlScalarNode(key)] = new YamlScalarNode(value);
            }
            else
            {
                themeNode.Children[new YamlScalarNode(key)] = new YamlScalarNode(value);
            }
        }

        var wrapper = new YamlMappingNode();
        wrapper.Children[new YamlScalarNode("theme")] = themeNode;
        var document = new YamlStream(new YamlDocument(wrapper));
        using var writer = new StringWriter();
        document.Save(writer, assignAnchors: false);
        var block = writer.ToString()
            .Replace("\r\n", "\n")
            .TrimEnd('\n')
            .Split('\n')
            .ToList();

        static string TrailingComment(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
                    return line[i..].TrimEnd();
            }

            return "";
        }

        var original = lines[themeIndex];
        var comment = original.IndexOf(':') is var colon && colon >= 0
            ? TrailingComment(original[(colon + 1)..])
            : "";
        if (comment.Length > 0)
            block[0] = block[0] + " " + comment;

        lines.RemoveRange(themeIndex, themeEnd - themeIndex);
        lines.InsertRange(themeIndex, block);
        return string.Join(newLine, lines);
    }

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlMappingNode map)
        {
            return map;
        }

        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }
}
