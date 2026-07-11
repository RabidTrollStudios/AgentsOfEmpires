using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Unit partial — command initiation (StartTraining, StartBuilding, StartGathering, etc.).
	///
	/// TEST-ONLY ENTRY POINTS. Production drives units through
	/// AgentActionsAdapter → DeferredCommandQueue → AgentSDK.CommandProcessor. These
	/// StartX methods exist so PlayMode tests can put a unit into an action state with
	/// a single call. To avoid a second, divergent copy of command logic they now
	/// forward to the SAME shared CommandProcessor the production path uses — so tests
	/// exercise the real engine. They must not be reintroduced into the live game loop.
	/// </summary>
	public partial class Unit
	{
		#region Start Actions (test-only shims over the shared CommandProcessor)

		private ITickWorld TickWorld => GameManager.Instance.GetTickWorld();

		/// <summary>Start training another unit (test-only; forwards to CommandProcessor.ProcessTrain).</summary>
		internal void StartTraining(TrainEventArgs args)
		{
			CommandProcessor.ProcessTrain(this, args.UnitType, TickWorld);
		}

		/// <summary>Start building a structure (test-only; forwards to CommandProcessor.ProcessBuild).</summary>
		internal void StartBuilding(BuildEventArgs args)
		{
			CommandProcessor.ProcessBuild(
				this, new Position(args.TargetPosition.x, args.TargetPosition.y),
				args.UnitType, TickWorld);
		}

		/// <summary>Start moving to a target cell (test-only; forwards to CommandProcessor.ProcessMove).</summary>
		internal void StartMoving(MoveEventArgs args)
		{
			CommandProcessor.ProcessMove(
				this, new Position(args.Target.x, args.Target.y), TickWorld);
		}

		/// <summary>Start gathering from a mine to a base (test-only; forwards to CommandProcessor.ProcessGather).</summary>
		internal void StartGathering(GatherEventArgs args)
		{
			CommandProcessor.ProcessGather(
				this, args.ResourceUnit.UnitNbr, args.BaseUnit.UnitNbr, TickWorld);
		}

		/// <summary>Start attacking a target (test-only; forwards to CommandProcessor.ProcessAttack).</summary>
		internal void StartAttacking(AttackEventArgs args)
		{
			CommandProcessor.ProcessAttack(this, args.Target.UnitNbr, TickWorld);
		}

		/// <summary>Start repairing a building (test-only; forwards to CommandProcessor.ProcessRepair).</summary>
		internal void StartRepairing(RepairEventArgs args)
		{
			CommandProcessor.ProcessRepair(this, args.Building.UnitNbr, TickWorld);
		}

		/// <summary>Start healing an allied unit (test-only; forwards to CommandProcessor.ProcessHeal).</summary>
		internal void StartHealing(HealEventArgs args)
		{
			CommandProcessor.ProcessHeal(this, args.Target.UnitNbr, TickWorld);
		}

		#endregion
	}
}
