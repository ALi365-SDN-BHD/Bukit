using System.Security;
using System.Text.RegularExpressions;
using Bukit.Cli.Commands.SeoAuthorityInsights;
using Bukit.Cli.Commands.SeoInsights;
using Bukit.Cli.Shared.Cli.Binding;

namespace Bukit.Cli.Commands;

internal static partial class SeoAuthorityInsightsCommand
{
    private const int MaximumObservationFiles = 10;
    private static readonly StringComparer PathIdentityComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    internal static int Run(CliBoundCommand command)
    {
        try
        {
            if (command.GetArgument(1) is not null)
            {
                throw Failure("usage_invalid");
            }

            var outputDirectory = ResolveLocalPath(command.GetString("--dir") ?? "dist", "dir_invalid");
            var routeMapPath = ResolveLocalPath(
                command.GetString("--routes") ?? Path.Combine(outputDirectory, ".bukit", "seo-route-map.json"),
                "routes_path_invalid");
            var rulePath = ResolveRequiredLocalPath(command.GetString("--rules"), "rules_required", "rules_path_invalid");
            var observationPaths = ResolveObservationPaths(command.GetString("--observations"));
            var outputPath = ResolveLocalPath(
                command.GetString("--out") ?? Path.Combine(outputDirectory, ".bukit", ExternalAuthorityReportWriter.FileName),
                "output_path_invalid");

            RejectOutputConflict(outputPath, routeMapPath, rulePath, observationPaths);

            var ruleProfile = ReadRuleProfile(rulePath);
            var datasets = observationPaths
                .Select(path => (path, ExternalAuthorityObservationReader.Read(path)))
                .ToArray();
            var options = new SeoObservationUrlOptions(
                ruleProfile.SiteHost,
                new HashSet<string>(ruleProfile.HostAliases, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(ruleProfile.IgnoredQueryParameters, StringComparer.OrdinalIgnoreCase));
            var report = ExternalAuthorityReportWriter.Assemble(
                routeMapPath,
                datasets,
                options,
                DateTimeOffset.UtcNow);

            WriteReport(outputPath, report, routeMapPath, rulePath, observationPaths);

            var counts = report.JoinQuality;
            var unmatchedCount = report.UnmatchedCitedUrls.Count;
            var ambiguousCount = report.AmbiguousCitedUrls.Count;
            var hasJoinGaps = unmatchedCount != 0 || ambiguousCount != 0;
            var strictJoinFailed = command.GetBool("--strict-join") && hasJoinGaps;
            var classification = strictJoinFailed
                ? "strict-join-failed"
                : hasJoinGaps ? "join-gaps-allowed" : "complete";

            Console.WriteLine(
                $"SEO authority insights: sourceRows={counts.SourceRows} matched={counts.MatchedRows} unmatched={unmatchedCount} ambiguous={ambiguousCount}");
            Console.WriteLine($"SEO authority insights report: {outputPath}");
            Console.WriteLine($"SEO authority insights classification: {classification}");
            return strictJoinFailed ? 1 : 0;
        }
        catch (SeoAuthorityInsightsCommandException exception)
        {
            Console.Error.WriteLine($"SEO authority insights failed: {exception.Code}.");
            return 2;
        }
        catch (SeoRouteMapReader.RouteMapDataException exception)
        {
            Console.Error.WriteLine($"SEO authority insights failed: {exception.Code}.");
            return 2;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"SEO authority insights failed: {StableDataCode(exception)}.");
            return 2;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("SEO authority insights failed: input_unavailable.");
            return 2;
        }
    }

    private static SeoInsightsRuleProfile ReadRuleProfile(string path)
    {
        try
        {
            return SeoInsightsRuleProfileReader.Read(path);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure("rules_unavailable");
        }
    }

