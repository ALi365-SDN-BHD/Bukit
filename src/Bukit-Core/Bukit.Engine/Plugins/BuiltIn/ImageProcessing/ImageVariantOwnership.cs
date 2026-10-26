using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness;

namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Tracks which variant files Bukit owns, resolves output-relative identities,
/// and removes stale or orphaned owned variants.
/// </summary>
internal static class ImageVariantOwnership
{
    internal static void AddTrackedOutput(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> outputs,
        string sizedFile)
    {
        var relPath = Path.GetRelativePath(context.OutputDir, sizedFile)
            .Replace("\\", "/", StringComparison.Ordinal);
        outputs.Add(new PluginOutputTrackingInfo("image-processing", "after-build", relPath));
    }

    internal static bool IsTrackedOutput(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> outputs,
        string path)
    {
        var relPath = Path.GetRelativePath(context.OutputDir, path)
            .Replace("\\", "/", StringComparison.Ordinal);
        return outputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", relPath));
    }

    internal static void PreservePriorWebpOutputs(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs)
    {
        foreach (var output in priorOutputs.Where(output =>
            output.Path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
            output.Path.EndsWith($".webp{FreshnessSuffix}", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryResolveRelativeIdentity(context.OutputDir, output.Path, out var path) && File.Exists(path))
            {
                generatedOutputs.Add(output);
            }
        }
    }

    internal static void SetTrackedOutputs(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> generatedOutputs)
    {
        if (generatedOutputs.Count > 0)
        {
            context.Set(BuildDataKeys.PluginOutputs, generatedOutputs);
        }
        else
        {
            context.Remove(BuildDataKeys.PluginOutputs);
        }
    }

    internal static HashSet<PluginOutputTrackingInfo> GetPriorPluginOutputs(BuildContext context)
    {
        if (context.TryGet(BuildDataKeys.PriorPluginOutputs, out var outputs))
        {
            return outputs;
        }

        return new HashSet<PluginOutputTrackingInfo>();
    }

    internal static bool HasPriorOwnership(
        string outputDir,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        string variantFile,
        string sidecarFile)
    {
        var variantPath = GetRelativeIdentity(outputDir, variantFile);
        var sidecarPath = GetRelativeIdentity(outputDir, sidecarFile);
        return priorOutputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", variantPath)) &&
               priorOutputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", sidecarPath));
    }

    internal static string GetRelativeIdentity(string outputDir, string path)
    {
        var identity = Path.GetRelativePath(Path.GetFullPath(outputDir), Path.GetFullPath(path))
            .Replace("\\", "/", StringComparison.Ordinal);
        if (!IsNormalizedRelativeIdentity(identity))
        {
            throw new ArgumentException("Managed image identity must remain inside the output directory.", nameof(path));
        }

        return identity;
    }

    internal static bool IsNormalizedRelativeIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) ||
            Path.IsPathRooted(identity) ||
            identity.Contains('\\'))
        {
            return false;
        }

        return identity.Split('/', StringSplitOptions.None)
            .All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    internal static bool TryResolveRelativeIdentity(
        string outputDir,
        string identity,
        out string path)
    {
        path = string.Empty;
        if (!IsNormalizedRelativeIdentity(identity))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(
                outputDir,
                identity.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(GetRelativeIdentity(outputDir, candidate), identity, StringComparison.Ordinal))
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool IsWithinDirectory(string directory, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               !string.Equals(relative, "..", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    internal static bool IsGeneratedSizedImage(string path)
    {
        // Filename shape only. Generated identity itself must be proven by the
        // ownership/freshness manifest, see IsOwnedGeneratedVariant.
        var stem = Path.GetFileNameWithoutExtension(path);
        var suffixStart = stem.LastIndexOf('-');
        if (suffixStart <= 0 || suffixStart == stem.Length - 2)
        {
            return false;
        }

        var suffix = stem[(suffixStart + 1)..];
        return suffix.EndsWith('w')
               && suffix.Length > 1
               && int.TryParse(suffix.AsSpan(0, suffix.Length - 1), out _);
    }

    internal static bool IsOwnedGeneratedVariant(
        string outputDir,
        string path,
        HashSet<PluginOutputTrackingInfo> priorOutputs)
    {
        // A *-<digits>w file is only a generated variant when the ownership manifest
        // proves it. User source files with the same name shape remain inputs.
        if (!IsGeneratedSizedImage(path))
        {
            return false;
        }

        var sidecarFile = path + FreshnessSuffix;
        if (!HasPriorOwnership(outputDir, priorOutputs, path, sidecarFile) ||
            !TryReadFreshnessRecord(sidecarFile, out var freshness) ||
            !IsExpectedVariantPath(path, freshness.Size, freshness.Format) ||
            !MatchesVariantIdentity(freshness, path))
        {
            return false;
        }

        try
        {
            return string.Equals(
                freshness.VariantPath,
                GetRelativeIdentity(outputDir, path),
                StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static void CleanupOrphanedOwnedVariants(
        string outputDir,
        string assetsDir,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        CancellationToken cancellationToken)
    {
        foreach (var sidecarFile in SafeFileEnumerator.EnumerateFiles(assetsDir, $"*{FreshnessSuffix}"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var variantFile = sidecarFile[..^FreshnessSuffix.Length];
            if (!HasPriorOwnership(outputDir, priorOutputs, variantFile, sidecarFile) ||
                !TryReadFreshnessRecord(sidecarFile, out var freshness) ||
                !string.Equals(freshness.VariantPath, GetRelativeIdentity(outputDir, variantFile), StringComparison.Ordinal) ||
                !IsExpectedVariantPath(variantFile, freshness.Size, freshness.Format) ||
                !MatchesVariantIdentity(freshness, variantFile) ||
                !TryResolveRelativeIdentity(outputDir, freshness.SourcePath, out var sourceFile) ||
                !IsWithinDirectory(assetsDir, sourceFile) ||
                File.Exists(sourceFile))
            {
                continue;
            }

            TryDelete(variantFile);
            TryDelete(sidecarFile);
        }
    }

    internal static bool IsExpectedVariantPath(string variantFile, int size, string format)
    {
        var extension = Path.GetExtension(variantFile);
        var stem = Path.GetFileNameWithoutExtension(variantFile);
        return string.Equals(extension.ToLowerInvariant(), format, StringComparison.Ordinal) &&
               stem.Length > $"-{size}w".Length &&
               stem.EndsWith($"-{size}w", StringComparison.Ordinal);
    }

    internal static void CleanupStaleVariants(
        string outputDir,
        string sourceFile,
        IReadOnlyList<int> currentSizes,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        string? variantExtension = null)
    {
        var dir = Path.GetDirectoryName(sourceFile)!;
        var baseName = Path.GetFileNameWithoutExtension(sourceFile);
        var ext = variantExtension ?? Path.GetExtension(sourceFile);
        var currentSizeSet = new HashSet<int>(currentSizes);

        foreach (var existingFile in SafeFileEnumerator.EnumerateFiles(dir, $"{baseName}-*w{ext}"))
        {
            var stem = Path.GetFileNameWithoutExtension(existingFile);
            var suffix = stem.Substring(baseName.Length);
            // Parse -NNNw pattern
            if (suffix.Length > 2 && suffix[^1] == 'w'
                && int.TryParse(suffix.AsSpan(1, suffix.Length - 2), out var parsedSize)
                && !currentSizeSet.Contains(parsedSize)
                && HasPriorOwnership(
                    outputDir,
                    priorOutputs,
                    existingFile,
                    existingFile + FreshnessSuffix)
                && TryReadFreshness(
                    existingFile + FreshnessSuffix,
                    outputDir,
                    sourceFile,
                    existingFile,
                    parsedSize,
                    out _))
            {
                TryDelete(existingFile);
                TryDelete(existingFile + FreshnessSuffix);
            }
        }
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
