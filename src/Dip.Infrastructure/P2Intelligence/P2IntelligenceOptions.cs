namespace Dip.Infrastructure.P2Intelligence;

public sealed class P2IntelligenceOptions
{
    public const string SectionName="P2Intelligence";
    public string? AiEndpoint{get;set;}
    public string? AiApiKey{get;set;}
    public string AiModel{get;set;}="evidence-grounded-model";
    public int AiTimeoutSeconds{get;set;}=45;
    public bool AllowExternalAi{get;set;}=false;
    public bool AllowFixProposal{get;set;}=false;
}