    private static IReadOnlyList<string> ResolveObservationPaths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Failure("observations_required");
        }

        var entries = value.Split(',', StringSplitOptions.None);
        if (entries.Length is < 1 or > MaximumObservationFiles)
        {
            throw Failure("observations_count_invalid");
        }

        var unique = new HashSet<string>(PathIdentityComparer);
        var paths = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                throw Failure("observations_list_invalid");
            }

            var path = ResolveLocalPath(trimmed, "observations_path_invalid");
            var semanticPath = ResolveCanonicalPathIdentity(path);
            if (!unique.Add(semanticPath))
            {
                throw Failure("observations_duplicate");
            }

            paths.Add(path);
        }

        return paths;
    }

    private static string ResolveRequiredLocalPath(string? value, string requiredCode, string invalidCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Failure(requiredCode);
        }

        return ResolveLocalPath(value, invalidCode);
    }

    private static string ResolveLocalPath(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || IsUriOrNetworkPath(value))
        {
            throw Failure(code);
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or SecurityException or IOException)
        {
            throw Failure(code);
        }
    }

    private static string ResolveCanonicalPathIdentity(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                throw Failure("path_identity_unavailable");
            }

            var components = fullPath[root.Length..].Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var index = 0; index < components.Length; index++)
            {
                var candidate = Path.Combine(current, components[index]);
                var existing = ExistingFileSystemInfo(candidate);
                if (existing is null)
                {
                    for (; index < components.Length; index++)
                    {
                        current = Path.Combine(current, components[index]);
                    }

                    return Path.GetFullPath(current);
                }

                var resolved = existing.ResolveLinkTarget(returnFinalTarget: true);
                current = resolved is null
                    ? candidate
                    : ResolveCanonicalPathIdentity(resolved.FullName);
            }

            return Path.GetFullPath(current);
        }
        catch (SeoAuthorityInsightsCommandException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            throw Failure("path_identity_unavailable");
        }
    }

    private static FileSystemInfo? ExistingFileSystemInfo(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        if (File.Exists(path))
        {
            return new FileInfo(path);
        }

        var probe = new FileInfo(path);
        if (probe.LinkTarget is not null)
        {
            throw Failure("path_identity_unavailable");
        }

        return null;
    }

    private static bool IsUriOrNetworkPath(string path)
    {
        if (path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\')
        {
            return false;
        }

        return UriSchemeRegex().IsMatch(path);
    }

    private static void RejectOutputConflict(
        string outputPath,
        string routeMapPath,
        string rulePath,
        IReadOnlyList<string> observationPaths)
    {
        var outputSemanticPath = ResolveCanonicalPathIdentity(outputPath);
        if (PathIdentityComparer.Equals(outputSemanticPath, ResolveCanonicalPathIdentity(routeMapPath)) ||
            PathIdentityComparer.Equals(outputSemanticPath, ResolveCanonicalPathIdentity(rulePath)) ||
            observationPaths.Any(path => PathIdentityComparer.Equals(outputSemanticPath, ResolveCanonicalPathIdentity(path))))
        {
            throw Failure("output_conflict");
        }
    }

    private static void WriteReport(
        string outputPath,
        ExternalAuthorityReport report,
        string routeMapPath,
        string rulePath,
        IReadOnlyList<string> observationPaths)
    {
        var parent = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw Failure("output_path_invalid");
        }

        var stagingRoot = Path.Combine(parent, $".seo-authority-insights-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(parent);
            ExternalAuthorityReportWriter.Write(stagingRoot, report);
            var stagedReport = Path.Combine(stagingRoot, ".bukit", ExternalAuthorityReportWriter.FileName);
            RejectOutputConflict(outputPath, routeMapPath, rulePath, observationPaths);
            File.Move(stagedReport, outputPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            throw Failure("output_unavailable");
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }
            }
            catch (Exception)
            {
                // The report result has already been decided; never leak cleanup paths.
            }
        }
    }

    private static string StableDataCode(InvalidDataException exception)
    {
        var separator = exception.Message.IndexOf(':');
        var code = separator < 0 ? exception.Message : exception.Message[..separator];
        if (!DataCodeRegex().IsMatch(code))
        {
            return "input_invalid";
        }

        return code.Replace('.', '_').Replace('-', '_');
    }

    private static SeoAuthorityInsightsCommandException Failure(string code) => new(code);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemeRegex();

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DataCodeRegex();

    private sealed class SeoAuthorityInsightsCommandException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}
