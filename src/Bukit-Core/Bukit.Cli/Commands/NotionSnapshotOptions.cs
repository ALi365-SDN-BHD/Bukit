using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

/// <summary>
/// <c>--notion-snapshot &lt;dir&gt;</c> and <c>--notion-offline</c>: record a Notion offline
/// snapshot during a build with a token, or read only from it (no token, no network).
/// Both override <c>cacheDir</c> and <c>cacheMode</c> of every Notion source.
/// </summary>
internal static class NotionSnapshotOptions
{
    internal const string SnapshotOption = "--notion-snapshot";
    internal const string OfflineOption = "--notion-offline";

    /// <summary>Opens the snapshot for <c>build</c>: recording, or offline reads. Null when not requested.</summary>
    internal static NotionSnapshot? OpenForBuild(CliBoundCommand command)
    {
        var (directory, offline) = Read(command);
        if (directory is null)
        {
            return null;
        }

        return offline ? NotionSnapshot.OpenOffline(directory) : NotionSnapshot.BeginWrite(directory);
    }

    /// <summary>
    /// Opens the snapshot for <c>config check</c> and <c>doctor</c>, which only read it, so
    /// <c>--notion-snapshot</c> needs <c>--notion-offline</c> there.
    /// </summary>
    internal static NotionSnapshot? OpenForCheck(CliBoundCommand command)
    {
        var (directory, offline) = Read(command);
        if (directory is null)
        {
            return null;
        }

        if (!offline)
        {
            throw new CommandArgumentException($"{SnapshotOption} only records during build; add {OfflineOption} to check against the snapshot.");
        }

        return NotionSnapshot.OpenOffline(directory);
    }

    private static (string? Directory, bool Offline) Read(CliBoundCommand command)
    {
        var directory = command.GetString(SnapshotOption);
        var offline = command.GetBool(OfflineOption);
        if (offline && string.IsNullOrWhiteSpace(directory))
        {
            throw new CommandArgumentException($"{OfflineOption} requires {SnapshotOption} <dir>.");
        }

        return (string.IsNullOrWhiteSpace(directory) ? null : directory, offline);
    }
}
