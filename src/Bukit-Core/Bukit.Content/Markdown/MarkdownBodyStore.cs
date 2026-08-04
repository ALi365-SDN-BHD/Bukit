using Bukit.Engine.Abstractions.Content;
using Bukit.Shared.IO;
using System.Text;

namespace Bukit.Content.Markdown;

internal sealed class MarkdownBodyStore : IContentBodyStore
{
    private readonly string _sourceRoot;
    private readonly ISafeSourceFileOpener _opener;

    internal MarkdownBodyStore(string sourceRoot, ISafeSourceFileOpener? opener = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceRoot);
        _sourceRoot = Path.GetFullPath(sourceRoot);
        _opener = opener ?? new PlatformSafeSourceFileOpener();
    }

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(document.Body.Html))
        {
            return new ContentBody(document.Body.Html);
        }

        if (string.IsNullOrWhiteSpace(document.Body.BodyKey))
        {
            throw new InvalidOperationException($"Markdown document '{document.Id}' is missing BodyKey.");
        }

        // The body key is only a candidate identity recorded during
        // enumeration. Re-open through the no-follow handle and re-verify
        // final-path containment on every load; never trust the pathname.
        var candidate = document.Body.BodyKey!;
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.GetFullPath(Path.Combine(_sourceRoot, candidate));
        }

        using var verified = _opener.Open(candidate, _sourceRoot);
        string markdown;
        using (var reader = new StreamReader(verified.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
        {
            markdown = await reader.ReadToEndAsync(cancellationToken);
        }

        return new ContentBody(MarkdownTextHelper.RenderHtml(markdown));
    }
}
