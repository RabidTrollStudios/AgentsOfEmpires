using System;
using NUnit.Framework;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests directly targeting PriorityNode&lt;T&gt;: construction, copy constructor,
	/// CompareTo ordering, and index management.
	/// Complements PriorityQueueTests which focuses on queue-level behavior.
	/// </summary>
	[TestFixture]
	public class PriorityNodeTests
	{
		#region Constructor

		/// <summary>
		/// PriorityNode stores the item correctly.
		/// </summary>
		[Test]
		public void PriorityNode_StoresItem()
		{
			var node = new PriorityNode<int>(42, 3.0);
			Assert.AreEqual(42, node.Item, "PriorityNode should store the item");
		}

		/// <summary>
		/// PriorityNode stores the priority correctly.
		/// </summary>
		[Test]
		public void PriorityNode_StoresPriority()
		{
			var node = new PriorityNode<int>(42, 7.5);
			Assert.AreEqual(7.5, node.Priority, 0.0001, "PriorityNode should store the priority");
		}

		/// <summary>
		/// PriorityNode initializes index to -1 (not yet enqueued).
		/// </summary>
		[Test]
		public void PriorityNode_InitialIndex_IsMinusOne()
		{
			var node = new PriorityNode<int>(0, 1.0);
			Assert.AreEqual(-1, node.Index,
				"Newly created PriorityNode should have index = -1 (not yet in queue)");
		}

		#endregion

		#region Copy Constructor

		/// <summary>
		/// Copy constructor preserves item.
		/// </summary>
		[Test]
		public void PriorityNodeCopyCtor_PreservesItem()
		{
			var original = new PriorityNode<string>("hello", 2.0);
			var copy = new PriorityNode<string>(original);
			Assert.AreEqual("hello", copy.Item, "Copy ctor should preserve item");
		}

		/// <summary>
		/// Copy constructor preserves priority.
		/// </summary>
		[Test]
		public void PriorityNodeCopyCtor_PreservesPriority()
		{
			var original = new PriorityNode<int>(99, 5.5);
			var copy = new PriorityNode<int>(original);
			Assert.AreEqual(5.5, copy.Priority, 0.0001, "Copy ctor should preserve priority");
		}

		/// <summary>
		/// Copy constructor preserves the index value.
		/// </summary>
		[Test]
		public void PriorityNodeCopyCtor_PreservesIndex()
		{
			var pq = new PriorityQueue<int>();
			var original = new PriorityNode<int>(10, 1.0);
			pq.Enqueue(original); // sets index to 0

			var copy = new PriorityNode<int>(original);
			Assert.AreEqual(original.Index, copy.Index,
				"Copy ctor should preserve the index value");
		}

		#endregion

		#region CompareTo

		/// <summary>
		/// CompareTo returns a negative value when this node has lower priority
		/// (lower priority value = higher queue priority — min-heap).
		/// </summary>
		[Test]
		public void CompareTo_LowerPriority_ReturnsNegative()
		{
			var lower = new PriorityNode<int>(1, 1.0);
			var higher = new PriorityNode<int>(2, 5.0);

			Assert.Less(lower.CompareTo(higher), 0,
				"Node with lower priority value should compare as 'less than' higher priority");
		}

		/// <summary>
		/// CompareTo returns a positive value when this node has higher priority value.
		/// </summary>
		[Test]
		public void CompareTo_HigherPriority_ReturnsPositive()
		{
			var lower = new PriorityNode<int>(1, 1.0);
			var higher = new PriorityNode<int>(2, 5.0);

			Assert.Greater(higher.CompareTo(lower), 0,
				"Node with higher priority value should compare as 'greater than' lower priority");
		}

		/// <summary>
		/// CompareTo returns 0 for equal priorities.
		/// </summary>
		[Test]
		public void CompareTo_EqualPriority_ReturnsZero()
		{
			var a = new PriorityNode<int>(1, 3.0);
			var b = new PriorityNode<int>(2, 3.0);

			Assert.AreEqual(0, a.CompareTo(b),
				"Nodes with equal priority should compare as equal");
		}

		/// <summary>
		/// CompareTo throws an exception when compared to null.
		/// </summary>
		[Test]
		public void CompareTo_Null_ThrowsException()
		{
			var node = new PriorityNode<int>(1, 1.0);
			Assert.Throws<Exception>(() => node.CompareTo(null),
				"CompareTo(null) should throw an exception");
		}

		#endregion

		#region Index After Enqueue

		/// <summary>
		/// After enqueueing, the node's index is updated to a valid heap position (>= 0).
		/// </summary>
		[Test]
		public void AfterEnqueue_IndexIsNonNegative()
		{
			var pq = new PriorityQueue<int>();
			var node = new PriorityNode<int>(42, 1.0);
			pq.Enqueue(node);

			Assert.GreaterOrEqual(node.Index, 0,
				"After enqueue, node.index should be a valid heap position");
		}

		/// <summary>
		/// The node placed at the root of the heap (lowest priority) has index = 0.
		/// </summary>
		[Test]
		public void LowestPriorityNode_HasIndexZero()
		{
			var pq = new PriorityQueue<int>();
			var nodeA = new PriorityNode<int>(1, 5.0);
			var nodeB = new PriorityNode<int>(2, 1.0); // lowest value = root

			pq.Enqueue(nodeA);
			pq.Enqueue(nodeB);

			Assert.AreEqual(0, nodeB.Index,
				"The node with the lowest priority value should be at heap root (index 0)");
		}

		#endregion
	}
}
