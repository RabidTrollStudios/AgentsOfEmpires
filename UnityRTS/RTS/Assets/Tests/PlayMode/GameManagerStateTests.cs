using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;
using Preloader;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for the GameManager Update() state machine:
	/// - SHOWING_WINNER with banner not expired: state remains SHOWING_WINNER
	/// - SHOWING_WINNER with expired banner, rounds not complete: transitions to RESTARTING
	/// - SHOWING_WINNER with expired banner, all rounds complete, dllNames null:
	///     transitions to FINISHED (via DisplaySingleAgentResults)
	///
	/// GameManager is on an inactive GO in tests so Update() is never called
	/// automatically; we drive it manually via reflection.
	/// </summary>
	[TestFixture]
	public class GameManagerStateTests : PlayModeTestBase
	{
		// ── Reflection helpers ─────────────────────────────────────────────────

		private static GameManager GM => GameManager.Instance;

		private static void SetProp(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GM, value);

		private static void SetField(string name, object value) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GM, value);

		/// <summary>
		/// Manually drives one Update() step on the inactive GameManager singleton.
		/// </summary>
		private static void InvokeUpdate() =>
			typeof(GameManager)
				.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GM, null);

		private static int GetGameStateInt()
		{
			var field = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return (int)field.GetValue(GM);
		}

		private static void SetGameState(int stateValue)
		{
			var field = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(GM, Enum.ToObject(field.FieldType, stateValue));
		}

		private static float GetTimeToDisplayBanner() =>
			(float)typeof(GameManager)
				.GetProperty("TimeToDisplayBanner", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(GM);

		// ── Factory helpers ────────────────────────────────────────────────────

		/// <summary>
		/// Creates a minimal GameOverUI (Canvas + Text child) and registers it for teardown.
		/// </summary>
		private GameObject MakeGameOverUI()
		{
			var root = new GameObject("TestGameOverUI");
			ctx.CreatedObjects.Add(root);
			root.AddComponent<Canvas>();
			var textGo = new GameObject("BannerText");
			textGo.transform.SetParent(root.transform);
			textGo.AddComponent<Text>();
			return root;
		}

		/// <summary>
		/// Creates a minimal PrefabLoader with the given GameOverUI and registers it for teardown.
		/// </summary>
		private PrefabLoader MakePrefabs(GameObject gameOverUI)
		{
			var go = new GameObject("TestPrefabLoader");
			ctx.CreatedObjects.Add(go);
			var prefabs = go.AddComponent<PrefabLoader>();
			prefabs.GameOverUI = gameOverUI;
			return prefabs;
		}

		// ── SHOWING_WINNER: banner not expired ────────────────────────────────

		[UnityTest]
		public IEnumerator Update_ShowingWinner_BannerNotExpired_StateRemainsShowingWinner()
		{
			SetGameState(2); // SHOWING_WINNER
			SetProp("TimeToDisplayBanner", 100f);

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(2, GetGameStateInt(),
				"gameState should remain SHOWING_WINNER (2) when TimeToDisplayBanner has not expired");
			Assert.Greater(GetTimeToDisplayBanner(), 0f,
				"TimeToDisplayBanner should still be positive after one frame");
		}

		// ── SHOWING_WINNER → RESTARTING ───────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_ShowingWinner_BannerExpired_RoundsNotComplete_TransitionsToRestarting()
		{
			SetGameState(2); // SHOWING_WINNER
			SetProp("TimeToDisplayBanner", -1f); // already expired

			// DetermineRoundsCompleted sums AgentWins: 0 + 0 = 0 != TotalNbrOfRounds (3)
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 0 },
				{ Constants.RED_ABBR,   0 }
			});
			GM.TotalNbrOfRounds = 3;

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(3, GetGameStateInt(),
				"gameState should transition to RESTARTING (3) when banner expires and rounds remain");
		}

		// ── SHOWING_WINNER → FINISHED ─────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_ShowingWinner_BannerExpired_AllRoundsComplete_DllNamesNull_TransitionsToFinished()
		{
			// Build minimal Prefabs so DisplaySingleAgentResults can write to GameOverUI
			var gameOverUI = MakeGameOverUI();
			var prefabs    = MakePrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetGameState(2); // SHOWING_WINNER
			SetProp("TimeToDisplayBanner", -1f);

			// sum = 1 == TotalNbrOfRounds (1) → FINISHED path
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 1 },
				{ Constants.RED_ABBR,   0 }
			});
			GM.TotalNbrOfRounds = 1;

			// Agents needed for CloseCommandLog and DisplaySingleAgentResults
			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, ctx.Agent0Go },
				{ 1, ctx.Agent1Go }
			});

			// dllNames is null by default in the test environment (no agentLoader)
			SetField("dllNames", null);

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(4, GetGameStateInt(),
				"gameState should transition to FINISHED (4) when all rounds complete and dllNames is null");
			Assert.AreEqual(3.0f, GetTimeToDisplayBanner(), 0.001f,
				"TimeToDisplayBanner should be reset to 3.0f after transitioning to FINISHED");
		}
	}
}
