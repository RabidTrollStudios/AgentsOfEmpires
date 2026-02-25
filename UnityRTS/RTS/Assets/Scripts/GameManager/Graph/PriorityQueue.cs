using System;
using System.Collections.Generic;

namespace GameManager.Graph
{
	internal class PriorityNode<V> : IComparable
    {
        public V item;
        public double priority;  // LOWER value is HIGHER priority
        public int index;

        public PriorityNode(V item, double priority)
        {
            this.item = item;
            this.priority = priority;
            this.index = -1;
        }
        public PriorityNode(PriorityNode<V> node)
        {
            this.item = node.item;
            this.priority = node.priority;
            this.index = node.index;
        }
        public int CompareTo(Object item)
        {
            PriorityNode<V> i = (PriorityNode<V>)item;
            if (i == null)
            {
                throw new Exception("ERROR: Cannot compare to " + item);
            }

            return this.priority.CompareTo(i.priority);
        }
    }

	internal class PriorityQueue<T>
    {
        List<PriorityNode<T>> priorityQueue = new List<PriorityNode<T>>();

        public int Count => priorityQueue.Count;

        public void Clear()
        {
            priorityQueue.Clear();
        }
        public void Enqueue(PriorityNode<T> node)
        {
            // Add to the end of the list
            node.index = priorityQueue.Count;
            priorityQueue.Add(node);
            int current = priorityQueue.Count - 1;
            RaisePriority(current);
        }
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
            priorityQueue[0].index = 0;
            priorityQueue.RemoveAt(priorityQueue.Count - 1);

            LowerPriority(0);

            return node;
        }
        public void ChangePriority(PriorityNode<T> node, double newPriority)
        {
            if (0 <= node.index && node.index < priorityQueue.Count)
            {
                node.priority = newPriority;
                priorityQueue[node.index].priority = newPriority;

                RaisePriority(node.index);
                LowerPriority(node.index);
            }
        }

        private void RaisePriority(int current)
        {
            PriorityNode<T> newItem = priorityQueue[current];
            int parent = (current - 1) / 2;

            // Percolate UP
            while (current > 0 && newItem.CompareTo(priorityQueue[parent]) < 0)
            {
                // Copy parent down into current
                priorityQueue[current] = priorityQueue[parent];
                priorityQueue[current].index = current;
                current = parent;
                parent = (current - 1) / 2;
            }
            priorityQueue[current] = newItem;
            priorityQueue[current].index = current;
        }

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
                        priorityQueue[parent].index = parent;

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
                priorityQueue[parent].index = parent;
            }
        }

        public override string ToString()
        {
            string output = "[ ";
            for (int i = 0; i < priorityQueue.Count; ++i)
            {
                output += priorityQueue[i].priority + " ";
            }
            output += "]";
            return output;
        }
    }
}