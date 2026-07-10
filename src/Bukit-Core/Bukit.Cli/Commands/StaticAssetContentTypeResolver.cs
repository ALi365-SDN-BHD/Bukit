namespace Bukit.Cli.Commands;

internal static class StaticAssetContentTypeResolver
{
    internal static string ResolvePath(string path)
        => ResolveExtension(Path.GetExtension(path));

    internal static string ResolveExtension(string? extension)
        => (extension ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" or ".map" => "application/json; charset=utf-8",
            ".xml" => "application/xml; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            ".webmanifest" => "application/manifest+json; charset=utf-8",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
}
