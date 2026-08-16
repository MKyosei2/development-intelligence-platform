using System;using System.IO;using System.Text;using Dip.UnityCollector.Editor.Models;using UnityEngine;
namespace Dip.UnityCollector.Editor.Reporting
{
    public static class UnityCollectorReportWriter
    {
        public static void Write(string path,CollectorReport report){if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Collector output path is required.",nameof(path));var full=Path.GetFullPath(path);var directory=Path.GetDirectoryName(full);if(!string.IsNullOrWhiteSpace(directory))Directory.CreateDirectory(directory);File.WriteAllText(full,JsonUtility.ToJson(report,true),new UTF8Encoding(false));}
    }
}
