// Purpose: Represents P er fo rm an ce Bu dg et Ev al ua ti on in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class PerformanceBudgetEvaluation
{
    private PerformanceBudgetEvaluation() { }
    public PerformanceBudgetEvaluation(Guid id,Guid projectId,Guid profileId,Guid? replayRunId,Guid? benchmarkRunId,PerformanceBudgetVerdict verdict,string summary,string detailJson,DateTimeOffset evaluatedAtUtc){if(projectId==Guid.Empty||profileId==Guid.Empty)throw new ArgumentException("Project and profile IDs are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        ProfileId=profileId;
        ReplayRunId=replayRunId;
        BenchmarkRunId=benchmarkRunId;
        Verdict=verdict;
        Summary=Required(summary,2000,nameof(summary));
        DetailJson=Required(detailJson,32000,nameof(detailJson));
        EvaluatedAtUtc=evaluatedAtUtc;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid ProfileId{get;
        private set;
        }
        public Guid? ReplayRunId{get;
        private set;
        }
        public Guid? BenchmarkRunId{get;
        private set;
        }
        public PerformanceBudgetVerdict Verdict{get;
        private set;
        }
        public string Summary{get;
        private set;
        }=string.Empty;
        public string DetailJson{get;
        private set;
        }="{}";
        public DateTimeOffset EvaluatedAtUtc{get;
        private set;
        }
    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;
        if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return v;
        }
}
