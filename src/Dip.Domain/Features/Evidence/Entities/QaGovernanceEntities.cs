// Purpose: Represents Q aG ov er na nc eE nt it ie s in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaFindingState
{
    private QaFindingState(){}
    public QaFindingState(Guid id,Guid projectId,string fingerprint,Guid findingId,string subjectKey,string? commitSha,DateTimeOffset now)
    {Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=Req(projectId);
        Fingerprint=Text(fingerprint,64);
        LastFindingId=findingId;
        SubjectKey=Text(subjectKey,200);
        LastCommitSha=Opt(commitSha,64);
        Status=FindingLifecycleStatus.New;
        OwnershipSource=QaOwnershipSource.Unassigned;
        Occurrences=1;
        Version=1;
        FirstSeenAtUtc=LastSeenAtUtc=UpdatedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string Fingerprint{get;
        private set;
        }=string.Empty;
        public Guid LastFindingId{get;
        private set;
        }
        public string SubjectKey{get;
        private set;
        }=string.Empty;
        public string? LastCommitSha{get;
        private set;
        }
        public FindingLifecycleStatus Status{get;
        private set;
        }
        public int Occurrences{get;
        private set;
        }
        public string? Team{get;
        private set;
        }
        public string? Workstream{get;
        private set;
        }
        public QaOwnershipSource OwnershipSource{get;
        private set;
        }
        public string? DispositionReason{get;
        private set;
        }
        public string? UpdatedBy{get;
        private set;
        }
        public DateTimeOffset FirstSeenAtUtc{get;
        private set;
        }
        public DateTimeOffset LastSeenAtUtc{get;
        private set;
        }
        public DateTimeOffset UpdatedAtUtc{get;
        private set;
        }
        public long Version{get;
        private set;
        }
    public void Observe(Guid findingId,string? commitSha,DateTimeOffset now){LastFindingId=findingId;
        LastCommitSha=Opt(commitSha,64);
        Occurrences++;
        Version++;
        LastSeenAtUtc=UpdatedAtUtc=now;
        if(Status==FindingLifecycleStatus.New&&Occurrences>=2)Status=FindingLifecycleStatus.Confirmed;
        if(Status is FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Verified or FindingLifecycleStatus.Closed)Status=FindingLifecycleStatus.Confirmed;
        }
    public void Transition(FindingLifecycleStatus status,string reason,string actor,DateTimeOffset now){if(status!=Status&&!CanTransition(Status,status))throw new InvalidOperationException($"Finding lifecycle transition {Status} -> {status} is not allowed.");
        Status=status;
        DispositionReason=Text(reason,2000);
        UpdatedBy=Text(actor,200);
        Version++;
        UpdatedAtUtc=now;
        }
    private static bool CanTransition(FindingLifecycleStatus from,FindingLifecycleStatus to)=>from switch{FindingLifecycleStatus.New=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Assigned or FindingLifecycleStatus.InProgress or FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Suppressed or FindingLifecycleStatus.AcceptedRisk or FindingLifecycleStatus.Duplicate or FindingLifecycleStatus.WontFix,FindingLifecycleStatus.Confirmed=>to is FindingLifecycleStatus.Assigned or FindingLifecycleStatus.InProgress or FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Suppressed or FindingLifecycleStatus.AcceptedRisk or FindingLifecycleStatus.Duplicate or FindingLifecycleStatus.WontFix or FindingLifecycleStatus.Closed,FindingLifecycleStatus.Assigned=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.InProgress or FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Suppressed or FindingLifecycleStatus.AcceptedRisk or FindingLifecycleStatus.Duplicate or FindingLifecycleStatus.WontFix or FindingLifecycleStatus.Closed,FindingLifecycleStatus.InProgress=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Suppressed or FindingLifecycleStatus.AcceptedRisk or FindingLifecycleStatus.WontFix,FindingLifecycleStatus.Fixed=>to is FindingLifecycleStatus.Verified or FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.InProgress,FindingLifecycleStatus.Verified=>to is FindingLifecycleStatus.Closed or FindingLifecycleStatus.Confirmed,FindingLifecycleStatus.Closed=>to is FindingLifecycleStatus.Confirmed,FindingLifecycleStatus.Suppressed=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Fixed or FindingLifecycleStatus.Closed,FindingLifecycleStatus.AcceptedRisk=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Closed,FindingLifecycleStatus.Duplicate=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Closed,FindingLifecycleStatus.WontFix=>to is FindingLifecycleStatus.Confirmed or FindingLifecycleStatus.Closed,_=>false};
    public void Assign(string? team,string? workstream,QaOwnershipSource source,DateTimeOffset now){Team=Opt(team,200);
        Workstream=Opt(workstream,200);
        OwnershipSource=source;
        if(Status is FindingLifecycleStatus.New or FindingLifecycleStatus.Confirmed)Status=FindingLifecycleStatus.Assigned;
        Version++;
        UpdatedAtUtc=now;
        }
    private static Guid Req(Guid v)=>v==Guid.Empty?throw new ArgumentException("Guid is required."):v;
        private static string Text(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException($"Text must be 1-{m} characters.");
        return s;
        }
        private static string? Opt(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;
        var s=v.Trim();
        if(s.Length>m)throw new ArgumentException($"Text must be <= {m} characters.");
        return s;
        }
}

public sealed class QaWaiver
{
    private QaWaiver(){}
    public QaWaiver(Guid id,Guid projectId,string name,string reason,string actor,string? fingerprint,string? ruleId,string? pathPattern,DateTimeOffset createdAtUtc,DateTimeOffset expiresAtUtc)
    {
        if(projectId==Guid.Empty)throw new ArgumentException(nameof(projectId));
        if(expiresAtUtc<=createdAtUtc)throw new ArgumentException("Waiver expiry must be after creation.");
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;Name=T(name,200);Reason=T(reason,2000);RequestedBy=T(actor,200);
        Fingerprint=O(fingerprint,64);
            RuleId=O(ruleId,300);
            PathPattern=O(pathPattern,1000);
            CreatedAtUtc=UpdatedAtUtc=createdAtUtc;
            ExpiresAtUtc=expiresAtUtc;
            Status=WaiverStatus.Requested;
            Version=1;
    }
    public Guid Id{get;private set;}public Guid ProjectId{get;private set;}public string Name{get;private set;}=string.Empty;public string Reason{get;private set;}=string.Empty;
    public string RequestedBy{get;private set;}=string.Empty;public string? ApprovedBy{get;private set;}public string? DecisionReason{get;private set;}
    public string? Fingerprint{get;private set;}public string? RuleId{get;private set;}public string? PathPattern{get;private set;}public WaiverStatus Status{get;private set;}
    public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset UpdatedAtUtc{get;
        private set;
        }
        public DateTimeOffset ExpiresAtUtc{get;
        private set;
        }
        public DateTimeOffset? ApprovedAtUtc{get;
        private set;
        }
        public DateTimeOffset? RevokedAtUtc{get;
        private set;
        }
        public long Version{get;
        private set;
        }
    public bool IsActive(DateTimeOffset now)=>Status==WaiverStatus.Active&&now<ExpiresAtUtc;
    public void Approve(string actor,string reason,DateTimeOffset now){if(Status!=WaiverStatus.Requested)throw new InvalidOperationException("Only requested waivers can be approved.");
        if(string.Equals(RequestedBy,actor,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Waiver requester cannot approve the same waiver.");
        ApprovedBy=T(actor,200);
        DecisionReason=T(reason,2000);
        ApprovedAtUtc=UpdatedAtUtc=now;
        Status=WaiverStatus.Active;
        Version++;
        }
    public void Reject(string actor,string reason,DateTimeOffset now){if(Status!=WaiverStatus.Requested)throw new InvalidOperationException("Only requested waivers can be rejected.");
        ApprovedBy=T(actor,200);
        DecisionReason=T(reason,2000);
        UpdatedAtUtc=now;
        Status=WaiverStatus.Rejected;
        Version++;
        }
    public void Revoke(string actor,string reason,DateTimeOffset now){if(Status!=WaiverStatus.Active)throw new InvalidOperationException("Only active waivers can be revoked.");
        ApprovedBy=T(actor,200);
        DecisionReason=T(reason,2000);
        Status=WaiverStatus.Revoked;
        RevokedAtUtc=UpdatedAtUtc=now;
        Version++;
        }
    public void Revoke(DateTimeOffset now){Revoke(ApprovedBy??RequestedBy,"Revoked",now);}
    public void Expire(DateTimeOffset now){if(Status==WaiverStatus.Active&&now>=ExpiresAtUtc){Status=WaiverStatus.Expired;UpdatedAtUtc=now;Version++;}}
    private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }
        private static string? O(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;
        var s=v.Trim();
        if(s.Length>m)throw new ArgumentException();
        return s;
        }
}

public sealed class QaTestReliability
{
    private QaTestReliability(){}
    public QaTestReliability(Guid id,Guid projectId,string testKey,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId==Guid.Empty?throw new ArgumentException(nameof(projectId)):projectId;
        TestKey=Txt(testKey,500);
        Classification=TestReliabilityClassification.Unknown;
        FirstObservedAtUtc=LastObservedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string TestKey{get;
        private set;
        }=string.Empty;
        public int PassCount{get;
        private set;
        }
        public int FailCount{get;
        private set;
        }
        public int ConsecutivePasses{get;
        private set;
        }
        public int ConsecutiveFailures{get;
        private set;
        }
        public TestReliabilityClassification Classification{get;
        private set;
        }
        public string? LastCommitSha{get;
        private set;
        }
        public DateTimeOffset FirstObservedAtUtc{get;
        private set;
        }
        public DateTimeOffset LastObservedAtUtc{get;
        private set;
        }
        public decimal FailureRatePercent=>PassCount+FailCount==0?0m:decimal.Round((decimal)FailCount/(PassCount+FailCount)*100m,2);
    public void Observe(bool passed,bool infrastructureFailure,string? commitSha,DateTimeOffset now){LastCommitSha=string.IsNullOrWhiteSpace(commitSha)?null:commitSha.Trim();
        LastObservedAtUtc=now;
        if(infrastructureFailure){Classification=TestReliabilityClassification.EnvironmentFailure;
        return;
        }
        if(passed){PassCount++;
        ConsecutivePasses++;
        ConsecutiveFailures=0;
        }
        else{FailCount++;
        ConsecutiveFailures++;
        ConsecutivePasses=0;
        }var total=PassCount+FailCount;
        Classification=total<3?TestReliabilityClassification.Unknown:FailCount==0?TestReliabilityClassification.StablePass:PassCount==0&&FailCount>=3?TestReliabilityClassification.StableFail:FailureRatePercent is>=5m and<=95m?TestReliabilityClassification.Flaky:FailureRatePercent>95m?TestReliabilityClassification.StableFail:TestReliabilityClassification.StablePass;
        }
    private static string Txt(string? v,int m){var s=v?.Trim()??"";if(s.Length<1||s.Length>m)throw new ArgumentException();return s;}
}

public sealed class QaRegressionBaseline
{
    private QaRegressionBaseline(){}
    public QaRegressionBaseline(Guid id,Guid projectId,string subjectKey,string metricKey,string commitSha,decimal value,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        SubjectKey=Txt(subjectKey,200);
        MetricKey=Txt(metricKey,200);
        BaselineCommit=Txt(commitSha,64);
        LastKnownGoodCommit=BaselineCommit;
        BaselineValue=value;
        CurrentValue=value;
        Status=QaRegressionStatus.Baseline;
        UpdatedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string SubjectKey{get;
        private set;
        }=string.Empty;
        public string MetricKey{get;
        private set;
        }=string.Empty;
        public string BaselineCommit{get;
        private set;
        }=string.Empty;
        public string LastKnownGoodCommit{get;
        private set;
        }=string.Empty;
        public string? FirstBadCommit{get;
        private set;
        }
        public decimal BaselineValue{get;
        private set;
        }
        public decimal CurrentValue{get;
        private set;
        }
        public decimal RegressionPercent{get;
        private set;
        }
        public QaRegressionStatus Status{get;
        private set;
        }
        public DateTimeOffset UpdatedAtUtc{get;
        private set;
        }
    public void Observe(string commitSha,decimal value,bool healthy,DateTimeOffset now){CurrentValue=value;
        RegressionPercent=BaselineValue==0?(value<=0?0m:100m):decimal.Round((value-BaselineValue)/Math.Abs(BaselineValue)*100m,2);
        if(healthy){LastKnownGoodCommit=Txt(commitSha,64);
        if(Status==QaRegressionStatus.Regressed)Status=QaRegressionStatus.Recovered;
        else Status=QaRegressionStatus.Healthy;
        FirstBadCommit=null;
        }
        else{FirstBadCommit??=Txt(commitSha,64);
        Status=QaRegressionStatus.Regressed;
        }UpdatedAtUtc=now;
        }
    public void Rebaseline(string commitSha,decimal value,DateTimeOffset now){BaselineCommit=Txt(commitSha,64);
        LastKnownGoodCommit=BaselineCommit;
        FirstBadCommit=null;
        BaselineValue=CurrentValue=value;
        RegressionPercent=0;
        Status=QaRegressionStatus.Baseline;
        UpdatedAtUtc=now;
        }
    private static string Txt(string? v,int m){var s=v?.Trim()??"";if(s.Length<1||s.Length>m)throw new ArgumentException();return s;}
}

public sealed class QaOwnershipRule
{
    private QaOwnershipRule(){}
        public QaOwnershipRule(Guid id,Guid projectId,string name,string pathPattern,string team,string? workstream,int priority,bool enabled,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        Name=T(name,200);
        PathPattern=T(pathPattern,1000);
        Team=T(team,200);
        Workstream=O(workstream,200);
        Priority=Math.Clamp(priority,-10000,10000);
        Enabled=enabled;
        CreatedAtUtc=now;
        UpdatedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string Name{get;
        private set;
        }=string.Empty;
        public string PathPattern{get;
        private set;
        }=string.Empty;
        public string Team{get;
        private set;
        }=string.Empty;
        public string? Workstream{get;
        private set;
        }
        public int Priority{get;
        private set;
        }
        public bool Enabled{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset UpdatedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }
        private static string? O(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;
        var s=v.Trim();
        if(s.Length>m)throw new ArgumentException();
        return s;
        }}

public sealed class QaPolicy
{
    private QaPolicy(){}
    public QaPolicy(Guid id,Guid projectId,string name,string version,string rulesJson,bool enabled,DateTimeOffset now)
    {Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        Name=T(name,200);
        Version=T(version,80);
        RulesJson=T(rulesJson,32000);
        Enabled=enabled;
        LifecycleStatus=enabled?QaPolicyLifecycleStatus.Active:QaPolicyLifecycleStatus.Draft;
        Revision=1;
        ConcurrencyVersion=1;
        CreatedAtUtc=UpdatedAtUtc=now;
        if(enabled)ActivatedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string Name{get;
        private set;
        }=string.Empty;
        public string Version{get;
        private set;
        }=string.Empty;
        public string RulesJson{get;
        private set;
        }=string.Empty;
        public bool Enabled{get;
        private set;
        }
    public QaPolicyLifecycleStatus LifecycleStatus{get;
        private set;
        }
        public int Revision{get;
        private set;
        }
        public Guid? SupersedesPolicyId{get;
        private set;
        }
        public string? ApprovedBy{get;
        private set;
        }
        public string? ActivatedBy{get;
        private set;
        }
    public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset UpdatedAtUtc{get;
        private set;
        }
        public DateTimeOffset? ApprovedAtUtc{get;
        private set;
        }
        public DateTimeOffset? ActivatedAtUtc{get;
        private set;
        }
        public long ConcurrencyVersion{get;
        private set;
        }
    public void Approve(string actor,DateTimeOffset now){if(LifecycleStatus!=QaPolicyLifecycleStatus.Draft)throw new InvalidOperationException("Only draft policies can be approved.");
        ApprovedBy=T(actor,200);
        ApprovedAtUtc=UpdatedAtUtc=now;
        LifecycleStatus=QaPolicyLifecycleStatus.Approved;
        Enabled=false;
        ConcurrencyVersion++;
        }
    public void Activate(string actor,Guid? supersedesId,int revision,DateTimeOffset now){if(LifecycleStatus!=QaPolicyLifecycleStatus.Approved)throw new InvalidOperationException("Only approved policies can be activated.");
        ActivatedBy=T(actor,200);
        ActivatedAtUtc=UpdatedAtUtc=now;
        SupersedesPolicyId=supersedesId;
        Revision=Math.Max(1,revision);
        LifecycleStatus=QaPolicyLifecycleStatus.Active;
        Enabled=true;
        ConcurrencyVersion++;
        }
    public void Supersede(DateTimeOffset now){LifecycleStatus=QaPolicyLifecycleStatus.Superseded;Enabled=false;UpdatedAtUtc=now;ConcurrencyVersion++;}
    public void Archive(DateTimeOffset now){LifecycleStatus=QaPolicyLifecycleStatus.Archived;Enabled=false;UpdatedAtUtc=now;ConcurrencyVersion++;}
    private static string T(string? v,int m){var s=v?.Trim()??"";if(s.Length<1||s.Length>m)throw new ArgumentException();return s;}
}
public sealed class QaPolicyEvaluation
{
    private QaPolicyEvaluation(){}
        public QaPolicyEvaluation(Guid id,Guid projectId,Guid qaRunId,Guid? policyId,string policyVersion,QaGateVerdict verdict,string blockersJson,string warningsJson,string summary,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        QaRunId=qaRunId;
        PolicyId=policyId;
        PolicyVersion=T(policyVersion,80);
        Verdict=verdict;
        BlockersJson=T(blockersJson,16000);
        WarningsJson=T(warningsJson,16000);
        Summary=T(summary,4000);
        EvaluatedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid QaRunId{get;
        private set;
        }
        public Guid? PolicyId{get;
        private set;
        }
        public string PolicyVersion{get;
        private set;
        }=string.Empty;
        public QaGateVerdict Verdict{get;
        private set;
        }
        public string BlockersJson{get;
        private set;
        }="[]";
        public string WarningsJson{get;
        private set;
        }="[]";
        public string Summary{get;
        private set;
        }=string.Empty;
        public DateTimeOffset EvaluatedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }}

public sealed class QaEvidenceProvenance
{
    private QaEvidenceProvenance(){}
        public QaEvidenceProvenance(Guid id,Guid projectId,Guid qaRunId,string? commitSha,string environmentFingerprint,string toolchainJson,string normalizerVersion,string policyVersion,string inputHash,string evidenceHash,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        QaRunId=qaRunId;
        CommitSha=O(commitSha,64);
        EnvironmentFingerprint=T(environmentFingerprint,128);
        ToolchainJson=T(toolchainJson,16000);
        NormalizerVersion=T(normalizerVersion,80);
        PolicyVersion=T(policyVersion,80);
        InputHash=T(inputHash,64);
        EvidenceHash=T(evidenceHash,64);
        CapturedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid QaRunId{get;
        private set;
        }
        public string? CommitSha{get;
        private set;
        }
        public string EnvironmentFingerprint{get;
        private set;
        }=string.Empty;
        public string ToolchainJson{get;
        private set;
        }="{}";
        public string NormalizerVersion{get;
        private set;
        }=string.Empty;
        public string PolicyVersion{get;
        private set;
        }=string.Empty;
        public string InputHash{get;
        private set;
        }=string.Empty;
        public string EvidenceHash{get;
        private set;
        }=string.Empty;
        public DateTimeOffset CapturedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }
        private static string? O(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;
        var s=v.Trim();
        if(s.Length>m)throw new ArgumentException();
        return s;
        }}

public sealed class QaSystemHealthSnapshot
{
    private QaSystemHealthSnapshot(){}
        public QaSystemHealthSnapshot(Guid id,Guid projectId,Guid? qaRunId,string component,QaSystemHealthStatus status,string detail,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        QaRunId=qaRunId;
        Component=T(component,120);
        Status=status;
        Detail=T(detail,4000);
        ObservedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid? QaRunId{get;
        private set;
        }
        public string Component{get;
        private set;
        }=string.Empty;
        public QaSystemHealthStatus Status{get;
        private set;
        }
        public string Detail{get;
        private set;
        }=string.Empty;
        public DateTimeOffset ObservedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }}
public sealed class QaQualityTrendSnapshot
{
    private QaQualityTrendSnapshot(){}
        public QaQualityTrendSnapshot(Guid id,Guid projectId,Guid qaRunId,int openFindings,int critical,int warning,int flaky,int activeWaivers,decimal debtScore,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        QaRunId=qaRunId;
        OpenFindingCount=Math.Max(0,openFindings);
        CriticalCount=Math.Max(0,critical);
        WarningCount=Math.Max(0,warning);
        FlakyCount=Math.Max(0,flaky);
        ActiveWaiverCount=Math.Max(0,activeWaivers);
        TechnicalDebtScore=Math.Clamp(debtScore,0m,1000m);
        CapturedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid QaRunId{get;
        private set;
        }
        public int OpenFindingCount{get;
        private set;
        }
        public int CriticalCount{get;
        private set;
        }
        public int WarningCount{get;
        private set;
        }
        public int FlakyCount{get;
        private set;
        }
        public int ActiveWaiverCount{get;
        private set;
        }
        public decimal TechnicalDebtScore{get;
        private set;
        }
        public DateTimeOffset CapturedAtUtc{get;
        private set;
        }}

public sealed class QaTestImpactRule
{
    private QaTestImpactRule(){}
        public QaTestImpactRule(Guid id,Guid projectId,string pathPattern,string testKey,string commandJson,int priority,bool enabled,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        PathPattern=T(pathPattern,1000);
        TestKey=T(testKey,500);
        CommandJson=T(commandJson,8000);
        Priority=Math.Clamp(priority,-10000,10000);
        Enabled=enabled;
        CreatedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string PathPattern{get;
        private set;
        }=string.Empty;
        public string TestKey{get;
        private set;
        }=string.Empty;
        public string CommandJson{get;
        private set;
        }="{}";
        public int Priority{get;
        private set;
        }
        public bool Enabled{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }}
public sealed class QaArtifactRetentionRule
{
    private QaArtifactRetentionRule(){}
        public QaArtifactRetentionRule(Guid id,Guid projectId,string kindPattern,string retentionClass,int retentionDays,bool preserveOnCritical,bool enabled,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        KindPattern=T(kindPattern,200);
        RetentionClass=T(retentionClass,40);
        RetentionDays=Math.Clamp(retentionDays,1,3650);
        PreserveOnCritical=preserveOnCritical;
        Enabled=enabled;
        CreatedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string KindPattern{get;
        private set;
        }=string.Empty;
        public string RetentionClass{get;
        private set;
        }=string.Empty;
        public int RetentionDays{get;
        private set;
        }
        public bool PreserveOnCritical{get;
        private set;
        }
        public bool Enabled{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }}
public sealed class QaRunCostRecord
{
    private QaRunCostRecord(){}
        public QaRunCostRecord(Guid id,Guid projectId,Guid qaRunId,long wallDurationMs,long toolDurationMs,long artifactBytes,decimal estimatedCpuSeconds,string budgetVerdict,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        QaRunId=qaRunId;
        WallDurationMs=Math.Max(0,wallDurationMs);
        ToolDurationMs=Math.Max(0,toolDurationMs);
        ArtifactBytes=Math.Max(0,artifactBytes);
        EstimatedCpuSeconds=Math.Max(0,estimatedCpuSeconds);
        BudgetVerdict=budgetVerdict.Length<=40?budgetVerdict:budgetVerdict[..40];
        CapturedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid QaRunId{get;
        private set;
        }
        public long WallDurationMs{get;
        private set;
        }
        public long ToolDurationMs{get;
        private set;
        }
        public long ArtifactBytes{get;
        private set;
        }
        public decimal EstimatedCpuSeconds{get;
        private set;
        }
        public string BudgetVerdict{get;
        private set;
        }=string.Empty;
        public DateTimeOffset CapturedAtUtc{get;
        private set;
        }}
public sealed class QaBugMinimizationRecord
{
    private QaBugMinimizationRecord(){}
        public QaBugMinimizationRecord(Guid id,Guid projectId,Guid findingId,string originalInput,string minimizedInput,int originalUnits,int minimizedUnits,QaMinimizationStatus status,string detail,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        FindingId=findingId;
        OriginalInput=T(originalInput,16000);
        MinimizedInput=T(minimizedInput,16000);
        OriginalUnits=Math.Max(0,originalUnits);
        MinimizedUnits=Math.Max(0,minimizedUnits);
        Status=status;
        Detail=T(detail,4000);
        CreatedAtUtc=now;
        }
        public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid FindingId{get;
        private set;
        }
        public string OriginalInput{get;
        private set;
        }=string.Empty;
        public string MinimizedInput{get;
        private set;
        }=string.Empty;
        public int OriginalUnits{get;
        private set;
        }
        public int MinimizedUnits{get;
        private set;
        }
        public QaMinimizationStatus Status{get;
        private set;
        }
        public string Detail{get;
        private set;
        }=string.Empty;
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        private static string T(string? v,int m){var s=v?.Trim()??"";
        if(s.Length<1||s.Length>m)throw new ArgumentException();
        return s;
        }}
