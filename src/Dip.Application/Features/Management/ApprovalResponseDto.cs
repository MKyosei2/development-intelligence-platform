// Purpose: Defines the ApprovalResponseDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ApprovalResponseDto(string Actor, string ActorRole, string Decision, string? Comment, DateTimeOffset RespondedAtUtc);
