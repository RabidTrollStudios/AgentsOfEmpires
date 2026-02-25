using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using GameManager.Graph;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameManager
{
	/// <summary>
	/// Manages the game map, grid cells, pathfinding graph, and buildability queries.
	/// </summary>
	public class MapManager
	{
		private bool hasLoggedPathDiag = false;
		/// <summary>
		/// Size of the map, +x is "right", +y is "up", z is ignored
		/// </summary>
		public Vector3Int MapSize { get; private set; }

		/// <summary>
		/// The tilemap that renders the Influence Map on top of the game grid
		/// </summary>
		public Tilemap InfluenceMap { get; set; }

		/// <summary>
		/// 2D array of gridcells the size of the Map
		/// </summary>
		internal GridCell[,] GridCells { get; private set; }

		/// <summary>
		/// Graph used for pathfinding
		/// </summary>
		private Graph<GridCell> Graph { get; set; }

		/// <summary>
		/// Primary tilemap used to define the grid size
		/// </summary>
		private Tilemap mainTilemap;

		/// <summary>
		/// Generate the graph based on the tilemaps
		/// </summary>
		/// <param name="grid">the grid containing all tilemaps</param>
		/// <param name="logContext">GameObject for debug log context</param>
		/// <returns>the generated graph, or null on error</returns>
		internal Graph<GridCell> GenerateGraph(GameObject grid, GameObject logContext)
		{
			Graph = new Graph<GridCell>();

			// Find the largest bounds from all of the tilemaps
			MapSize = Vector3Int.zero;
			foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
			{
				tilemap.CompressBounds();

				if (tilemap.size.x > MapSize.x)
					MapSize = new Vector3Int(tilemap.size.x, MapSize.y, MapSize.z);
				if (tilemap.size.y > MapSize.y)
					MapSize = new Vector3Int(MapSize.x, tilemap.size.y, MapSize.z);
				if (tilemap.size.z > MapSize.z)
					MapSize = new Vector3Int(MapSize.x, MapSize.y, tilemap.size.z);
			}

			// If there are no tilemaps to process, produce an error
			if (grid.GetComponentsInChildren<Tilemap>().Length == 0)
			{
				GameManager.Instance.Log("ERROR: no tilemaps", logContext);
				return null;
			}

			// Use the first tilemap as the map size and locations of tiles
			mainTilemap = grid.GetComponentsInChildren<Tilemap>()[0];

			// Create the nodes
			GridCells = new GridCell[MapSize.x, MapSize.y];
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					Vector3Int position = new Vector3Int(i, j, 0);
					GridCells[i, j] = new GridCell(mainTilemap, position);
					Graph.AddNode(Utility.GridToInt(position, MapSize), GridCells[i, j]);
				}
			}

			// Build edges from all neighboring tiles
			GenerateEdges();

			// Set all of the unbuildable nodes by iterating through the Tilemaps
			for (int t = 1; t < grid.GetComponentsInChildren<Tilemap>().Length; ++t)
			{
				Tilemap tilemap = grid.GetComponentsInChildren<Tilemap>()[t];

				if (tilemap.CompareTag("InfluenceMap"))
				{
					InfluenceMap = tilemap;
					continue;
				}

				for (int i = 0; i < MapSize.x; ++i)
				{
					for (int j = 0; j < MapSize.y; ++j)
					{
						Vector3Int position = new Vector3Int(i, j, 0);

						TileBase tile = tilemap.GetTile(position);
						if (tile != null)
						{
							GridCells[i, j].SetBuildable(false);
							GridCells[i, j].SetWalkable(false);
						}
					}
				}
			}
			return Graph;
		}

		/// <summary>
		/// Generate all of the edges of the graph
		/// </summary>
		private void GenerateEdges()
		{
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					for (int m = i - 1; m < i + 2; ++m)
					{
						for (int n = j - 1; n < j + 2; ++n)
						{
							if (m >= 0 && n >= 0 && m < MapSize.x && n < MapSize.y
								&& (i != m || j != n))
							{
								Graph.AddEdge(Utility.GridToInt(new Vector3Int(i, j, 0), MapSize),
											  Utility.GridToInt(new Vector3Int(m, n, 0), MapSize),
											  Vector3.Distance(GridCells[i, j].Position, GridCells[m, n].Position));
							}
						}
					}
				}
			}

		}

		/// <summary>
		/// Determines if a specific tile is buildable
		/// </summary>
		public bool IsGridPositionBuildable(Vector3Int position)
		{
			return GridCells[position.x, position.y].IsBuildable();
		}

		/// <summary>
		/// Determines if a specific tile is walkable (passable for pathfinding).
		/// Walkable cells include those occupied by mobile units but not terrain or buildings.
		/// </summary>
		public bool IsGridPositionWalkable(Vector3Int position)
		{
			return GridCells[position.x, position.y].IsWalkable();
		}

		/// <summary>
		/// Determines if the unit can be built in that area (based on size of unit)
		/// </summary>
		public bool IsAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			return IsAreaBuildable(unitType, gridPosition, null);
		}

		/// <summary>
		/// Determines if the unit can be built in that area, optionally ignoring
		/// a set of positions (e.g., the building worker's cell).
		/// </summary>
		public bool IsAreaBuildable(UnitType unitType, Vector3Int gridPosition, HashSet<Vector3Int> excludePositions)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (excludePositions != null && excludePositions.Contains(gridPos))
						continue;

					if (!Utility.IsValidGridLocation(gridPos, MapSize)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the unit can be built in that area with a walkable "boundary" around it.
		/// </summary>
		public bool IsBoundedAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			return IsBoundedAreaBuildable(unitType, gridPosition, null);
		}

		/// <summary>
		/// Determines if the unit can be built in that area with a walkable "boundary" around it,
		/// optionally ignoring a set of positions (e.g., friendly workers who can move).
		/// </summary>
		public bool IsBoundedAreaBuildable(UnitType unitType, Vector3Int gridPosition, HashSet<Vector3Int> excludePositions)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = -1; i <= size.x; ++i)
			{
				for (int j = -1; j <= size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (excludePositions != null && excludePositions.Contains(gridPos))
						continue;

					if (!Utility.IsValidGridLocation(gridPos, MapSize)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the gridPosition is a neighbor of the unit
		/// </summary>
		public bool IsNeighborOfUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
		{
			var neighbors = GetGridPositionsNearUnit(unitType, unitGridPosition);
			return neighbors.Contains(gridPosition);
		}

		/// <summary>
		/// Get all of the grid positions surrounding a particular unit
		/// </summary>
		public List<Vector3Int> GetGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos;
			List<Vector3Int> positions = new List<Vector3Int>();

			for (int i = gridPosition.x - 1; i <= gridPosition.x + Constants.UNIT_SIZE[unitType].x; ++i)
			{
				gridPos = new Vector3Int(i, gridPosition.y + 1, 0);
				if (Utility.IsValidGridLocation(gridPos, MapSize))
					positions.Add(gridPos);

				gridPos = new Vector3Int(i, gridPosition.y - Constants.UNIT_SIZE[unitType].y, 0);
				if (Utility.IsValidGridLocation(gridPos, MapSize))
					positions.Add(gridPos);
			}

			for (int j = gridPosition.y - Constants.UNIT_SIZE[unitType].y + 1; j <= gridPosition.y; ++j)
			{
				gridPos = new Vector3Int(gridPosition.x - 1, j, 0);
				if (Utility.IsValidGridLocation(gridPos, MapSize))
					positions.Add(gridPos);

				gridPos = new Vector3Int(gridPosition.x + Constants.UNIT_SIZE[unitType].x, j, 0);
				if (Utility.IsValidGridLocation(gridPos, MapSize))
					positions.Add(gridPos);
			}

			return positions;
		}

		/// <summary>
		/// Find all of the buildable grid positions near this unit
		/// </summary>
		public List<Vector3Int> GetBuildableGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			List<Vector3Int> positions = GetGridPositionsNearUnit(unitType, gridPosition);
			return positions.Where(IsGridPositionBuildable).ToList();
		}

		/// <summary>
		/// Find a random location that is buildable for the unit type provided
		/// </summary>
		public Vector3Int GetRandomBuildableLocation(UnitType unitType)
		{
			Vector3Int location = Vector3Int.zero;

			do
			{
				location = new Vector3Int(UnityEngine.Random.Range(1, MapSize.x), UnityEngine.Random.Range(1, MapSize.y), 0);
			} while (!IsAreaBuildable(unitType, location));

			return location;
		}

		/// <summary>
		/// Get the path from a gridPosition to a position near the unit on any side of it
		/// </summary>
		public List<Vector3Int> GetPathToUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition, bool avoidUnits = false)
		{
			List<Vector3Int> path = new List<Vector3Int>();
			List<Vector3Int> allNeighbors = GetGridPositionsNearUnit(unitType, unitGridPosition);
			List<Vector3Int> positions = GetBuildableGridPositionsNearUnit(unitType, unitGridPosition);

			// Sort neighbors by distance to the requesting unit so it targets the closest open cell first
			positions.Sort((a, b) =>
			{
				float distA = (a - gridPosition).sqrMagnitude;
				float distB = (b - gridPosition).sqrMagnitude;
				return distA.CompareTo(distB);
			});

			// One-time diagnostic dump for failed BUILD pathfinding
			bool shouldLog = !hasLoggedPathDiag;

			foreach (var position in positions)
			{
				path = GetPathBetweenGridPositions(gridPosition, position, avoidUnits);
				if (path.Count > 0)
				{
					return path;
				}
				if (shouldLog)
				{
					GameManager.Instance.Log($"PATH_DIAG: {gridPosition}->{position} failed: {Graph.LastSearchResult} expansions={Graph.LastSearchExpansions}",
						GameManager.Instance.gameObject);
				}
			}

			if (shouldLog && path.Count == 0)
			{
				hasLoggedPathDiag = true;
				string diagPath = Application.dataPath + "/../PathDiag.txt";
				using (var w = new StreamWriter(diagPath, false))
				{
					w.WriteLine($"GetPathToUnit FAILED");
					w.WriteLine($"  from={gridPosition} to={unitType} at {unitGridPosition}");
					w.WriteLine($"  mapSize={MapSize}");
					w.WriteLine($"  totalNeighbors={allNeighbors.Count} buildableNeighbors={positions.Count}");
					w.WriteLine();
					w.WriteLine("All neighbors:");
					foreach (var n in allNeighbors)
					{
						bool buildable = IsGridPositionBuildable(n);
						w.WriteLine($"  {n} buildable={buildable}");
					}
					w.WriteLine();
					w.WriteLine("A* attempts (buildable neighbors only):");
					// Re-run to capture per-attempt diagnostics
					foreach (var position in positions)
					{
						GetPathBetweenGridPositions(gridPosition, position);
						w.WriteLine($"  {gridPosition} -> {position}: result={Graph.LastSearchResult} expansions={Graph.LastSearchExpansions}");
					}
					w.WriteLine();
					w.WriteLine($"Start cell {gridPosition} buildable={IsGridPositionBuildable(gridPosition)}");
					// Check if start cell's immediate neighbors are buildable
					w.WriteLine("Start cell neighbors:");
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 && dy == 0) continue;
							var neighbor = gridPosition + new Vector3Int(dx, dy, 0);
							if (Utility.IsValidGridLocation(neighbor, MapSize))
								w.WriteLine($"  {neighbor} buildable={IsGridPositionBuildable(neighbor)}");
						}
					}
				}
				GameManager.Instance.Log($"PATH_DIAG: wrote diagnostics to {diagPath}", GameManager.Instance.gameObject);
			}
			return path;
		}

		/// <summary>
		/// Gets the path between two grid positions
		/// </summary>
		public List<Vector3Int> GetPathBetweenGridPositions(Vector3Int startGridPosition, Vector3Int endGridPosition, bool avoidUnits = false)
		{
			List<Vector3Int> path = new List<Vector3Int>();

			int start = Utility.GridToInt(startGridPosition, MapSize);
			int end = Utility.GridToInt(endGridPosition, MapSize);

			List<int> pathOfInts = Graph.AStarSearch(start, end, avoidUnits: avoidUnits);

			foreach (var nodeNbr in pathOfInts)
			{
				path.Add(Utility.IntToGrid(nodeNbr, MapSize));
			}

			return path;
		}

		/// <summary>
		/// Set the unit's current cell(s) to buildable or not
		/// </summary>
		public void SetAreaBuildability(UnitType unitType, Vector3Int gridPosition, bool isBuildable)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (Utility.IsValidGridLocation(gridPos, MapSize))
					{
						GridCells[gridPos.x, gridPos.y].SetBuildable(isBuildable);
						// Mobile units don't block pathfinding — keep cell walkable
						if (isBuildable || !Constants.CAN_MOVE[unitType])
							GridCells[gridPos.x, gridPos.y].SetWalkable(isBuildable);
						// else: mobile unit occupying cell → isBuildable=false, isWalkable stays true
					}
				}
			}
		}
	}
}
