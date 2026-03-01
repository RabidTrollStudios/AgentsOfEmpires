using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for AStarSearch's avoidUnits=true parameter, which uses
	/// IsBuildable() instead of IsWalkable() to determine traversability.
	/// This simulates pathfinding that avoids cells occupied by friendly units.
	/// </summary>
	[TestFixture]
	public class AStarAvoidUnitsTests
	{
		private const int H = 5;

		#region End Node Blocking

		/// <summary>
		/// With avoidUnits=true, if the end node is not buildable, returns "end_blocked".
		/// </summary>
		[Test]
		public void AvoidUnits_EndNotBuildable_ReturnsEndBlocked()
		{
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			cells[4, 4].SetBuildable(false);

			var path = graph.AStarSearch(start, end, avoidUnits: true);

			Assert.AreEqual(0, path.Count);
			Assert.AreEqual("end_blocked", graph.LastSearchResult,
				"avoidUnits=true: non-buildable end node should be 'end_blocked'");
		}

		/// <summary>
		/// With avoidUnits=false (default), a non-buildable but walkable end node
		/// is NOT blocked — path is found.
		/// </summary>
		[Test]
		public void DefaultMode_EndNotBuildable_ButWalkable_FindsPath()
		{
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			// Not buildable but still walkable
			cells[4, 4].SetBuildable(false);

			var path = graph.AStarSearch(start, end, avoidUnits: false);

			Assert.Greater(path.Count, 0,
				"Default mode: non-buildable but walkable end should still be reachable");
			Assert.AreEqual("found", graph.LastSearchResult);
		}

		#endregion

		#region Mid-Path Avoidance

		/// <summary>
		/// With avoidUnits=true, non-buildable cells in the middle are avoided.
		/// A path going only through buildable cells is returned.
		/// </summary>
		[Test]
		public void AvoidUnits_NonBuildableInMiddle_PathGoesAround()
		{
			// Wall column at x=2 (cells not buildable but still walkable)
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			for (int y = 1; y < 4; y++)
				cells[2, y].SetBuildable(false);

			int start = GraphTestHelper.NodeNbr(0, 2, H);
			int end = GraphTestHelper.NodeNbr(4, 2, H);

			var path = graph.AStarSearch(start, end, avoidUnits: true);

			Assert.AreEqual("found", graph.LastSearchResult,
				"Path should still exist going around the non-buildable cells");
			Assert.Greater(path.Count, 0);

			// Verify no path node is a non-buildable cell
			foreach (int nodeNbr in path)
			{
				int x = nodeNbr / H;
				int y = nodeNbr % H;
				Assert.IsTrue(cells[x, y].IsBuildable(),
					$"Path node ({x},{y}) should be buildable when avoidUnits=true");
			}
		}

		/// <summary>
		/// With avoidUnits=false (default), walkable but non-buildable cells CAN
		/// appear in the path (the wall is traversed, not avoided).
		/// </summary>
		[Test]
		public void DefaultMode_NonBuildableCellsTraversable_PathMayGoThrough()
		{
			// Cells at x=2 are NOT buildable but ARE walkable — default mode can cross them
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 1);
			cells[2, 0].SetBuildable(false);
			// Walkable is still true by default

			int start = GraphTestHelper.NodeNbr(0, 0, 1);
			int end = GraphTestHelper.NodeNbr(4, 0, 1);

			var path = graph.AStarSearch(start, end, avoidUnits: false);

			Assert.AreEqual("found", graph.LastSearchResult,
				"Default mode should traverse non-buildable (but walkable) cells");
			Assert.Greater(path.Count, 0);
		}

		#endregion

		#region Both Modes Agree on Open Grid

		/// <summary>
		/// On an open grid (all cells walkable and buildable), avoidUnits=true and
		/// avoidUnits=false produce paths of equal length.
		/// </summary>
		[Test]
		public void OpenGrid_BothModes_SamePathLength()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			var pathDefault = graph.AStarSearch(start, end, avoidUnits: false);
			var pathAvoid = graph.AStarSearch(start, end, avoidUnits: true);

			Assert.AreEqual(pathDefault.Count, pathAvoid.Count,
				"On an open grid (all cells buildable+walkable), both modes should find equivalent-length paths");
		}

		#endregion

		#region Fully Blocked with avoidUnits

		/// <summary>
		/// With avoidUnits=true, if all neighbors of start are not buildable,
		/// the search is exhausted and returns an empty path.
		/// </summary>
		[Test]
		public void AvoidUnits_AllNeighborsNonBuildable_Exhausted()
		{
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			int startX = 2, startY = 2;

			// Block all 8 neighbors of (2,2) as non-buildable
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					cells[startX + dx, startY + dy].SetBuildable(false);
				}
			}

			int start = GraphTestHelper.NodeNbr(startX, startY, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			var path = graph.AStarSearch(start, end, avoidUnits: true);

			Assert.AreEqual(0, path.Count,
				"Path should be empty when all neighbors are non-buildable in avoidUnits mode");
			Assert.AreEqual("exhausted", graph.LastSearchResult);
		}

		#endregion
	}
}
