// Purpose: Represents M et ri c in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class Metric { private Metric(){}
    public Metric(Guid id,Guid runId,string name,decimal value,string unit,decimal? baseline,Guid? baselinePipelineRunId=null,string? environmentId=null,string? scenarioId=null,string? platform=null,string? buildConfiguration=null,string? benchmarkVersion=null){Id=id==Guid.Empty?Guid.NewGuid():id;
    PipelineRunId=runId;
    Name=Req(name,120);
    Value=value;
    Unit=Req(unit,30);
    BaselineValue=baseline;
    BaselinePipelineRunId=baselinePipelineRunId;
    EnvironmentId=Opt(environmentId,120);
    ScenarioId=Opt(scenarioId,120);
    Platform=Opt(platform,80);
    BuildConfiguration=Opt(buildConfiguration,80);
    BenchmarkVersion=Opt(benchmarkVersion,80);
    if(baseline.HasValue){Difference=value-baseline.Value;
    DifferencePercent=baseline.Value==0?null:decimal.Round((value-baseline.Value)/baseline.Value*100m,2);
    ComparisonStatus=baseline.Value==0?MetricComparisonStatus.NotComparable:MetricComparisonStatus.Comparable;
    }
    else ComparisonStatus=MetricComparisonStatus.BaselineMissing;
    }
    public Guid Id{get;
    private set;
    }
    public Guid PipelineRunId{get;
    private set;
    }
    public Guid? BaselinePipelineRunId{get;
    private set;
    }
    public string Name{get;
    private set;
    }=string.Empty;
    public decimal Value{get;
    private set;
    }
    public string Unit{get;
    private set;
    }=string.Empty;
    public decimal? BaselineValue{get;
    private set;
    }
    public decimal? Difference{get;
    private set;
    }
    public decimal? DifferencePercent{get;
    private set;
    }
    public Severity Severity{get;
    private set;
    }=Severity.Info;
    public MetricComparisonStatus ComparisonStatus{get;
    private set;
    }
    public string? EnvironmentId{get;
    private set;
    }
    public string? ScenarioId{get;
    private set;
    }
    public string? Platform{get;
    private set;
    }
    public string? BuildConfiguration{get;
    private set;
    }
    public string? BenchmarkVersion{get;
    private set;
    }
    public void SetSeverity(Severity s)=>Severity=s;
    public void MarkEnvironmentMismatch()=>ComparisonStatus=MetricComparisonStatus.EnvironmentMismatch;
    private static string Req(string? v,int max){var s=v?.Trim()??"";
    if(s.Length is <1||s.Length>max)throw new ArgumentException();
    return s;
    }
    private static string? Opt(string? v,int max){if(string.IsNullOrWhiteSpace(v))return null;
    var s=v.Trim();
    if(s.Length>max)throw new ArgumentException();
    return s;
    } }