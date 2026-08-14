using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Dip.Domain.ValueObjects;

[JsonConverter(typeof(LanguageIdJsonConverter))]
public readonly record struct LanguageId
{
    private static readonly Regex ValidPattern = new("^[a-z0-9][a-z0-9+.#_-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public LanguageId(string value)
    {
        var normalized = Normalize(value);
        if (!ValidPattern.IsMatch(normalized))
            throw new ArgumentException("Language id must be 1-40 characters and contain only letters, numbers, +, ., #, _, or -.", nameof(value));
        Value = normalized;
    }

    public string Value { get; }

    public static LanguageId Python => new("python");
    public static LanguageId CSharp => new("csharp");
    public static LanguageId Cpp => new("cpp");
    public static LanguageId JavaScript => new("javascript");
    public static LanguageId TypeScript => new("typescript");
    public static LanguageId Java => new("java");
    public static LanguageId Kotlin => new("kotlin");
    public static LanguageId Go => new("go");
    public static LanguageId Rust => new("rust");
    public static LanguageId Lua => new("lua");
    public static LanguageId Hlsl => new("hlsl");
    public static LanguageId Glsl => new("glsl");

    public static LanguageId Parse(string value) => new(value);
    public static bool TryParse(string? value, out LanguageId language)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) { language = default; return false; }
            language = new LanguageId(value);
            return true;
        }
        catch (ArgumentException)
        {
            language = default;
            return false;
        }
    }

    public bool Is(string value) => Value.Equals(Normalize(value), StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "c#" or "cs" or "dotnet" => "csharp",
        "c++" or "cc" or "cxx" => "cpp",
        "js" or "node" or "nodejs" => "javascript",
        "ts" => "typescript",
        "golang" => "go",
        _ => (value ?? string.Empty).Trim().ToLowerInvariant()
    };
}

public sealed class LanguageIdJsonConverter : JsonConverter<LanguageId>
{
    public override LanguageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("QA language must be a string language id.");
        var value = reader.GetString();
        if (!LanguageId.TryParse(value, out var language))
            throw new JsonException($"Invalid QA language id '{value}'.");
        return language;
    }

    public override void Write(Utf8JsonWriter writer, LanguageId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}
