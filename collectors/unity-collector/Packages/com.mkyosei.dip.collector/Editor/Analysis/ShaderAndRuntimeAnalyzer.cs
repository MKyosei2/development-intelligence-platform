using System;using System.IO;using System.Linq;using Dip.UnityCollector.Editor.Models;using UnityEditor;using UnityEngine;
namespace Dip.UnityCollector.Editor.Analysis
{
    public static class ShaderAndRuntimeAnalyzer
    {
        public static void Analyze(CollectorReport report,int maxScriptWarnings)
        {
            var shaderErrors=0,shaderWarnings=0;
                foreach(var guid in AssetDatabase.FindAssets("t:Shader")){var path=AssetDatabase.GUIDToAssetPath(guid);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
                if(shader==null)continue;
                try{foreach(var msg in ShaderUtil.GetShaderMessages(shader)){var severity=msg.severity.ToString();
                if(severity.Equals("Error",StringComparison.OrdinalIgnoreCase)){shaderErrors++;
                report.issues.Add(new CollectorIssue{category="shader",severity="Critical",title="Shader compile error",description=msg.message,source=path,ruleId="UNITY_SHADER_COMPILE"});
                }
                else{shaderWarnings++;
                if(shaderWarnings<=50)report.issues.Add(new CollectorIssue{category="shader",severity="Warning",title="Shader compiler warning",description=msg.message,source=path,ruleId="UNITY_SHADER_WARNING"});
                }}}
                catch(Exception ex){report.issues.Add(new CollectorIssue{category="collector",severity="Info",title="Shader message inspection skipped",description=ex.Message,source=path});
                }}
            var updateAllocations=0;
                var saveWithoutVersion=0;
                foreach(var guid in AssetDatabase.FindAssets("t:MonoScript")){var path=AssetDatabase.GUIDToAssetPath(guid);
                string text;
                try{text=File.ReadAllText(path);
                }
                catch{continue;
                }var lines=text.Split('\n');
                for(var i=0;i<lines.Length;i++){if(!(lines[i].Contains("Update(")||lines[i].Contains("LateUpdate(")||lines[i].Contains("FixedUpdate(")))continue;
                var end=Math.Min(lines.Length,i+80);
                for(var j=i;j<end;j++)if(lines[j].Contains("new ")||lines[j].Contains(".ToList(")||lines[j].Contains(".ToArray(")||lines[j].Contains("string.Format(")){updateAllocations++;
                if(updateAllocations<=maxScriptWarnings)report.issues.Add(new CollectorIssue{category="runtime-allocation",severity="Warning",title="Potential per-frame managed allocation",description=$"Allocation-like expression appears near a frame callback at line {j+1}. Confirm with the Unity Profiler before optimizing.",source=path,ruleId="UNITY_FRAME_ALLOC"});
                break;
                }}
                if(text.Contains("PlayerPrefs.",StringComparison.Ordinal)&&!text.Contains("saveVersion",StringComparison.OrdinalIgnoreCase)&&!text.Contains("schemaVersion",StringComparison.OrdinalIgnoreCase)){saveWithoutVersion++;
                if(saveWithoutVersion<=maxScriptWarnings)report.issues.Add(new CollectorIssue{category="save-compatibility",severity="Warning",title="Save data has no obvious schema/version key",description="PlayerPrefs usage was found without an obvious save/schema version token. Verify backward-compatible migrations for released builds.",source=path,ruleId="UNITY_SAVE_VERSION"});
                }}
            report.metrics.Add(new CollectorMetric{name="shader_compile_error_count",value=shaderErrors,unit="count"});
                report.metrics.Add(new CollectorMetric{name="shader_compile_warning_count",value=shaderWarnings,unit="count"});
                report.metrics.Add(new CollectorMetric{name="potential_frame_allocation_site_count",value=updateAllocations,unit="count"});
                report.metrics.Add(new CollectorMetric{name="save_compatibility_review_count",value=saveWithoutVersion,unit="count"});
        }
    }
}
