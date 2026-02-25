using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	[TestFixture]
	public class AStarSearchTests
	{
		private const int H = 5; // default grid height for helper

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

			// Block the end node
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

			// Block only the start node — neighbors are still walkable,
			// so A* should expand from start into walkable neighbors and find a path.
			// This simulates a unit inside its own building needing to pathfind out.
			cells[0, 0].SetWalkable(false);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0, "Path should be found via walkable neighbors");
			Assert.AreEqual("found", graph.LastSearchResult);
			// Path should not include the start node (standard A* behavior)
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

			// Path should not pass through any wall cell
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

			// maxExpansions = 1, should hit cap immediately
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

		[Test]
		public void ResetSearch_ClearsAllNodes()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			// Run a search to populate node state
			graph.AStarSearch(start, end);

			// Now reset
			graph.ResetSearch();

			foreach (var kvp in graph.nodesDict)
			{
				Assert.AreEqual(double.MaxValue, kvp.Value.cost);
				Assert.IsNull(kvp.Value.backPtr);
				Assert.IsNull(kvp.Value.priorityNode);
			}
		}

		[Test]
		public void RunTwice_SecondSearchValid()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int a = GraphTestHelper.NodeNbr(0, 0, 5);
			int b = GraphTestHelper.NodeNbr(4, 4, 5);
			int c = GraphTestHelper.NodeNbr(4, 0, 5);

			var path1 = graph.AStarSearch(a, b);
			Assert.AreEqual("found", graph.LastSearchResult);

			var path2 = graph.AStarSearch(a, c);
			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.AreEqual(c, path2[path2.Count - 1]);
		}

		[Test]
		public void PathDoesNotIncludeStart()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, 5);
			int end = GraphTestHelper.NodeNbr(3, 3, 5);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0);
			Assert.AreNotEqual(start, path[0]);
		}

		[Test]
		public void PathEndsAtTarget()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, 5);
			int end = GraphTestHelper.NodeNbr(4, 4, 5);

			var path = graph.AStarSearch(start, end);

			Assert.Greater(path.Count, 0);
			Assert.AreEqual(end, path[path.Count - 1]);
		}

		[Test]
		public void PathIsContiguous()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(10, 10);
			int start = GraphTestHelper.NodeNbr(0, 0, 10);
			int end = GraphTestHelper.NodeNbr(9, 9, 10);

			var path = graph.AStarSearch(start, end);
			Assert.Greater(path.Count, 0);

			// Prepend start for contiguity check
			var full = new List<int> { start };
			full.AddRange(path);

			for (int i = 0; i < full.Count - 1; i++)
			{
				int ax = full[i] / 10, ay = full[i] % 10;
				int bx = full[i + 1] / 10, by = full[i + 1] % 10;
				int dx = Math.Abs(ax - bx);
				int dy = Math.Abs(ay - by);

				Assert.IsTrue(dx <= 1 && dy <= 1 && (dx + dy) > 0,
					"Non-contiguous step from ({0},{1}) to ({2},{3})", ax, ay, bx, by);
			}
		}

		[Test]
		public void FindClosestNeighbor_NoEdges_ReturnsMinusOne()
		{
			// Build a graph with isolated nodes (no edges)
			var graph = new Graph<TestCell>();
			var cell0 = new TestCell(new Vector3Int(0, 0, 0));
			var cell1 = new TestCell(new Vector3Int(5, 5, 0));
			graph.AddNode(0, cell0);
			graph.AddNode(1, cell1);
			// No edges added to node 1

			int result = graph.FindClosestNeighborToTarget(0, 1);
			Assert.AreEqual(-1, result);
		}

		[Test]
		public void FindClosestNeighbor_ReturnsNearest()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			int start = GraphTestHelper.NodeNbr(0, 0, 5);
			int target = GraphTestHelper.NodeNbr(2, 2, 5);

			int closest = graph.FindClosestNeighborToTarget(start, target);

			// Closest neighbor of (2,2) to (0,0) should be (1,1)
			Assert.AreEqual(GraphTestHelper.NodeNbr(1, 1, 5), closest);
		}

		[Test]
		public void LargeGrid_100x100_CompletesQuickly()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(100, 100);
			int start = GraphTestHelper.NodeNbr(0, 0, 100);
			int end = GraphTestHelper.NodeNbr(99, 99, 100);

			var sw = Stopwatch.StartNew();
			var path = graph.AStarSearch(start, end);
			sw.Stop();

			Assert.AreEqual("found", graph.LastSearchResult);
			Assert.Greater(path.Count, 0);
			Assert.Less(sw.ElapsedMilliseconds, 5000, "A* on 100x100 should complete in under 5 seconds");
		}
	}
}
