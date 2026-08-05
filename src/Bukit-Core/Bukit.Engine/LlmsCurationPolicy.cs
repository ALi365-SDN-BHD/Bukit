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
        if (!ContentFieldReader.TryGetField(fields, "geo", out var geoField))
        {
            return new LlmsCurationParseResult(true, LlmsCurationPolicy.Default, Array.Empty<string>());
        }

        if (geoField.Value is not IReadOnlyDictionary<string, object> geo)
        {
            return InvalidShape();
        }

        if (!geo.TryGetValue("llms", out var llmsValue))
        {
            return new LlmsCurationParseResult(true, LlmsCurationPolicy.Default, Array.Empty<string>());
        }

        if (llmsValue is not IReadOnlyDictionary<string, object> llms)
        {
            return InvalidShape();
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
        if (llms.TryGetValue("visibility", out var rawVisibility))
        {
            if (!TryReadDeclaredString(rawVisibility, out var visibilityValue))
            {
                errors.Add("geo.llms_visibility_invalid");
            }
            else
            {
                visibility = visibilityValue switch
                {
                    "auto" => LlmsVisibility.Auto,
                    "include" => LlmsVisibility.Include,
                    "exclude" => LlmsVisibility.Exclude,
                    _ => LlmsVisibility.Auto
                };
                if (visibility == LlmsVisibility.Auto && visibilityValue != "auto")
                {
                    errors.Add("geo.llms_visibility_invalid");
                }
            }
        }

        var tier = LlmsTier.Primary;
        if (llms.TryGetValue("tier", out var rawTier))
        {
            if (!TryReadDeclaredString(rawTier, out var tierValue))
            {
                errors.Add("geo.llms_tier_invalid");
            }
            else
            {
                tier = tierValue switch
                {
                    "primary" => LlmsTier.Primary,
                    "optional" => LlmsTier.Optional,
                    _ => LlmsTier.Primary
                };
                if (tier == LlmsTier.Primary && tierValue != "primary")
                {
                    errors.Add("geo.llms_tier_invalid");
                }
            }
        }

        var priority = 0;
        if (llms.TryGetValue("priority", out var priorityValue))
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

    private static LlmsCurationParseResult InvalidShape()
        => new(false, LlmsCurationPolicy.Default, ["geo.llms_field_unknown"]);

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

    private static bool TryReadDeclaredString(object? value, out string normalized)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = text.Trim().ToLowerInvariant();
        return true;
    }
}
