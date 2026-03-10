using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// Validates and dispatches agent commands (Move, Build, Gather, Train, Attack)
	/// to the appropriate units.
	/// </summary>
	public class EventDispatcher
	{
		private UnitManager unitManager;
		private MapManager mapManager;

		public EventDispatcher(UnitManager unitManager, MapManager mapManager)
		{
			this.unitManager = unitManager;
			this.mapManager = mapManager;
		}

		/// <summary>
		/// Validates and dispatches a move command
		/// </summary>
		public void MoveEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			MoveEventArgs args = (MoveEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to move event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Unit.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for move event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			if (!unit.GetComponent<Unit>().CanMove)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot move " + unit.GetComponent<Unit>().UnitType, logContext);
				return;
			}

			if (!Utility.IsValidGridLocation(args.Target))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: invalid target grid position " + args.Target, logContext);
				return;
			}

			unit.GetComponent<Unit>().StartMoving(args);
		}

		/// <summary>
		/// Validates and dispatches a build command
		/// </summary>
		public void BuildEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			BuildEventArgs args = (BuildEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to build event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Unit.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for build event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			if (!unit.GetComponent<Unit>().CanBuild
				|| !Constants.BUILDS[unit.GetComponent<Unit>().UnitType].Contains(args.UnitType))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot build " + args.UnitType, logContext);
				return;
			}

			// Exclude the building pawn's cell - the pawn will move to a neighbor before building
			var pawnExclusion = new HashSet<Vector3Int> { unit.GetComponent<Unit>().GridPosition };
			if (!Utility.IsValidGridLocation(args.TargetPosition) || !mapManager.IsAreaBuildable(args.UnitType, args.TargetPosition, pawnExclusion))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: invalid target grid position " + args.TargetPosition, logContext);
				return;
			}

			bool hasDependencies = true;
			string dependencyName = "";
			foreach (UnitType uT in Constants.DEPENDENCY[args.UnitType])
			{
				if (unitManager.GetUnitNbrsOfType(uT).Where(
						u => unitManager.GetUnit(u).IsBuilt).ToList().Count == 0)
				{
					hasDependencies = false;
					dependencyName += uT + " ";
				}
			}

			if (!hasDependencies)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: Missing dependencies " + dependencyName + "for building " + args.UnitType, logContext);
				return;
			}
			unit.GetComponent<Unit>().StartBuilding(args);
		}

		/// <summary>
		/// Validates and dispatches a gather command
		/// </summary>
		public void GatherEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			GatherEventArgs args = (GatherEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Unit == null || args.BaseUnit == null || args.ResourceUnit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to gather event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Unit.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for gather event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			if (!unit.GetComponent<Unit>().CanGather)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot gather " + args.Unit.UnitType, logContext);
				return;
			}

			if (args.ResourceUnit.UnitType != UnitType.MINE)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: cannot gather from  " + args.ResourceUnit.UnitType, logContext);
				return;
			}

			if (args.BaseUnit.UnitType != UnitType.BASE
				|| args.BaseUnit.Agent.GetComponent<AgentController>().Agent.AgentNbr != agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: cannot return resources to  " + args.BaseUnit.UnitType, logContext);
				return;
			}

			unit.GetComponent<Unit>().StartGathering(args);
		}

		/// <summary>
		/// Validates and dispatches a train command
		/// </summary>
		public void TrainEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			TrainEventArgs args = (TrainEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to train event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Unit.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for train event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			if (!unit.GetComponent<Unit>().CanTrain
				|| !unit.GetComponent<Unit>().CanTrainUnit(args.UnitType))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot train " + args.UnitType, logContext);
				return;
			}

			unit.GetComponent<Unit>().StartTraining(args);
		}

		/// <summary>
		/// Validates and dispatches a repair command
		/// </summary>
		public void RepairEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			RepairEventArgs args = (RepairEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Pawn == null || args.Building == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to repair event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Pawn.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for repair event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			if (!unit.GetComponent<Unit>().CanBuild)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot repair " + unit.GetComponent<Unit>().UnitType, logContext);
				return;
			}

			Unit buildingUnit = unitManager.GetUnit(args.Building.UnitNbr);
			if (buildingUnit == null || !buildingUnit.IsBuilt)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: building not found or not finished for repair event", logContext);
				return;
			}

			unit.GetComponent<Unit>().StartRepairing(args);
		}

		/// <summary>
		/// Validates and dispatches an attack command
		/// </summary>
		public void AttackEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			AttackEventArgs args = (AttackEventArgs)e;
			GameObject logContext = GameManager.Instance.gameObject;

			if (agent == null || args.Unit == null || args.Target == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to attack event", logContext);
				return;
			}

			GameObject unit = unitManager.GetUnit(args.Unit.UnitNbr)?.gameObject;
			if (unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit not found for attack event", logContext);
				return;
			}

			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", logContext);
				return;
			}

			Unit enemyUnit = unitManager.GetUnit(args.Target.UnitNbr);
			if (enemyUnit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: target unit not found for attack event", logContext);
				return;
			}

			if (agent.AgentNbr == enemyUnit.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: agent can't attack its own units", logContext);
				return;
			}

			unit.GetComponent<Unit>().StartAttacking(args);
		}
	}
}
