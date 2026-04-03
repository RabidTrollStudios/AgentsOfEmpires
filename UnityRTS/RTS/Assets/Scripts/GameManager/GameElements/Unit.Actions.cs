using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Unit partial — command initiation (StartTraining, StartBuilding, StartGathering, etc.).
	///
	/// Each method validates preconditions, deducts gold, computes paths, and sets
	/// the unit's action state so TickEngine can advance the task each tick.
	/// Also provides helpers for accessing the owning agent's command logger and analytics.
	/// </summary>
	public partial class Unit
	{
		#region Start Actions

		private CommandLogger GetCmdLog()
		{
			return Agent?.GetComponent<AgentController>()?.Agent?.CmdLog;
		}

		private RoundStats GetRoundStats()
		{
			return Agent?.GetComponent<AgentController>()?.Agent?.Analytics?.CurrentRound;
		}

		private Agent GetOwnerAgent()
		{
			return Agent?.GetComponent<AgentController>()?.Agent;
		}

		/// <summary>
		/// Start training another unit
		/// </summary>
		/// <param name="args">arguments for the training task</param>
		internal void StartTraining(TrainEventArgs args)
		{
			// If this unit is currently idle, train the new entity
			if (CurrentAction == UnitAction.IDLE && CanTrain && IsBuilt)
			{
				// If we can train this type of unit
				if (Constants.TRAINS[UnitType].Contains(args.UnitType)
					&& Agent.GetComponent<AgentController>().Agent.Gold >= (int)Constants.COST[args.UnitType])
				{
					// Set the training task and start the timer
					CurrentAction = UnitAction.TRAIN;
					taskTime = 0f;
					taskUnitType = args.UnitType;
					int trainCost = (int)Constants.COST[args.UnitType];
					Agent.GetComponent<AgentController>().Agent.Gold -= trainCost;
					var trainStats = GetRoundStats();
					trainStats?.RecordGoldSpent(trainCost);
					trainStats?.UpdatePeakGold(Agent.GetComponent<AgentController>().Agent.Gold);
					GetCmdLog()?.LogCommand("TRAIN", $"{UnitType}#{UnitNbr} -> {args.UnitType}",
						$"STARTED (gold remaining={Agent.GetComponent<AgentController>().Agent.Gold})");
				}
				else
				{
					string reason = !Constants.TRAINS[UnitType].Contains(args.UnitType)
						? $"can't train {args.UnitType}"
						: $"not enough gold (have {Agent.GetComponent<AgentController>().Agent.Gold}, need {(int)Constants.COST[args.UnitType]})";
					GetCmdLog()?.LogCommand("TRAIN", $"{UnitType}#{UnitNbr} -> {args.UnitType}", $"EXEC_FAILED: {reason}");
					GetOwnerAgent()?.RecordFailedCommand(new FailedCommand(UnitNbr, CommandType.Train,
						!Constants.TRAINS[UnitType].Contains(args.UnitType) ? CommandResult.UNIT_CANNOT_PERFORM_ACTION : CommandResult.INSUFFICIENT_GOLD));
				}
			}
			else
			{
				string reason = CurrentAction != UnitAction.IDLE ? $"unit not idle (current={CurrentAction})"
					: !CanTrain ? "unit can't train"
					: "building not finished";
				GetCmdLog()?.LogCommand("TRAIN", $"{UnitType}#{UnitNbr} -> {args.UnitType}", $"EXEC_FAILED: {reason}");
				GetOwnerAgent()?.RecordFailedCommand(new FailedCommand(UnitNbr, CommandType.Train,
					CurrentAction != UnitAction.IDLE ? CommandResult.UNIT_BUSY
					: !CanTrain ? CommandResult.UNIT_CANNOT_PERFORM_ACTION
					: CommandResult.BUILDING_NOT_FINISHED));
			}
		}

		/// <summary>
		/// Find an unfinished building of the given type anchored at position.
		/// Multiple pawns can build the same building simultaneously.
		/// </summary>
		private Unit FindUnbuiltBuildingAt(Vector3Int position, UnitType unitType)
		{
			foreach (var kvp in GameManager.Instance.Units.GetAllUnits())
			{
				Unit u = kvp.Value.GetComponent<Unit>();
				if (u != null && !u.IsBuilt && u.UnitType == unitType && u.GridPosition == position)
					return u;
			}
			return null;
		}

		/// <summary>
		/// Start building another unit
		/// </summary>
		/// <param name="args">arguments for the building task</param>
		internal void StartBuilding(BuildEventArgs args)
		{
			// Exclude the building pawn's cell - the pawn will move to a neighbor before building
			var pawnExclusion = new HashSet<Vector3Int> { GridPosition };

			// ── RESUME PATH ──────────────────────────────────────────────────────
			// If there is already an incomplete building at the target, skip placement
			// and walk the pawn over to finish it.
			Unit pausedBuilding = FindUnbuiltBuildingAt(args.TargetPosition, args.UnitType);
			if (pausedBuilding != null && CanBuild && CurrentAction != UnitAction.BUILD)
			{
				UpdatePath(GridPosition, pausedBuilding.UnitType, pausedBuilding.GridPosition, forceImmediate: true);
				if (path.Count > 0)
				{
					currentBuilding = pausedBuilding.gameObject;
					pausedBuilding.ActiveBuilders.Add(UnitNbr);
					CurrentAction = UnitAction.BUILD;
					buildPhase = BuildPhase.TO_POSITION;
					taskUnitType = pausedBuilding.UnitType;
					TargetGridPos = pausedBuilding.GridPosition;
					GetCmdLog()?.LogCommand("BUILD", $"pawn#{UnitNbr} -> RESUME {args.UnitType} at {args.TargetPosition}",
						$"RESUMING (progress={pausedBuilding.BuildProgress:F2}s, path={path.Count} steps)");
				}
				else
				{
					GetCmdLog()?.LogCommand("BUILD", $"pawn#{UnitNbr} -> RESUME {args.UnitType} at {args.TargetPosition}",
						"EXEC_FAILED: no path to paused building");
				}
				return;
			}
			// ── END RESUME PATH ──────────────────────────────────────────────────

			// If this unit is currently idle, build the new unit
			if (CurrentAction != UnitAction.BUILD
				&& CanBuild
				&& CanBuildUnit(args.UnitType)
				&& GameManager.Instance.Map.IsAreaBuildable(args.UnitType, args.TargetPosition, pawnExclusion)
				&& Agent.GetComponent<AgentController>().Agent.Gold >= (int)Constants.COST[args.UnitType])
			{
				TargetGridPos = args.TargetPosition;
				TargetUnitType = args.UnitType;
				pathFailCount = 0;
				pathBackoffMultiplier = 1;

				// Get the path to a neighbor of the unit to build (bypass cooldown for initial path)
				UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true);

				// If there is a path to the open cell, head toward it
				if (path.Count > 0)
				{
					currentBuilding = GameManager.Instance.Units.PlaceUnit(Agent, args.TargetPosition, args.UnitType, Color.white);
					currentBuilding.GetComponent<Unit>().ActiveBuilders.Add(UnitNbr);
					CurrentAction = UnitAction.BUILD;
					buildPhase = BuildPhase.TO_POSITION;
					taskUnitType = args.UnitType;
					TargetGridPos = args.TargetPosition;
					int buildCost = (int)Constants.COST[taskUnitType];
					Agent.GetComponent<AgentController>().Agent.Gold -= buildCost;
					var buildStats = GetRoundStats();
					buildStats?.RecordGoldSpent(buildCost);
					buildStats?.UpdatePeakGold(Agent.GetComponent<AgentController>().Agent.Gold);
					GetCmdLog()?.LogCommand("BUILD", $"pawn#{UnitNbr} -> {args.UnitType} at {args.TargetPosition}",
						$"STARTED (path={path.Count} steps, gold remaining={Agent.GetComponent<AgentController>().Agent.Gold})");
				}
				else
				{
					GetCmdLog()?.LogCommand("BUILD", $"pawn#{UnitNbr} -> {args.UnitType} at {args.TargetPosition}",
						"EXEC_FAILED: no path found to build site");
					GetOwnerAgent()?.RecordFailedCommand(new FailedCommand(UnitNbr, CommandType.Build, CommandResult.NO_PATH_FOUND));
				}
			}
			else
			{
				var buildFailReason = CurrentAction == UnitAction.BUILD ? CommandResult.UNIT_BUSY
					: !CanBuild ? CommandResult.UNIT_CANNOT_PERFORM_ACTION
					: !CanBuildUnit(args.UnitType) ? CommandResult.UNIT_CANNOT_PERFORM_ACTION
					: !GameManager.Instance.Map.IsAreaBuildable(args.UnitType, args.TargetPosition, pawnExclusion) ? CommandResult.AREA_NOT_BUILDABLE
					: CommandResult.INSUFFICIENT_GOLD;
				string reason = CurrentAction == UnitAction.BUILD ? "already building"
					: !CanBuild ? "unit can't build"
					: !CanBuildUnit(args.UnitType) ? $"can't build {args.UnitType}"
					: !GameManager.Instance.Map.IsAreaBuildable(args.UnitType, args.TargetPosition, pawnExclusion) ? $"area not buildable at {args.TargetPosition} (re-check)"
					: $"not enough gold (have {Agent.GetComponent<AgentController>().Agent.Gold}, need {(int)Constants.COST[args.UnitType]})";
				GetCmdLog()?.LogCommand("BUILD", $"pawn#{UnitNbr} at {GridPosition} -> {args.UnitType} at {args.TargetPosition}",
					$"EXEC_FAILED: {reason}");
				GetOwnerAgent()?.RecordFailedCommand(new FailedCommand(UnitNbr, CommandType.Build, buildFailReason));
			}
		}

		/// <summary>
		/// Start moving this agent
		/// </summary>
		/// <param name="args">arguments for moving task</param>
		internal void StartMoving(MoveEventArgs args)
		{

			// Allow MOVE to interrupt BUILD/REPAIR
			if (CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR)
			{
				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
				currentBuilding = null;
				CurrentAction = UnitAction.IDLE;
			}

			if (CanMove)
			{
				pathFailCount = 0;
				pathBackoffMultiplier = 1;

				// Try avoidUnits first — this produces a path through truly empty cells
				// that FixedUpdate can follow immediately without local-avoidance delays.
				path = GameManager.Instance.Map.GetPathBetweenGridPositions(GridPosition, args.Target, avoidUnits: true);
				pathIndex = 0;
				PathProgress = 0f;

				// Fall back to walkable path if surrounded (avoidUnits can't expand any neighbors)
				if (path.Count == 0)
				{
					path = GameManager.Instance.Map.GetPathBetweenGridPositions(GridPosition, args.Target);
					pathIndex = 0;
				}

				if (path.Count > 0)
				{
					TargetGridPos = args.Target;
					TargetUnitType = args.UnitType;
					CurrentAction = UnitAction.MOVE;
					GetCmdLog()?.LogCommand("MOVE", $"{UnitType}#{UnitNbr} at {GridPosition} -> {args.Target}",
						$"STARTED (path={path.Count} steps)");
				}
				else
				{
					GetCmdLog()?.LogCommand("MOVE", $"{UnitType}#{UnitNbr} at {GridPosition} -> {args.Target}",
						"EXEC_FAILED: no path found");
				}
			}
			else
			{
				GetCmdLog()?.LogCommand("MOVE", $"{UnitType}#{UnitNbr} at {GridPosition} -> {args.Target}",
					"EXEC_FAILED: unit can't move");
			}
		}

		/// <summary>
		/// Start gathering a resource
		/// </summary>
		/// <param name="args">arguments for the gathering task</param>
		internal void StartGathering(GatherEventArgs args)
		{

			if (CurrentAction != UnitAction.BUILD && CurrentAction != UnitAction.REPAIR
				&& CanGather
				&& GameManager.Instance.Units.GetUnit(args.ResourceUnit.UnitNbr) != null
				&& GameManager.Instance.Units.GetUnit(args.BaseUnit.UnitNbr) != null)
			{
				TargetGridPos = args.ResourceUnit.GridPosition;
				TargetUnitType = args.ResourceUnit.UnitType;
				pathFailCount = 0;
				pathBackoffMultiplier = 1;

				// Set the mine and base for this task
				MineUnit = GameManager.Instance.Units.GetUnit(args.ResourceUnit.UnitNbr);
				BaseUnit = GameManager.Instance.Units.GetUnit(args.BaseUnit.UnitNbr);
				CurrentAction = UnitAction.GATHER;
				gatherPhase = GatherPhase.TO_MINE;

				// Path to the mine via the shared grid (syncs TickPath for MovementSystem)
				var tickPath = GameManager.Instance.Map.Grid.FindPathToUnit(
					new AgentSDK.Position(GridPosition.x, GridPosition.y),
					args.ResourceUnit.UnitType,
					new AgentSDK.Position(args.ResourceUnit.GridPosition.x, args.ResourceUnit.GridPosition.y));
				((AgentSDK.ITickUnit)this).TickPath = tickPath;
				((AgentSDK.ITickUnit)this).PathIndex = 0;
				GetCmdLog()?.LogCommand("GATHER", $"pawn#{UnitNbr} at {GridPosition} -> mine#{args.ResourceUnit.UnitNbr} at {args.ResourceUnit.GridPosition}, base#{args.BaseUnit.UnitNbr}",
					$"STARTED (path={path.Count} steps)");
			}
			else if (!(CurrentAction == UnitAction.IDLE || CurrentAction == UnitAction.MOVE))
			{
				GetCmdLog()?.LogCommand("GATHER", $"pawn#{UnitNbr} -> mine#{args.ResourceUnit?.UnitNbr}",
					$"EXEC_FAILED: unit busy (current={CurrentAction})");
			}
			else if (!CanGather)
			{
				GetCmdLog()?.LogCommand("GATHER", $"{UnitType}#{UnitNbr} -> mine#{args.ResourceUnit?.UnitNbr}",
					"EXEC_FAILED: unit can't gather");
			}
			else
			{
				GetCmdLog()?.LogCommand("GATHER", $"pawn#{UnitNbr}",
					"EXEC_FAILED: resource or base unit no longer exists");
			}
		}

		/// <summary>
		/// Start attacking another agent
		/// </summary>
		/// <param name="args">arguments for attacking task</param>
		internal void StartAttacking(AttackEventArgs args)
		{

			if (CurrentAction != UnitAction.BUILD && CurrentAction != UnitAction.REPAIR && CanAttack)
			{
				var targetUnit = args.Target.GetComponent<Unit>();
				pathFailCount = 0;
				pathBackoffMultiplier = 1;

				// Check if already within attack range of the closest cell — if so, start
				// attacking immediately without pathfinding toward the target.
				// This prevents ranged units from walking up to melee range.
				float dist = DistanceToClosestCell(targetUnit);

				if (dist < Constants.ATTACK_RANGE[UnitType] + 0.5f)
				{
					path.Clear();
					pathIndex = 0;
					PathProgress = 0f;
					TargetGridPos = targetUnit.GridPosition;
					TargetUnitType = targetUnit.UnitType;
					CurrentAction = UnitAction.ATTACK;
					AttackUnit = args.Target;
					damage = 0.0f;
					totalDamage = 0.0f;
					GetCmdLog()?.LogCommand("ATTACK", $"{UnitType}#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
						$"STARTED (already in range, dist={dist:F1})");
				}
				else
				{
					UpdatePath(GridPosition, targetUnit.UnitType, targetUnit.GridPosition, forceImmediate: true);

					if (path.Count > 0)
					{
						TargetGridPos = targetUnit.GridPosition;
						TargetUnitType = targetUnit.UnitType;
						CurrentAction = UnitAction.ATTACK;
						AttackUnit = args.Target;
						damage = 0.0f;
						totalDamage = 0.0f;
						GetCmdLog()?.LogCommand("ATTACK", $"{UnitType}#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
							$"STARTED (path={path.Count} steps)");
					}
					else
					{
						GetCmdLog()?.LogCommand("ATTACK", $"{UnitType}#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
							"EXEC_FAILED: no path found to target");
					}
				}
			}
			else
			{
				string reason = CurrentAction == UnitAction.BUILD ? "unit is building" : "unit can't attack";
				GetCmdLog()?.LogCommand("ATTACK", $"{UnitType}#{UnitNbr} -> target#{args.Target?.GetComponent<Unit>()?.UnitNbr}",
					$"EXEC_FAILED: {reason}");
			}
		}

		/// <summary>
		/// Start repairing a damaged building
		/// </summary>
		internal void StartRepairing(RepairEventArgs args)
		{

			if (CurrentAction == UnitAction.BUILD)
			{
				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
				currentBuilding = null;
			}

			if (!CanBuild)
			{
				GetCmdLog()?.LogCommand("REPAIR", $"{UnitType}#{UnitNbr} -> {args.Building.UnitType}#{args.Building.UnitNbr}",
					"EXEC_FAILED: unit can't repair");
				return;
			}

			var building = args.Building;
			pathFailCount = 0;
			pathBackoffMultiplier = 1;

			UpdatePath(GridPosition, building.UnitType, building.GridPosition, forceImmediate: true);

			if (path.Count > 0 || GameManager.Instance.Map.IsNeighborOfUnit(GridPosition, building.UnitType, building.GridPosition))
			{
				currentBuilding = building.gameObject;
				CurrentAction = UnitAction.REPAIR;
				buildPhase = BuildPhase.TO_POSITION;
				TargetGridPos = building.GridPosition;
				TargetUnitType = building.UnitType;
				GetCmdLog()?.LogCommand("REPAIR", $"pawn#{UnitNbr} at {GridPosition} -> {building.UnitType}#{building.UnitNbr} at {building.GridPosition}",
					$"STARTED (health={building.Health}/{Constants.HEALTH[building.UnitType]}, path={path.Count} steps)");
			}
			else
			{
				GetCmdLog()?.LogCommand("REPAIR", $"pawn#{UnitNbr} at {GridPosition} -> {building.UnitType}#{building.UnitNbr} at {building.GridPosition}",
					"EXEC_FAILED: no path found to building");
			}
		}

		/// <summary>
		/// Start healing an allied unit
		/// </summary>
		internal void StartHealing(HealEventArgs args)
		{
			if (CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR)
			{
				GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} -> target#{args.Target?.GetComponent<Unit>()?.UnitNbr}",
					$"EXEC_FAILED: unit is {CurrentAction}");
				return;
			}

			if (!CanHeal)
			{
				GetCmdLog()?.LogCommand("HEAL", $"{UnitType}#{UnitNbr} -> target#{args.Target?.GetComponent<Unit>()?.UnitNbr}",
					"EXEC_FAILED: unit can't heal");
				return;
			}

			if (Mana < GameConstants.MANA_COST)
			{
				GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} -> target#{args.Target?.GetComponent<Unit>()?.UnitNbr}",
					$"EXEC_FAILED: insufficient mana ({Mana:F0}/{GameConstants.MANA_COST})");
				return;
			}

			var targetUnit = args.Target.GetComponent<Unit>();
			pathFailCount = 0;
			pathBackoffMultiplier = 1;

			// Check if already within heal range
			float dist = DistanceToClosestCell(targetUnit);
			float healRange = Constants.HEAL_RANGE[UnitType];

			if (dist < healRange + 0.5f)
			{
				path.Clear();
				pathIndex = 0;
				PathProgress = 0f;
				TargetGridPos = targetUnit.GridPosition;
				TargetUnitType = targetUnit.UnitType;
				CurrentAction = UnitAction.HEAL;
				healTargetNbr = targetUnit.UnitNbr;
				GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
					$"STARTED (already in range, dist={dist:F1})");
			}
			else
			{
				UpdatePath(GridPosition, targetUnit.UnitType, targetUnit.GridPosition, forceImmediate: true);

				if (path.Count > 0)
				{
					TargetGridPos = targetUnit.GridPosition;
					TargetUnitType = targetUnit.UnitType;
					CurrentAction = UnitAction.HEAL;
					healTargetNbr = targetUnit.UnitNbr;
					GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
						$"STARTED (path={path.Count} steps)");
				}
				else
				{
					GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} at {GridPosition} -> {targetUnit.UnitType}#{targetUnit.UnitNbr} at {targetUnit.GridPosition}",
						"EXEC_FAILED: no path found to target");
				}
			}
		}

		#endregion
	}
}
