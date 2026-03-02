using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for A* path structural properties, state management,
	/// FindClosestNeighborToTarget, and performance on large grids.
	/// </summary>
	[TestFixture]
	public class AStarPathPropertiesTests
	{
		[Test]
		public void ResetSearch_ClearsAllNodes()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			graph.AStarSearch(start, end);
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

		// ── FindClosestNeighborToTarget ─────────────────────────────────────────

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

		// ── Performance ────────────────────────────────────────────────────────

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
