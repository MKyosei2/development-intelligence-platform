using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using UnityEngine;

namespace DIP.Collector
{
    internal static class DipNamedPipeClient
    {
        public static bool Send(string json)
        {
            var pipeName = Environment.GetEnvironmentVariable("DIP_AGENT_PIPE_NAME");
            if (string.IsNullOrWhiteSpace(pipeName)) return false;
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                pipe.Connect(250);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("DIP collector bridge unavailable: " + exception.Message);
                return false;
            }
        }
    }
}
