using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests directly targeting Edge&lt;T&gt; behavior: constructor invariants,
	/// copy constructor, and GetNeighbor directional correctness.
	/// </summary>
	[TestFixture]
	public class GraphEdgeTests
	{
		#region Constructor / Storage

		/// <summary>
		/// The edge cost passed to AddEdge is stored correctly on the edge object.
		/// </summary>
		[Test]
		public void Edge_Cost_StoredCorrectly()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 7.5);

			Assert.AreEqual(7.5, graph.Edges[0].Cost, 0.0001,
				"Edge cost should match the value passed to AddEdge");
		}

		/// <summary>
		/// Edge.Start points to the node with the start number.
		/// </summary>
		[Test]
		public void Edge_Start_PointsToStartNode()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			Assert.AreEqual(0, graph.Edges[0].Start.Number,
				"Edge.Start should be the node with the start number");
		}

		/// <summary>
		/// Edge.End points to the node with the end number.
		/// </summary>
		[Test]
		public void Edge_End_PointsToEndNode()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			Assert.AreEqual(1, graph.Edges[0].End.Number,
				"Edge.End should be the node with the end number");
		}

		/// <summary>
		/// Different edge costs are distinguishable — two edges store their own costs independently.
		/// </summary>
		[Test]
		public void TwoEdges_StoreSeparateCosts()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddNode(2, new TestCell(new Vector3Int(0, 1, 0)));
			graph.AddEdge(0, 1, 2.0);
			graph.AddEdge(0, 2, 5.0);

			var edgeTo1 = graph.NodesDict[0].Edges.Find(e => e.End.Number == 1 || e.Start.Number == 1);
			var edgeTo2 = graph.NodesDict[0].Edges.Find(e => e.End.Number == 2 || e.Start.Number == 2);

			Assert.IsNotNull(edgeTo1, "Should find edge to node 1");
			Assert.IsNotNull(edgeTo2, "Should find edge to node 2");
			Assert.AreNotEqual(edgeTo1.Cost, edgeTo2.Cost,
				"Two edges with different costs should store their costs independently");
		}

		#endregion

		#region GetNeighbor

		/// <summary>
		/// GetNeighbor(startNode) returns the end node.
		/// </summary>
		[Test]
		public void GetNeighbor_FromStart_ReturnsEnd()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			var edge = graph.Edges[0];
			var startNode = graph.NodesDict[0];
			var endNode = graph.NodesDict[1];

			Assert.AreSame(endNode, edge.GetNeighbor(startNode),
				"GetNeighbor(startNode) should return the end node");
		}

		/// <summary>
		/// GetNeighbor(endNode) returns the start node (undirected edge).
		/// </summary>
		[Test]
		public void GetNeighbor_FromEnd_ReturnsStart()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			var edge = graph.Edges[0];
			var startNode = graph.NodesDict[0];
			var endNode = graph.NodesDict[1];

			Assert.AreSame(startNode, edge.GetNeighbor(endNode),
				"GetNeighbor(endNode) should return the start node");
		}

		/// <summary>
		/// GetNeighbor with a node that is neither start nor end falls back to returning start.
		/// </summary>
		[Test]
		public void GetNeighbor_FromThirdNode_ReturnsStart()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddNode(2, new TestCell(new Vector3Int(2, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			var edge = graph.Edges[0];
			var thirdNode = graph.NodesDict[2];
			var startNode = graph.NodesDict[0];

			// Per implementation: if (start == item) return end; else return start;
			Assert.AreSame(startNode, edge.GetNeighbor(thirdNode),
				"GetNeighbor with a non-endpoint node falls back to returning start");
		}

		/// <summary>
		/// GetNeighbor is consistent: calling it twice from the same node returns the same result.
		/// </summary>
		[Test]
		public void GetNeighbor_CalledTwice_ReturnsSameResult()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			var edge = graph.Edges[0];
			var startNode = graph.NodesDict[0];

			var first = edge.GetNeighbor(startNode);
			var second = edge.GetNeighbor(startNode);

			Assert.AreSame(first, second,
				"GetNeighbor should return the same node on repeated calls");
		}

		#endregion

		#region Edge Sharing Between Nodes

		/// <summary>
		/// An edge added between two nodes is the same object in both nodes' edge lists.
		/// </summary>
		[Test]
		public void SharedEdge_SameObjectInBothNodes()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));
			graph.AddNode(1, new TestCell(new Vector3Int(1, 0, 0)));
			graph.AddEdge(0, 1, 1.0);

			var edgeFromNode0 = graph.NodesDict[0].Edges[0];
			var edgeFromNode1 = graph.NodesDict[1].Edges[0];

			Assert.AreSame(edgeFromNode0, edgeFromNode1,
				"The edge in node0's list and node1's list should be the same object reference");
		}

		#endregion
	}
}
