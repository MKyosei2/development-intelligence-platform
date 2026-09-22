// Purpose: Provides the infrastructure capability represented by R el ea se Pr ov en an ce Co ll ec to r.
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Dip.Application.Abstractions.Provenance;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceCollector(IOptions<ReleaseProvenanceOptions> options):IReleaseProvenanceCollector
{
    private readonly ReleaseProvenanceOptions _options=options.Value;

    public Task<ProvenanceSnapshot> CollectAsync(string repositoryRoot,CancellationToken ct)
    {
        var allowed=Path.GetFullPath(_options.AllowedRoot);
            var root=Path.IsPathRooted(repositoryRoot)?Path.GetFullPath(repositoryRoot):Path.GetFullPath(Path.Combine(allowed,repositoryRoot));
            EnsureInsideRoot(root,allowed);
            if(!Directory.Exists(root))throw new DirectoryNotFoundException(root);
        var components=new List<ProvenanceComponent>();
        foreach(var file in EnumerateSafe(root,"*.csproj",500)){ct.ThrowIfCancellationRequested();
            try{var doc=XDocument.Load(file);
            foreach(var package in doc.Descendants().Where(x=>x.Name.LocalName=="PackageReference")){var name=package.Attribute("Include")?.Value??package.Attribute("Update")?.Value;
            if(string.IsNullOrWhiteSpace(name))continue;
            var version=package.Attribute("Version")?.Value??package.Elements().FirstOrDefault(x=>x.Name.LocalName=="Version")?.Value??"unspecified";
            components.Add(new("library",name,version,Relative(root,file),null));
            }}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Xml.XmlException){}}
        foreach(var file in EnumerateSafe(root,"package.json",100)){ct.ThrowIfCancellationRequested();
            try{using var doc=JsonDocument.Parse(File.ReadAllText(file));
            AddNpm(doc.RootElement,"dependencies",file,root,components);
            AddNpm(doc.RootElement,"devDependencies",file,root,components);
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){components.Add(new("application","npm-package-manifest","unparsed",Relative(root,file),Hash(file)));
            }}
        foreach(var file in EnumerateSafe(root,"requirements*.txt",100)){ct.ThrowIfCancellationRequested();
            foreach(var line in File.ReadLines(file).Select(x=>x.Trim()).Where(x=>x.Length>0&&!x.StartsWith('#')).Take(2000)){var split=line.Split(new[]{"==",">=","<=","~=","!="},2,StringSplitOptions.None);
            components.Add(new("library",split[0].Trim(),split.Length>1?split[1].Trim():"unspecified",Relative(root,file),null));
            }}
        var evidenceFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(x=>!IsIgnoredPath(root,x)).Where(IsReleaseEvidenceFile).Take(1000)){ct.ThrowIfCancellationRequested();
            try{evidenceFiles[Relative(root,file)]=Hash(file);
            }
            catch(IOException){}}
        var snapshot=new ProvenanceSnapshot(root,System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,System.Runtime.InteropServices.RuntimeInformation.OSDescription,components.GroupBy(x=>(x.Type,x.Name,x.Version,x.Path)).Select(x=>x.First()).OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray(),evidenceFiles);return Task.FromResult(snapshot);
    }

    private static IEnumerable<string> EnumerateSafe(string root,string pattern,int take)=>Directory.EnumerateFiles(root,pattern,SearchOption.AllDirectories).Where(x=>!IsIgnoredPath(root,x)).Take(take);
    private static bool IsIgnoredPath(string root,string path){var rel=Relative(root,path);return rel.Split('/').Any(x=>x is ".git" or "bin" or "obj" or "node_modules" or "__pycache__");}
    private static void AddNpm(JsonElement rootElement,string property,string file,string root,List<ProvenanceComponent> components){if(!rootElement.TryGetProperty(property,out var deps)||deps.ValueKind!=JsonValueKind.Object)return;
        foreach(var dep in deps.EnumerateObject())components.Add(new("library",dep.Name,dep.Value.GetString()??"unspecified",Relative(root,file),null));
        }
    private static bool IsReleaseEvidenceFile(string path){var name=Path.GetFileName(path);
        var ext=Path.GetExtension(path).ToLowerInvariant();
        return name.Equals("Directory.Packages.props",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".lock.json",StringComparison.OrdinalIgnoreCase)||ext is ".dll" or ".exe" or ".zip" or ".nupkg"||(ext==".json"&&name.Contains("manifest",StringComparison.OrdinalIgnoreCase));
        }
    private static string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
    private static string Relative(string root,string file)=>Path.GetRelativePath(root,file).Replace('\\','/');
    private static void EnsureInsideRoot(string candidate,string root){var relative=Path.GetRelativePath(root,candidate);if(relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal)||Path.IsPathRooted(relative))throw new UnauthorizedAccessException("Release provenance RepositoryRoot must stay inside configured ReleaseProvenance:AllowedRoot.");}
}
