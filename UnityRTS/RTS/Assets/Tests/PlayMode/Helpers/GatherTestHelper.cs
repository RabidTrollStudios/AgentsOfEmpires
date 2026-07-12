using System;
using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Helper utilities for gather-focused Play Mode tests.
	/// Provides shorthand for setting up gather scenarios and
	/// waiting for deposit milestones.
	/// </summary>
	public static class GatherTestHelper
	{
		/// <summary>
		/// Set up a standard gather scenario: place a built BASE, a MINE, and a PAWN.
		/// Does NOT start gathering — caller decides when to issue the command.
		/// </summary>
		/// <returns>Tuple of (pawn, mine, baseUnit).</returns>
		public static (Unit pawn, Unit mine, Unit baseUnit) SetupBasicGather(
			PlayModeTestContext ctx,
			Vector3Int basePos,
			Vector3Int minePos,
			Vector3Int pawnPos)
		{
			Unit baseUnit = PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent0Go, UnitType.BASE, basePos);
			baseUnit.IsBuilt = true;
			Unit mine = PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent0Go, UnitType.MINE, minePos);
			Unit pawn = PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent0Go, UnitType.PAWN, pawnPos);
			return (pawn, mine, baseUnit);
		}

		/// <summary>
		/// Wait until the agent's gold has increased by at least minimumIncrease
		/// from its initial value.
		/// </summary>
		public static IEnumerator WaitForGoldIncrease(
			Agent agent, int initialGold, int minimumIncrease,
			float timeoutSeconds = 10f)
		{
			// GameManager GO is inactive in tests → FixedUpdate never fires. Drive ticks
			// explicitly; timeoutSeconds is reinterpreted as a tick budget (×20 @ 20 Hz).
			int maxTicks = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds * 20f));
			int ticks = 0;
			while (agent.Gold < initialGold + minimumIncrease)
			{
				if (ticks++ >= maxTicks)
				{
					Assert.Fail(
						$"Gold did not increase by {minimumIncrease} within {timeoutSeconds}s " +
						$"(started at {initialGold}, now {agent.Gold})");
					yield break;
				}
				GameManager.Instance.SimulateTick();
				yield return null;
			}
		}

		/// <summary>
		/// Wait until at least nDeposits separate gold increases have been observed.
		/// Each time agent.Gold exceeds the last observed value, a deposit is counted.
		/// </summary>
		public static IEnumerator WaitForNDeposits(
			Agent agent, int nDeposits,
			float timeoutSeconds = 15f)
		{
			int depositsObserved = 0;
			int lastGold = agent.Gold;
			// GameManager GO is inactive in tests → FixedUpdate never fires. Drive ticks
			// explicitly; timeoutSeconds is reinterpreted as a tick budget (×20 @ 20 Hz).
			int maxTicks = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds * 20f));
			int ticks = 0;

			while (depositsObserved < nDeposits)
			{
				if (ticks++ >= maxTicks)
				{
					Assert.Fail(
						$"Did not observe {nDeposits} deposits within {timeoutSeconds}s " +
						$"(got {depositsObserved})");
					yield break;
				}
				GameManager.Instance.SimulateTick();
				if (agent.Gold > lastGold)
				{
					depositsObserved++;
					lastGold = agent.Gold;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Assert that the agent's gold increased by at least the expected amount
		/// compared to the recorded initial value.
		/// </summary>
		public static void AssertGoldIncreasedBy(Agent agent, int initialGold, int minimumIncrease)
		{
			Assert.GreaterOrEqual(
				agent.Gold - initialGold, minimumIncrease,
				$"Expected gold to increase by at least {minimumIncrease} " +
				$"but only increased by {agent.Gold - initialGold}");
		}

		/// <summary>
		/// Assert that the pawn is still in GATHER state (continuing to mine, not stuck).
		/// </summary>
		public static void AssertPawnStillGathering(Unit pawn)
		{
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should remain in GATHER state while mine is alive");
		}
	}
}
