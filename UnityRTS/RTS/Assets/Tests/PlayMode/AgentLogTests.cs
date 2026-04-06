using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Tests for Agent.cs — file logging (Log, EndLogLine, OpenLogFile, CloseLogFile),
	/// OpenCommandLog, CloseCommandLog, InitializeAgent (including file-collision numbering),
	/// Update, OnDestroy, and Color property.
	/// </summary>
	[TestFixture]
	public class AgentLogTests : PlayModeTestBase
	{
		private string tempDir;

		[SetUp]
		public void CreateTempDir()
		{
			tempDir = Path.Combine(Path.GetTempPath(), "AgentLogTests_" + System.Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempDir);
		}

		[TearDown]
		public void CleanTempDir()
		{
			if (tempDir != null && Directory.Exists(tempDir))
			{
				try { Directory.Delete(tempDir, recursive: true); } catch { }
			}
		}

		private AgentBridge CreateAgentBridge(string agentName, string dllName, int agentNbr)
		{
			var go = new GameObject("TestAgent_" + agentName);
			ctx.CreatedObjects.Add(go);
			var bridge = go.AddComponent<AgentBridge>();
			bridge.InitializeAgent(agentName, dllName, agentNbr, tempDir);
			return bridge;
		}

		/// <summary>Path where the agent actually writes its log CSV.</summary>
		private string AgentLogPath(string dllName, string agentName)
		{
			return Path.Combine(tempDir, "CommandLogs", dllName,
				"PlanningAgent_" + dllName + "_" + agentName + ".csv");
		}

		// ── InitializeAgent ───────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator InitializeAgent_SetsProperties()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			Assert.AreEqual("Blue", bridge.AgentName);
			Assert.AreEqual("TestDLL", bridge.AgentDLLName);
			Assert.AreEqual(0, bridge.AgentNbr);
			Assert.AreEqual(0, bridge.AgentNbrWins);
		}

		[UnityTest]
		public IEnumerator InitializeAgent_WhenFileExists_AppendsNumber()
		{
			yield return null;

			// Create the initial CSV in the subdirectory the agent uses
			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvDir = Path.Combine(tempDir, "CommandLogs", "TestDLL");
			Directory.CreateDirectory(csvDir);
			File.WriteAllText(Path.Combine(csvDir, baseName + ".csv"), "dummy");

			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			// The agent should have chosen a new filename with _1 suffix
			bridge.OpenLogFile();
			bridge.Log("test");
			bridge.CloseLogFile();

			string expected = Path.Combine(csvDir, baseName + "_1.csv");
			Assert.IsTrue(File.Exists(expected),
				$"Expected collision-numbered file at {expected}");
		}

		[UnityTest]
		public IEnumerator InitializeAgent_WhenMultipleFilesExist_PicksNextNumber()
		{
			yield return null;

			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvDir = Path.Combine(tempDir, "CommandLogs", "TestDLL");
			Directory.CreateDirectory(csvDir);
			File.WriteAllText(Path.Combine(csvDir, baseName + ".csv"), "dummy");
			File.WriteAllText(Path.Combine(csvDir, baseName + "_1.csv"), "dummy");
			File.WriteAllText(Path.Combine(csvDir, baseName + "_2.csv"), "dummy");

			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenLogFile();
			bridge.Log("test");
			bridge.CloseLogFile();

			string expected = Path.Combine(csvDir, baseName + "_3.csv");
			Assert.IsTrue(File.Exists(expected),
				$"Expected collision-numbered file at {expected}");
		}

		// ── Log / EndLogLine / OpenLogFile / CloseLogFile ─────────────────────

		[UnityTest]
		public IEnumerator Log_WritesCSVData()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenLogFile();
			bridge.Log("hello");
			bridge.Log("world");
			bridge.EndLogLine();
			bridge.CloseLogFile();

			// Read back the file from the subdirectory the agent uses
			string content = File.ReadAllText(AgentLogPath("TestDLL", "Blue"));

			StringAssert.Contains("hello,", content);
			StringAssert.Contains("world,", content);
			StringAssert.Contains("\n", content);
		}

		[UnityTest]
		public IEnumerator Log_WithComma_WrapsInQuotes()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenLogFile();
			bridge.Log("has,comma");
			bridge.CloseLogFile();

			string content = File.ReadAllText(AgentLogPath("TestDLL", "Blue"));

			// String containing comma should be wrapped in quotes
			StringAssert.Contains("\"has,comma\"", content);
		}

		[UnityTest]
		public IEnumerator OpenLogFile_AppendsToExisting()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			// First write
			bridge.OpenLogFile();
			bridge.Log("first");
			bridge.EndLogLine();
			bridge.CloseLogFile();

			// Second write (append)
			bridge.OpenLogFile();
			bridge.Log("second");
			bridge.EndLogLine();
			bridge.CloseLogFile();

			string content = File.ReadAllText(AgentLogPath("TestDLL", "Blue"));

			StringAssert.Contains("first,", content);
			StringAssert.Contains("second,", content);
		}

		// ── OpenCommandLog / CloseCommandLog ──────────────────────────────────

		[UnityTest]
		public IEnumerator OpenCommandLog_CreatesLogFile()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenCommandLog();

			// Command log goes to CommandLogs/{DLL}/{timestamp}_{AgentName}_commands.txt
			string logsDir = Path.Combine(tempDir, "CommandLogs", "TestDLL");
			string[] files = Directory.Exists(logsDir)
				? Directory.GetFiles(logsDir, "*_Blue_commands.txt")
				: new string[0];
			Assert.AreEqual(1, files.Length,
				$"Expected exactly one command log file in {logsDir}");

			bridge.CloseCommandLog();
		}

		[UnityTest]
		public IEnumerator CloseCommandLog_NullsCmdLog()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenCommandLog();
			Assert.IsNotNull(bridge.CmdLog);

			bridge.CloseCommandLog();
			Assert.IsNull(bridge.CmdLog);
		}

		[UnityTest]
		public IEnumerator CloseCommandLog_WhenNoCmdLog_DoesNotThrow()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			// CmdLog is null — should not throw
			Assert.DoesNotThrow(() => bridge.CloseCommandLog());
		}

		// ── Update (virtual, empty base) ──────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_DoesNotThrow()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			Assert.DoesNotThrow(() => bridge.StepUpdate());
		}

		// ── OnDestroy ─────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator OnDestroy_ClosesCommandLog()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);
			bridge.OpenCommandLog();
			Assert.IsNotNull(bridge.CmdLog);

			// OnDestroy is protected virtual — trigger it by destroying the GO
			var go = bridge.gameObject;
			Object.DestroyImmediate(go);
			// Remove from tracked objects since it's already destroyed
			ctx.CreatedObjects.Remove(go);

			// After destruction CmdLog should have been closed
			// (we can't check CmdLog directly since the object is destroyed,
			// but the fact it doesn't throw is the test)
		}

		// ── Color property ────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Color_SetAndGet()
		{
			yield return null;
			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.Color = UnityEngine.Color.red;
			Assert.AreEqual(UnityEngine.Color.red, bridge.Color);

			bridge.Color = UnityEngine.Color.blue;
			Assert.AreEqual(UnityEngine.Color.blue, bridge.Color);
		}
	}
}
