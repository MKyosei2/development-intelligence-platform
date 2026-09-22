using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.RegularExpressions;using Dip.UnityCollector.Editor.Models;using UnityEditor;
namespace Dip.UnityCollector.Editor.Analysis
{
    public static class AssetDependencyAnalyzer
    {
        private static readonly Regex GuidRef=new Regex(@"guid:\s*([0-9a-fA-F]{32})",RegexOptions.Compiled);
        private static readonly HashSet<string> Builtin=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"0000000000000000e000000000000000","0000000000000000f000000000000000"};
        public static void Analyze(CollectorReport report,int maxFindings)
        {
            var unresolved=0;var scanned=0;var referenced=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var guids=AssetDatabase.FindAssets(string.Empty,new[]{"Assets"});
            foreach(var guid in guids){var path=AssetDatabase.GUIDToAssetPath(guid);
                var ext=Path.GetExtension(path).ToLowerInvariant();
                if(ext!=".unity"&&ext!=".prefab"&&ext!=".asset"&&ext!=".mat"&&ext!=".controller")continue;
                string text;
                try{text=File.ReadAllText(path);
                }
                catch{continue;
                }scanned++;
                foreach(Match m in GuidRef.Matches(text)){var g=m.Groups[1].Value;
                if(Builtin.Contains(g)||!referenced.Add(g))continue;
                if(string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(g))){unresolved++;
                if(unresolved<=maxFindings)report.issues.Add(new CollectorIssue{category="asset-reference",severity="Critical",title="Unresolved serialized asset GUID",description=$"Serialized asset reference points to missing GUID {g}.",source=path,ruleId="UNITY_MISSING_GUID"});
                }}}
            var usesAddressables=AssetDatabase.FindAssets("t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).Any(p=>{try{return File.ReadAllText(p).Contains("UnityEngine.AddressableAssets",StringComparison.Ordinal);}catch{return false;}});
                if(usesAddressables&&!AssetDatabase.IsValidFolder("Assets/AddressableAssetsData"))report.issues.Add(new CollectorIssue{category="addressables",severity="Warning",title="Addressables API used without AddressableAssetsData",description="Scripts reference Unity Addressables, but the expected AddressableAssetsData folder was not found. Verify initialization/build configuration.",source="Assets",ruleId="UNITY_ADDRESSABLES_CONFIG"});
            report.metrics.Add(new CollectorMetric{name="serialized_asset_files_scanned",value=scanned,unit="count"});report.metrics.Add(new CollectorMetric{name="unresolved_asset_guid_count",value=unresolved,unit="count"});
        }
    }
}
