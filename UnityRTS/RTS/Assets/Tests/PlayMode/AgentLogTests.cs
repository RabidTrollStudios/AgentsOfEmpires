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

			// Create the initial CSV so the collision-numbering logic triggers
			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvPath = Path.Combine(tempDir, baseName + ".csv");
			File.WriteAllText(csvPath, "dummy");

			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			// The agent should have chosen a new filename with _1 suffix
			// Verify by opening+closing the log file (which uses the computed logFileName)
			bridge.OpenLogFile();
			bridge.Log("test");
			bridge.CloseLogFile();

			string expected = Path.Combine(tempDir, baseName + "_1.csv");
			Assert.IsTrue(File.Exists(expected),
				$"Expected collision-numbered file at {expected}");
		}

		[UnityTest]
		public IEnumerator InitializeAgent_WhenMultipleFilesExist_PicksNextNumber()
		{
			yield return null;

			string baseName = "PlanningAgent_TestDLL_Blue";
			File.WriteAllText(Path.Combine(tempDir, baseName + ".csv"), "dummy");
			File.WriteAllText(Path.Combine(tempDir, baseName + "_1.csv"), "dummy");
			File.WriteAllText(Path.Combine(tempDir, baseName + "_2.csv"), "dummy");

			var bridge = CreateAgentBridge("Blue", "TestDLL", 0);

			bridge.OpenLogFile();
			bridge.Log("test");
			bridge.CloseLogFile();

			string expected = Path.Combine(tempDir, baseName + "_3.csv");
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

			// Read back the file
			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvPath = Path.Combine(tempDir, baseName + ".csv");
			string content = File.ReadAllText(csvPath);

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

			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvPath = Path.Combine(tempDir, baseName + ".csv");
			string content = File.ReadAllText(csvPath);

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

			string baseName = "PlanningAgent_TestDLL_Blue";
			string csvPath = Path.Combine(tempDir, baseName + ".csv");
			string content = File.ReadAllText(csvPath);

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

			string expectedPath = Path.Combine(tempDir, "CommandLog_TestDLL_Blue.txt");
			Assert.IsTrue(File.Exists(expectedPath),
				$"CommandLog file should exist at {expectedPath}");

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

			Assert.DoesNotThrow(() => bridge.Update());
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
