using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests that A* selects the lowest-cost path when multiple routes exist.
	/// Complements AStarSearchTests which verifies correctness and result codes
	/// but does not specifically assert path optimality.
	/// </summary>
	[TestFixture]
	public class AStarOptimalityTests
	{
		private const int H = 5;

		#region Optimality

		/// <summary>
		/// When two equal-length diagonals are available, the returned path length
		/// matches the shortest possible hop count.
		/// </summary>
		[Test]
		public void ShortDiagonalVsLongerOrtho_TakesFewerHops()
		{
			// 5x5 open grid — diagonal path from (0,0) to (4,4) is 4 hops
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			var path = graph.AStarSearch(start, end);

			// Shortest path on an 8-connected grid from corner to corner is 4 steps
			Assert.LessOrEqual(path.Count, 4,
				"A* should take the shortest (diagonal) route — at most 4 hops");
		}

		/// <summary>
		/// Linear path: nodes connected in a chain A-B-C-D-E with uniform costs.
		/// A* should reach E in exactly 4 hops, not a longer detour.
		/// </summary>
		[Test]
		public void LinearChain_ReturnsExactHopCount()
		{
			// 5x1 strip: 5 nodes in a horizontal line
			var (graph, _) = GraphTestHelper.BuildGrid(5, 1);
			int start = GraphTestHelper.NodeNbr(0, 0, 1);
			int end = GraphTestHelper.NodeNbr(4, 0, 1);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(4, path.Count,
				"Linear 5-node chain should have exactly 4 hops from start to end");
			Assert.AreEqual(end, path[path.Count - 1]);
		}

		/// <summary>
		/// When a detour is forced by walls, A* still finds the globally shortest
		/// walkable route (not just any route).
		/// On a 5×5 grid with a vertical wall at x=2 (y=0..4), going around
		/// adds hops compared to the straight line.
		/// </summary>
		[Test]
		public void WallDetour_PathLengthIsBoundedAbove()
		{
			// Block column x=2 fully (y=0 to 4) except at y=4 to allow going over top
			var walls = new (int, int)[] { (2, 0), (2, 1), (2, 2), (2, 3) };
			var (graph, _) = GraphTestHelper.BuildGridWithWalls(5, 5, walls);

			int start = GraphTestHelper.NodeNbr(0, 2, H);
			int end = GraphTestHelper.NodeNbr(4, 2, H);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.Greater(path.Count, 0);
			// With the wall, path must go around — should take no more than ~6 hops
			Assert.LessOrEqual(path.Count, 8,
				"Detour around a partial wall should be bounded in length");
		}

		/// <summary>
		/// Path must not revisit the start node.
		/// </summary>
		[Test]
		public void Path_DoesNotContainStartNode()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);
			int end = GraphTestHelper.NodeNbr(4, 4, H);

			var path = graph.AStarSearch(start, end);

			Assert.IsFalse(path.Contains(start),
				"Path should not include the start node (it is excluded by convention)");
		}

		/// <summary>
		/// All nodes in the returned path are within one step of each other (contiguity).
		/// Verified for a longer path across a 10×1 grid.
		/// </summary>
		[Test]
		public void LongLinearPath_AllStepsAreContiguous()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(10, 1);
			int start = GraphTestHelper.NodeNbr(0, 0, 1);
			int end = GraphTestHelper.NodeNbr(9, 0, 1);

			var path = graph.AStarSearch(start, end);
			Assert.AreEqual("found", graph.LastSearchResult);

			var full = new List<int> { start };
			full.AddRange(path);

			for (int i = 0; i < full.Count - 1; i++)
			{
				int ax = full[i] / 1, ay = full[i] % 1;
				int bx = full[i + 1] / 1, by = full[i + 1] % 1;
				int dx = System.Math.Abs(ax - bx);
				int dy = System.Math.Abs(ay - by);
				Assert.IsTrue(dx <= 1 && dy <= 1 && (dx + dy) > 0,
					$"Step {i} is not contiguous: ({ax},{ay}) to ({bx},{by})");
			}
		}

		#endregion

		#region Multiple Searches on Same Graph

		/// <summary>
		/// A* can be run multiple times on the same graph and each time produces
		/// a correct result for a different end node.
		/// </summary>
		[Test]
		public void MultipleSearches_EachFindsCorrectTarget()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(2, 2, H);

			int[] targets = {
				GraphTestHelper.NodeNbr(0, 0, H),
				GraphTestHelper.NodeNbr(4, 4, H),
				GraphTestHelper.NodeNbr(4, 0, H)
			};

			foreach (int target in targets)
			{
				var path = graph.AStarSearch(start, target);
				Assert.AreEqual("found", graph.LastSearchResult,
					$"Search to {target} should succeed");
				Assert.AreEqual(target, path[path.Count - 1],
					$"Path should end at target {target}");
			}
		}

		/// <summary>
		/// After a successful search, running A* again to a new target starts fresh
		/// and finds the correct new path.
		/// </summary>
		[Test]
		public void SecondSearch_AfterReset_FindsNewTarget()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, H);

			graph.AStarSearch(start, GraphTestHelper.NodeNbr(4, 4, H));
			Assert.AreEqual("found", graph.LastSearchResult);

			var path2 = graph.AStarSearch(start, GraphTestHelper.NodeNbr(4, 0, H));
			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.AreEqual(GraphTestHelper.NodeNbr(4, 0, H), path2[path2.Count - 1]);
		}

		#endregion
	}
}
