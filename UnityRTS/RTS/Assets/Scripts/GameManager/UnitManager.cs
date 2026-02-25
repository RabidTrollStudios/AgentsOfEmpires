using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using Preloader;
using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// Manages all units in the game: creation, destruction, and queries.
	/// </summary>
	public class UnitManager
	{
		/// <summary>
		/// Collection of Units in the game
		/// </summary>
		private Dictionary<int, GameObject> Units { get; set; }

		/// <summary>
		/// Number of units created (used for assigning unique IDs)
		/// </summary>
		private int NbrOfUnits { get; set; }

		/// <summary>
		/// Prefabs for orc units
		/// </summary>
		public Dictionary<UnitType, GameObject> OrcUnitPrefabs { get; set; }

		/// <summary>
		/// Prefabs for human units
		/// </summary>
		public Dictionary<UnitType, GameObject> HumanUnitPrefabs { get; set; }

		/// <summary>
		/// Collection of all unit prefabs keyed by agent number
		/// </summary>
		public Dictionary<int, Dictionary<UnitType, GameObject>> UnitPrefabs { get; set; }

		/// <summary>
		/// Reference to the map manager for buildability updates
		/// </summary>
		private MapManager mapManager;

		/// <summary>
		/// Reference to the prefab loader for the unit debugger prefab
		/// </summary>
		private PrefabLoader prefabs;

		public UnitManager(MapManager mapManager, PrefabLoader prefabs)
		{
			this.mapManager = mapManager;
			this.prefabs = prefabs;
			Units = new Dictionary<int, GameObject>();
			UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
		}

		/// <summary>
		/// Reset units for a new round
		/// </summary>
		public void ResetForRound()
		{
			NbrOfUnits = 0;
		}

		/// <summary>
		/// Place a specific unit on a specific location
		/// </summary>
		public GameObject PlaceUnit(GameObject agent, Vector3Int gridPosition, UnitType unitType, Color color)
		{
			Vector3 position = gridPosition + new Vector3(Constants.UNIT_SIZE[unitType].x * 0.5f,
								   1f - Constants.UNIT_SIZE[unitType].y * 0.5f);

			GameObject unit = Object.Instantiate(
				UnitPrefabs[agent.GetComponent<AgentController>().Agent.AgentNbr][unitType],
				position, Quaternion.identity);
			unit.AddComponent<Unit>();
			unit.GetComponent<Unit>().Initialize(agent, gridPosition, unitType, NbrOfUnits++);

			GameObject unitDebugger = Object.Instantiate(prefabs.UnitDebuggerPrefab, gridPosition, Quaternion.identity);
			unitDebugger.gameObject.GetComponentInChildren<Canvas>().enabled = false;
			unitDebugger.transform.SetParent(unit.transform);

			Units.Add(unit.GetComponent<Unit>().UnitNbr, unit);

			mapManager.SetAreaBuildability(unit.GetComponent<Unit>().UnitType, gridPosition, false);

			return unit;
		}

		/// <summary>
		/// Destroys a specific unit and clears its area
		/// </summary>
		public void DestroyUnit(GameObject unit)
		{
			mapManager.SetAreaBuildability(unit.GetComponent<Unit>().UnitType, unit.GetComponent<Unit>().GridPosition, true);
			Units.Remove(unit.GetComponent<Unit>().UnitNbr);
			Object.Destroy(unit);
		}

		/// <summary>
		/// Get a specific unit based on its unit number
		/// </summary>
		public Unit GetUnit(int unitNbr)
		{
			if (Units.ContainsKey(unitNbr))
			{ return Units[unitNbr].GetComponent<Unit>(); }
			else
			{ return null; }
		}

		/// <summary>
		/// Gets a list of units of the given type
		/// </summary>
		public List<int> GetUnitNbrsOfType(UnitType unitType)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType).ToList();
		}

		/// <summary>
		/// Gets a list of units of the given type for the given agent
		/// </summary>
		public List<int> GetUnitNbrsOfType(UnitType unitType, int agentNbr)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType
								&& Units[key].GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr == agentNbr).ToList();
		}

		/// <summary>
		/// Set all units to inactive (used before showing winner)
		/// </summary>
		public void SetAllUnitsInactive()
		{
			List<int> unitNbrs = Units.Keys.ToList();

			foreach (var unitNbr in unitNbrs)
			{
				if (Units.ContainsKey(unitNbr))
				{
					Units[unitNbr].SetActive(false);
				}
			}
		}

		/// <summary>
		/// Destroy all units (used when restarting a round)
		/// </summary>
		public void DestroyAllUnits()
		{
			List<int> unitNbrs = Units.Keys.ToList();

			foreach (var unitNbr in unitNbrs)
			{
				if (Units.ContainsKey(unitNbr))
				{
					DestroyUnit(Units[unitNbr]);
				}
			}
			Units = new Dictionary<int, GameObject>();
		}

		/// <summary>
		/// Get the raw Units dictionary (for win condition checks)
		/// </summary>
		public Dictionary<int, GameObject> GetAllUnits()
		{
			return Units;
		}
	}
}
