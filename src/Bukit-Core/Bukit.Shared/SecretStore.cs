using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Shared;

/// <summary>
/// Raised when the <c>--secrets-stdin</c> payload is unusable. Messages are fixed strings and never
/// contain any part of the payload.
/// </summary>
internal sealed class SecretsInputException(string message) : Exception(message)
{
    public const string DiagnosticCode = "secrets-stdin-invalid";
}

/// <summary>
/// The single place Bukit reads secrets (tokens) from. Values passed with <c>--secrets-stdin</c> win;
/// otherwise the process environment is used, so env-based callers keep working unchanged.
/// Secrets read from standard input are kept in memory only: they are never written back to the
/// environment, so child processes started by Bukit do not inherit them.
/// </summary>
internal static partial class SecretStore
{
    public const string StdinFlag = "--secrets-stdin";
    public const int MaxStdinBytes = 64 * 1024;

    private static readonly Lock Gate = new();
    private static IReadOnlyDictionary<string, string> s_stdinSecrets =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>True when secrets were supplied on standard input for this process.</summary>
    public static bool HasStdinSecrets
    {
        get
        {
            lock (Gate)
            {
                return s_stdinSecrets.Count > 0;
            }
        }
    }

    /// <summary>Returns the secret named <paramref name="name"/>: stdin value first, then the environment.</summary>
    public static string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        lock (Gate)
        {
            if (s_stdinSecrets.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return Environment.GetEnvironmentVariable(name);
    }

    /// <summary>Reads, parses and installs the stdin secrets. Throws <see cref="SecretsInputException"/>.</summary>
    public static void LoadFromStream(Stream input)
        => Install(Parse(ReadBounded(input)));

    /// <summary>Installs already-parsed secrets (used by tests and by <see cref="LoadFromStream"/>).</summary>
    public static void Install(IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        var copy = new Dictionary<string, string>(secrets, StringComparer.Ordinal);
        lock (Gate)
        {
            s_stdinSecrets = copy;
        }
    }

    /// <summary>Forgets all stdin secrets. Intended for tests.</summary>
    public static void Reset() => Install(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Serializes the stdin secrets so they can be forwarded to a child Bukit process over its stdin.</summary>
    public static string SerializeStdinSecrets()
    {
        IReadOnlyDictionary<string, string> snapshot;
        lock (Gate)
        {
            snapshot = s_stdinSecrets;
        }

        // Relaxed escaping keeps non-ASCII text unescaped, so the forwarded payload stays about
        // the size of the original input and within the child's MaxStdinBytes limit.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in snapshot)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Replaces every stdin secret value that appears in <paramref name="text"/> with <c>***</c>.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        IReadOnlyDictionary<string, string> snapshot;
        lock (Gate)
        {
            snapshot = s_stdinSecrets;
        }

        if (snapshot.Count == 0)
        {
            return text;
        }

        var redacted = text;
        foreach (var value in snapshot.Values
                     .Where(static value => value.Length > 0)
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(static value => value.Length))
        {
            redacted = redacted.Replace(value, "***", StringComparison.Ordinal);
        }

        return redacted;
    }

    /// <summary>The stdin secret values, for maskers that redact output. Never log this.</summary>
    public static IReadOnlyCollection<string> StdinSecretValues
    {
        get
        {
            lock (Gate)
            {
                return [.. s_stdinSecrets.Values];
            }
        }
    }

    internal static byte[] ReadBounded(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            int read;
            try
            {
                read = input.Read(chunk, 0, chunk.Length);
            }
            catch (IOException)
            {
                throw new SecretsInputException("--secrets-stdin could not read standard input.");
            }

            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxStdinBytes)
            {
                throw new SecretsInputException($"--secrets-stdin input exceeds the {MaxStdinBytes / 1024} KiB limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Parses one JSON object of secret name to non-empty string value. Error messages are fixed and
    /// never include the input, a key, a value, or a parser position excerpt.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaxStdinBytes)
        {
            throw new SecretsInputException($"--secrets-stdin input exceeds the {MaxStdinBytes / 1024} KiB limit.");
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(utf8);
        }
        catch (DecoderFallbackException)
        {
            throw new SecretsInputException("--secrets-stdin input is not valid UTF-8.");
        }

        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SecretsInputException("--secrets-stdin was given but standard input was empty. Write one JSON object such as {\"NOTION_TOKEN\":\"...\"} to stdin.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
        }
        catch (JsonException)
        {
            // JsonException messages can quote input characters, so they are never surfaced.
            throw new SecretsInputException("--secrets-stdin input is not valid JSON. Expected one object of name to string value; the input is not echoed.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SecretsInputException("--secrets-stdin input must be a JSON object of name to string value.");
            }

            var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                string name;
                try
                {
                    name = property.Name;
                }
                catch (InvalidOperationException)
                {
                    // An escape such as a lone surrogate (\uD800) cannot be decoded to text.
                    throw new SecretsInputException("--secrets-stdin input contains an invalid escape sequence. The input is not echoed.");
                }

                if (!SecretNamePattern().IsMatch(name))
                {
                    throw new SecretsInputException("--secrets-stdin names must be environment-variable style (letters, digits and underscores, not starting with a digit). The name is not echoed.");
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new SecretsInputException("--secrets-stdin values must be JSON strings. The value is not echoed.");
                }

                string value;
                try
                {
                    value = property.Value.GetString()!;
                }
                catch (InvalidOperationException)
                {
                    throw new SecretsInputException("--secrets-stdin input contains an invalid escape sequence. The input is not echoed.");
                }
                if (value.Length == 0)
                {
                    throw new SecretsInputException("--secrets-stdin values must not be empty strings.");
                }

                if (!secrets.TryAdd(name, value))
                {
                    throw new SecretsInputException("--secrets-stdin input contains a duplicate name. The name is not echoed.");
                }
            }

            return secrets;
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNamePattern();
}
