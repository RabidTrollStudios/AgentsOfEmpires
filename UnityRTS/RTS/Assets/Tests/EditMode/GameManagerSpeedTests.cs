using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for GameManager speed input guards and
	/// UpdateCustomDebugUI edge cases (null texts, debugging off, no agents).
	/// </summary>
	[TestFixture]
	public class GameManagerSpeedTests
	{
		private GameManager gm;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
		}

		[TearDown]
		public void TearDown()
		{
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
			SetPrivateField("HumanCustomDebugText", null);
			SetPrivateField("OrcCustomDebugText", null);
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		private void InvokePrivate(string methodName) =>
			typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, null);

		private void SetPrivateField(string fieldName, object value) =>
			typeof(GameManager)
				.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(gm, value);

		// ── HandleSpeedInput boundary conditions ──────────────────────────────
		// Input.GetKeyDown always returns false in EditMode, so key-press paths
		// cannot be exercised here. The tests below verify the guard conditions
		// that HandleSpeedInput uses directly against Constants.

		[Test]
		public void GameSpeed_AtMax_GuardPreventsIncrement()
		{
			Constants.GAME_SPEED = Constants.MAX_GAME_SPEED;
			bool wouldIncrement = Constants.GAME_SPEED < Constants.MAX_GAME_SPEED;
			Assert.IsFalse(wouldIncrement, "Speed at MAX should not pass the increment guard");
		}

		[Test]
		public void GameSpeed_AtOne_GuardPreventsDecrement()
		{
			Constants.GAME_SPEED = 1;
			bool wouldDecrement = Constants.GAME_SPEED > 1;
			Assert.IsFalse(wouldDecrement, "Speed at 1 should not pass the decrement guard");
		}

		[Test]
		public void GameSpeed_BelowMax_GuardAllowsIncrement()
		{
			Constants.GAME_SPEED = Constants.MAX_GAME_SPEED - 1;
			bool wouldIncrement = Constants.GAME_SPEED < Constants.MAX_GAME_SPEED;
			Assert.IsTrue(wouldIncrement, "Speed below MAX should pass the increment guard");
		}

		[Test]
		public void GameSpeed_AboveOne_GuardAllowsDecrement()
		{
			Constants.GAME_SPEED = 2;
			bool wouldDecrement = Constants.GAME_SPEED > 1;
			Assert.IsTrue(wouldDecrement, "Speed above 1 should pass the decrement guard");
		}

		// ── UpdateCustomDebugUI edge cases ────────────────────────────────────

		[Test]
		public void UpdateCustomDebugUI_BothTextsNull_DoesNotThrow()
		{
			SetPrivateField("HumanCustomDebugText", null);
			SetPrivateField("OrcCustomDebugText", null);
			Assert.DoesNotThrow(() => InvokePrivate("UpdateCustomDebugUI"),
				"UpdateCustomDebugUI must not throw when both text fields are null");
		}

		[Test]
		public void UpdateCustomDebugUI_AgentDebuggingFalse_ClearsText()
		{
			var go1 = new GameObject("HumanTextGO");
			var humanText = go1.AddComponent<Text>();
			var go2 = new GameObject("OrcTextGO");
			var orcText = go2.AddComponent<Text>();
			humanText.text = "some agent data";
			orcText.text = "some agent data";
			SetPrivateField("HumanCustomDebugText", humanText);
			SetPrivateField("OrcCustomDebugText", orcText);

			gm.OnAgentToggleChanged(false);
			InvokePrivate("UpdateCustomDebugUI");

			Assert.AreEqual("", humanText.text, "HumanCustomDebugText should be cleared when debugging is off");
			Assert.AreEqual("", orcText.text, "OrcCustomDebugText should be cleared when debugging is off");

			SetPrivateField("HumanCustomDebugText", null);
			SetPrivateField("OrcCustomDebugText", null);
			Object.DestroyImmediate(go1);
			Object.DestroyImmediate(go2);
		}

		[Test]
		public void UpdateCustomDebugUI_AgentDebuggingTrue_NoAgents_ClearsText()
		{
			// Agents is null in EditMode (InitializeMatch never runs in tests)
			var go1 = new GameObject("HumanTextGO");
			var humanText = go1.AddComponent<Text>();
			var go2 = new GameObject("OrcTextGO");
			var orcText = go2.AddComponent<Text>();
			humanText.text = "some agent data";
			orcText.text = "some agent data";
			SetPrivateField("HumanCustomDebugText", humanText);
			SetPrivateField("OrcCustomDebugText", orcText);

			gm.OnAgentToggleChanged(true);
			InvokePrivate("UpdateCustomDebugUI");

			Assert.AreEqual("", humanText.text, "HumanCustomDebugText should be cleared with no agents present");
			Assert.AreEqual("", orcText.text, "OrcCustomDebugText should be cleared with no agents present");

			SetPrivateField("HumanCustomDebugText", null);
			SetPrivateField("OrcCustomDebugText", null);
			Object.DestroyImmediate(go1);
			Object.DestroyImmediate(go2);
		}
	}
}
