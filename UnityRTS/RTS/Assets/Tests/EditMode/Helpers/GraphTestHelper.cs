using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Builds Graph&lt;TestCell&gt; instances wired as 2D grids with 8-connectivity.
	/// Node number encoding: nodeNbr = x * height + y (matches Utility.GridToInt).
	/// </summary>
	internal static class GraphTestHelper
	{
		internal static (Graph<TestCell> graph, TestCell[,] cells) BuildGrid(int width, int height)
			=> BuildGridWithWalls(width, height, null);

		internal static (Graph<TestCell> graph, TestCell[,] cells) BuildGridWithWalls(
			int width, int height, (int x, int y)[] walls)
		{
			var graph = new Graph<TestCell>();
			var cells = new TestCell[width, height];

			// Create nodes
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					cells[x, y] = new TestCell(new Vector3Int(x, y, 0));
					int nbr = x * height + y;
					graph.AddNode(nbr, cells[x, y]);
				}
			}

			// Apply walls (unwalkable + unbuildable)
			if (walls != null)
			{
				foreach (var (wx, wy) in walls)
				{
					cells[wx, wy].SetWalkable(false);
					cells[wx, wy].SetBuildable(false);
				}
			}

			// Create 8-connected edges
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					int fromNbr = x * height + y;
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 && dy == 0) continue;
							int nx = x + dx, ny = y + dy;
							if (nx >= 0 && ny >= 0 && nx < width && ny < height)
							{
								int toNbr = nx * height + ny;
								double cost = Vector3.Distance(
									cells[x, y].GetPosition(),
									cells[nx, ny].GetPosition());
								graph.AddEdge(fromNbr, toNbr, cost);
							}
						}
					}
				}
			}

			return (graph, cells);
		}

		internal static int NodeNbr(int x, int y, int height) => x * height + y;
	}
}
