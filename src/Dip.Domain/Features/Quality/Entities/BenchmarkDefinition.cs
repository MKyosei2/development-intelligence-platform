// Purpose: Represents B en ch ma rk De fi ni ti on in the DIP domain.
namespace Dip.Domain.Entities;
public sealed class BenchmarkDefinition
{
    private BenchmarkDefinition() { }
    public BenchmarkDefinition(Guid id,string key,string name,string kind,string configurationJson,bool enabled,DateTimeOffset createdAtUtc)
    {Id=id==Guid.Empty?Guid.NewGuid():id;Key=Req(key,160);Name=Req(name,200);Kind=Req(kind,80);ConfigurationJson=Req(configurationJson,32000);Enabled=enabled;CreatedAtUtc=createdAtUtc;}
    public Guid Id{get;private set;} public string Key{get;private set;}=string.Empty; public string Name{get;private set;}=string.Empty; public string Kind{get;private set;}=string.Empty; public string ConfigurationJson{get;private set;}="{}"; public bool Enabled{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;}
    private static string Req(string?v,int max){var s=v?.Trim()??string.Empty;if(s.Length is <1||s.Length>max)throw new ArgumentException();return s;}
}
