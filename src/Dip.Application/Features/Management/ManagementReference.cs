// Purpose: Defines ManagementReference.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementReference(string Type, string Key);
