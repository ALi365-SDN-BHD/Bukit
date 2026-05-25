using Bukit.Config;

namespace Bukit.Engine;

public sealed record BuildPipelineContext(
    AppConfig Config,
    string RootDir,
    ConfigOverrides Overrides);

public sealed class BuildPipeline
{
    private readonly Func<BuildPipelineContext, CancellationToken, Task<BuildResult>> _executor;

    public BuildPipeline(Func<BuildPipelineContext, CancellationToken, Task<BuildResult>> executor)
    {
        _executor = executor;
    }

    public Task<BuildResult> ExecuteAsync(BuildPipelineContext context, CancellationToken cancellationToken = default)
    {
        return _executor(context, cancellationToken);
    }
}
