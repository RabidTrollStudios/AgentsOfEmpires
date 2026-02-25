using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using GameManager.GameElements;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Creates a MapManager backed by a synthetic GridCell[,] array.
	/// Uses a real Tilemap from a throwaway GameObject (valid in EditMode)
	/// and sets private fields via reflection.
	/// </summary>
	internal static class MapManagerTestHelper
	{
		internal static (MapManager manager, GameObject tilemapGo) Build(
			int width, int height, (int x, int y)[] blockedCells = null)
		{
			var manager = new MapManager();
			var mapSize = new Vector3Int(width, height, 0);

			// Set MapSize via reflection (private set)
			typeof(MapManager)
				.GetProperty("MapSize", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(manager, mapSize);

			// Create a Tilemap via a temporary GameObject (valid in EditMode)
			var go = new GameObject("TestTilemap");
			go.AddComponent<Grid>();
			var tilemap = go.AddComponent<Tilemap>();

			// Allocate GridCells using real GridCell constructor
			var cells = new GridCell[width, height];
			for (int x = 0; x < width; x++)
				for (int y = 0; y < height; y++)
					cells[x, y] = new GridCell(tilemap, new Vector3Int(x, y, 0));

			// Apply blocked cells
			if (blockedCells != null)
			{
				foreach (var (bx, by) in blockedCells)
				{
					cells[bx, by].SetBuildable(false);
					cells[bx, by].SetWalkable(false);
				}
			}

			// Set GridCells via reflection (private set)
			typeof(MapManager)
				.GetProperty("GridCells", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(manager, cells);

			// Build a Graph<GridCell> matching the grid
			var graph = new Graph<GridCell>();
			for (int x = 0; x < width; x++)
				for (int y = 0; y < height; y++)
					graph.AddNode(Utility.GridToInt(new Vector3Int(x, y, 0), mapSize), cells[x, y]);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 && dy == 0) continue;
							int nx = x + dx, ny = y + dy;
							if (nx >= 0 && ny >= 0 && nx < width && ny < height)
							{
								graph.AddEdge(
									Utility.GridToInt(new Vector3Int(x, y, 0), mapSize),
									Utility.GridToInt(new Vector3Int(nx, ny, 0), mapSize),
									Vector3.Distance(cells[x, y].GetPosition(),
													 cells[nx, ny].GetPosition()));
							}
						}
					}
				}
			}

			// Set private Graph property via reflection
			typeof(MapManager)
				.GetProperty("Graph", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(manager, graph);

			return (manager, go);
		}
	}
}
