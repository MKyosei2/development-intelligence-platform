using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.Policies;
using Microsoft.Extensions.Configuration;
namespace Dip.Application.Features.Enterprise;
public sealed record SignedAgentPolicyEnvelope(string AgentId,Guid ProjectId,int Version,string PolicyJson,string Sha256,string? SignatureBase64,string SignatureAlgorithm,DateTimeOffset IssuedAtUtc);
public sealed class AgentPolicyDistributionService(PolicyResolver policies,IConfiguration configuration)
{
    public async Task<SignedAgentPolicyEnvelope> GetAsync(Guid projectId,string agentId,int version,CancellationToken ct)
    {
        var policy=await policies.ResolveAsync(projectId,agentId,ct);var json=JsonSerializer.Serialize(policy,new JsonSerializerOptions(JsonSerializerDefaults.Web));var bytes=Encoding.UTF8.GetBytes(json);var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();var signature=Sign(bytes);return new(agentId,projectId,Math.Max(1,version),json,hash,signature,"ECDSA-P256-SHA256",DateTimeOffset.UtcNow);
    }
    private string? Sign(byte[] bytes){var pem=configuration["DIP:PolicySigning:PrivateKeyPem"]??Environment.GetEnvironmentVariable("DIP_POLICY_SIGNING_PRIVATE_KEY_PEM");if(string.IsNullOrWhiteSpace(pem))return null;using var key=ECDsa.Create();key.ImportFromPem(pem);return Convert.ToBase64String(key.SignData(bytes,HashAlgorithmName.SHA256));}
}
