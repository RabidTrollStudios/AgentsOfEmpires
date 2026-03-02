using NUnit.Framework;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for the core A* search algorithm:
	/// same-node early exit, basic paths, wall avoidance,
	/// blocked endpoints, and expansion cap behavior.
	/// </summary>
	[TestFixture]
	public class AStarCoreTests
	{
		[Test]
		public void SameNode_ReturnsEmpty_SameNode()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int node = GraphTestHelper.NodeNbr(2, 2, 5);

			var path = graph.AStarSearch(node, node);

			Assert.AreEqual(0, path.Count);
			Assert.AreEqual("same_node", graph.LastSearchResult);
		}

		[Test]
		public void AdjacentNodes_ReturnsOneStep()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(2, 2, 5);
			int end = GraphTestHelper.NodeNbr(2, 3, 5);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(1, path.Count);
			Assert.AreEqual(end, path[0]);
			Assert.AreEqual("found", graph.LastSearchResult);
		}

		[Test]
		public void DiagonalPath_ReturnsOneStep()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(2, 2, 5);
			int end = GraphTestHelper.NodeNbr(3, 3, 5);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(1, path.Count);
			Assert.AreEqual(end, path[0]);
			Assert.AreEqual("found", graph.LastSearchResult);
		}

		[Test]
		public void LongerPath_CorrectLength()
		{
			// 5x1 strip: nodes (0,0) through (4,0)
			var (graph, _) = GraphTestHelper.BuildGrid(5, 1);
			int start = GraphTestHelper.NodeNbr(0, 0, 1);
			int end = GraphTestHelper.NodeNbr(4, 0, 1);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(4, path.Count); // path excludes start
			Assert.AreEqual(end, path[path.Count - 1]);
			Assert.AreEqual("found", graph.LastSearchResult);
		}

		[Test]
		public void EndNodeBlocked_ReturnsEmpty()
		{
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			int end = GraphTestHelper.NodeNbr(4, 4, 5);
			cells[4, 4].SetWalkable(false);

			int start = GraphTestHelper.NodeNbr(0, 0, 5);
			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(0, path.Count);
			Assert.AreEqual("end_blocked", graph.LastSearchResult);
		}

		[Test]
		public void StartBlocked_StillFindsPathThroughWalkableNeighbors()
		{
			var (graph, cells) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, 5);
			int end = GraphTestHelper.NodeNbr(4, 4, 5);

			// Block only the start — neighbors are still walkable,
			// so A* should expand from start into walkable neighbors and find a path.
			cells[0, 0].SetWalkable(false);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0, "Path should be found via walkable neighbors");
			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.AreNotEqual(start, path[0]);
		}

		[Test]
		public void WallChannel_PathGoesAround()
		{
			// 5x5 grid with a vertical wall at x=2, y=1..3
			var walls = new (int, int)[] { (2, 1), (2, 2), (2, 3) };
			var (graph, _) = GraphTestHelper.BuildGridWithWalls(5, 5, walls);

			int start = GraphTestHelper.NodeNbr(0, 2, 5);
			int end = GraphTestHelper.NodeNbr(4, 2, 5);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0, "Path should exist around the wall");
			Assert.AreEqual("found", graph.LastSearchResult);

			foreach (int nodeNbr in path)
			{
				Assert.AreNotEqual(GraphTestHelper.NodeNbr(2, 1, 5), nodeNbr);
				Assert.AreNotEqual(GraphTestHelper.NodeNbr(2, 2, 5), nodeNbr);
				Assert.AreNotEqual(GraphTestHelper.NodeNbr(2, 3, 5), nodeNbr);
			}
		}

		[Test]
		public void FullyEnclosed_ReturnsExhausted()
		{
			// 5x5 grid, block all neighbors of (2,2)
			var walls = new (int, int)[]
			{
				(1,1), (1,2), (1,3),
				(2,1),        (2,3),
				(3,1), (3,2), (3,3)
			};
			var (graph, _) = GraphTestHelper.BuildGridWithWalls(5, 5, walls);

			int start = GraphTestHelper.NodeNbr(2, 2, 5);
			int end = GraphTestHelper.NodeNbr(4, 4, 5);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual(0, path.Count);
			Assert.AreEqual("exhausted", graph.LastSearchResult);
		}

		[Test]
		public void ExpansionCap_ReturnsCap()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(10, 10);
			int start = GraphTestHelper.NodeNbr(0, 0, 10);
			int end = GraphTestHelper.NodeNbr(9, 9, 10);

			// maxExpansions = 1 hits cap immediately
			var path = graph.AStarSearch(start, end, maxExpansions: 1);

			Assert.AreEqual(0, path.Count);
			Assert.AreEqual("cap", graph.LastSearchResult);
		}

		[Test]
		public void DefaultCap_FindsPath()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(10, 10);
			int start = GraphTestHelper.NodeNbr(0, 0, 10);
			int end = GraphTestHelper.NodeNbr(9, 9, 10);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0);
			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.Less(graph.LastSearchExpansions, 2000);
		}
	}
}
