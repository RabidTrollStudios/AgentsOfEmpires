using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests directly targeting Node&lt;T&gt; behavior: constructor invariants,
	/// copy constructor semantics, and ResetSearchVariables state restoration.
	/// </summary>
	[TestFixture]
	public class GraphNodeTests
	{
		#region Constructor

		/// <summary>
		/// AddNode stores the correct node number, retrievable via nodesDict.
		/// </summary>
		[Test]
		public void Node_Number_MatchesAddedValue()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(7, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.AreEqual(7, graph.NodesDict[7].Number,
				"Node number should match the value passed to AddNode");
		}

		/// <summary>
		/// AddNode stores the correct item reference.
		/// </summary>
		[Test]
		public void Node_Item_MatchesAddedCell()
		{
			var graph = new Graph<TestCell>();
			var cell = new TestCell(new Vector3Int(3, 4, 0));
			graph.AddNode(0, cell);

			Assert.AreSame(cell, graph.NodesDict[0].Item,
				"Node item should be the same reference as the cell passed to AddNode");
		}

		/// <summary>
		/// A freshly added node should have cost == double.MaxValue.
		/// </summary>
		[Test]
		public void Node_InitialCost_IsMaxValue()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.AreEqual(double.MaxValue, graph.NodesDict[0].Cost,
				"Fresh node cost should be double.MaxValue before any search");
		}

		/// <summary>
		/// A freshly added node should have backPtr == null.
		/// </summary>
		[Test]
		public void Node_InitialBackPtr_IsNull()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.IsNull(graph.NodesDict[0].BackPtr,
				"Fresh node backPtr should be null before any search");
		}

		/// <summary>
		/// A freshly added node should have priorityNode == null.
		/// </summary>
		[Test]
		public void Node_InitialPriorityNode_IsNull()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.IsNull(graph.NodesDict[0].PriorityNode,
				"Fresh node priorityNode should be null before any search");
		}

		/// <summary>
		/// A freshly added node should have an empty edges list.
		/// </summary>
		[Test]
		public void Node_InitialEdges_IsEmpty()
		{
			var graph = new Graph<TestCell>();
			graph.AddNode(0, new TestCell(new Vector3Int(0, 0, 0)));

			Assert.AreEqual(0, graph.NodesDict[0].Edges.Count,
				"Fresh node should have no edges before AddEdge is called");
		}

		#endregion

		#region Copy Constructor

		/// <summary>
		/// The Node copy constructor preserves the node number.
		/// </summary>
		[Test]
		public void NodeCopyCtor_PreservesNumber()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			var original = graph.NodesDict[GraphTestHelper.NodeNbr(1, 1, 3)];
			var copy = new Node<TestCell>(original);

			Assert.AreEqual(original.Number, copy.Number,
				"Copy constructor should preserve node number");
		}

		/// <summary>
		/// The Node copy constructor preserves the item reference.
		/// </summary>
		[Test]
		public void NodeCopyCtor_PreservesItem()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			var original = graph.NodesDict[GraphTestHelper.NodeNbr(1, 1, 3)];
			var copy = new Node<TestCell>(original);

			Assert.AreSame(original.Item, copy.Item,
				"Copy constructor should preserve item reference");
		}

		/// <summary>
		/// The Node copy constructor does NOT copy the edges list.
		/// (Copy is a shallow metadata copy only, used in A* for backpointer tracking.)
		/// </summary>
		[Test]
		public void NodeCopyCtor_EdgesListIsEmpty()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			// Centre node has 8 edges in an 8-connected 3x3 grid
			var original = graph.NodesDict[GraphTestHelper.NodeNbr(1, 1, 3)];
			Assert.Greater(original.Edges.Count, 0, "Precondition: original node should have edges");

			var copy = new Node<TestCell>(original);

			Assert.AreEqual(0, copy.Edges.Count,
				"Copy constructor should NOT copy edges — new node starts with empty edges list");
		}

		#endregion

		#region ResetSearchVariables

		/// <summary>
		/// After running A*, ResetSearch restores all node costs to double.MaxValue.
		/// </summary>
		[Test]
		public void ResetSearchVariables_RestoresCostToMaxValue()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			graph.AStarSearch(start, end);
			graph.ResetSearch();

			foreach (var node in graph.NodesDict.Values)
			{
				Assert.AreEqual(double.MaxValue, node.Cost,
					$"Node {node.Number} cost should be MaxValue after reset");
			}
		}

		/// <summary>
		/// After running A*, ResetSearch clears all backPtr references.
		/// </summary>
		[Test]
		public void ResetSearchVariables_ClearsBackPtr()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			graph.AStarSearch(start, end);
			graph.ResetSearch();

			foreach (var node in graph.NodesDict.Values)
			{
				Assert.IsNull(node.BackPtr,
					$"Node {node.Number} backPtr should be null after reset");
			}
		}

		/// <summary>
		/// After running A*, ResetSearch clears all priorityNode references.
		/// </summary>
		[Test]
		public void ResetSearchVariables_ClearsPriorityNode()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			graph.AStarSearch(start, end);
			graph.ResetSearch();

			foreach (var node in graph.NodesDict.Values)
			{
				Assert.IsNull(node.PriorityNode,
					$"Node {node.Number} priorityNode should be null after reset");
			}
		}

		#endregion
	}
}
