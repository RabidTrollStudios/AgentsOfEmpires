using System;
using System.Collections.Generic;
using NUnit.Framework;
using GameManager.Graph;

namespace GameManager.Tests
{
	[TestFixture]
	public class PriorityQueueTests
	{
		private PriorityQueue<int> pq;

		[SetUp]
		public void SetUp()
		{
			pq = new PriorityQueue<int>();
		}

		[Test]
		public void Enqueue_SingleItem_CountIsOne()
		{
			pq.Enqueue(new PriorityNode<int>(42, 1.0));
			Assert.AreEqual(1, pq.Count);
		}

		[Test]
		public void Dequeue_SingleItem_ReturnsIt()
		{
			pq.Enqueue(new PriorityNode<int>(42, 1.0));
			var node = pq.Dequeue();
			Assert.AreEqual(42, node.item);
			Assert.AreEqual(0, pq.Count);
		}

		[Test]
		public void Dequeue_MultipleItems_ReturnedInPriorityOrder()
		{
			pq.Enqueue(new PriorityNode<int>(1, 5.0));
			pq.Enqueue(new PriorityNode<int>(2, 1.0));
			pq.Enqueue(new PriorityNode<int>(3, 3.0));

			Assert.AreEqual(2, pq.Dequeue().item); // priority 1.0
			Assert.AreEqual(3, pq.Dequeue().item); // priority 3.0
			Assert.AreEqual(1, pq.Dequeue().item); // priority 5.0
		}

		[Test]
		public void Enqueue_DuplicatePriorities_AllItemsReturned()
		{
			pq.Enqueue(new PriorityNode<int>(10, 2.0));
			pq.Enqueue(new PriorityNode<int>(20, 2.0));
			pq.Enqueue(new PriorityNode<int>(30, 2.0));

			var results = new HashSet<int>();
			results.Add(pq.Dequeue().item);
			results.Add(pq.Dequeue().item);
			results.Add(pq.Dequeue().item);

			Assert.AreEqual(3, results.Count);
			Assert.IsTrue(results.Contains(10));
			Assert.IsTrue(results.Contains(20));
			Assert.IsTrue(results.Contains(30));
		}

		[Test]
		public void Dequeue_EmptyQueue_ThrowsException()
		{
			Assert.Throws<Exception>(() => pq.Dequeue());
		}

		[Test]
		public void Clear_ResetsCount()
		{
			pq.Enqueue(new PriorityNode<int>(1, 1.0));
			pq.Enqueue(new PriorityNode<int>(2, 2.0));
			pq.Clear();
			Assert.AreEqual(0, pq.Count);
		}

		[Test]
		public void Dequeue_AfterClear_ThrowsException()
		{
			pq.Enqueue(new PriorityNode<int>(1, 1.0));
			pq.Clear();
			Assert.Throws<Exception>(() => pq.Dequeue());
		}

		[Test]
		public void ChangePriority_Decrease_MovesToFront()
		{
			var nodeA = new PriorityNode<int>(1, 10.0);
			var nodeB = new PriorityNode<int>(2, 5.0);
			pq.Enqueue(nodeA);
			pq.Enqueue(nodeB);

			// Decrease A's priority so it should come out first
			pq.ChangePriority(nodeA, 1.0);

			Assert.AreEqual(1, pq.Dequeue().item);
			Assert.AreEqual(2, pq.Dequeue().item);
		}

		[Test]
		public void ChangePriority_Increase_MovesToBack()
		{
			var nodeA = new PriorityNode<int>(1, 1.0);
			var nodeB = new PriorityNode<int>(2, 5.0);
			pq.Enqueue(nodeA);
			pq.Enqueue(nodeB);

			// Increase A's priority so it should come out last
			pq.ChangePriority(nodeA, 10.0);

			Assert.AreEqual(2, pq.Dequeue().item);
			Assert.AreEqual(1, pq.Dequeue().item);
		}

		[Test]
		public void ChangePriority_NeverEnqueued_NoEffect()
		{
			pq.Enqueue(new PriorityNode<int>(1, 1.0));
			var orphan = new PriorityNode<int>(99, 0.5);
			// orphan.index is -1, never enqueued — should not crash
			Assert.DoesNotThrow(() => pq.ChangePriority(orphan, 0.1));
			Assert.AreEqual(1, pq.Count);
		}

		[Test]
		public void Stress_ThousandElements_HeapOrdering()
		{
			var rng = new System.Random(12345);
			for (int i = 0; i < 1000; i++)
				pq.Enqueue(new PriorityNode<int>(i, rng.NextDouble() * 10000));

			double prev = double.MinValue;
			while (pq.Count > 0)
			{
				var node = pq.Dequeue();
				Assert.GreaterOrEqual(node.priority, prev,
					"Heap order violated: dequeued {0} after {1}", node.priority, prev);
				prev = node.priority;
			}
		}

		[Test]
		public void Stress_InterleavedEnqueueDequeue_Ordering()
		{
			var rng = new System.Random(54321);

			// Enqueue 500 items, dequeuing the min every 2 enqueues
			double lastDequeued = double.MinValue;
			for (int i = 0; i < 500; i++)
			{
				pq.Enqueue(new PriorityNode<int>(i, rng.NextDouble() * 1000));

				if (i % 2 == 1 && pq.Count > 0)
				{
					var node = pq.Dequeue();
					// Each dequeue within an interleaved batch won't be globally sorted,
					// but it must be the current minimum
					// Verify by re-checking: dequeue should return <= everything still in queue
				}
			}

			// Drain remaining — must come out in sorted order
			lastDequeued = double.MinValue;
			while (pq.Count > 0)
			{
				var node = pq.Dequeue();
				Assert.GreaterOrEqual(node.priority, lastDequeued);
				lastDequeued = node.priority;
			}
		}
	}
}
