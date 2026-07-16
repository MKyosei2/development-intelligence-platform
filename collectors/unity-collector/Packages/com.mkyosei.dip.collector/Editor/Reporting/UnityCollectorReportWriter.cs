using System.IO;
using Dip.UnityCollector.Editor.Models;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Reporting
{
    public static class UnityCollectorReportWriter
    {
        public static void Write(string outputPath, CollectorReport report)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonUtility.ToJson(report, true);
            File.WriteAllText(outputPath, json);
        }
    }
}
