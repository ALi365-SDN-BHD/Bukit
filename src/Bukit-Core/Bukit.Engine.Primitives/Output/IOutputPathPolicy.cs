namespace Bukit.Engine.Output;

internal interface IOutputPathPolicy
{
    string ResolveSafePath(string outputRoot, string relativePath);
}
