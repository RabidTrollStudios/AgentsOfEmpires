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
		public void Move(Unit unit, Vector3Int target)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("MOVE", $"unit=null -> {target}", "FAILED: unit is null");
				return;
			}
			if (!unit.CanMove)
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: unit can't move");
				return;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: target not on map");
				return;
			}
			if (!GameManager.Instance.Map.IsGridPositionWalkable(target))
			{
				CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "FAILED: target not walkable");
				return;
			}

			CmdLog?.LogCommand("MOVE", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.MoveEventHandler(this, new MoveEventArgs(unit, unit.UnitType, target));
		}

		/// <summary>
		/// Command to send a unit to build another unit at a particular point
		/// on the grid
		/// </summary>
		/// <param name="unit">the building unit</param>
		/// <param name="target">the location to build the new unit</param>
		/// <param name="unitType">the new type of unit to build</param>
		public void Build(Unit unit, Vector3Int target, UnitType unitType)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("BUILD", $"unit=null -> {unitType} at {target}", "FAILED: unit is null");
				return;
			}
			if (!unit.CanBuild)
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", "FAILED: unit can't build");
				return;
			}
			if (!unit.CanBuildUnit(unitType))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: unit can't build {unitType}");
				return;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", "FAILED: target not on map");
				return;
			}
			// Exclude the building worker's cell - the worker will move to a neighbor before building
			var workerExclusion = new HashSet<Vector3Int> { unit.GridPosition };
			if (!GameManager.Instance.Map.IsAreaBuildable(unitType, target, workerExclusion))
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: area not buildable at {target}");
				return;
			}

			// Check if all the dependencies are satisfied
			var missingDeps = Constants.DEPENDENCY[unitType]
				.Where(dep => GameManager.Instance.Units.GetUnitNbrsOfType(dep)
					.All(u => !GameManager.Instance.Units.GetUnit(u).IsBuilt))
				.ToList();

			if (missingDeps.Count > 0)
			{
				CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target}", $"FAILED: missing dependency {string.Join(", ", missingDeps)}");
				return;
			}

			CmdLog?.LogCommand("BUILD", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {unitType} at {target} (gold={Gold})", "SUCCESS (dispatched)");
			GameManager.Instance.Events.BuildEventHandler(this, new BuildEventArgs(unit, target, unitType));
		}

		/// <summary>
		/// Command to send a unit to gather resources from a particular resource
		/// </summary>
		/// <param name="unit">the gathering unit</param>
		/// <param name="resource">the resource to gather</param>
		/// <param name="baseUnit">the base to return the resource to</param>
		public void Gather(Unit unit, Unit resource, Unit baseUnit)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("GATHER", "unit=null", "FAILED: unit is null");
				return;
			}
			if (resource == null)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition}", "FAILED: resource is null");
				return;
			}
			if (resource.UnitType != UnitType.MINE)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> {resource.UnitType}#{resource.UnitNbr}", "FAILED: resource is not a mine");
				return;
			}
			if (baseUnit == null)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}", "FAILED: base is null");
				return;
			}
			if (baseUnit.UnitType != UnitType.BASE)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}, {baseUnit.UnitType}#{baseUnit.UnitNbr}", "FAILED: base unit is not a BASE");
				return;
			}
			if (!unit.CanGather)
			{
				CmdLog?.LogCommand("GATHER", $"{unit.UnitType}#{unit.UnitNbr} -> mine#{resource.UnitNbr}, base#{baseUnit.UnitNbr}", "FAILED: unit can't gather");
				return;
			}

			CmdLog?.LogCommand("GATHER", $"worker#{unit.UnitNbr} at {unit.GridPosition} -> mine#{resource.UnitNbr} at {resource.GridPosition}, base#{baseUnit.UnitNbr} at {baseUnit.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.GatherEventHandler(this, new GatherEventArgs(unit, resource, baseUnit));
		}

		/// <summary>
		/// Command to train a unit
		/// </summary>
		/// <param name="unit">unit that will do the training</param>
		/// <param name="unitType">type of unit to train</param>
		public void Train(Unit unit, UnitType unitType)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("TRAIN", $"unit=null -> {unitType}", "FAILED: unit is null");
				return;
			}
			if (!unit.CanTrain)
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", "FAILED: unit can't train");
				return;
			}
			if (!unit.IsBuilt)
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", "FAILED: building not finished");
				return;
			}
			if (!unit.CanTrainUnit(unitType))
			{
				CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType}", $"FAILED: can't train {unitType}");
				return;
			}

			CmdLog?.LogCommand("TRAIN", $"{unit.UnitType}#{unit.UnitNbr} -> {unitType} (gold={Gold})", "SUCCESS (dispatched)");
			GameManager.Instance.Events.TrainEventHandler(this, new TrainEventArgs(unit, unitType));
		}

		/// <summary>
		/// Command to attack another unit
		/// </summary>
		/// <param name="unit">unit that will do the attacking</param>
		/// <param name="target">unit to attack</param>
		public void Attack(Unit unit, Unit target)
		{
			if (unit == null)
			{
				CmdLog?.LogCommand("ATTACK", "unit=null", "FAILED: unit is null");
				return;
			}
			if (target == null)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> target=null", "FAILED: target is null");
				return;
			}
			if (!unit.CanAttack)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target.UnitType}#{target.UnitNbr}", "FAILED: unit can't attack");
				return;
			}
			if (target.UnitType == UnitType.MINE)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} -> MINE#{target.UnitNbr}", "FAILED: can't attack a mine");
				return;
			}
			if (unit.Agent.GetComponent<AgentController>().Agent.AgentNbr
				== target.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} -> {target.UnitType}#{target.UnitNbr}", "FAILED: can't attack own units");
				return;
			}

			CmdLog?.LogCommand("ATTACK", $"{unit.UnitType}#{unit.UnitNbr} at {unit.GridPosition} -> {target.UnitType}#{target.UnitNbr} at {target.GridPosition}", "SUCCESS (dispatched)");
			GameManager.Instance.Events.AttackEventHandler(this, new AttackEventArgs(unit, target));
		}

		#endregion
	}
}
