namespace Bukit.PublicApiDrift;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["compare", var baselinePath, var currentPath])
            {
                var baseline = BaselineFile.Load(baselinePath, BaselineValidationMode.Committed);
                var current = BaselineFile.Load(currentPath, BaselineValidationMode.Candidate);
                var diagnostics = ApiSurfaceComparer.Compare(baseline, current);
                foreach (var item in diagnostics) Console.Error.WriteLine(item);
                return diagnostics.Count == 0 ? 0 : 1;
            }

            if (args is ["check", var checkBaselinePath, var checkRepositoryRoot, var checkConfiguration])
            {
                var baseline = BaselineFile.Load(checkBaselinePath, BaselineValidationMode.Committed);
                var current = ApiSurfaceCapture.Capture(baseline, checkRepositoryRoot, checkConfiguration);
                var diagnostics = ApiSurfaceComparer.Compare(baseline, current);
                foreach (var item in diagnostics) Console.Error.WriteLine(item);
                return diagnostics.Count == 0 ? 0 : 1;
            }

            if (args is ["snapshot", var snapshotBaselinePath, var outputPath, var snapshotRepositoryRoot, var snapshotConfiguration])
            {
                var baseline = BaselineFile.Load(snapshotBaselinePath, BaselineValidationMode.Committed);
                var current = ApiSurfaceCapture.Capture(baseline, snapshotRepositoryRoot, snapshotConfiguration);
                BaselineFile.WriteNew(outputPath, current, snapshotRepositoryRoot);
                Console.Out.WriteLine($"wrote public API candidate: {Path.GetFullPath(outputPath)}");
                return 0;
            }

            Console.Error.WriteLine("usage: Bukit.PublicApiDrift compare BASELINE CURRENT | check BASELINE ROOT CONFIGURATION | snapshot BASELINE OUTPUT ROOT CONFIGURATION");
            return 2;
        }
        catch (Exception exception)
        {
            var root = exception.GetBaseException();
            var message = root.Message.Replace('\r', ' ').Replace('\n', ' ');
            if (message.Length > 400) message = message[..400];
            Console.Error.WriteLine($"gate-error: {root.GetType().FullName}: {message}");
            return 2;
        }
    }
}
