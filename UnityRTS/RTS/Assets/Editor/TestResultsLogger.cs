using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Automatically writes test results to Logs/ after every test run.
/// EditMode results go to test-results-editmode.txt, PlayMode to test-results-playmode.txt.
/// Claude Code can then read those files without requiring copy-paste.
/// </summary>
[InitializeOnLoad]
public static class TestResultsLogger
{
    private static readonly string LogsDir =
        Path.Combine(Application.dataPath, "..", "Logs");

    static TestResultsLogger()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private class Callbacks : ICallbacks
    {
        private bool _isPlayMode;

        public void RunStarted(ITestAdaptor testsToRun)
        {
            // Detect mode from the root suite name (contains "PlayMode" or "EditMode")
            _isPlayMode = ContainsPlayMode(testsToRun);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string mode = _isPlayMode ? "PlayMode" : "EditMode";
            string fileName = _isPlayMode ? "test-results-playmode.txt" : "test-results-editmode.txt";
            string path = Path.Combine(LogsDir, fileName);

            int passed = 0, failed = 0, skipped = 0;
            CountResults(result, ref passed, ref failed, ref skipped);

            var sb = new StringBuilder();
            sb.AppendLine($"=== {mode} Test Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine($"Result: {result.TestStatus}  |  Duration: {result.Duration:F2}s");
            sb.AppendLine($"Passed: {passed}  Failed: {failed}  Skipped: {skipped}");
            sb.AppendLine();

            WriteResult(sb, result, 0);

            try
            {
                Directory.CreateDirectory(LogsDir);
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[TestResultsLogger] {mode} results written to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestResultsLogger] Failed to write results: {ex.Message}");
            }
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        private static bool ContainsPlayMode(ITestAdaptor node)
        {
            if (node == null) return false;
            if (node.FullName != null && node.FullName.Contains("PlayMode")) return true;
            if (node.HasChildren)
                foreach (var child in node.Children)
                    if (ContainsPlayMode(child)) return true;
            return false;
        }

        private static void CountResults(ITestResultAdaptor result, ref int passed, ref int failed, ref int skipped)
        {
            if (!result.HasChildren)
            {
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:  passed++;  break;
                    case TestStatus.Failed:  failed++;  break;
                    case TestStatus.Skipped: skipped++; break;
                }
                return;
            }
            foreach (var child in result.Children)
                CountResults(child, ref passed, ref failed, ref skipped);
        }

        private static void WriteResult(StringBuilder sb, ITestResultAdaptor result, int depth)
        {
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
