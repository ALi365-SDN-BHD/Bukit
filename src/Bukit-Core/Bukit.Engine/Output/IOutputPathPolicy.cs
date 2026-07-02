namespace Bukit.Engine.Output;

public interface IOutputPathPolicy
{
    string ResolveSafePath(string outputRoot, string relativePath);
}
