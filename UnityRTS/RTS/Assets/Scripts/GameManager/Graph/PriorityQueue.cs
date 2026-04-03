using System;
using System.Collections.Generic;

namespace GameManager.Graph
{
	/// <summary>
	/// Wrapper pairing an item with a priority value for use in <see cref="PriorityQueue{T}"/>.
	/// Tracks its current <see cref="Index"/> in the heap array so
	/// <see cref="PriorityQueue{T}.ChangePriority"/> can update it in O(log n).
	/// Lower priority values are dequeued first (min-heap).
	/// </summary>
	internal class PriorityNode<V> : IComparable
    {
        public V Item;
        public double Priority;  // LOWER value is HIGHER priority
        /// <summary>Current position in the heap array (-1 if not enqueued).</summary>
        public int Index;

        public PriorityNode(V item, double priority)
        {
            this.Item = item;
            this.Priority = priority;
            this.Index = -1;
        }

        /// <summary>Copy constructor.</summary>
        public PriorityNode(PriorityNode<V> node)
        {
            this.Item = node.Item;
            this.Priority = node.Priority;
            this.Index = node.Index;
        }

        public int CompareTo(Object item)
        {
            PriorityNode<V> i = (PriorityNode<V>)item;
            if (i == null)
            {
                throw new Exception("ERROR: Cannot compare to " + item);
            }

            return this.Priority.CompareTo(i.Priority);
        }
    }

	/// <summary>
	/// Min-heap priority queue supporting O(log n) enqueue, dequeue, and priority update.
	/// Used by <see cref="Graph{T}.AStarSearch"/> as the open set.
	/// Each <see cref="PriorityNode{T}"/> tracks its heap index so
	/// <see cref="ChangePriority"/> can re-heapify without a linear scan.
	/// </summary>
	internal class PriorityQueue<T>
    {
        List<PriorityNode<T>> priorityQueue = new List<PriorityNode<T>>();

        /// <summary>Number of elements currently in the queue.</summary>
        public int Count => priorityQueue.Count;

        /// <summary>Remove all elements from the queue.</summary>
        public void Clear()
        {
            priorityQueue.Clear();
        }

        /// <summary>Insert a node into the heap and bubble it up to its correct position.</summary>
        public void Enqueue(PriorityNode<T> node)
        {
            // Add to the end of the list
            node.Index = priorityQueue.Count;
            priorityQueue.Add(node);
            int current = priorityQueue.Count - 1;
            RaisePriority(current);
        }
        /// <summary>Remove and return the element with the lowest priority value (highest urgency).</summary>
        public PriorityNode<T> Dequeue()
        {
            if (priorityQueue.Count == 0)
            {
                throw new Exception("Cannot dequeue from an empty queue");
            }

            // Store the first item, we will return it
            PriorityNode<T> node = new PriorityNode<T>(priorityQueue[0]);

            // Store the last item, remove it from the list
            priorityQueue[0] = priorityQueue[priorityQueue.Count - 1];
            priorityQueue[0].Index = 0;
            priorityQueue.RemoveAt(priorityQueue.Count - 1);

            LowerPriority(0);

            return node;
        }
        /// <summary>
        /// Update a node's priority and restore heap order.
        /// Uses the node's tracked <see cref="PriorityNode{T}.Index"/> to find it in O(1),
        /// then re-heapifies in O(log n).
        /// </summary>
        public void ChangePriority(PriorityNode<T> node, double newPriority)
        {
            if (0 <= node.Index && node.Index < priorityQueue.Count)
            {
                node.Priority = newPriority;
                priorityQueue[node.Index].Priority = newPriority;

                RaisePriority(node.Index);
                LowerPriority(node.Index);
            }
        }

        /// <summary>Bubble an element up toward the root while it has higher priority than its parent.</summary>
        private void RaisePriority(int current)
        {
            PriorityNode<T> newItem = priorityQueue[current];
            int parent = (current - 1) / 2;

            // Percolate UP
            while (current > 0 && newItem.CompareTo(priorityQueue[parent]) < 0)
            {
                // Copy parent down into current
                priorityQueue[current] = priorityQueue[parent];
                priorityQueue[current].Index = current;
                current = parent;
                parent = (current - 1) / 2;
            }
            priorityQueue[current] = newItem;
            priorityQueue[current].Index = current;
        }

        /// <summary>Sink an element down through the heap while it has lower priority than a child.</summary>
        private void LowerPriority(int current)
        {
            if (priorityQueue.Count >= 1)
            {
                PriorityNode<T> lastItem = priorityQueue[current];

                // Percolate Down
                int parent = current;
                int left = parent * 2 + 1;
                int right = parent * 2 + 2;
                int swap = current;
                bool swapped = true;

                while (swapped && left < priorityQueue.Count)
                {
                    // Assume we will swap with the left child
                    swap = left;

                    // If the right child exists and its priority is less than the left child,
                    // choose the right child as the "to swap" item
                    if (right < priorityQueue.Count && priorityQueue[right].CompareTo(priorityQueue[left]) < 0)
                    {
                        swap = right;
                    }

                    // If the "to swap" item is lower priority than the parent, swap them.
                    if (priorityQueue[swap].CompareTo(lastItem) < 0)
                    {
                        priorityQueue[parent] = priorityQueue[swap];
                        priorityQueue[parent].Index = parent;

                        parent = swap;
                        left = parent * 2 + 1;
                        right = parent * 2 + 2;
                    }
                    else
                    {
                        swapped = false;
                    }
                }
                priorityQueue[parent] = lastItem;
                priorityQueue[parent].Index = parent;
            }
        }

        public override string ToString()
        {
            string output = "[ ";
            for (int i = 0; i < priorityQueue.Count; ++i)
            {
                output += priorityQueue[i].Priority + " ";
            }
            output += "]";
            return output;
        }
    }
}