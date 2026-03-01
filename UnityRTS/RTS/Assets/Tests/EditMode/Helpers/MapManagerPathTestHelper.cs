using System.Collections.Generic;
using GameManager.GameElements;
using UnityEngine;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Helper that builds MapManager instances pre-configured for path-testing
	/// scenarios, such as corridor maps, L-shaped walls, and ring blockades.
	/// </summary>
	public static class MapManagerPathTestHelper
	{
		/// <summary>
		/// Build a map with a horizontal wall blocking the path at a given y row,
		/// leaving a single gap at gapX open.
		/// </summary>
		/// <param name="width">Map width</param>
		/// <param name="height">Map height</param>
		/// <param name="wallY">Y coordinate of the blocking row</param>
		/// <param name="gapX">X coordinate of the single passable cell in the wall</param>
		public static (MapManager manager, GameObject go) BuildHorizontalWall(
			int width, int height, int wallY, int gapX)
		{
			var blocked = new List<(int, int)>();
			for (int x = 0; x < width; x++)
			{
				if (x != gapX)
					blocked.Add((x, wallY));
			}
			return MapManagerTestHelper.Build(width, height, blocked.ToArray());
		}

		/// <summary>
		/// Build a map with a vertical wall blocking the path at a given x column,
		/// leaving a single gap at gapY open.
		/// </summary>
		public static (MapManager manager, GameObject go) BuildVerticalWall(
			int width, int height, int wallX, int gapY)
		{
			var blocked = new List<(int, int)>();
			for (int y = 0; y < height; y++)
			{
				if (y != gapY)
					blocked.Add((wallX, y));
			}
			return MapManagerTestHelper.Build(width, height, blocked.ToArray());
		}

		/// <summary>
		/// Build a map with a ring of blocked cells surrounding a center position,
		/// simulating a unit completely enclosed by obstacles.
		/// </summary>
		/// <param name="width">Map width</param>
		/// <param name="height">Map height</param>
		/// <param name="center">Center of the ring</param>
		/// <param name="radius">Ring radius (number of cells from center to ring)</param>
		public static (MapManager manager, GameObject go) BuildEnclosedRing(
			int width, int height, Vector3Int center, int radius)
		{
			var blocked = new List<(int, int)>();

			// Top and bottom rows of the ring
			for (int x = center.x - radius; x <= center.x + radius; x++)
			{
				blocked.Add((x, center.y + radius));
				blocked.Add((x, center.y - radius));
			}

			// Left and right columns of the ring (excluding corners already added)
			for (int y = center.y - radius + 1; y < center.y + radius; y++)
			{
				blocked.Add((center.x - radius, y));
				blocked.Add((center.x + radius, y));
			}

			return MapManagerTestHelper.Build(width, height, blocked.ToArray());
		}

		/// <summary>
		/// Block an entire rectangular area, simulating a large obstacle.
		/// </summary>
		/// <param name="width">Map width</param>
		/// <param name="height">Map height</param>
		/// <param name="areaTopLeft">Top-left (minimum x, maximum y) of the blocked area</param>
		/// <param name="areaWidth">Width of the blocked area</param>
		/// <param name="areaHeight">Height of the blocked area</param>
		public static (MapManager manager, GameObject go) BuildBlockedRect(
			int width, int height,
			Vector3Int areaTopLeft, int areaWidth, int areaHeight)
		{
			var blocked = new List<(int, int)>();
			for (int x = areaTopLeft.x; x < areaTopLeft.x + areaWidth; x++)
				for (int y = areaTopLeft.y; y < areaTopLeft.y + areaHeight; y++)
					blocked.Add((x, y));

			return MapManagerTestHelper.Build(width, height, blocked.ToArray());
		}

		/// <summary>
		/// Assert that a path exists between two positions on the given manager.
		/// Throws an NUnit assertion failure if no path is found.
		/// </summary>
		public static void AssertPathExists(MapManager manager, Vector3Int from, Vector3Int to,
			string message = null)
		{
			var path = manager.GetPathBetweenGridPositions(from, to);
			NUnit.Framework.Assert.Greater(path.Count, 0,
				message ?? $"Expected a path from {from} to {to} but none was found");
		}

		/// <summary>
		/// Assert that no path exists between two positions on the given manager.
		/// </summary>
		public static void AssertNoPath(MapManager manager, Vector3Int from, Vector3Int to,
			string message = null)
		{
			var path = manager.GetPathBetweenGridPositions(from, to);
			NUnit.Framework.Assert.AreEqual(0, path.Count,
				message ?? $"Expected no path from {from} to {to} but one was found");
		}
	}
}
