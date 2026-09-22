// Purpose: Defines a focused responsibility within the development intelligence platform.
#if UNITY_INCLUDE_TESTS
using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace DIP.Collector
{
    [InitializeOnLoad]
    internal static class DipTestCollector
    {
        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result)
                => DipEmitter.Emit(23, metadata: new DipMetadata
                {
                    buildResult = result.TestStatus.ToString(),
                    totalErrors = result.FailCount,
                    totalWarnings = result.SkipCount
                });
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }

        static DipTestCollector()
        {
            try { ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks()); }
            catch (Exception) {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
    }
}
#endif
