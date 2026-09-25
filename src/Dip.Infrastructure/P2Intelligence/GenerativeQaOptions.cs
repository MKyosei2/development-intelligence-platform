// Purpose: Configures the opt-in OpenAI-compatible provider used by evidence-grounded generative QA.
namespace Dip.Infrastructure.P2Intelligence;

public sealed class GenerativeQaOptions
{
    public const string SectionName = "GenerativeQa";

    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxOutputTokens { get; set; } = 4096;
    public int MaxInputCharacters { get; set; } = 180_000;
    public int MaxResponseCharacters { get; set; } = 1_000_000;
    public int MaxRetries { get; set; } = 2;
    public int MaxPatchFiles { get; set; } = 10;
    public int MaxPatchChangedLines { get; set; } = 1200;
    public double Temperature { get; set; } = 0.1;
    public string HypothesisPromptVersion { get; set; } = "dip.qa.hypothesis.v1";
    public string FixPromptVersion { get; set; } = "dip.qa.fix.v1";

    public void ValidateForEnabled()
    {
        if (!Enabled)
            return;
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("GenerativeQa:Endpoint must be an absolute HTTPS URI.");
        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("GenerativeQa:Model is required when GenerativeQa is enabled.");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("GenerativeQa:ApiKey is required when GenerativeQa is enabled. Prefer the GenerativeQa__ApiKey environment variable.");
        if (TimeoutSeconds is < 5 or > 600)
            throw new InvalidOperationException("GenerativeQa:TimeoutSeconds must be between 5 and 600.");
        if (MaxOutputTokens is < 256 or > 32768)
            throw new InvalidOperationException("GenerativeQa:MaxOutputTokens must be between 256 and 32768.");
        if (MaxInputCharacters is < 1000 or > 1_000_000)
            throw new InvalidOperationException("GenerativeQa:MaxInputCharacters must be between 1000 and 1000000.");
        if (MaxResponseCharacters is < 10_000 or > 4_000_000)
            throw new InvalidOperationException("GenerativeQa:MaxResponseCharacters must be between 10000 and 4000000.");
        if (MaxRetries is < 0 or > 5)
            throw new InvalidOperationException("GenerativeQa:MaxRetries must be between 0 and 5.");
        if (MaxPatchFiles is < 1 or > 50)
            throw new InvalidOperationException("GenerativeQa:MaxPatchFiles must be between 1 and 50.");
        if (MaxPatchChangedLines is < 1 or > 20_000)
            throw new InvalidOperationException("GenerativeQa:MaxPatchChangedLines must be between 1 and 20000.");
        if (Temperature is < 0 or > 1)
            throw new InvalidOperationException("GenerativeQa:Temperature must be between 0 and 1.");
    }
}
