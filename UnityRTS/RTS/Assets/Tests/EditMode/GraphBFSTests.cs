using System;
using NUnit.Framework;
using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for BreadthFirstSearch behavior and the isUniform flag guard.
	/// BFS is currently a stub: it throws on non-uniform graphs and returns
	/// an empty path on uniform graphs (not yet implemented).
	/// </summary>
	[TestFixture]
	public class GraphBFSTests
	{
		private bool _originalIsUniform;

		[SetUp]
		public void SetUp()
		{
			_originalIsUniform = Graph<TestCell>.isUniform;
		}

		[TearDown]
		public void TearDown()
		{
			Graph<TestCell>.isUniform = _originalIsUniform;
		}

		#region Non-Uniform Guard

		/// <summary>
		/// BreadthFirstSearch throws when isUniform is false (default).
		/// Non-uniform graphs cannot use BFS because edges have different costs.
		/// </summary>
		[Test]
		public void BFS_NonUniform_ThrowsException()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			Graph<TestCell>.isUniform = false;

			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			Assert.Throws<Exception>(
				() => graph.BreadthFirstSearch(start, end),
				"BFS should throw when isUniform is false");
		}

		/// <summary>
		/// A* still functions correctly when isUniform is false (it does not depend on this flag).
		/// </summary>
		[Test]
		public void AStar_NonUniform_StillFindsPath()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(5, 5);
			Graph<TestCell>.isUniform = false;

			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			var path = graph.AStarSearch(start, end);

			Assert.AreEqual("found", graph.LastSearchResult,
				"A* should not be affected by the isUniform flag");
		}

		#endregion

		#region Uniform Mode

		/// <summary>
		/// BreadthFirstSearch does not throw when isUniform is true.
		/// (BFS itself is not yet implemented and returns empty; this verifies no exception.)
		/// </summary>
		[Test]
		public void BFS_Uniform_DoesNotThrow()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			Graph<TestCell>.isUniform = true;

			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			Assert.DoesNotThrow(
				() => graph.BreadthFirstSearch(start, end),
				"BFS should not throw when isUniform is true");
		}

		/// <summary>
		/// BreadthFirstSearch with isUniform=true returns an empty path
		/// (stub implementation — not yet filled in).
		/// </summary>
		[Test]
		public void BFS_Uniform_ReturnsEmptyPath()
		{
			var (graph, _) = GraphTestHelper.BuildGrid(3, 3);
			Graph<TestCell>.isUniform = true;

			int start = GraphTestHelper.NodeNbr(0, 0, 3);
			int end = GraphTestHelper.NodeNbr(2, 2, 3);

			var path = graph.BreadthFirstSearch(start, end);

			Assert.AreEqual(0, path.Count,
				"BFS stub should return empty path when isUniform is true");
		}

		#endregion

		#region isUniform State

		/// <summary>
		/// isUniform defaults to false on the Graph class (non-uniform by default).
		/// </summary>
		[Test]
		public void IsUniform_DefaultIsFalse()
		{
			// The saved value from SetUp should be false (default)
			Assert.IsFalse(_originalIsUniform,
				"Graph.isUniform should default to false");
		}

		/// <summary>
		/// isUniform can be set to true and the value persists (it is a static field).
		/// </summary>
		[Test]
		public void IsUniform_CanBeSetToTrue()
		{
			Graph<TestCell>.isUniform = true;
			Assert.IsTrue(Graph<TestCell>.isUniform,
				"Graph.isUniform should reflect the assigned value");
		}

		#endregion
	}
}
