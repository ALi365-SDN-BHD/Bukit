namespace Bukit.Shared;

public static class ValueCoercion
{
    public static bool IsTruthy(object? value)
    {
        if (value is null) return false;
        if (value is true) return true;
        if (value is false) return false;

        var s = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return false;

        return s switch
        {
            "true" or "True" or "TRUE" => true,
            "yes" or "Yes" or "YES" => true,
            "1" => true,
            "on" or "On" or "ON" => true,
            _ => false
        };
    }

    public static bool IsFalsy(object? value)
    {
        if (value is null) return true;
        if (value is false) return true;
        if (value is true) return false;

        var s = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return true;

        return s switch
        {
            "false" or "False" or "FALSE" => true,
            "no" or "No" or "NO" => true,
            "0" => true,
            "off" or "Off" or "OFF" => true,
            _ => false
        };
    }

    public static bool? ToBooleanOrNull(object? value)
    {
        if (IsTruthy(value)) return true;
        if (IsFalsy(value)) return false;
        return null;
    }
}
