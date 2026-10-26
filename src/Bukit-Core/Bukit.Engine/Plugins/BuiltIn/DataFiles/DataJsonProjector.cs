using System.Text;
using System.Text.Json;

namespace Bukit.Engine.Plugins.BuiltIn.DataFiles;

/// <summary>
/// Streams a JSON data file into dictionaries and lists while charging every
/// node against the document and projection budgets.
/// </summary>
internal static class DataJsonProjector
{
    internal static object? Parse(
        TextReader content,
        DocumentTraversalState traversal,
        int maxDocumentDepth)
    {
        var builder = new JsonProjectionBuilder(traversal);
        var state = new JsonReaderState(new JsonReaderOptions
        {
            MaxDepth = GetParserMaxDepth(maxDocumentDepth)
        });
        var chars = new char[4096];
        var bytes = new byte[16384];
        var bufferedBytes = 0;
        var isFinalBlock = false;
        var utf8 = new UTF8Encoding(false, true);
        var encoder = utf8.GetEncoder();

        while (true)
        {
            if (!isFinalBlock)
            {
                var charsRead = content.Read(chars, 0, chars.Length);
                isFinalBlock = charsRead == 0;
                EnsureCapacity(
                    ref bytes,
                    bufferedBytes + utf8.GetMaxByteCount(charsRead));
                encoder.Convert(
                    chars.AsSpan(0, charsRead),
                    bytes.AsSpan(bufferedBytes),
                    isFinalBlock,
                    out var charsUsed,
                    out var bytesUsed,
                    out _);
                if (charsUsed != charsRead)
                {
                    throw new InvalidDataException("Unable to transcode the JSON data file.");
                }

                bufferedBytes += bytesUsed;
            }

            var reader = new Utf8JsonReader(
                bytes.AsSpan(0, bufferedBytes),
                isFinalBlock,
                state);
            while (reader.Read())
            {
                builder.Accept(ref reader);
            }

            var consumed = checked((int)reader.BytesConsumed);
            state = reader.CurrentState;
            bufferedBytes -= consumed;
            if (bufferedBytes > 0 && consumed > 0)
            {
                bytes.AsSpan(consumed, bufferedBytes).CopyTo(bytes);
            }

            if (isFinalBlock)
            {
                break;
            }

            if (consumed == 0 && bufferedBytes == bytes.Length)
            {
                EnsureCapacity(ref bytes, checked(bytes.Length * 2));
            }
        }

        return builder.Complete();
    }

    private static int GetParserMaxDepth(int maxDocumentDepth)
        => maxDocumentDepth < int.MaxValue ? maxDocumentDepth + 1 : int.MaxValue;

    private static void EnsureCapacity(ref byte[] buffer, int required)
    {
        if (required <= buffer.Length) return;
        Array.Resize(ref buffer, Math.Max(required, checked(buffer.Length * 2)));
    }

    private static object ConvertJsonNumber(
        ref Utf8JsonReader reader,
        DocumentTraversalState traversal)
    {
        if (reader.TryGetInt64(out var integer)) return integer;
        if (reader.TryGetDouble(out var number)) return number;
        return traversal.TrackResultString(Encoding.UTF8.GetString(reader.ValueSpan));
    }

    private sealed class JsonProjectionBuilder(DocumentTraversalState traversal)
    {
        private readonly Stack<JsonContainerFrame> _containers = new();
        private bool _hasRoot;
        private object? _root;

        internal void Accept(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    traversal.Visit(reader.CurrentDepth);
                    var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    AttachValue(map);
                    _containers.Push(new JsonContainerFrame(map));
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (_containers.Count == 0) throw new JsonException();
                    _containers.Pop();
                    break;
                case JsonTokenType.StartArray:
                    traversal.Visit(reader.CurrentDepth);
                    var list = new List<object>();
                    AttachValue(list);
                    _containers.Push(new JsonContainerFrame(list));
                    break;
                case JsonTokenType.PropertyName:
                    var property = reader.GetString() ?? string.Empty;
                    if (_containers.Count == 0 || !_containers.Peek().IsMap)
                    {
                        throw new JsonException();
                    }

                    traversal.VisitScalar(property.Length);
                    traversal.VisitResultEntry();
                    traversal.VisitResultString(property.Length);
                    _containers.Peek().PendingProperty = property;
                    break;
                case JsonTokenType.String:
                    traversal.Visit(reader.CurrentDepth);
                    var text = reader.GetString() ?? string.Empty;
                    traversal.VisitScalar(text.Length);
                    AttachValue(traversal.TrackResultString(text));
                    break;
                case JsonTokenType.Number:
                    traversal.Visit(reader.CurrentDepth);
                    traversal.VisitScalar(
                        reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length);
                    AttachValue(ConvertJsonNumber(ref reader, traversal));
                    break;
                case JsonTokenType.True:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(true);
                    break;
                case JsonTokenType.False:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(false);
                    break;
                case JsonTokenType.Null:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(null);
                    break;
            }
        }

        internal object? Complete()
        {
            if (!_hasRoot || _containers.Count != 0)
            {
                throw new JsonException();
            }

            return _root;
        }

        private void AttachValue(object? value)
        {
            if (_containers.Count == 0)
            {
                if (_hasRoot) throw new JsonException();
                _hasRoot = true;
                _root = value;
                return;
            }

            var parent = _containers.Peek();
            if (parent.Map is not null)
            {
                var property = parent.PendingProperty ?? throw new JsonException();
                parent.Map.Add(property, value ?? string.Empty);
                parent.PendingProperty = null;
            }
            else
            {
                traversal.VisitResultEntry();
                parent.List!.Add(value ?? string.Empty);
            }
        }
    }

    private sealed class JsonContainerFrame
    {
        internal JsonContainerFrame(Dictionary<string, object> map) => Map = map;
        internal JsonContainerFrame(List<object> list) => List = list;

        internal Dictionary<string, object>? Map { get; }
        internal List<object>? List { get; }
        internal bool IsMap => Map is not null;
        internal string? PendingProperty { get; set; }
    }
}
