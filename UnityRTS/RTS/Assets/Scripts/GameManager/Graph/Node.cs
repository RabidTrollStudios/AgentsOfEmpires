using System.Collections.Generic;

namespace GameManager.Graph
{
	/// <summary>
	/// A node in the <see cref="Graph{T}"/>. Wraps a domain item (e.g., <see cref="GridCell"/>)
	/// and maintains its adjacency list plus transient A* search state.
	/// </summary>
	internal class Node<V> where V : IColorable, IBuildable
    {
        /// <summary>Adjacent edges (undirected — each edge appears in both endpoint nodes).</summary>
        public List<Edge<V>> Edges = new List<Edge<V>>();

        /// <summary>The domain object this node represents (e.g., a GridCell).</summary>
        public V Item;

        /// <summary>Unique identifier matching the node's key in <see cref="Graph{T}.NodesDict"/>.</summary>
        public int Number;

        // --- A* transient search state (reset between searches) ---

        /// <summary>Best-known g-cost from the start node. Reset to MaxValue before each search.</summary>
        public double Cost = double.MaxValue;

        /// <summary>Back-pointer to the predecessor in the current search path.</summary>
		public PriorityNode<Node<V>> BackPtr = null;

        /// <summary>This node's entry in the priority queue (null if not yet enqueued).</summary>
		public PriorityNode<Node<V>> PriorityNode = null;

        public Node(int number, V item)
        {
            this.Number = number;
            this.Item = item;
        }

        /// <summary>Copy constructor (copies identity, not search state).</summary>
        public Node(Node<V> node)
        {
            this.Number = node.Number;
            this.Item = node.Item;
        }

        /// <summary>Clear transient A* state so this node can participate in a new search.</summary>
        public void ResetSearchVariables()
        {
            Cost = double.MaxValue;
            BackPtr = null;
            PriorityNode = null;
        }
    }
}
