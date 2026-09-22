// Purpose: Defines domain behavior and state for this feature area.
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Dip.Domain.ValueObjects;

/// <summary>
/// Extensible language identifier used by QA. Unlike the former QaLanguage enum,
/// adding a language does not require a Domain release.
/// </summary>
[JsonConverter(typeof(LanguageIdJsonConverter))]
public readonly record struct LanguageId
{
    private static readonly Regex Valid = new("^[a-z0-9][a-z0-9._+-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["py"] = "python",
        ["python"] = "python",
        ["cs"] = "csharp",
        ["c#"] = "csharp",
        ["csharp"] = "csharp",
        ["cc"] = "cpp",
        ["cxx"] = "cpp",
        ["c++"] = "cpp",
        ["cpp"] = "cpp",
        ["js"] = "javascript",
        ["javascript"] = "javascript",
        ["node"] = "javascript",
        ["ts"] = "typescript",
        ["typescript"] = "typescript",
        ["golang"] = "go",
        ["go"] = "go",
        ["rs"] = "rust",
        ["rust"] = "rust",
        ["kt"] = "kotlin",
        ["kts"] = "kotlin",
        ["kotlin"] = "kotlin",
        ["rb"] = "ruby",
        ["ruby"] = "ruby",
        ["sh"] = "bash",
        ["shell"] = "bash",
        ["bash"] = "bash",
        ["ps1"] = "powershell",
        ["pwsh"] = "powershell",
        ["powershell"] = "powershell",
        ["objective-c"] = "objectivec",
        ["objc"] = "objectivec",
        ["objectivec"] = "objectivec",
        ["c"] = "c",
        ["java"] = "java",
        ["php"] = "php",
        ["lua"] = "lua",
        ["swift"] = "swift",
        ["sql"] = "sql",
        ["hlsl"] = "hlsl",
        ["glsl"] = "glsl",
        ["gdscript"] = "gdscript",
        ["auto"] = "auto",
        ["multi"] = "multi",
        ["unknown"] = "unknown"
    };

    public LanguageId(string value)
    {
        var canonical = Canonicalize(value);
        if (!Valid.IsMatch(canonical)) throw new ArgumentException("LanguageId must be 1-40 canonical ASCII characters.", nameof(value));
        Value = canonical;
    }

    public string Value { get; }
    public bool IsAuto => string.Equals(Value, "auto", StringComparison.Ordinal);
    public bool IsMulti => string.Equals(Value, "multi", StringComparison.Ordinal);
    public bool IsUnknown => string.Equals(Value, "unknown", StringComparison.Ordinal);

    public static LanguageId Auto => new("auto");
    public static LanguageId Multi => new("multi");
    public static LanguageId Unknown => new("unknown");
    public static LanguageId Python => new("python");
    public static LanguageId CSharp => new("csharp");
    public static LanguageId Cpp => new("cpp");
    public static LanguageId C => new("c");
    public static LanguageId JavaScript => new("javascript");
    public static LanguageId TypeScript => new("typescript");
    public static LanguageId Java => new("java");
    public static LanguageId Kotlin => new("kotlin");
    public static LanguageId Go => new("go");
    public static LanguageId Rust => new("rust");
    public static LanguageId Lua => new("lua");
    public static LanguageId Ruby => new("ruby");
    public static LanguageId Php => new("php");
    public static LanguageId Swift => new("swift");
    public static LanguageId ObjectiveC => new("objectivec");
    public static LanguageId Bash => new("bash");
    public static LanguageId PowerShell => new("powershell");
    public static LanguageId Sql => new("sql");
    public static LanguageId Hlsl => new("hlsl");
    public static LanguageId Glsl => new("glsl");
    public static LanguageId Gdscript => new("gdscript");

    public static LanguageId Parse(string value) => new(value);
    public static bool TryParse(string? value, out LanguageId language)
    {
        try { if (string.IsNullOrWhiteSpace(value)) { language = Unknown; return false; } language = new(value); return true; }
        catch (ArgumentException) { language = Unknown; return false; }
    }
    public override string ToString() => string.IsNullOrWhiteSpace(Value) ? "unknown" : Value;

    private static string Canonicalize(string value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Aliases.TryGetValue(text, out var alias)) return alias;
        return text.Replace(' ', '-');
    }
}

public sealed class LanguageIdJsonConverter : JsonConverter<LanguageId>
{
    public override LanguageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return LanguageId.Parse(reader.GetString() ?? "unknown");
        // Backward compatibility for the old QaLanguage enum JSON representation.
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var legacy))
            return legacy switch { 0 => LanguageId.Python, 1 => LanguageId.CSharp, 2 => LanguageId.Cpp, _ => throw new JsonException($"Unknown legacy QA language value {legacy}.") };
        throw new JsonException("LanguageId must be a string.");
    }
    public override void Write(Utf8JsonWriter writer, LanguageId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
