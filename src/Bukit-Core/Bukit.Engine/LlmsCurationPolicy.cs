using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal enum LlmsVisibility
{
    Auto,
    Include,
    Exclude
}

internal enum LlmsTier
{
    Primary,
    Optional
}

internal sealed record LlmsCurationPolicy(
    LlmsVisibility Visibility,
    LlmsTier Tier,
    int Priority)
{
    internal static readonly LlmsCurationPolicy Default =
        new(LlmsVisibility.Auto, LlmsTier.Primary, 0);
}

internal sealed record LlmsCurationParseResult(
    bool Valid,
    LlmsCurationPolicy Policy,
    IReadOnlyList<string> ErrorCodes);

internal static class LlmsCurationPolicyParser
{
    internal const int PriorityMinimum = -100;
    internal const int PriorityMaximum = 100;

    private static readonly string[] KnownFields = ["visibility", "tier", "priority"];

    internal static LlmsCurationParseResult Parse(ContentDocument document)
        => Parse(document.CustomFields);

    internal static LlmsCurationParseResult Parse(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (!ContentFieldReader.TryGetField(fields, "geo", out var geoField) ||
            geoField.Value is not IReadOnlyDictionary<string, object> geo ||
            !geo.TryGetValue("llms", out var llmsValue) ||
            llmsValue is not IReadOnlyDictionary<string, object> llms)
        {
            return new LlmsCurationParseResult(true, LlmsCurationPolicy.Default, Array.Empty<string>());
        }

        var errors = new List<string>();

        foreach (var key in llms.Keys)
        {
            if (!KnownFields.Contains(key, StringComparer.Ordinal))
            {
                errors.Add("geo.llms_field_unknown");
            }
        }

        var visibility = LlmsVisibility.Auto;
        var visibilityValue = ReadString(llms, "visibility");
        if (visibilityValue is not null)
        {
            visibility = visibilityValue switch
            {
                "auto" => LlmsVisibility.Auto,
                "include" => LlmsVisibility.Include,
                "exclude" => LlmsVisibility.Exclude,
                _ => LlmsVisibility.Auto
            };
            if (visibility == LlmsVisibility.Auto &&
                !string.Equals(visibilityValue, "auto", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("geo.llms_visibility_invalid");
            }
        }

        var tier = LlmsTier.Primary;
        var tierValue = ReadString(llms, "tier");
        if (tierValue is not null)
        {
            tier = tierValue switch
            {
                "primary" => LlmsTier.Primary,
                "optional" => LlmsTier.Optional,
                _ => LlmsTier.Primary
            };
            if (tier == LlmsTier.Primary &&
                !string.Equals(tierValue, "primary", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("geo.llms_tier_invalid");
            }
        }

        var priority = 0;
        if (llms.TryGetValue("priority", out var priorityValue) && priorityValue is not null)
        {
            if (TryReadPriority(priorityValue, out var parsed))
            {
                if (parsed < PriorityMinimum || parsed > PriorityMaximum)
                {
                    errors.Add("geo.llms_priority_invalid");
                }
                else
                {
                    priority = parsed;
                }
            }
            else
            {
                errors.Add("geo.llms_priority_invalid");
            }
        }

        if (errors.Count > 0)
        {
            return new LlmsCurationParseResult(false, LlmsCurationPolicy.Default, errors.ToArray());
        }

        return new LlmsCurationParseResult(true, new LlmsCurationPolicy(visibility, tier, priority), Array.Empty<string>());
    }

    private static bool TryReadPriority(object value, out int priority)
    {
        switch (value)
        {
            case int i:
                priority = i;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                priority = (int)l;
                return true;
            case short s:
                priority = s;
                return true;
            default:
                priority = 0;
                return false;
        }
    }

    private static string? ReadString(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.ToLowerInvariant();
    }
}
