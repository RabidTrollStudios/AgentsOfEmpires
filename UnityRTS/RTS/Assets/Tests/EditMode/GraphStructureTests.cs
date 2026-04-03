using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	[TestFixture]
	public class GraphStructureTests
	{
		#region Happy Path

		/// <summary>
		/// Adding a node should make it retrievable from nodesDict.
		/// </summary>
		[Test]
		public void AddNode_NodeExistsInDict()
		{
			var graph = new Graph<TestCell>();
			var cell = new TestCell(new Vector3Int(0, 0, 0));

			graph.AddNode(0, cell);

			Assert.IsTrue(graph.NodesDict.ContainsKey(0),
				"Node 0 should exist in nodesDict after AddNode");
		}

		/// <summary>
		/// Adding two nodes with an edge should increase the global edge list count.
		/// </summary>
		[Test]
		public void AddEdge_IncreasesGlobalEdgeCount()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));

			graph.AddEdge(0, 1, 1.0);

			Assert.AreEqual(1, graph.Edges.Count,
				"Global edge list should have exactly one entry after AddEdge");
		}

		/// <summary>
		/// After AddEdge, the edge should appear in the start node's adjacency list.
		/// </summary>
		[Test]
		public void AddEdge_EdgeAppearsInStartNodeEdges()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));

			graph.AddEdge(0, 1, 1.0);

			Assert.AreEqual(1, graph.NodesDict[0].Edges.Count,
				"Start node's edge list should contain the newly added edge");
		}

		/// <summary>
		/// After AddEdge, the edge should also appear in the end node's adjacency list
		/// (Graph.AddEdge adds to both ends).
		/// </summary>
		[Test]
		public void AddEdge_EdgeAppearsInEndNodeEdges()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));

			graph.AddEdge(0, 1, 1.0);

			Assert.AreEqual(1, graph.NodesDict[1].Edges.Count,
				"End node's edge list should also contain the added edge (bidirectional storage)");
		}

		/// <summary>
		/// Adding multiple nodes should all be accessible by their keys.
		/// </summary>
		[Test]
		public void AddMultipleNodes_AllRetrievable()
		{
			var graph = new Graph<TestCell>();
			for (int i = 0; i < 5; i++)
				graph.AddNode(i, new TestCell(new Vector3Int(i, 0, 0)));

			for (int i = 0; i < 5; i++)
				Assert.IsTrue(graph.NodesDict.ContainsKey(i),
					$"Node {i} should exist after being added");
			Assert.AreEqual(5, graph.NodesDict.Count,
				"nodesDict should contain exactly 5 nodes");
		}

		/// <summary>
		/// The copy constructor should preserve all nodes from the source graph.
		/// </summary>
		[Test]
		public void CopyConstructor_PreservesNodeCount()
		{
			var (original, _) = GraphTestHelper.BuildGrid(3, 3);
			var copy = new Graph<TestCell>(original);

			Assert.AreEqual(original.NodesDict.Count, copy.NodesDict.Count,
				"Copy should have the same number of nodes as the original");
		}

		/// <summary>
		/// The copy constructor should preserve all edges from the source graph.
		/// </summary>
		[Test]
		public void CopyConstructor_PreservesEdgeCount()
		{
			var (original, _) = GraphTestHelper.BuildGrid(3, 3);
			var copy = new Graph<TestCell>(original);

			Assert.AreEqual(original.Edges.Count, copy.Edges.Count,
				"Copy should have the same number of edges as the original");
		}

		/// <summary>
		/// An isolated node (no edges added) should have an empty edges list.
		/// </summary>
		[Test]
		public void IsolatedNode_HasNoEdges()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.AreEqual(0, graph.NodesDict[0].Edges.Count,
				"Isolated node should have zero edges");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// Edge cost is stored correctly and accessible through the edge object.
		/// </summary>
		[Test]
		public void AddEdge_CostStoredCorrectly()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(3, 4, 0)));

			double cost = 5.0;
			graph.AddEdge(0, 1, cost);

			Assert.AreEqual(cost, graph.Edges[0].Cost, 0.0001,
				"Edge cost should match the value passed to AddEdge");
		}

		/// <summary>
		/// A fresh graph with no nodes or edges should have empty collections.
		/// </summary>
		[Test]
		public void EmptyGraph_HasNoNodesOrEdges()
		{
			var graph = new Graph<TestCell>();

			Assert.AreEqual(0, graph.NodesDict.Count, "New graph should have zero nodes");
			Assert.AreEqual(0, graph.Edges.Count, "New graph should have zero edges");
		}

		/// <summary>
		/// A node can have multiple edges (fan-out). Adding 3 edges from node 0
		/// should result in 3 entries in node 0's edges list.
		/// </summary>
		[Test]
		public void AddMultipleEdgesFromOneNode_AllInEdgeList()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddNode(2, new TestCell(new Vector3Int(0, 1, 0)));
			graph.AddNode(3, new TestCell(new Vector3Int(1, 1, 0)));

			graph.AddEdge(0, 1, 1.0);
			graph.AddEdge(0, 2, 1.0);
			graph.AddEdge(0, 3, 1.414);

			Assert.AreEqual(3, graph.NodesDict[0].Edges.Count,
				"Node 0 should have 3 edges after three AddEdge calls from it");
		}

		#endregion
	}
}
