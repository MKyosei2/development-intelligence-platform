// Purpose: Coordinates E vi de nc eI nt eg ri ty Se rv ic e application behavior for Enterprise.
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Enterprise;

public sealed record AppendIntegrityCommand(Guid OrganizationId,Guid ProjectId,Guid EvidenceId,string PayloadJson,string? SourceSignatureBase64,string? SourceKeyId,string? PublicKeyPem);
public sealed class EvidenceIntegrityService(IEnterpriseEvidenceRepository repo,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<EvidenceIntegrityReceipt> AppendAsync(AppendIntegrityCommand c,CancellationToken ct)
    {
        var payloadHash=Hash(c.PayloadJson);var previous=(await repo.LatestReceiptAsync(c.OrganizationId,c.ProjectId,ct))?.ChainHash??new string('0',64);
        var status=EvidenceChainStatus.Unsigned;
        if(!string.IsNullOrWhiteSpace(c.SourceSignatureBase64)&&!string.IsNullOrWhiteSpace(c.PublicKeyPem))
        {
            try{using var ecdsa=ECDsa.Create();ecdsa.ImportFromPem(c.PublicKeyPem);var ok=ecdsa.VerifyData(Encoding.UTF8.GetBytes(c.PayloadJson),Convert.FromBase64String(c.SourceSignatureBase64),HashAlgorithmName.SHA256);status=ok?EvidenceChainStatus.Verified:EvidenceChainStatus.InvalidSignature;}catch{status=EvidenceChainStatus.InvalidSignature;}
        }
        var chain=Hash(previous+"|"+payloadHash+"|"+c.EvidenceId.ToString("N"));
        var receipt=new EvidenceIntegrityReceipt(Guid.NewGuid(),c.OrganizationId,c.ProjectId,c.EvidenceId,payloadHash,previous,chain,c.SourceSignatureBase64,c.SourceKeyId,status,clock.UtcNow);
        await repo.AddReceiptAsync(receipt,ct);await uow.SaveChangesAsync(ct);return receipt;
    }
    public Task<IReadOnlyList<EvidenceIntegrityReceipt>> ListAsync(Guid projectId,int take,CancellationToken ct)=>repo.ListReceiptsAsync(projectId,Math.Clamp(take,1,500),ct);
    public async Task<bool> VerifyChainAsync(Guid projectId,CancellationToken ct)
    {var rows=(await repo.ListReceiptsAsync(projectId,5000,ct)).OrderBy(x=>x.IngestedAtUtc).ToArray();var previous=new string('0',64);foreach(var row in rows){var expected=Hash(previous+"|"+row.PayloadHash+"|"+row.EvidenceId.ToString("N"));if(!string.Equals(expected,row.ChainHash,StringComparison.OrdinalIgnoreCase)||!string.Equals(previous,row.PreviousHash,StringComparison.OrdinalIgnoreCase))return false;previous=row.ChainHash;}return true;}
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
