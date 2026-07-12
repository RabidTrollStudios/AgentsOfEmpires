using System;
using System.Collections;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Base class for all Play Mode test fixtures.
	/// Provides setup/teardown of the synthetic game environment
	/// and convenience helpers for placing units and waiting.
	/// </summary>
	public abstract class PlayModeTestBase
	{
		protected PlayModeTestContext ctx;

		[UnitySetUp]
		public IEnumerator SetUp()
		{
			ctx = PlayModeTestHelper.BuildTestEnvironment();
			// Yield one frame so Unity processes Awake/Start on any MonoBehaviours
			yield return null;
		}

		[UnityTearDown]
		public IEnumerator TearDown()
		{
			// Set gameState to FINISHED so Unit.Update/FixedUpdate early-return via IsPlaying
			// before we destroy anything. This prevents NREs during the teardown frame.
			if (GameManager.Instance != null)
			{
				var gameStateField = typeof(GameManager).GetField("gameState",
					BindingFlags.NonPublic | BindingFlags.Instance);
				gameStateField.SetValue(GameManager.Instance,
					System.Enum.ToObject(gameStateField.FieldType, 4)); // FINISHED = 4
			}

			PlayModeTestHelper.TearDown(ctx);
			ctx = null;
			// Yield one frame so Object.Destroy calls are processed
			yield return null;

			// Clear the singleton AFTER units are destroyed
			typeof(GameManager)
				.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, null);
		}

		/// <summary>
		/// Place a unit owned by agent 0 at the given grid position.
		/// </summary>
		protected Unit PlaceUnit(UnitType unitType, Vector3Int gridPosition)
		{
			return PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent0Go, unitType, gridPosition);
		}

		/// <summary>
		/// Place a unit owned by a specific agent at the given grid position.
		/// </summary>
		protected Unit PlaceUnit(UnitType unitType, Vector3Int gridPosition, GameObject agentGo)
		{
			return PlayModeTestHelper.PlaceUnit(ctx, agentGo, unitType, gridPosition);
		}

		/// <summary>
		/// Get the Agent component for agent 0.
		/// </summary>
		protected Agent GetAgent0()
		{
			return ctx.GetAgent(0);
		}

		/// <summary>
		/// Wait until predicate is true, or fail after timeout.
		/// </summary>
		protected IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 10f,
			string failMessage = "Timed out waiting for condition")
		{
			float elapsed = 0f;
			while (!predicate())
			{
				elapsed += Time.deltaTime;
				if (elapsed > timeoutSeconds)
				{
					Assert.Fail(failMessage + $" (waited {timeoutSeconds}s)");
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Drive game ticks until the predicate is true, or fail after maxTicks.
		///
		/// Use this instead of <see cref="WaitUntil"/> whenever the condition depends
		/// on the game advancing (movement, combat, gather/build progress, unit death).
		/// The test GameManager lives on an INACTIVE GameObject, so its FixedUpdate
		/// never fires — nothing advances unless we tick explicitly. Each iteration
		/// calls GameManager.SimulateTick() (advances all units via the shared
		/// TickEngine), then yields one frame so coroutine-style state settles.
		/// </summary>
		/// <remarks>
		/// Signature is drop-in compatible with <see cref="WaitUntil"/> so call sites
		/// that relied on background ticking can switch by renaming the call. The
		/// <paramref name="timeoutSeconds"/> value is reinterpreted as a tick budget
		/// (ticks = timeoutSeconds * 20, matching the 20 Hz tick rate) so existing
		/// timeouts stay generous.
		/// </remarks>
		protected IEnumerator WaitForTick(Func<bool> predicate, float timeoutSeconds = 10f,
			string failMessage = "Condition not met within tick budget")
		{
			int maxTicks = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds * 20f));
			int ticks = 0;
			while (!predicate())
			{
				if (ticks++ >= maxTicks)
				{
					Assert.Fail(failMessage + $" (drove {maxTicks} ticks)");
					yield break;
				}
				GameManager.Instance.SimulateTick();
				yield return null;
			}
		}

		/// <summary>
		/// Wait a fixed number of frames.
		/// </summary>
		protected IEnumerator WaitFrames(int count)
		{
			for (int i = 0; i < count; i++)
				yield return null;
		}

		/// <summary>
		/// Wait for a fixed number of FixedUpdate cycles.
		/// </summary>
		protected IEnumerator WaitFixedFrames(int count)
		{
			for (int i = 0; i < count; i++)
				yield return new WaitForFixedUpdate();
		}
	}
}
