using System;
using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Arguments for the Move action
	/// </summary>
	internal class MoveEventArgs : EventArgs
	{
		/// <summary>
		/// Creates an instance of the event arguments
		/// </summary>
		/// <param name="unit">unit that will move</param>
		/// <param name="unitType">type of unit that will move</param>
		/// <param name="targetPosition">location to move to</param>
		internal MoveEventArgs(Unit unit, UnitType unitType, Vector3Int targetPosition)
		{
			Target = targetPosition;
			Unit = unit;
			UnitType = unitType;
		}

		/// <summary>
		/// Location to which the unit will move
		/// </summary>
		internal Vector3Int Target { get; set; }

		/// <summary>
		/// Unit that will move
		/// </summary>
		internal Unit Unit { get; set; }

		/// <summary>
		/// Type of unit that will move
		/// </summary>
		internal UnitType UnitType { get; set; }
	}

	/// <summary>
	/// Arguments for the Gather action
	/// </summary>
	internal class GatherEventArgs : EventArgs
	{
		/// <summary>
		/// Creates an instance of the event arguments
		/// </summary>
		/// <param name="unit">unit that will gather</param>
		/// <param name="resourceUnit">resource that the unit will gather from</param>
		/// <param name="baseUnit">base at which the unit will deposit gathered resources</param>
		internal GatherEventArgs(Unit unit, Unit resourceUnit, Unit baseUnit)
		{
			ResourceUnit = resourceUnit;
			Unit = unit;
			BaseUnit = baseUnit;
		}

		/// <summary>
		/// Resource from which the unit will gather
		/// </summary>
		internal Unit ResourceUnit { get; set; }

		/// <summary>
		/// Unit that will gather
		/// </summary>
		internal Unit Unit { get; set; }

		/// <summary>
		/// Base at which the unit will deposit gathered resources
		/// </summary>
		internal Unit BaseUnit { get; set; }
	}

	/// <summary>
	/// Arguments for the Attack action
	/// </summary>
	internal class AttackEventArgs : EventArgs
	{
		/// <summary>
		/// Creates an instance of the event arguments
		/// </summary>
		/// <param name="unit">unit that will be attacking</param>
		/// <param name="targetUnit">unit that will be attacked</param>
		internal AttackEventArgs(Unit unit, Unit targetUnit)
		{
			Target = targetUnit;
			Unit = unit;
		}

		/// <summary>
		/// Unit that will be attacked
		/// </summary>
		internal Unit Target { get; set; }

		/// <summary>
		/// Unit that will be attacking
		/// </summary>
		internal Unit Unit { get; set; }
	}

	/// <summary>
	/// Arguments for the Train action
	/// </summary>
	internal class TrainEventArgs : EventArgs
	{
		/// <summary>
		/// Creates an instance of the event arguments
		/// </summary>
		/// <param name="unit">unit that will be training</param>
		/// <param name="unitType">type of unit to train</param>
		internal TrainEventArgs(Unit unit, UnitType unitType)
		{
			UnitType = unitType;
			Unit = unit;
		}

		/// <summary>
		/// Type of unit to train
		/// </summary>
		internal UnitType UnitType { get; set; }

		/// <summary>
		/// Unit that will be doing the training
		/// </summary>
		internal Unit Unit { get; set; }
	}

	/// <summary>
	/// Arguments for the Build action
	/// </summary>
	internal class BuildEventArgs : EventArgs
	{
		/// <summary>
		/// Creates an instance of the event arguments
		/// </summary>
		/// <param name="unit">unit that will be building</param>
		/// <param name="targetPositionPosition">position to build the new unit</param>
		/// <param name="unitType">type of unit to build</param>
		internal BuildEventArgs(Unit unit, Vector3Int targetPositionPosition, UnitType unitType)
		{
			Unit = unit;
			TargetPosition = targetPositionPosition;
			UnitType = unitType;
		}

		/// <summary>
		/// Position to build the new unit
		/// </summary>
		internal Vector3Int TargetPosition { get; set; }

		/// <summary>
		/// Type of unit to build
		/// </summary>
		internal UnitType UnitType { get; set; }

		/// <summary>
		/// Unit that will do the building
		/// </summary>
		internal Unit Unit { get; set; }
	}
}

