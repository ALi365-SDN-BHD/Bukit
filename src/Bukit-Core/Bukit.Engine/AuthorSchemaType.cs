namespace Bukit.Engine;

internal static class AuthorSchemaType
{
    internal const string Person = "Person";
    internal const string Organization = "Organization";

    internal static bool IsValid(string? value) => Normalize(value) is not null;

    internal static string? Resolve(string? author, string? declaredType)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(declaredType)
            ? Person
            : Normalize(declaredType);
    }

    internal static string? Normalize(string? value)
    {
        if (string.Equals(value?.Trim(), Person, StringComparison.OrdinalIgnoreCase))
        {
            return Person;
        }

        if (string.Equals(value?.Trim(), Organization, StringComparison.OrdinalIgnoreCase))
        {
            return Organization;
        }

        return null;
    }
}
