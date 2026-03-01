using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Automatically writes test results to Logs/test-results.txt after every test run.
/// Claude Code can then read that file without requiring copy-paste.
/// </summary>
[InitializeOnLoad]
public static class TestResultsLogger
{
    private static readonly string ResultsPath =
        Path.Combine(Application.dataPath, "..", "Logs", "test-results.txt");

    static TestResultsLogger()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Test Run Finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine($"Result: {result.TestStatus}  |  Duration: {result.Duration:F2}s");
            sb.AppendLine();

            WriteResult(sb, result, 0);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
                File.WriteAllText(ResultsPath, sb.ToString());
                Debug.Log($"[TestResultsLogger] Results written to {ResultsPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestResultsLogger] Failed to write results: {ex.Message}");
            }
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            // Individual failures are captured in RunFinished via tree traversal
        }

        private static void WriteResult(StringBuilder sb, ITestResultAdaptor result, int depth)
        {
            // Only print leaf tests (actual test methods), skip suite nodes
            bool isLeaf = !result.HasChildren;

            if (isLeaf)
            {
                string indent = new string(' ', depth * 2);
                string status = result.TestStatus switch
                {
                    TestStatus.Passed  => "PASS",
                    TestStatus.Failed  => "FAIL",
                    TestStatus.Skipped => "SKIP",
                    _                  => result.TestStatus.ToString()
                };

                sb.AppendLine($"{indent}[{status}] {result.Test.FullName}");

                if (result.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(result.Message))
                {
                    foreach (var line in result.Message.Split('\n'))
                        sb.AppendLine($"{indent}       {line.TrimEnd()}");
                }
            }

            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                    WriteResult(sb, child, depth + 1);
            }
        }
    }
}
