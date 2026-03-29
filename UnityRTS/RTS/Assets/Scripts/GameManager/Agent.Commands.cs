using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using System.Linq;
using UnityEngine;

namespace GameManager
{
	public abstract partial class Agent
	{
		#region Event Throwers
		/// <summary>
		/// Command to move a unit to an arbitrary point on the grid
		/// </summary>
		/// <param name="unit">the unit to move</param>
		/// <param name="target">the point to move to</param>
		public CommandResult Move(Unit unit, Vector3Int target)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("MOVE", $"unit=null -> {target}", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (!unit.CanMove)
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: unit can't move");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: target not on map");
				return CommandResult.INVALID_POSITION;
			}
			if (!GameManager.Instance.Map.IsGridPositionWalkable(target))
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: target not walkable");
				return CommandResult.POSITION_NOT_WALKABLE;
			}

			CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.MoveEventHandler(this, new MoveEventArgs(unit, unit.UnitType, target));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command to send a unit to build another unit at a particular point
		/// on the grid
		/// </summary>
		/// <param name="unit">the building unit</param>
		/// <param name="target">the location to build the new unit</param>
		/// <param name="unitType">the new type of unit to build</param>
		public CommandResult Build(Unit unit, Vector3Int target, UnitType unitType)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("BUILD", $"unit=null -> {unitType} at {target}", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (!unit.CanBuild)
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", "FAILED: unit can't build");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (!unit.CanBuildUnit(unitType))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: unit can't build {unitType}");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", "FAILED: target not on map");
				return CommandResult.INVALID_POSITION;
			}
			// Exclude the building pawn's cell - the pawn will move to a neighbor before building
			var pawnExclusion = new HashSet<Vector3Int> { unit.GridPosition };
			if (!GameManager.Instance.Map.IsAreaBuildable(unitType, target, pawnExclusion))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: area not buildable at {target}");
				return CommandResult.AREA_NOT_BUILDABLE;
			}

			// Check if all the dependencies are satisfied
			var missingDeps = Constants.DEPENDENCY[unitType]
				.Where(dep => GameManager.Instance.Units.GetUnitNbrsOfType(dep)
					.All(u => !GameManager.Instance.Units.GetUnit(u).IsBuilt))
				.ToList();

			if (missingDeps.Count > 0)
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: missing dependency {string.Join(", ", missingDeps)}");
				return CommandResult.MISSING_DEPENDENCY;
			}

			CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target} (gold={Gold})", "SUCCESS (dispatched)");
			GameManager.Instance.Events.BuildEventHandler(this, new BuildEventArgs(unit, target, unitType));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command to send a unit to gather resources from a particular resource
		/// </summary>
		/// <param name="unit">the gathering unit</param>
		/// <param name="resource">the resource to gather</param>
		/// <param name="baseUnit">the base to return the resource to</param>
		public CommandResult Gather(Unit unit, Unit resource, Unit baseUnit)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("GATHER", "unit=null", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (resource == null)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition}", "FAILED: resource is null");
				return CommandResult.TARGET_NOT_FOUND;
			}
			if (resource.UnitType != UnitType.MINE)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> {resource.UnitType}#{resource.UnitNbr}", "FAILED: resource is not a mine");
				return CommandResult.INVALID_TARGET;
			}
			if (baseUnit == null)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}", "FAILED: base is null");
				return CommandResult.TARGET_NOT_FOUND;
			}
			if (baseUnit.UnitType != UnitType.BASE)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}, {baseUnit.UnitType}#{baseUnit.UnitNbr}", "FAILED: base unit is not a BASE");
				return CommandResult.INVALID_TARGET;
			}
			if (!unit.CanGather)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}, base#{baseUnit.UnitNbr}", "FAILED: unit can't gather");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}

			CmdLog?.LogCommand("GATHER", $"pawn#{unit.UnitNbr} at {unit.GridPosition} -> mine#{resource.UnitNbr} at {resource.GridPosition}, base#{baseUnit.UnitNbr} at {baseUnit.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.GatherEventHandler(this, new GatherEventArgs(unit, resource, baseUnit));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command to train a unit
		/// </summary>
		/// <param name="unit">unit that will do the training</param>
		/// <param name="unitType">type of unit to train</param>
		public CommandResult Train(Unit unit, UnitType unitType)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("TRAIN", $"unit=null -> {unitType}", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (!unit.CanTrain)
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", "FAILED: unit can't train");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (!unit.IsBuilt)
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", "FAILED: building not finished");
				return CommandResult.BUILDING_NOT_FINISHED;
			}
			if (!unit.CanTrainUnit(unitType))
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", $"FAILED: can't train {unitType}");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}

			CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType} (gold={Gold})", "SUCCESS (dispatched)");
			GameManager.Instance.Events.TrainEventHandler(this, new TrainEventArgs(unit, unitType));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command to attack another unit
		/// </summary>
		/// <param name="unit">unit that will do the attacking</param>
		/// <param name="target">unit to attack</param>
		public CommandResult Attack(Unit unit, Unit target)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("ATTACK", "unit=null", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (target == null)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> target=null", "FAILED: target is null");
				return CommandResult.TARGET_NOT_FOUND;
			}
			if (!unit.CanAttack)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target.UnitType}#{target.UnitNbr}", "FAILED: unit can't attack");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (target.UnitType == UnitType.MINE)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} -> MINE#{target.UnitNbr}", "FAILED: can't attack a mine");
				return CommandResult.INVALID_TARGET;
			}
			if (unit.Agent.GetComponent<AgentController>().Agent.AgentNbr
				== target.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", "FAILED: can't attack own units");
				return CommandResult.FRIENDLY_FIRE;
			}

			CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target.UnitType}#{target.UnitNbr} at {target.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.AttackEventHandler(this, new AttackEventArgs(unit, target));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command to send a pawn to repair a damaged friendly building
		/// </summary>
		/// <param name="unit">the pawn unit that will repair</param>
		/// <param name="building">the building to repair</param>
		public CommandResult Repair(Unit unit, Unit building)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("REPAIR", "unit=null", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (building == null)
			{
				CmdLog?.LogCommand("REPAIR", $"{unit.UnitType}#{unit.UnitNbr}", "FAILED: building is null");
				return CommandResult.TARGET_NOT_FOUND;
			}
			if (!unit.CanBuild)
			{
				CmdLog?.LogCommand("REPAIR", $"{unit.UnitType}#{unit.UnitNbr} -> {building.UnitType}#{building.UnitNbr}", "FAILED: unit can't repair");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (building.CanMove || building.UnitType == UnitType.MINE)
			{
				CmdLog?.LogCommand("REPAIR", $"{unit.UnitType}#{unit.UnitNbr} -> {building.UnitType}#{building.UnitNbr}", "FAILED: target is not a building");
				return CommandResult.INVALID_TARGET;
			}
			if (!building.IsBuilt)
			{
				CmdLog?.LogCommand("REPAIR", $"{unit.UnitType}#{unit.UnitNbr} -> {building.UnitType}#{building.UnitNbr}", "FAILED: building not finished");
				return CommandResult.BUILDING_NOT_FINISHED;
			}
			if (unit.Agent.GetComponent<AgentController>().Agent.AgentNbr
				!= building.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				CmdLog?.LogCommand("REPAIR", $"{unit.UnitType}#{unit.UnitNbr} -> {building.UnitType}#{building.UnitNbr}", "FAILED: not your building");
				return CommandResult.FRIENDLY_FIRE;
			}

			CmdLog?.LogCommand("REPAIR", $"pawn#{unit.UnitNbr} at {unit.GridPosition} -> {building.UnitType}#{building.UnitNbr} at {building.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.RepairEventHandler(this, new RepairEventArgs(unit, building));
			return CommandResult.SUCCESS;
		}

		/// <summary>
		/// Command a monk to heal a friendly unit
		/// </summary>
		/// <param name="unit">the monk unit</param>
		/// <param name="target">the friendly unit to heal</param>
		public CommandResult Heal(Unit unit, Unit target)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("HEAL", "unit=null", "FAILED: unit is null");
				return CommandResult.UNIT_NOT_FOUND;
			}
			if (target == null)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr}", "FAILED: target is null");
				return CommandResult.TARGET_NOT_FOUND;
			}
			if (!unit.CanHeal)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", "FAILED: unit can't heal");
				return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
			}
			if (!target.CanMove)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", "FAILED: can only heal mobile units");
				return CommandResult.INVALID_TARGET;
			}
			if (unit.Agent.GetComponent<AgentController>().Agent.AgentNbr
				!= target.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", "FAILED: can only heal own units");
				return CommandResult.INVALID_TARGET;
			}
			if (unit.Mana < GameConstants.MANA_COST)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", $"FAILED: insufficient mana ({unit.Mana:F0}/{GameConstants.MANA_COST})");
				return CommandResult.INSUFFICIENT_MANA;
			}
			float targetMaxHealth = Constants.HEALTH[target.UnitType];
			if (target.Health > targetMaxHealth - GameConstants.HEAL_AMOUNT)
			{
				CmdLog?.LogCommand("HEAL", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", $"FAILED: target not missing enough HP (needs {GameConstants.HEAL_AMOUNT}+ missing)");
				return CommandResult.INVALID_TARGET;
			}

			CmdLog?.LogCommand("HEAL", $"MONK#{unit.UnitNbr} at {unit.GridPosition} -> {target.UnitType}#{target.UnitNbr} at {target.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.HealEventHandler(this, new HealEventArgs(unit, target));
			return CommandResult.SUCCESS;
		}

		#endregion
	}
}
