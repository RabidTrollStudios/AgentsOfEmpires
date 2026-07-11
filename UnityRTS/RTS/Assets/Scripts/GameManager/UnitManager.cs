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
		/// Prefabs for red units
		/// </summary>
		public Dictionary<UnitType, GameObject> RedUnitPrefabs { get; set; }

		/// <summary>
		/// Prefabs for blue units
		/// </summary>
		public Dictionary<UnitType, GameObject> BlueUnitPrefabs { get; set; }

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
			Vector3 position;
			if (Constants.CAN_MOVE[unitType])
			{
				// Mobile units: transform at cell bottom so feet align with grid and Y-sort is correct
				position = gridPosition + new Vector3(Constants.UNIT_SIZE[unitType].x * 0.5f, 0f);
			}
			else
			{
				// Buildings: anchor is bottom-left. Footprint extends right and up.
				// Passage row (top, j=sizeY-1) is walkable.
				// Body rows: 0..sizeY-2 (height = sizeY-1).
				// Visual center of body: anchor + (sizeX/2, bodyHeight/2)
				var size = Constants.UNIT_SIZE[unitType];
				float bodyHeight = size.y > 1 ? size.y - 1 : size.y;
				position = gridPosition + new Vector3(size.x * 0.5f, bodyHeight * 0.5f);
			}

			GameObject unit = Object.Instantiate(
				UnitPrefabs[agent.GetComponent<AgentController>().Agent.AgentNbr][unitType],
				position, Quaternion.identity);
			unit.AddComponent<Unit>();

			// Sort the body sprite by its pivot (feet) for correct Y-depth against trees
			var sr = unit.GetComponent<SpriteRenderer>();
			if (sr != null)
				sr.spriteSortPoint = SpriteSortPoint.Pivot;
			unit.GetComponent<Unit>().Initialize(agent, gridPosition, unitType, NbrOfUnits++);

			// Assign buildings and mines to the Buildings layer
			if (!Constants.CAN_MOVE[unitType])
			{
				int buildingsLayer = LayerMask.NameToLayer("Buildings");
				unit.layer = buildingsLayer;
				foreach (Transform child in unit.transform)
					child.gameObject.layer = buildingsLayer;
			}

			GameObject unitDebugger = Object.Instantiate(prefabs.UnitDebuggerPrefab, gridPosition, Quaternion.identity);
			unitDebugger.gameObject.GetComponentInChildren<Canvas>().enabled = false;
			unitDebugger.transform.SetParent(unit.transform);

			Units.Add(unit.GetComponent<Unit>().UnitNbr, unit);

			mapManager.SetUnitFootprint(unitType, gridPosition, true);

			return unit;
		}

		/// <summary>
		/// Place a neutral unit (no owning agent). Uses agent 0's prefab for visuals.
		/// Used for mines which are not owned by either player.
		/// </summary>
		public GameObject PlaceNeutralUnit(Vector3Int gridPosition, UnitType unitType, Color color)
		{
			// Bottom-left anchor. Body center for visual placement.
			var size = Constants.UNIT_SIZE[unitType];
			float bodyHeight = size.y > 1 ? size.y - 1 : size.y;
			Vector3 position = gridPosition + new Vector3(size.x * 0.5f, bodyHeight * 0.5f);

			// Use agent 0's prefab (mines look the same regardless)
			GameObject unit = Object.Instantiate(
				UnitPrefabs[0][unitType],
				position, Quaternion.identity);
			unit.AddComponent<Unit>();

			var sr = unit.GetComponent<SpriteRenderer>();
			if (sr != null)
				sr.spriteSortPoint = SpriteSortPoint.Pivot;
			unit.GetComponent<Unit>().Initialize(null, gridPosition, unitType, NbrOfUnits++);

			if (!Constants.CAN_MOVE[unitType])
			{
				int buildingsLayer = LayerMask.NameToLayer("Buildings");
				unit.layer = buildingsLayer;
				foreach (Transform child in unit.transform)
					child.gameObject.layer = buildingsLayer;
			}

			GameObject unitDebugger = Object.Instantiate(prefabs.UnitDebuggerPrefab, gridPosition, Quaternion.identity);
			unitDebugger.gameObject.GetComponentInChildren<Canvas>().enabled = false;
			unitDebugger.transform.SetParent(unit.transform);

			Units.Add(unit.GetComponent<Unit>().UnitNbr, unit);
			mapManager.SetUnitFootprint(unitType, gridPosition, true);

			return unit;
		}

		/// <summary>
		/// Destroys a specific unit and clears its area
		/// </summary>
		public void DestroyUnit(GameObject unit)
		{
			var u = unit.GetComponent<Unit>();
			mapManager.SetUnitFootprint(u.UnitType, u.GridPosition, false);
			Units.Remove(u.UnitNbr);
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
		// Agent-facing unit lists are sorted ascending by UnitNbr so both engines
		// present units in an identical, deterministic order regardless of Dictionary
		// enumeration (which differs between Mono and .NET). See engine-parity H3.
		public List<int> GetUnitNbrsOfType(UnitType unitType)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType)
							  .OrderBy(key => key).ToList();
		}

		/// <summary>
		/// Gets a list of units of the given type for the given agent
		/// </summary>
		public List<int> GetUnitNbrsOfType(UnitType unitType, int agentNbr)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType
								&& Units[key].GetComponent<Unit>().OwnerAgentNbr == agentNbr)
							  .OrderBy(key => key).ToList();
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
