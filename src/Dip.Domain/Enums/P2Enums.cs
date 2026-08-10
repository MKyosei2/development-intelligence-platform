namespace Dip.Domain.Enums;

public enum IntelligenceRunType { HardwareCommunity, MinimumHardware, AdaptiveQa, GitRegression, Flaky, Soak, VisualRegression, VideoRegression, AudioRegression, Mutation, Fuzz, RootCause, Forecast, Decision }
public enum IntelligenceRunStatus { Queued, Running, Succeeded, Warning, Failed, InfrastructureFailure, Cancelled }
public enum ForecastConfidence { Unverified, Low, Medium, High }
public enum HardwareSubmissionStatus { Pending, Accepted, Rejected }
