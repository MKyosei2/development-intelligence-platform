// Purpose: Defines the IRepositoryTechnologyDetector application boundary.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public interface IRepositoryTechnologyDetector { Task<QaTechnologyProfile> DetectAsync(string workspace, CancellationToken cancellationToken); }
