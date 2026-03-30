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
		/// Shared GameGrid for grid state and pathfinding.
		/// Stays in sync with GridCells; both sides and SimGame use the same logic.
		/// </summary>
		public AgentSDK.GameGrid Grid { get; private set; }

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

			// Use the Ground tilemap to define the playable grid
			mainTilemap = null;
			foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
			{
				if (tilemap.gameObject.name == "Ground 1")
				{
					mainTilemap = tilemap;
					break;
				}
			}
			if (mainTilemap == null)
			{
				GameManager.Instance.Log("ERROR: no 'Ground 1' tilemap found", logContext);
				return null;
			}

			// Create the nodes; cells with no ground tile are obstacles
			GridCells = new GridCell[MapSize.x, MapSize.y];
			Grid = new AgentSDK.GameGrid(MapSize.x, MapSize.y);
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					Vector3Int position = new Vector3Int(i, j, 0);
					GridCells[i, j] = new GridCell(mainTilemap, position);
					Graph.AddNode(Utility.GridToInt(position, MapSize), GridCells[i, j]);

					if (mainTilemap.GetTile(position) == null)
					{
						GridCells[i, j].SetBuildable(false);
						GridCells[i, j].SetWalkable(false);
						Grid.SetCellBlocked(i, j);
					}
				}
			}

			// Build edges from all neighboring tiles
			GenerateEdges();

			// Mark obstacle cells from specific tilemap layers and locate the InfluenceMap
			foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
			{
				if (tilemap.CompareTag("InfluenceMap"))
				{
					InfluenceMap = tilemap;
					continue;
				}

				// Only Walls and Trees layers define obstacles
				string layerName = tilemap.gameObject.name;
				if (layerName != "Walls" && layerName != "Trees")
					continue;

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
							Grid.SetCellBlocked(i, j);
						}
					}
				}

				// Replace tree tiles with individual SpriteRenderers so each tree
				// participates in Y-depth sorting via SpriteSortPoint.Pivot.
				// TilemapRenderer (even Individual mode) uses sprite bounds center
				// for sorting, which breaks Y-depth with tall tree sprites.
				if (layerName == "Trees")
				{
					var treeParent = new GameObject("TreeSprites");
					treeParent.transform.SetParent(grid.transform);

					BoundsInt bounds = tilemap.cellBounds;
					for (int x = bounds.xMin; x < bounds.xMax; x++)
					{
						for (int y = bounds.yMin; y < bounds.yMax; y++)
						{
							Vector3Int cellPos = new Vector3Int(x, y, 0);
							Sprite sprite = tilemap.GetSprite(cellPos);
							if (sprite == null)
								continue;

							// Position at tile anchor (0.5, 0.5) to match original tilemap placement
							Vector3 worldPos = tilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);

							var treeGo = new GameObject($"Tree_{x}_{y}");
							treeGo.transform.SetParent(treeParent.transform);
							treeGo.transform.position = worldPos;

							var sr = treeGo.AddComponent<SpriteRenderer>();
							sr.sprite = sprite;
							sr.sortingLayerName = "Agents";
							sr.sortingOrder = 0;
							sr.spriteSortPoint = SpriteSortPoint.Pivot;
							sr.color = tilemap.GetColor(cellPos);

							// Preserve tile flip/rotation
							Matrix4x4 matrix = tilemap.GetTransformMatrix(cellPos);
							sr.flipX = matrix.m00 < 0;
							sr.flipY = matrix.m11 < 0;
						}
					}

					// Disable the TilemapRenderer so tiles don't double-render
					var tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
					if (tilemapRenderer != null)
						tilemapRenderer.enabled = false;
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
		/// A passage cell is walkable but not buildable, and units should move through
		/// it freely (e.g., building top rows). Used by movement to distinguish from
		/// cells temporarily blocked by mobile units.
		/// </summary>
		public bool IsGridPositionPassage(Vector3Int position)
		{
			return GridCells[position.x, position.y].IsPassage();
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
		/// a set of positions (e.g., the building pawn's cell).
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
		/// optionally ignoring a set of positions (e.g., friendly pawns who can move).
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
		/// Determines if the gridPosition is a neighbor of the unit.
		/// For buildings with a walkable top row, the top row passage cells also count
		/// as neighbors (they're adjacent to the building's non-walkable body).
		/// </summary>
		public bool IsNeighborOfUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
		{
			var neighbors = GetGridPositionsNearUnit(unitType, unitGridPosition);
			if (neighbors.Contains(gridPosition))
				return true;

			// Accept walkable top-row passage cells as neighbors
			Vector3Int size = Constants.UNIT_SIZE[unitType];
			if (!Constants.CAN_MOVE[unitType] && size.y > 1)
			{
				int y = unitGridPosition.y; // top row
				if (gridPosition.y == y
				    && gridPosition.x >= unitGridPosition.x
				    && gridPosition.x < unitGridPosition.x + size.x)
					return true;
			}
			return false;
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
		/// Find all approachable grid positions near this unit — buildable ring cells plus
		/// any walkable top-row passage cells (so units can stand right next to the body).
		/// </summary>
		public List<Vector3Int> GetBuildableGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			List<Vector3Int> positions = GetGridPositionsNearUnit(unitType, gridPosition);
			var result = positions.Where(IsGridPositionBuildable).ToList();

			// Include passage cells (building top row) as valid approach positions
			Vector3Int size = Constants.UNIT_SIZE[unitType];
			if (!Constants.CAN_MOVE[unitType] && size.y > 1)
			{
				int y = gridPosition.y; // top row
				for (int i = 0; i < size.x; i++)
				{
					var pos = new Vector3Int(gridPosition.x + i, y, 0);
					if (Utility.IsValidGridLocation(pos, MapSize) && IsGridPositionPassage(pos))
						result.Add(pos);
				}
			}

			return result;
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
		/// Get the path from a gridPosition to a position near the unit on any side of it.
		/// Delegates to shared GameGrid.FindPathToUnit for parity with SimGame (ADR-0001).
		/// </summary>
		public List<Vector3Int> GetPathToUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition, bool avoidUnits = false)
		{
			var start = new AgentSDK.Position(gridPosition.x, gridPosition.y);
			var anchor = new AgentSDK.Position(unitGridPosition.x, unitGridPosition.y);

			var posPath = Grid.FindPathToUnit(start, unitType, anchor);

			var path = new List<Vector3Int>(posPath.Count);
			foreach (var pos in posPath)
				path.Add(new Vector3Int(pos.X, pos.Y, 0));
			return path;
		}

		/// <summary>
		/// Gets the path between two grid positions using the shared GameGrid.
		/// This ensures identical pathfinding with SimGame for parity.
		/// </summary>
		public List<Vector3Int> GetPathBetweenGridPositions(Vector3Int startGridPosition, Vector3Int endGridPosition, bool avoidUnits = false)
		{
			var start = new AgentSDK.Position(startGridPosition.x, startGridPosition.y);
			var end = new AgentSDK.Position(endGridPosition.x, endGridPosition.y);

			var posPath = Grid.FindPath(start, end, avoidUnits);

			var path = new List<Vector3Int>(posPath.Count);
			foreach (var pos in posPath)
				path.Add(new Vector3Int(pos.X, pos.Y, 0));

			return path;
		}

		/// <summary>
		/// Place or remove a unit's footprint on the grid.
		/// Updates both the shared GameGrid (CellState) and legacy GridCells.
		/// </summary>
		public void SetUnitFootprint(UnitType unitType, Vector3Int gridPosition, bool occupy)
		{
			var pos = new AgentSDK.Position(gridPosition.x, gridPosition.y);
			Grid.SetUnitFootprint(unitType, pos, occupy);

			// Keep legacy GridCells in sync
			Vector3Int size = Constants.UNIT_SIZE[unitType];
			bool canMove = Constants.CAN_MOVE[unitType];

			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					var gridPos = gridPosition + new Vector3Int(i, -j, 0);
					if (!Utility.IsValidGridLocation(gridPos, MapSize)) continue;

					if (!occupy)
					{
						GridCells[gridPos.x, gridPos.y].SetBuildable(true);
						GridCells[gridPos.x, gridPos.y].SetWalkable(true);
						GridCells[gridPos.x, gridPos.y].SetPassage(false);
					}
					else if (canMove)
					{
						GridCells[gridPos.x, gridPos.y].SetBuildable(false);
						// walkable stays true (pass-through)
					}
					else
					{
						bool isTopRow = j == 0 && size.y > 1;
						GridCells[gridPos.x, gridPos.y].SetBuildable(false);
						GridCells[gridPos.x, gridPos.y].SetWalkable(isTopRow);
						GridCells[gridPos.x, gridPos.y].SetPassage(isTopRow);
					}
				}
			}
		}

		/// <summary>Legacy alias — calls SetUnitFootprint with inverted boolean.</summary>
		public void SetAreaBuildability(UnitType unitType, Vector3Int gridPosition, bool isBuildable)
		{
			SetUnitFootprint(unitType, gridPosition, !isBuildable);
		}
	}
}
