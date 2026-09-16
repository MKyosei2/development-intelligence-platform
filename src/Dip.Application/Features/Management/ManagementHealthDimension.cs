// Purpose: Defines ManagementHealthDimension.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementHealthDimension(string Name,string Status,decimal Score,string Explanation);
